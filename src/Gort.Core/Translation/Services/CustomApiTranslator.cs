using System.Text;
using System.Text.Json;
using Gort.Core.Calibration;
using Gort.Core.Translation.Presets;

namespace Gort.Core.Translation.Services;

/// <summary>
/// VI.5 / RF-292 a RF-301 — API personalizada.
///
/// O usuário informa uma URL e o programa faz POST com um corpo JSON. Sem preset, o formato
/// é o padrão de RF-292; com preset, o corpo e a leitura da resposta saem dos MODELOS que o
/// usuário escreveu — o que torna o serviço capaz de falar com qualquer API que aceite JSON,
/// sem uma linha de código por serviço.
///
/// Este é o único serviço da PARTE VI que não depende de credencial de terceiro: quem
/// fornece o endereço é o usuário. Por isso é o que dá para construir e verificar aqui.
/// </summary>
public sealed class CustomApiTranslator : ITranslationService
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly ApiPreset? _preset;
    private readonly string _url;
    private readonly Action<string>? _log;

    public CustomApiTranslator(string url, ApiPreset? preset = null, HttpClient? http = null,
                               Action<string>? log = null)
    {
        _url = url;
        _preset = preset;
        _log = log;
        _ownsHttp = http is null;

        // RF-248 / P-54 — o tempo limite é do serviço, não do laço.
        _http = http ?? new HttpClient { Timeout = P.FreeWebTranslatorTimeout };
    }

    /// <summary>
    /// RF-306 — Cada preset aparece como uma entrada SEPARADA na lista de serviços,
    /// identificada como "Custom – &lt;nome&gt;". O identificador segue a mesma regra, para
    /// que o perfil aponte para o preset certo e não só para "customapi".
    /// </summary>
    public string Key => _preset is null ? "customapi" : $"customapi:{_preset.Name}";

    public async Task<TranslationOutcome> TranslateAsync(
        string text, TranslationContext context, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(_url))
            return TranslationOutcome.Failed("Nenhum endereço configurado para a API personalizada.");

        string source = context.SourceCode;
        string target = context.TargetCode;

        // RF-295 — o preset pode fixar os códigos de idioma; sem ele valem os do contexto.
        if (_preset is { SameLanguageCodesAsWeb: false })
        {
            source = _preset.SourceCode;
            target = _preset.TargetCode;
        }

        string? body = BuildBody(text, source, target, out string? templateError);
        if (body is null) return TranslationOutcome.Failed(templateError!);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

            // RF-301 — cabeçalhos adicionais; linhas malformadas são registradas e ignoradas.
            foreach (var (name, value) in RequestTemplate.ParseHeaders(_preset?.Headers ?? "", _log))
            {
                if (!request.Headers.TryAddWithoutValidation(name, value))
                    _log?.Invoke($"Cabeçalho recusado pela pilha HTTP: {name}");
            }

            using var response = await _http.SendAsync(request, cancellation)
                                            .ConfigureAwait(false);

            string payload = await response.Content.ReadAsStringAsync(cancellation)
                                                   .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return TranslationOutcome.Failed(
                    $"A API personalizada respondeu {(int)response.StatusCode}: {Shorten(payload)}");
            }

            return Read(payload);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return new TranslationOutcome("", null, Cancelled: true);
        }
        catch (Exception ex)
        {
            // RF-561 — nenhuma falha de rede encerra o programa; ela vira mensagem.
            return TranslationOutcome.Failed(ex.Message);
        }
    }

    /// <summary>
    /// RF-292 / RF-296 — Sem preset, o formato padrão; com preset, o modelo do usuário.
    /// </summary>
    private string? BuildBody(string text, string source, string target, out string? error)
    {
        error = null;

        if (_preset is null || string.IsNullOrWhiteSpace(_preset.RequestTemplate))
        {
            // RF-292 — nome, texto, código de destino e código de origem.
            return JsonSerializer.Serialize(new
            {
                name = "gort",
                text,
                resultCode = target,
                sourceCode = source,
            });
        }

        return RequestTemplate.Build(_preset.RequestTemplate, text, source, target, out error);
    }

    /// <summary>
    /// RF-292 a RF-294, RF-300 — Lê a resposta.
    ///
    /// Sem preset, o formato padrão tem campo de resultado, código de erro e mensagem de
    /// erro. Com preset, a chave do resultado sai do modelo de resposta e é procurada
    /// recursivamente.
    /// </summary>
    private TranslationOutcome Read(string payload)
    {
        string? key = _preset is null
            ? null
            : RequestTemplate.ResultKeyOf(_preset.ResponseTemplate);

        if (key is not null)
        {
            string? found = RequestTemplate.FindResult(payload, key);
            return found is null
                ? TranslationOutcome.Failed(
                    $"A resposta não tem o campo '{key}': {Shorten(payload)}")
                : TranslationOutcome.Ok(found);
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            // RF-293 — código de erro diferente de "0" produz erro com a mensagem recebida.
            if (TryGet(root, "errorCode", out var code))
            {
                string codeText = code.ValueKind == JsonValueKind.Number
                    ? code.GetRawText()
                    : code.GetString() ?? "0";

                if (codeText is not ("0" or ""))
                {
                    string message = TryGet(root, "errorMessage", out var m)
                        ? m.GetString() ?? codeText
                        : codeText;
                    return TranslationOutcome.Failed(message);
                }
            }

            if (!TryGet(root, "result", out var result))
                return TranslationOutcome.Failed(
                    $"A resposta não tem o campo de resultado: {Shorten(payload)}");

            // RF-294 — o campo pode ser texto OU vetor de textos; quando vetor, as partes
            // são concatenadas.
            return TranslationOutcome.Ok(result.ValueKind == JsonValueKind.Array
                ? string.Concat(result.EnumerateArray().Select(e => e.GetString() ?? ""))
                : result.GetString() ?? "");
        }
        catch (JsonException ex)
        {
            // PARTE VIII — "Serviço devolve resposta em formato inesperado": mensagem
            // descrevendo a falha de ANÁLISE, e o laço continua.
            return TranslationOutcome.Failed($"Resposta em formato inesperado: {ex.Message}");
        }
    }

    private static bool TryGet(JsonElement root, string name, out JsonElement value)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    /// <summary>Uma mensagem de erro precisa caber na tela; a resposta inteira não cabe.</summary>
    private static string Shorten(string text)
        => text.Length <= 200 ? text : text[..200] + "…";

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
