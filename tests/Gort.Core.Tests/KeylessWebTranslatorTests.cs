using System.Net;
using System.Text.Json;
using Gort.Core.Calibration;
using Gort.Core.Translation;
using Gort.Core.Translation.Services;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>VI.2 / RF-254 🔒 — Tradutor web sem chave, com espaçamento aleatório.</summary>
public class KeylessWebTranslatorTests
{
    private static readonly TranslationContext Context =
        new() { SourceCode = "en", TargetCode = "pt-BR" };

    private sealed class Clock
    {
        public DateTime Now = new(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
        public DateTime Read() => Now;
    }

    private sealed class Handler : HttpMessageHandler
    {
        public string Body { get; init; } = """{"result": "Olá"}""";
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public string Sent { get; private set; } = "";
        public List<string> Headers { get; } = new();
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Sent = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            foreach (var h in request.Headers) Headers.Add(h.Key);

            return new HttpResponseMessage(Status) { Content = new StringContent(Body) };
        }
    }

    private static KeylessWebTranslatorOptions Options() => new()
    {
        Endpoint = "https://exemplo.invalido/traduzir",
        Headers = new[]
        {
            ("User-Agent", "Mozilla/5.0"),
            ("Accept-Language", "pt-BR,pt;q=0.9"),
        },
    };

    [Fact]
    public async Task O_texto_e_os_codigos_vao_no_corpo()
    {
        var handler = new Handler();
        using var service = new KeylessWebTranslator(Options(), new HttpClient(handler));

        await service.TranslateAsync("Hello", Context, CancellationToken.None);

        using var sent = JsonDocument.Parse(handler.Sent);
        Assert.Equal("Hello", sent.RootElement.GetProperty("text").GetString());
        Assert.Equal("en", sent.RootElement.GetProperty("source").GetString());
        Assert.Equal("pt-BR", sent.RootElement.GetProperty("target").GetString());
    }

    /// <summary>
    /// RF-254 — Cabeçalhos de NAVEGADOR. Eles vêm dos dados porque mudam com o tempo e sem
    /// aviso: um valor embutido no código envelheceria dentro do executável.
    /// </summary>
    [Fact]
    public async Task RF_254_os_cabecalhos_de_navegador_vao_na_requisicao()
    {
        var handler = new Handler();
        using var service = new KeylessWebTranslator(Options(), new HttpClient(handler));

        await service.TranslateAsync("Hello", Context, CancellationToken.None);

        Assert.Contains("User-Agent", handler.Headers);
        Assert.Contains("Accept-Language", handler.Headers);
    }

    /// <summary>
    /// RF-254 / P-56 🔒 — Depois de cada requisição, um intervalo ALEATÓRIO entre 0 e P-56
    /// antes de aceitar a próxima.
    ///
    /// O que denuncia um programa não é a frequência, é a REGULARIDADE: requisições a cada
    /// exatos 300 ms não vêm de gente. Por isso o intervalo é sorteado, e não fixo.
    /// </summary>
    [Fact]
    public async Task RF_254_o_intervalo_seguinte_fica_entre_zero_e_P56()
    {
        var clock = new Clock();
        using var service = new KeylessWebTranslator(
            Options(), new HttpClient(new Handler()), new Random(1), clock.Read);

        await service.TranslateAsync("Hello", Context, CancellationToken.None);

        var delay = service.NextAllowed - clock.Now;

        Assert.True(delay >= TimeSpan.Zero);
        Assert.True(delay <= TimeSpan.FromMilliseconds(P.PostRequestRandomDelayMaxMs));
        Assert.Equal(650, P.PostRequestRandomDelayMaxMs);
    }

    /// <summary>
    /// RF-254 — O intervalo VARIA entre requisições. Um valor constante seria tão regular
    /// quanto não ter intervalo nenhum, e é a regularidade que o requisito ataca.
    /// </summary>
    [Fact]
    public async Task RF_254_o_intervalo_varia_entre_requisicoes()
    {
        var clock = new Clock();
        using var service = new KeylessWebTranslator(
            Options(), new HttpClient(new Handler()), new Random(99), clock.Read);

        var delays = new List<TimeSpan>();
        for (int i = 0; i < 12; i++)
        {
            await service.TranslateAsync("Hello", Context, CancellationToken.None);
            delays.Add(service.NextAllowed - clock.Now);

            // O relógio não anda: o teste mede o SORTEIO, não a espera.
        }

        Assert.True(delays.Distinct().Count() > 1,
                    "o intervalo saiu constante — a regularidade é o que RF-254 ataca");
    }

    /// <summary>
    /// O intervalo é sorteado TENHA A REQUISIÇÃO DADO CERTO OU NÃO: uma falha seguida de
    /// retentativa imediata é o padrão mais automatizado que existe.
    /// </summary>
    [Fact]
    public async Task RF_254_uma_falha_tambem_agenda_o_intervalo()
    {
        var clock = new Clock();
        using var service = new KeylessWebTranslator(
            Options(), new HttpClient(new Handler { Status = HttpStatusCode.TooManyRequests }),
            new Random(3), clock.Read);

        var outcome = await service.TranslateAsync("Hello", Context, CancellationToken.None);

        Assert.NotNull(outcome.Error);
        Assert.True(service.NextAllowed > DateTime.MinValue);
    }

    [Fact]
    public async Task Sem_endereco_o_servico_recusa_sem_chamar_nada()
    {
        var handler = new Handler();
        using var service = new KeylessWebTranslator(
            new KeylessWebTranslatorOptions(), new HttpClient(handler));

        Assert.NotNull((await service.TranslateAsync("x", Context, CancellationToken.None)).Error);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Uma_resposta_inesperada_vira_mensagem()
    {
        using var service = new KeylessWebTranslator(
            Options(), new HttpClient(new Handler { Body = "<html>erro</html>" }));

        Assert.NotNull((await service.TranslateAsync("x", Context, CancellationToken.None)).Error);
    }
}

/// <summary>RF-029 / RF-254 — As opções do tradutor sem chave vêm dos DADOS.</summary>
public class KeylessWebCatalogTests
{
    [Fact]
    public void RF_254_os_cabecalhos_de_navegador_vem_do_catalogo()
    {
        var catalog = Gort.Core.Catalog.AppCatalog.Load(TestPaths.DataDirectory);
        var options = catalog.KeylessWebTranslator;

        Assert.NotNull(options);
        Assert.NotEmpty(options!.Headers);
        Assert.Contains(options.Headers, h => h.Name == "User-Agent");
    }

    /// <summary>
    /// O endereço vazio DESABILITA o serviço — é o estado desta versão, porque o endereço
    /// depende do fornecedor e não é nosso para distribuir. O serviço recusa e explica, em
    /// vez de o programa fingir que ele não existe.
    /// </summary>
    [Fact]
    public async Task Sem_endereco_nos_dados_o_servico_recusa_e_explica()
    {
        var catalog = Gort.Core.Catalog.AppCatalog.Load(TestPaths.DataDirectory);
        if (catalog.KeylessWebTranslator is not { Endpoint.Length: 0 } options) return;

        using var service = new KeylessWebTranslator(options);

        var outcome = await service.TranslateAsync(
            "Hello", new TranslationContext { SourceCode = "en", TargetCode = "pt" },
            CancellationToken.None);

        Assert.NotNull(outcome.Error);
        Assert.Contains("não está configurado", outcome.Error!);
    }
}
