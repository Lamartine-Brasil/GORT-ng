using System.Text.Json;
using Gort.Core.Translation.Services;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>RF-296 a RF-301 — Modelo de requisição e de resposta da API personalizada.</summary>
public class RequestTemplateTests
{
    private static string Build(string template, string text = "Hello",
                                string source = "en", string target = "pt-BR")
        => RequestTemplate.Build(template, text, source, target, out _)!;

    // ── RF-296 — substituição dos marcadores ────────────────────────────────

    [Fact]
    public void RF_296_os_tres_marcadores_sao_substituidos()
    {
        string json = Build(
            """{"texto": "{OCR_TEXT}", "de": "{SOURCE_CODE}", "para": "{RESULT_CODE}"}""");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Hello", doc.RootElement.GetProperty("texto").GetString());
        Assert.Equal("en", doc.RootElement.GetProperty("de").GetString());
        Assert.Equal("pt-BR", doc.RootElement.GetProperty("para").GetString());
    }

    /// <summary>
    /// RF-296 — O texto é ESCAPADO para JSON. Sem o escape, um texto reconhecido com aspas
    /// ou quebra de linha — que é o caso comum — quebraria o JSON inteiro.
    /// </summary>
    [Fact]
    public void RF_296_o_texto_reconhecido_e_escapado()
    {
        string json = Build("""{"texto": "{OCR_TEXT}"}""",
                            text: "Ele disse \"olá\"\ne saiu\\");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Ele disse \"olá\"\ne saiu\\",
                     doc.RootElement.GetProperty("texto").GetString());
    }

    // ── RF-297 — sintaxe relaxada ───────────────────────────────────────────

    [Fact]
    public void RF_297_a_sintaxe_relaxada_vira_json()
    {
        string json = Build("modelo = gpt-4, texto = {OCR_TEXT}, temperatura = 0.7, "
                            + "fluxo = false, extra = null");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("gpt-4", doc.RootElement.GetProperty("modelo").GetString());
        Assert.Equal("Hello", doc.RootElement.GetProperty("texto").GetString());
        Assert.Equal(0.7, doc.RootElement.GetProperty("temperatura").GetDouble(), 3);
        Assert.False(doc.RootElement.GetProperty("fluxo").GetBoolean());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("extra").ValueKind);
    }

    /// <summary>RF-297 — Textos entre aspas são PRESERVADOS como estão.</summary>
    [Fact]
    public void RF_297_textos_entre_aspas_sao_preservados()
    {
        string json = Build("""nota = "vale 1, 2 e 3", numero = 5""");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("vale 1, 2 e 3", doc.RootElement.GetProperty("nota").GetString());
        Assert.Equal(5, doc.RootElement.GetProperty("numero").GetInt32());
    }

    /// <summary>RF-297 — Vetores são convertidos ELEMENTO A ELEMENTO.</summary>
    [Fact]
    public void RF_297_vetores_sao_convertidos_elemento_a_elemento()
    {
        string json = Build("paradas = [alfa, 2, true], texto = {OCR_TEXT}");

        using var doc = JsonDocument.Parse(json);
        var array = doc.RootElement.GetProperty("paradas");

        Assert.Equal(3, array.GetArrayLength());
        Assert.Equal("alfa", array[0].GetString());
        Assert.Equal(2, array[1].GetInt32());
        Assert.True(array[2].GetBoolean());
    }

    /// <summary>Um objeto aninhado passa pela mesma conversão, recursivamente.</summary>
    [Fact]
    public void RF_297_objetos_aninhados_tambem_sao_convertidos()
    {
        string json = Build("opcoes = { idioma = {RESULT_CODE}, forcar = true }");

        using var doc = JsonDocument.Parse(json);
        var options = doc.RootElement.GetProperty("opcoes");

        Assert.Equal("pt-BR", options.GetProperty("idioma").GetString());
        Assert.True(options.GetProperty("forcar").GetBoolean());
    }

    // ── RF-298 / RF-299 — chaves e validação ────────────────────────────────

    [Fact]
    public void RF_298_chaves_ausentes_sao_acrescentadas()
    {
        string json = Build("""  "texto": "{OCR_TEXT}"  """);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Hello", doc.RootElement.GetProperty("texto").GetString());
    }

    /// <summary>
    /// RF-299 — Um modelo que não vira JSON válido devolve ERRO descrevendo a falha de
    /// CONVERSÃO. Quem digitou o modelo precisa saber que o problema está nele, e não
    /// concluir que o serviço está fora do ar.
    /// </summary>
    [Fact]
    public void RF_299_um_modelo_invalido_devolve_erro_de_conversao()
    {
        var built = RequestTemplate.Build("""{"aberto": [1, 2 """, "x", "en", "pt",
                                          out string? error);

        Assert.Null(built);
        Assert.NotNull(error);
        Assert.Contains("modelo de requisição", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Um_modelo_vazio_devolve_erro()
    {
        Assert.Null(RequestTemplate.Build("   ", "x", "en", "pt", out string? error));
        Assert.NotNull(error);
    }

    /// <summary>
    /// JSON já válido passa INTOCADO. Convertê-lo pela via relaxada poderia estragá-lo, e
    /// quem escreveu JSON válido sabe o que está fazendo.
    /// </summary>
    [Fact]
    public void Um_modelo_ja_valido_passa_intocado()
    {
        string template = """{"a":{"b":[1,2,3]},"texto":"{OCR_TEXT}"}""";
        string json = Build(template);

        Assert.Equal(template.Replace("{OCR_TEXT}", "Hello"), json);
    }

    // ── RF-300 — o modelo de resposta ───────────────────────────────────────

    [Fact]
    public void RF_300_a_chave_do_resultado_sai_do_modelo_de_resposta()
    {
        Assert.Equal("traducao",
            RequestTemplate.ResultKeyOf("""{"traducao": "{RESULT_TEXT}", "erro": "0"}"""));

        Assert.Equal("saida",
            RequestTemplate.ResultKeyOf("saida = {RESULT_TEXT}"));

        Assert.Null(RequestTemplate.ResultKeyOf("""{"nada": "aqui"}"""));
    }

    /// <summary>
    /// RF-300 — A busca é RECURSIVA, em qualquer nível de aninhamento. A resposta de um
    /// serviço real quase nunca é plana, e exigir o caminho completo tornaria o recurso
    /// inútil para quem não conhece a API de cor.
    /// </summary>
    [Fact]
    public void RF_300_a_chave_e_procurada_em_qualquer_nivel()
    {
        Assert.Equal("Olá mundo", RequestTemplate.FindResult(
            """{"data": {"result": {"traducao": "Olá mundo"}}}""", "traducao"));

        Assert.Equal("Olá", RequestTemplate.FindResult(
            """{"choices": [{"message": {"content": "Olá"}}]}""", "content"));
    }

    /// <summary>RF-294 — O resultado pode ser VETOR de textos; as partes são concatenadas.</summary>
    [Fact]
    public void RF_294_um_vetor_de_textos_e_concatenado()
    {
        Assert.Equal("Olá mundo", RequestTemplate.FindResult(
            """{"traducao": ["Olá ", "mundo"]}""", "traducao"));
    }

    [Fact]
    public void Uma_resposta_sem_a_chave_devolve_nulo()
    {
        Assert.Null(RequestTemplate.FindResult("""{"outra": "coisa"}""", "traducao"));
        Assert.Null(RequestTemplate.FindResult("isto não é json", "traducao"));
    }

    // ── RF-301 — cabeçalhos ─────────────────────────────────────────────────

    /// <summary>
    /// RF-301 — Linhas malformadas são REGISTRADAS e ignoradas. Uma linha errada entre
    /// cinco não pode impedir as outras quatro de funcionarem.
    /// </summary>
    [Fact]
    public void RF_301_cabecalhos_malformados_sao_registrados_e_ignorados()
    {
        var log = new List<string>();

        var headers = RequestTemplate.ParseHeaders(
            "Authorization: Bearer abc\nisto está errado\nContent-Type: application/json\n"
            + ": sem nome\nX-Vazio:",
            log.Add);

        Assert.Equal(2, headers.Count);
        Assert.Equal(("Authorization", "Bearer abc"), headers[0]);
        Assert.Equal(("Content-Type", "application/json"), headers[1]);
        Assert.Equal(3, log.Count);
    }

    [Fact]
    public void Um_valor_com_dois_pontos_sobrevive()
    {
        var headers = RequestTemplate.ParseHeaders("Referer: https://exemplo.com/x");

        Assert.Equal(("Referer", "https://exemplo.com/x"), Assert.Single(headers));
    }
}
