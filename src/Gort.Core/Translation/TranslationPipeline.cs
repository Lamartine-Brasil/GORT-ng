using System.Text;
using Gort.Core.Caching;

namespace Gort.Core.Translation;

/// <summary>
/// Cap. 18.1 — Protocolo comum de tradução.
///
/// É o mesmo para TODOS os serviços: consulta às fontes locais, montagem de uma única
/// requisição em lote, divisão da resposta e gravação no cache. Acrescentar um serviço é
/// implementar <see cref="ITranslationService"/>; nada aqui muda (RF-566).
/// </summary>
public sealed class TranslationPipeline : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _current;

    /// <summary>RF-232 — Token separador do serviço ativo (P-51 ou P-52).</summary>
    public string SeparatorToken { get; set; } = Calibration.P.SeparatorToken;

    /// <summary>
    /// RF-234 / P-151 — Modo "token avançado". Desligado por padrão; configurável
    /// remotamente (RF-417).
    /// </summary>
    public bool AdvancedToken { get; set; } = Calibration.P.AdvancedTokenDefault;

    /// <summary>RF-215 — Coletânea do usuário, consultada antes da memória de resultados.</summary>
    public TranslationCollection? Collection { get; set; }

    /// <summary>RF-206 — Memória de resultados do serviço ativo.</summary>
    public ResultMemory? Memory { get; set; }

    /// <summary>
    /// RF-240 — "Ignorar tradução vazia": um resultado vazio não substitui a tradução
    /// anterior na tela. O pipeline apenas sinaliza; quem decide não desenhar é a janela.
    /// </summary>
    public bool IgnoreEmptyTranslation { get; set; }

    /// <summary>
    /// Executa o protocolo comum sobre uma lista de textos de origem.
    /// </summary>
    public async Task<BatchTranslation> TranslateAsync(
        IReadOnlyList<string> sources, ITranslationService service, TranslationContext context)
    {
        // RF-228 — um pedido com texto vazio devolve vazio SEM CHAMAR NADA.
        if (sources.Count == 0 || sources.All(string.IsNullOrEmpty))
        {
            return new BatchTranslation
            {
                Translations = sources.Select(_ => (string?)null).ToList(),
                Combined = "",
            };
        }

        // RF-229 — cada novo pedido cancela o anterior ainda em curso: se o conteúdo da tela
        // mudou, a tradução antiga já não interessa e segurá-la atrasa a nova.
        CancellationToken cancellation;
        lock (_gate)
        {
            _current?.Cancel();
            _current?.Dispose();
            _current = new CancellationTokenSource();
            cancellation = _current.Token;
        }

        var translations = new string?[sources.Count];
        var missing = new List<int>();

        // RF-230 — para cada texto: remover espaços à direita, consultar a coletânea do
        // usuário e a memória de resultados. Só os NÃO encontrados entram na requisição.
        for (int i = 0; i < sources.Count; i++)
        {
            string source = sources[i].TrimEnd();
            if (source.Length == 0) continue;

            string? known = Collection?.Lookup(source) ?? Memory?.Lookup(source);
            if (known is not null) translations[i] = known;
            else missing.Add(i);
        }

        if (missing.Count == 0)
        {
            return new BatchTranslation
            {
                Translations = translations,
                Combined = Combine(sources, translations),
                NetworkCount = 0,
            };
        }

        // RF-231 — todos os textos não encontrados vão em UMA ÚNICA requisição, cada um
        // precedido pelo token separador e seguido de quebra de linha.
        string token = EffectiveToken();
        var request = new StringBuilder();
        foreach (int i in missing)
        {
            request.Append(token);
            request.Append(sources[i].TrimEnd());
            request.Append('\n');
        }

        TranslationOutcome outcome;
        try
        {
            outcome = await service.TranslateAsync(request.ToString(), context, cancellation)
                                   .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            outcome = TranslationOutcome.CancelledResult;
        }
        catch (Exception ex)
        {
            outcome = TranslationOutcome.Failed(ex.Message);
        }

        // RF-238 — cancelamento não é tratado como erro.
        if (outcome.Cancelled)
        {
            return new BatchTranslation
            {
                Translations = translations,
                Combined = "",
                Cancelled = true,
                NetworkCount = missing.Count,
            };
        }

        // RF-236 — quando o serviço devolve erro, a mensagem ocupa o lugar de TODAS as
        // traduções, e o ciclo continua.
        if (outcome.IsError)
        {
            return new BatchTranslation
            {
                Translations = sources.Select(_ => (string?)outcome.Error).ToList(),
                Combined = outcome.Error!,
                Error = outcome.Error,
                NetworkCount = missing.Count,
            };
        }

        // RF-233 — a resposta é dividida pelo mesmo token e distribuída, em ordem, aos
        // textos que estavam faltando. Se vierem menos partes que textos, os restantes ficam
        // sem tradução — e isso NÃO é erro.
        var parts = SplitResponse(outcome.Text, token);

        for (int p = 0; p < missing.Count && p < parts.Count; p++)
        {
            int index = missing[p];
            string translated = parts[p];
            translations[index] = translated;

            // RF-235 — cada tradução obtida por rede é gravada na memória IMEDIATAMENTE.
            Memory?.Store(sources[index].TrimEnd(), translated);
        }

        return new BatchTranslation
        {
            Translations = translations,
            Combined = Combine(sources, translations),
            NetworkCount = missing.Count,
        };
    }

    /// <summary>
    /// RF-234 — No modo de token avançado, envia-se um token ENCURTADO: removem-se 3
    /// caracteres do início se o token tem 7 ou mais, ou 2 se tem 6. 🔒
    ///
    /// Motivo: alguns tradutores alteram o token; essa heurística tolera a alteração.
    /// </summary>
    internal string EffectiveToken()
    {
        if (!AdvancedToken) return SeparatorToken;

        if (SeparatorToken.Length >= 7) return SeparatorToken[3..];
        if (SeparatorToken.Length == 6) return SeparatorToken[2..];
        return SeparatorToken;
    }

    /// <summary>
    /// Divide a resposta pelo token e, no modo avançado, limpa as pontas de cada parte.
    /// </summary>
    internal List<string> SplitResponse(string response, string token)
    {
        var raw = token.Length == 0
            ? new List<string> { response }
            : response.Split(token, StringSplitOptions.None).ToList();

        // A primeira parte é o que veio ANTES do primeiro token — normalmente vazio.
        if (raw.Count > 0 && raw[0].Trim().Length == 0) raw.RemoveAt(0);

        var parts = new List<string>(raw.Count);
        foreach (var piece in raw)
        {
            string part = piece.Trim('\n', '\r');

            if (AdvancedToken && SeparatorToken.Length > 0)
            {
                // RF-234 — remove das PONTAS as repetições do primeiro caractere do token,
                // descartando partes que ficarem vazias.
                char marker = SeparatorToken[0];
                part = part.Trim(marker);
                if (part.Trim().Length == 0) continue;
            }

            parts.Add(part.Trim());
        }
        return parts;
    }

    /// <summary>
    /// RF-237 — Concatenação, para cada texto de origem, do token separador, da tradução e
    /// de uma quebra de linha.
    /// </summary>
    private string Combine(IReadOnlyList<string> sources, IReadOnlyList<string?> translations)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < sources.Count; i++)
        {
            if (translations[i] is null) continue;
            sb.Append(SeparatorToken);
            sb.Append(translations[i]);
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// RF-229 — Cancela a tradução em curso, sem que isso conte como erro (RF-238).
    /// Usado pelo protocolo de pausa do ciclo de vida (RF-012).
    /// </summary>
    public void CancelCurrent()
    {
        lock (_gate)
        {
            _current?.Cancel();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _current?.Cancel();
            _current?.Dispose();
            _current = null;
        }
    }
}
