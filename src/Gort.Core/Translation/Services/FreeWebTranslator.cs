using System.Net;
using System.Text;
using System.Text.Json;
using Gort.Core.Calibration;

namespace Gort.Core.Translation.Services;

/// <summary>Configuração do tradutor web gratuito, vinda dos dados (VI.1).</summary>
public sealed class FreeWebTranslatorOptions
{
    public required string Endpoint { get; init; }

    /// <summary>RF-245 — Identificador de cliente de alta qualidade, usado por padrão.</summary>
    public required string HighQualityClient { get; init; }

    /// <summary>RF-245 — Identificador de baixa qualidade, usado após um 429.</summary>
    public required string LowQualityClient { get; init; }

    /// <summary>RF-247 — Marcador visível prefixado ao resultado em modo de baixa qualidade.</summary>
    public string LowQualityMarker { get; init; } = "[qualidade reduzida] ";
}

/// <summary>
/// VI.1 / RF-244 a RF-248 — Tradutor web gratuito.
///
/// RF-225 — É o serviço PADRÃO: o que o programa oferece a quem nunca configurou nada, e
/// contra o qual a latência do ciclo deve ser medida.
///
/// O serviço impõe cota por hora e por endereço IP. RF-245 a RF-247 descrevem a degradação:
/// ao receber 429, troca-se para o cliente de menor qualidade por P-53 e o resultado passa
/// a vir com um marcador visível — em vez de o programa simplesmente parar de traduzir.
/// </summary>
public sealed class FreeWebTranslator : ITranslationService
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly FreeWebTranslatorOptions _options;
    private readonly Func<DateTime> _now;

    private DateTime _lowQualitySince = DateTime.MinValue;

    public FreeWebTranslator(FreeWebTranslatorOptions options, HttpClient? http = null,
                             Func<DateTime>? now = null)
    {
        _options = options;
        _now = now ?? (() => DateTime.UtcNow);
        _ownsHttp = http is null;

        // RF-248 / P-54 — o tempo limite é do serviço, não do laço: um ciclo travado em rede
        // lenta é pior que uma tradução perdida (P1).
        _http = http ?? new HttpClient { Timeout = P.FreeWebTranslatorTimeout };
    }

    public string Key => "webfree";

    /// <summary>
    /// RF-246 / RF-247 — Verdadeiro enquanto o modo de baixa qualidade vale. A interface
    /// indica esse estado ao usuário.
    /// </summary>
    public bool IsLowQuality
    {
        get
        {
            if (_lowQualitySince == DateTime.MinValue) return false;

            // RF-246 — o modo permanece por P-53 e depois volta AUTOMATICAMENTE ao normal.
            if (_now() - _lowQualitySince >= P.LowQualityModeDuration)
            {
                _lowQualitySince = DateTime.MinValue;
                return false;
            }
            return true;
        }
    }

    public async Task<TranslationOutcome> TranslateAsync(
        string text, TranslationContext context, CancellationToken cancellation)
    {
        if (string.IsNullOrEmpty(text)) return TranslationOutcome.Ok("");

        bool lowQuality = IsLowQuality;

        try
        {
            var (status, body) = await SendAsync(text, context, lowQuality, cancellation)
                                       .ConfigureAwait(false);

            if (status == HttpStatusCode.TooManyRequests)
            {
                // RF-245 — já em baixa qualidade: não há para onde degradar.
                if (lowQuality)
                {
                    return TranslationOutcome.Failed(
                        "A cota horária do tradutor web gratuito acabou. Ela se restabelece " +
                        "sozinha; enquanto isso, escolha outro serviço de tradução.");
                }

                // Troca para o cliente de baixa qualidade e repete UMA vez.
                _lowQualitySince = _now();
                (status, body) = await SendAsync(text, context, lowQuality: true, cancellation)
                                       .ConfigureAwait(false);
                lowQuality = true;

                if (status == HttpStatusCode.TooManyRequests)
                {
                    return TranslationOutcome.Failed(
                        "A cota horária do tradutor web gratuito acabou.");
                }
            }

            if (status != HttpStatusCode.OK)
                return TranslationOutcome.Failed($"O tradutor devolveu o código {(int)status}.");

            string translated = ParseResponse(body);

            // RF-247 — o resultado é prefixado com um marcador visível enquanto a qualidade
            // está reduzida, para que o usuário saiba por que a tradução piorou.
            if (lowQuality) translated = _options.LowQualityMarker + translated;

            return TranslationOutcome.Ok(translated);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // RF-238 — cancelamento não é erro.
            return TranslationOutcome.CancelledResult;
        }
        catch (TaskCanceledException)
        {
            // Tempo limite de P-54 esgotado.
            return TranslationOutcome.Failed(
                $"O tradutor não respondeu em {P.FreeWebTranslatorTimeout.TotalMilliseconds:0} ms.");
        }
        catch (Exception ex)
        {
            // RF-236 / RF-561 — nenhuma falha de rede encerra o laço.
            return TranslationOutcome.Failed($"Falha ao traduzir: {ex.Message}");
        }
    }

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(
        string text, TranslationContext context, bool lowQuality, CancellationToken cancellation)
    {
        string client = lowQuality ? _options.LowQualityClient : _options.HighQualityClient;

        // RF-239 — a tradução ponte passa pelo idioma intermediário em duas passagens; este
        // serviço não a declara suportada, então a origem vai direto ao destino.
        string url = $"{_options.Endpoint}" +
                     $"?client={Uri.EscapeDataString(client)}" +
                     $"&sl={Uri.EscapeDataString(context.SourceCode)}" +
                     $"&tl={Uri.EscapeDataString(context.TargetCode)}" +
                     $"&dt=t" +
                     $"&q={Uri.EscapeDataString(text)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
        request.Headers.TryAddWithoutValidation("Accept-Charset", "UTF-8");

        using var response = await _http.SendAsync(request, cancellation).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
        return (response.StatusCode, body);
    }

    /// <summary>
    /// RF-244 / VI.1 — A resposta é um vetor JSON cujo PRIMEIRO elemento é um vetor de
    /// segmentos. De cada segmento que seja um vetor cujo primeiro item é texto, extrai-se
    /// esse texto; as partes são concatenadas.
    ///
    /// A concatenação é direta, sem separador: o serviço quebra a resposta em segmentos por
    /// conveniência dele, e um separador inventado aqui apareceria no meio das frases.
    /// </summary>
    internal static string ParseResponse(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "";

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0) return "";

        var segments = root[0];
        if (segments.ValueKind != JsonValueKind.Array) return "";

        var sb = new StringBuilder();
        foreach (var segment in segments.EnumerateArray())
        {
            if (segment.ValueKind != JsonValueKind.Array || segment.GetArrayLength() == 0) continue;
            var first = segment[0];
            if (first.ValueKind == JsonValueKind.String) sb.Append(first.GetString());
        }
        return sb.ToString();
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}

/// <summary>
/// RF-241 a RF-243 — Banco de dados local como serviço de tradução: consulta local, sem
/// rede, instantânea.
///
/// RF-214 — Não usa memória de resultados: cachear uma consulta local seria só desperdício
/// de memória. RF-221 — não consulta a coletânea: seria consulta duplicada.
/// </summary>
public sealed class LocalDatabaseTranslator : ITranslationService
{
    private readonly Caching.LocalDatabase _database;

    public LocalDatabaseTranslator(Caching.LocalDatabase database) => _database = database;

    public string Key => "localdb";

    public Task<TranslationOutcome> TranslateAsync(
        string text, TranslationContext context, CancellationToken cancellation)
        => Task.FromResult(TranslationOutcome.Ok(_database.Lookup(text)));

    public void Dispose() { }
}
