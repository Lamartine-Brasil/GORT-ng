using Gort.Core.Calibration;
using Gort.Core.Structuring;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>Cap. 15.3 — Tratamento textual e montagem do texto exibido.</summary>
public class TextPostProcessorTests
{
    [Fact]
    public void RF_180_a_remocao_de_espacos_tira_todos_os_espacos_mantendo_as_quebras()
    {
        Assert.Equal("abcdef\nghi", TextPostProcessor.RemoveAllSpaces("abc def\ng hi"));
    }

    [Fact]
    public void RF_180_e_RF_181_a_remocao_de_espacos_vem_ANTES_do_dicionario()
    {
        // O dicionário corrige "ABC"; só encontra o alvo se os espaços já tiverem saído.
        var dict = new CorrectionDictionary();
        dict.Add("ABC", "certo");

        var o = new TextProcessingOptions { RemoveSpaces = true, UseDictionary = true };
        Assert.Equal("certo", TextPostProcessor.Treat("A B C", o, dict));

        // Sem remoção de espaços, o dicionário não casa.
        var o2 = new TextProcessingOptions { RemoveSpaces = false, UseDictionary = true };
        Assert.Equal("A B C", TextPostProcessor.Treat("A B C", o2, dict));
    }

    [Fact]
    public void RF_187_no_modo_sobreposicao_as_quebras_de_linha_NAO_sao_removidas()
    {
        var o = new TextProcessingOptions { WindowMode = WindowMode.Overlay };
        Assert.Equal("uma\ndois", TextPostProcessor.JoinLineBreaks("uma\ndois", o));
    }

    [Fact]
    public void RF_186_fora_da_sobreposicao_as_quebras_viram_espaco()
    {
        var o = new TextProcessingOptions { WindowMode = WindowMode.Layer };
        Assert.Equal("uma dois", TextPostProcessor.JoinLineBreaks("uma\ndois", o));
    }

    [Fact]
    public void RF_186_com_remocao_de_espacos_as_quebras_viram_nada()
    {
        var o = new TextProcessingOptions { WindowMode = WindowMode.Dark, RemoveSpaces = true };
        Assert.Equal("umadois", TextPostProcessor.JoinLineBreaks("uma\ndois", o));
    }

    [Fact]
    public void RF_186_o_banco_de_dados_local_preserva_as_quebras()
    {
        var o = new TextProcessingOptions
        {
            WindowMode = WindowMode.Dark, ServiceIsLocalDatabase = true,
        };
        Assert.Equal("uma\ndois", TextPostProcessor.JoinLineBreaks("uma\ndois", o));
    }

    [Fact]
    public void RF_186_o_modo_uma_linha_por_traducao_preserva_as_quebras()
    {
        var o = new TextProcessingOptions
        {
            WindowMode = WindowMode.Dark, OneLinePerTranslation = true,
        };
        Assert.Equal("uma\ndois", TextPostProcessor.JoinLineBreaks("uma\ndois", o));
    }

    [Fact]
    public void RF_188_a_requisicao_da_sobreposicao_carrega_todos_os_blocos_com_o_token()
    {
        string pedido = TextPostProcessor.BuildOverlayRequest(
            new[] { "primeiro", "segundo" }, P.SeparatorToken);
        Assert.Equal("\n//////primeiro\n//////segundo", pedido);
    }

    [Fact]
    public void RF_189_com_uma_unica_area_nao_ha_prefixo()
    {
        string t = TextPostProcessor.BuildDisplayText(
            new[] { (0, "hello", (string?)"olá") }, areaCount: 1, numberAreas: false);
        Assert.Equal("olá", t);
    }

    [Fact]
    public void RF_189_com_varias_areas_e_numeracao_ativa_o_prefixo_e_o_numero()
    {
        string t = TextPostProcessor.BuildDisplayText(
            new[] { (0, "a", (string?)"um"), (1, "b", (string?)"dois") },
            areaCount: 2, numberAreas: true);
        Assert.Equal("1 : um\n2 : dois", t);
    }

    [Fact]
    public void RF_189_com_varias_areas_e_numeracao_inativa_o_prefixo_e_um_traco()
    {
        string t = TextPostProcessor.BuildDisplayText(
            new[] { (0, "a", (string?)"um"), (1, "b", (string?)"dois") },
            areaCount: 2, numberAreas: false);
        Assert.Equal("- um\n- dois", t);
    }

