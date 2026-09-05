using System.Net;
using System.Text.Json;
using Gort.Core.Translation;
using Gort.Core.Translation.Presets;
using Gort.Core.Translation.Services;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>VI.5 / RF-292 a RF-301 — API personalizada.</summary>
public class CustomApiTranslatorTests
{
    private static readonly TranslationContext Context =
        new() { SourceCode = "en", TargetCode = "pt-BR" };

    private static (CustomApiTranslator Service, Recorder Handler) Build(
        string response, ApiPreset? preset = null,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new Recorder { Status = status, Body = response };
        var service = new CustomApiTranslator(
            "https://exemplo.invalido/traduzir", preset, new HttpClient(handler));
        return (service, handler);
    }

    // ── RF-292 — o formato padrão ───────────────────────────────────────────

    /// <summary>
    /// RF-292 — Sem preset, o POST leva nome, texto, código de destino e código de origem.
    /// </summary>
    [Fact]
    public async Task RF_292_o_corpo_padrao_leva_os_quatro_campos()
    {
        var (service, handler) = Build("""{"result": "Olá", "errorCode": "0"}""");

        await service.TranslateAsync("Hello", Context, CancellationToken.None);

        using var sent = JsonDocument.Parse(handler.Sent);
        Assert.Equal("Hello", sent.RootElement.GetProperty("text").GetString());
        Assert.Equal("pt-BR", sent.RootElement.GetProperty("resultCode").GetString());
        Assert.Equal("en", sent.RootElement.GetProperty("sourceCode").GetString());
        Assert.True(sent.RootElement.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task RF_292_o_resultado_sai_do_campo_de_resultado()
    {
        var (service, _) = Build("""{"result": "Olá mundo", "errorCode": "0"}""");

        var outcome = await service.TranslateAsync("Hello", Context, CancellationToken.None);

        Assert.Null(outcome.Error);
        Assert.Equal("Olá mundo", outcome.Text);
    }

    /// <summary>RF-293 — Código de erro diferente de "0" produz erro com a mensagem recebida.</summary>
    [Fact]
    public async Task RF_293_codigo_de_erro_produz_erro_com_a_mensagem_recebida()
    {
        var (service, _) = Build(
            """{"result": "", "errorCode": "7", "errorMessage": "cota esgotada"}""");

        var outcome = await service.TranslateAsync("Hello", Context, CancellationToken.None);

        Assert.Equal("cota esgotada", outcome.Error);
    }

    [Fact]
    public async Task RF_293_o_codigo_zero_nao_e_erro()
    {
        var (service, _) = Build("""{"result": "Olá", "errorCode": 0}""");

        Assert.Null((await service.TranslateAsync("Hello", Context, CancellationToken.None)).Error);
    }

    /// <summary>RF-294 — O campo de resultado pode ser VETOR; as partes são concatenadas.</summary>
    [Fact]
    public async Task RF_294_um_vetor_de_resultado_e_concatenado()
    {
        var (service, _) = Build("""{"result": ["Olá ", "mundo"], "errorCode": "0"}""");

        var outcome = await service.TranslateAsync("Hello", Context, CancellationToken.None);

        Assert.Equal("Olá mundo", outcome.Text);
    }

    // ── RF-295 a RF-301 — com preset ────────────────────────────────────────

    /// <summary>
    /// RF-296 / RF-297 — Com preset, o corpo sai do MODELO do usuário, na sintaxe relaxada.
    /// </summary>
    [Fact]
    public async Task RF_296_o_corpo_sai_do_modelo_do_preset()
    {
        var preset = new ApiPreset
        {
            Name = "meu",
            RequestTemplate = "model = gpt-4, prompt = {OCR_TEXT}, lang = {RESULT_CODE}",
            ResponseTemplate = """{"saida": "{RESULT_TEXT}"}""",
        };

        var (service, handler) = Build("""{"saida": "Olá"}""", preset);

        var outcome = await service.TranslateAsync("Hello", Context, CancellationToken.None);

        using var sent = JsonDocument.Parse(handler.Sent);
        Assert.Equal("gpt-4", sent.RootElement.GetProperty("model").GetString());
        Assert.Equal("Hello", sent.RootElement.GetProperty("prompt").GetString());
        Assert.Equal("pt-BR", sent.RootElement.GetProperty("lang").GetString());
        Assert.Equal("Olá", outcome.Text);
    }

    /// <summary>
    /// RF-300 — A chave do resultado é procurada RECURSIVAMENTE: a resposta de um serviço
    /// real quase nunca é plana.
    /// </summary>
    [Fact]
    public async Task RF_300_a_chave_do_resultado_e_encontrada_aninhada()
    {
        var preset = new ApiPreset
        {
            Name = "aninhado",
            RequestTemplate = "texto = {OCR_TEXT}",
            ResponseTemplate = "content = {RESULT_TEXT}",
        };

        var (service, _) = Build(
            """{"choices": [{"message": {"role": "assistant", "content": "Olá"}}]}""",
            preset);

        Assert.Equal("Olá",
            (await service.TranslateAsync("Hello", Context, CancellationToken.None)).Text);
    }

    /// <summary>RF-301 — Os cabeçalhos do preset vão na requisição.</summary>
    [Fact]
    public async Task RF_301_os_cabecalhos_do_preset_vao_na_requisicao()
    {
        var preset = new ApiPreset
        {
            Name = "comchave",
            Headers = "Authorization: Bearer abc\nlinha errada\nX-Origem: gort",
            RequestTemplate = "texto = {OCR_TEXT}",
            ResponseTemplate = "saida = {RESULT_TEXT}",
        };

        var log = new List<string>();
        var handler = new Recorder { Body = """{"saida": "Olá"}""" };
        using var service = new CustomApiTranslator(
            "https://exemplo.invalido/t", preset, new HttpClient(handler), log.Add);

        await service.TranslateAsync("Hello", Context, CancellationToken.None);

        Assert.Contains("Authorization: Bearer abc", handler.Headers);
        Assert.Contains("X-Origem: gort", handler.Headers);
        Assert.Single(log);   // a linha malformada foi registrada e ignorada
    }

    /// <summary>
    /// RF-295 — O preset pode fixar os códigos de idioma em vez de usar os do contexto.
    /// </summary>
    [Fact]
    public async Task RF_295_o_preset_pode_fixar_os_codigos_de_idioma()
    {
        var preset = new ApiPreset
        {
            Name = "fixo",
            SameLanguageCodesAsWeb = false,
            SourceCode = "eng",
            TargetCode = "por",
            RequestTemplate = "de = {SOURCE_CODE}, para = {RESULT_CODE}",
            ResponseTemplate = "saida = {RESULT_TEXT}",
        };

        var (service, handler) = Build("""{"saida": "Olá"}""", preset);

        await service.TranslateAsync("Hello", Context, CancellationToken.None);

        using var sent = JsonDocument.Parse(handler.Sent);
        Assert.Equal("eng", sent.RootElement.GetProperty("de").GetString());
        Assert.Equal("por", sent.RootElement.GetProperty("para").GetString());
    }

    /// <summary>
    /// RF-299 — Um modelo inválido devolve erro ANTES de qualquer chamada: não faz sentido
    /// gastar uma requisição com um corpo que já se sabe quebrado.
    /// </summary>
    [Fact]
    public async Task RF_299_um_modelo_invalido_nao_chega_a_fazer_a_chamada()
    {
        var preset = new ApiPreset
        {
            Name = "quebrado",
            RequestTemplate = """{"aberto": [1, 2 """,
            ResponseTemplate = "saida = {RESULT_TEXT}",
        };

        var (service, handler) = Build("""{"saida": "Olá"}""", preset);

        var outcome = await service.TranslateAsync("Hello", Context, CancellationToken.None);

        Assert.NotNull(outcome.Error);
        Assert.Equal(0, handler.Calls);
    }

    // ── PARTE VIII ──────────────────────────────────────────────────────────

    /// <summary>
    /// PARTE VIII — "Serviço devolve resposta em formato inesperado": mensagem descrevendo a
    /// falha de ANÁLISE, e o laço continua.
    /// </summary>
    [Fact]
    public async Task PARTE_VIII_resposta_em_formato_inesperado_vira_mensagem()
    {
        var (service, _) = Build("<html>erro do servidor</html>");

        var outcome = await service.TranslateAsync("Hello", Context, CancellationToken.None);

        Assert.NotNull(outcome.Error);
        Assert.Contains("formato inesperado", outcome.Error!);
    }

    /// <summary>PARTE VIII — "Rede cai": a mensagem é exibida e o laço continua.</summary>
    [Fact]
    public async Task PARTE_VIII_uma_falha_de_rede_vira_mensagem()
    {
        var handler = new Recorder { Throw = new HttpRequestException("sem rota para o host") };
        using var service = new CustomApiTranslator(
            "https://exemplo.invalido/t", null, new HttpClient(handler));

        var outcome = await service.TranslateAsync("Hello", Context, CancellationToken.None);

        Assert.Contains("sem rota", outcome.Error!);
    }

    [Fact]
    public async Task Um_codigo_http_de_erro_e_relatado_com_o_numero()
    {
        var (service, _) = Build("acesso negado", status: HttpStatusCode.Forbidden);

        var outcome = await service.TranslateAsync("Hello", Context, CancellationToken.None);

        Assert.Contains("403", outcome.Error!);
    }

    [Fact]
    public async Task Sem_endereco_o_servico_recusa_sem_chamar_nada()
    {
        var handler = new Recorder();
        using var service = new CustomApiTranslator("", null, new HttpClient(handler));

        Assert.NotNull((await service.TranslateAsync("Hello", Context, CancellationToken.None)).Error);
        Assert.Equal(0, handler.Calls);
    }

    /// <summary>
    /// RF-306 — Cada preset é uma entrada SEPARADA na lista de serviços; o identificador
    /// carrega o nome do preset para que o perfil aponte para o preset certo.
    /// </summary>
    [Fact]
    public void RF_306_o_identificador_carrega_o_nome_do_preset()
    {
        using var semPreset = new CustomApiTranslator("https://x.invalido");
        using var comPreset = new CustomApiTranslator(
            "https://x.invalido", new ApiPreset { Name = "servidor de casa" });

        Assert.Equal("customapi", semPreset.Key);
        Assert.Equal("customapi:servidor de casa", comPreset.Key);
    }

    // ── Dublê ───────────────────────────────────────────────────────────────

    private sealed class Recorder : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public string Body { get; init; } = "{}";
        public Exception? Throw { get; init; }

        public string Sent { get; private set; } = "";
        public List<string> Headers { get; } = new();
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Throw is not null) throw Throw;

            Sent = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);

            foreach (var header in request.Headers)
                Headers.Add($"{header.Key}: {string.Join(", ", header.Value)}");

            return new HttpResponseMessage(Status) { Content = new StringContent(Body) };
        }
    }
}
