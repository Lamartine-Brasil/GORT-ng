using Gort.Core.Catalog;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>
/// Critério de aceite de RF-566/RF-567: acrescentar um idioma, um motor de OCR ou um
/// serviço de tradução é uma alteração de DADOS. Estes testes leem os arquivos reais
/// de <c>data/</c> — se alguém mover uma decisão para o código, eles quebram.
/// </summary>
public class CatalogTests
{
    private static AppCatalog Load() => AppCatalog.Load(TestPaths.DataDirectory);

    [Fact]
    public void Carrega_os_arquivos_de_dados_reais()
    {
        var c = Load();
        Assert.NotEmpty(c.Languages);
        Assert.NotEmpty(c.OcrEngines);
        Assert.NotEmpty(c.TranslationServices);
    }

    [Fact]
    public void RF_309_escopo_inicial_tem_ingles_japones_e_portugues_do_brasil()
    {
        var c = Load();
        Assert.NotNull(c.Language("en"));
        Assert.NotNull(c.Language("ja"));
        Assert.NotNull(c.Language("pt-BR"));
    }

    [Fact]
    public void RF_314_idioma_de_destino_padrao_e_portugues_do_brasil()
        => Assert.Equal("pt-BR", Load().DefaultTargetLanguage);

    [Fact]
    public void RF_225_servico_padrao_e_o_tradutor_web_gratuito()
        => Assert.Equal("webfree", Load().DefaultTranslationService);

    [Fact]
    public void RF_311_japones_nao_separa_palavras_por_espaco_e_admite_vertical()
    {
        var ja = Load().Language("ja")!;
        Assert.False(ja.SeparatesWordsBySpace);   // governa RF-148
        Assert.True(ja.SupportsVertical);
        Assert.False(ja.RightToLeft);
    }

    [Fact]
    public void RF_311_ingles_separa_palavras_por_espaco()
        => Assert.True(Load().Language("en")!.SeparatesWordsBySpace);

    [Fact]
    public void RF_324_nenhum_idioma_do_escopo_inicial_declara_direita_para_esquerda()
        => Assert.All(Load().Languages, l => Assert.False(l.RightToLeft));

    [Fact]
    public void RF_122_o_motor_de_nuvem_nao_serve_para_tempo_real()
        => Assert.False(Load().OcrEngine("cloud")!.Realtime);

    [Fact]
    public void RF_351_motores_sem_posicao_de_palavra_sao_identificaveis()
    {
        var c = Load();
        Assert.True(c.OcrEngine("modern")!.WordPositions);
        Assert.False(c.OcrEngine("interpreted")!.WordPositions);
    }

    [Fact]
    public void RF_214_localdb_e_localproc_nao_usam_memoria_de_resultados()
    {
        var c = Load();
        Assert.False(c.Service("localdb")!.UsesResultMemory);
        Assert.False(c.Service("localproc")!.UsesResultMemory);
        Assert.True(c.Service("webfree")!.UsesResultMemory);
    }

    [Fact]
    public void RF_221_o_banco_de_dados_local_nao_consulta_a_coletanea()
        => Assert.False(Load().Service("localdb")!.UsesCollection);

    [Fact]
    public void RF_259_so_a_planilha_em_nuvem_suporta_traducao_ponte()
    {
        var c = Load();
        var comBridge = c.TranslationServices.Where(s => s.SupportsBridge).Select(s => s.Key);
        Assert.Equal(new[] { "spreadsheet" }, comBridge);
    }

    [Fact]
    public void RF_226_o_modelo_de_linguagem_e_secundario_e_nunca_o_padrao()
    {
        var c = Load();
        Assert.True(c.Service("llm")!.Secondary);
        Assert.NotEqual("llm", c.DefaultTranslationService);
    }

    [Fact]
    public void RF_232_os_tokens_separadores_vem_dos_dados()
    {
        var c = Load();
        Assert.Equal("//////", c.Service("webfree")!.SeparatorToken);   // P-51
        Assert.Equal("@@@@@@", c.Service("browser")!.SeparatorToken);   // P-52
    }

    [Fact]
    public void RF_313_o_idioma_de_destino_padrao_aparece_em_primeiro_lugar()
    {
        var c = Load();
        var alvos = c.LanguagesFor(c.Service("webfree")!, targetList: true);
        Assert.Equal("pt-BR", alvos[0].Key);
    }

    [Fact]
    public void RF_308_e_RF_511_idioma_sem_codigo_nao_aparece_na_lista_do_servico()
    {
        var c = Load();
        var svc = c.Service("localdb")!;   // não declara códigos para nenhum idioma
        Assert.Empty(c.LanguagesFor(svc, targetList: false));
    }

    [Theory]
    [InlineData("en", "en-US", true)]
    [InlineData("en-US", "en", true)]
    [InlineData("EN", "en-us", true)]
    [InlineData("en", "ja", false)]
    [InlineData("pt-BR", "pt-PT", true)]   // mesma subetiqueta primária
    [InlineData("", "en", false)]
    public void RF_316_en_e_en_US_sao_equivalentes(string a, string b, bool esperado)
        => Assert.Equal(esperado, AppCatalog.CodesEquivalent(a, b));

    [Fact]
    public void RF_151_a_lista_de_idiomas_de_um_motor_e_a_intersecao_com_a_tabela()
    {
        var c = Load();
        var idiomas = c.LanguagesFor(c.OcrEngine("modern")!);
        Assert.Equal(new[] { "en", "ja" }, idiomas.Select(l => l.Key));
    }

    [Fact]
    public void RF_136_a_lista_pode_ser_restringida_aos_idiomas_instalados()
    {
        var c = Load();
        var idiomas = c.LanguagesFor(c.OcrEngine("system")!, installed: new[] { "jpn" });
        Assert.Equal(new[] { "ja" }, idiomas.Select(l => l.Key));
    }

    [Fact]
    public void RF_279_a_lista_de_modelos_de_linguagem_vem_dos_dados()
    {
        var llm = Load().Llm;
        Assert.NotEmpty(llm.Models);
        Assert.Contains(llm.DefaultModel, llm.Models);
    }

    [Fact]
    public void RF_280_a_familia_e_o_porte_sao_deduzidos_do_nome_do_modelo()
    {
        var llm = Load().Llm;
        Assert.True(llm.IsLegacyFamily("gemini-2.0-flash"));
        Assert.False(llm.IsLegacyFamily("gemini-2.5-flash"));
        Assert.True(llm.IsPro("gemini-2.5-pro"));
        Assert.False(llm.IsPro("gemini-2.5-flash"));
    }

    [Fact]
    public void RF_387_ha_uma_lista_de_fontes_de_reserva_nos_dados()
        => Assert.NotEmpty(Load().FontFallbacks);

    [Fact]
    public void RF_028_identificador_desconhecido_devolve_nulo_em_vez_de_lancar()
    {
        var c = Load();
        Assert.Null(c.Service("servico_que_nao_existe"));
        Assert.Null(c.OcrEngine("motor_que_nao_existe"));
        Assert.Null(c.Language(null));
    }

    [Fact]
    public void P8_catalogo_ausente_nao_impede_a_abertura()
    {
        var c = AppCatalog.Load(Path.Combine(Path.GetTempPath(), "gort-pasta-que-nao-existe"));
        Assert.Empty(c.Languages);
        Assert.Empty(c.OcrEngines);
    }
}
