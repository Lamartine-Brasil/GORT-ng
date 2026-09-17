using System.Text;
using System.Text.Json;
using Gort.Core.Calibration;

namespace Gort.Core.Translation.Services;

/// <summary>Configuração do tradutor web sem chave, vinda dos dados (RF-254).</summary>
public sealed class KeylessWebTranslatorOptions
{
    /// <summary>Endereço público a que a requisição é feita. Vazio desabilita o serviço.</summary>
    public string Endpoint { get; init; } = "";

    /// <summary>
    /// RF-254 — Cabeçalhos de NAVEGADOR. Vêm dos dados porque mudam com o tempo e sem aviso:
    /// um valor embutido no código envelheceria dentro do executável, e atualizá-lo exigiria
    /// uma versão nova para o que é uma linha de configuração.
    /// </summary>
    public IReadOnlyList<(string Name, string Value)> Headers { get; init; }
        = Array.Empty<(string, string)>();

    /// <summary>Nome do campo que carrega o texto no corpo da requisição.</summary>
    public string TextField { get; init; } = "text";

    /// <summary>Nome do campo que carrega a tradução na resposta.</summary>
    public string ResultField { get; init; } = "result";

    public string SourceField { get; init; } = "source";
    public string TargetField { get; init; } = "target";
}

/// <summary>
/// VI.2 / RF-254 🔒 — Tradutor web SEM CHAVE do mesmo fornecedor do serviço comercial.
///
/// Faz POST a um endereço público com cabeçalhos de navegador. Depois de cada requisição
/// espera um intervalo ALEATÓRIO entre 0 e P-56 antes de aceitar a próxima.
///
/// O motivo do requisito, na letra dele: "espaçar as chamadas reduz bloqueio por
/// comportamento automatizado". O que denuncia um programa não é a frequência, é a
/// REGULARIDADE — requisições a cada exatos 300 ms não vêm de gente. Por isso o intervalo é
/// aleatório e não fixo, e por isso ele é 🔒: o teto foi calibrado contra o comportamento
/// real do serviço.
/// </summary>
public sealed class KeylessWebTranslator : ITranslationService
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly KeylessWebTranslatorOptions _options;
    private readonly Random _random;
    private readonly Func<DateTime> _now;

    private DateTime _nextAllowed = DateTime.MinValue;

    public KeylessWebTranslator(KeylessWebTranslatorOptions options, HttpClient? http = null,
                                Random? random = null, Func<DateTime>? now = null)
    {
        _options = options;
        _random = random ?? Random.Shared;
        _now = now ?? (() => DateTime.UtcNow);
        _ownsHttp = http is null;
        _http = http ?? new HttpClient { Timeout = P.FreeWebTranslatorTimeout };
    }

    public string Key => "webfree_kr";

    /// <summary>
    /// RF-254 — Quando a próxima requisição pode sair. Exposto para que o teste verifique o
    /// espaçamento sem esperar de verdade.
    /// </summary>
    public DateTime NextAllowed => _nextAllowed;

    /// <summary>Quanto falta esperar agora; zero quando já pode.</summary>
    public TimeSpan RemainingDelay
    {
        get
        {
            var remaining = _nextAllowed - _now();
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public async Task<TranslationOutcome> TranslateAsync(
        string text, TranslationContext context, CancellationToken cancellation)
    {
        if (_options.Endpoint.Length == 0)
            return TranslationOutcome.Failed("O tradutor web sem chave não está configurado.");

        // RF-254 — o espaçamento é ANTES da requisição, e não depois: esperar depois faria a
        // primeira chamada de uma rajada sair sem intervalo nenhum, que é justamente a que
        // chama atenção.
        var wait = RemainingDelay;
        if (wait > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(wait, cancellation).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return new TranslationOutcome("", null, Cancelled: true);
            }
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
            {
                Content = new StringContent(BuildBody(text, context), Encoding.UTF8,
                                            "application/json"),
            };

            // RF-254 — cabeçalhos de navegador.
            foreach (var (name, value) in _options.Headers)
                request.Headers.TryAddWithoutValidation(name, value);

            using var response = await _http.SendAsync(request, cancellation)
                                            .ConfigureAwait(false);

            string payload = await response.Content.ReadAsStringAsync(cancellation)
                                                   .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return TranslationOutcome.Failed(
                    $"O tradutor web sem chave respondeu {(int)response.StatusCode}.");
            }

            string? found = RequestTemplate.FindResult(payload, _options.ResultField);

            return found is null
                ? TranslationOutcome.Failed("Resposta em formato inesperado.")
                : TranslationOutcome.Ok(found);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return new TranslationOutcome("", null, Cancelled: true);
        }
        catch (Exception ex)
        {
            return TranslationOutcome.Failed(ex.Message);
        }
        finally
        {
            // O próximo intervalo é sorteado agora, tenha a requisição dado certo ou não:
            // uma falha seguida de retentativa imediata é o padrão mais automatizado que
            // existe, e é exatamente o que RF-254 quer evitar.
            ScheduleNext();
        }
    }

    /// <summary>RF-254 / P-56 🔒 — Sorteia o intervalo até a próxima requisição.</summary>
    private void ScheduleNext()
        => _nextAllowed = _now() + TimeSpan.FromMilliseconds(
            _random.Next(0, P.PostRequestRandomDelayMaxMs + 1));

    private string BuildBody(string text, TranslationContext context)
    {
        var body = new Dictionary<string, string>
        {
            [_options.TextField] = text,
            [_options.SourceField] = context.SourceCode,
            [_options.TargetField] = context.TargetCode,
        };

        return JsonSerializer.Serialize(body);
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