    [Fact]
    public void RF_190_o_marcador_de_sem_resultado_nao_e_concatenado()
    {
        string t = TextPostProcessor.BuildDisplayText(
            new[]
            {
                (0, "a", (string?)"um"),
                (1, "b", (string?)TextPostProcessor.NoResultMarker),
            },
            areaCount: 2, numberAreas: false);
        Assert.Equal("- um", t);
    }

    [Fact]
    public void RF_191_blocos_com_texto_reconhecido_vazio_nao_geram_entrada()
    {
        string t = TextPostProcessor.BuildDisplayText(
            new[] { (0, "", (string?)"ignorado"), (1, "b", (string?)"dois") },
            areaCount: 2, numberAreas: false);
        Assert.Equal("- dois", t);
    }

    // ── Dicionário de correção ───────────────────────────────────────────────

    [Fact]
    public void RF_183_o_modo_por_palavra_so_substitui_em_limites_de_palavra()
    {
        var d = new CorrectionDictionary { WholeWord = true };
        d.Add("cat", "gato");
        Assert.Equal("gato scatter", d.Apply("cat scatter"));

        d.WholeWord = false;
        Assert.Equal("gato sgatoter", d.Apply("cat scatter"));
    }

    [Fact]
    public void RF_182_passagens_adicionais_permitem_correcoes_encadeadas()
    {
        var d = new CorrectionDictionary { ExtraPasses = 0 };
        d.Add("A", "B");
        d.Add("B", "C");
        // Numa única passagem, A→B e depois B→C já acontecem em sequência dentro da mesma
        // passagem, porque as entradas são aplicadas em ordem.
        Assert.Equal("C", d.Apply("A"));

        var d2 = new CorrectionDictionary { ExtraPasses = 3 };
        d2.Add("B", "C");
        d2.Add("A", "B");   // ordem invertida: só uma segunda passagem resolve
        Assert.Equal("C", d2.Apply("A"));
    }

    [Fact]
    public void RF_185_o_formato_do_arquivo_de_dicionario_e_lido_e_escrito()
    {
        string f = Path.Combine(Path.GetTempPath(), "gort-dic",
                                Guid.NewGuid().ToString("N") + ".txt");
        CorrectionDictionary.AppendToFile(f, "erradoo", "correto");
        CorrectionDictionary.AppendToFile(f, "outroo", "outro");

        var d = CorrectionDictionary.Load(f);
        Assert.Equal(2, d.Count);
        Assert.Equal("correto outro", d.Apply("erradoo outroo"));
    }

    [Fact]
    public void Caso_de_erro_dicionario_ausente_nao_corrige_e_nao_falha()
    {
        var d = CorrectionDictionary.Load(Path.Combine(Path.GetTempPath(), "gort-sem-dic.txt"));
        Assert.Equal(0, d.Count);
        Assert.Equal("intacto", d.Apply("intacto"));
    }
}

/// <summary>19.2 — Composição do texto no modo escuro.</summary>
public class DarkModeTextTests
{
    /// <summary>
    /// RF-328 — Com a exibição do texto reconhecido ativa: a tradução, DUAS quebras de
    /// linha, o prefixo "OCR : " e o texto reconhecido.
    /// </summary>
    [Fact]
    public void RF_328_o_texto_reconhecido_vem_depois_de_duas_quebras_com_prefixo()
    {
        string texto = TextPostProcessor.ComposeDarkModeText(
            "Olá mundo", "Hello world", showRecognized: true);

        Assert.Equal("Olá mundo\n\nOCR : Hello world", texto);
    }

    [Fact]
    public void RF_328_com_a_exibicao_desligada_so_a_traducao_aparece()
        => Assert.Equal("Olá mundo",
            TextPostProcessor.ComposeDarkModeText("Olá mundo", "Hello world", false));

    [Fact]
    public void RF_328_sem_texto_reconhecido_nao_ha_prefixo_solto()
    {
        Assert.Equal("Olá mundo",
            TextPostProcessor.ComposeDarkModeText("Olá mundo", "", true));
        Assert.Equal("Olá mundo",
            TextPostProcessor.ComposeDarkModeText("Olá mundo", "   ", true));
    }

    /// <summary>RF-329 — As quebras de qualquer formato são normalizadas antes de exibir.</summary>
    [Theory]
    [InlineData("a\nb")]
    [InlineData("a\r\nb")]
    [InlineData("a\rb")]
    public void RF_329_as_quebras_sao_normalizadas_para_o_formato_da_plataforma(string entrada)
    {
        string saida = TextPostProcessor.NormalizeNewlines(entrada);
        Assert.Equal($"a{Environment.NewLine}b", saida);
    }
}
