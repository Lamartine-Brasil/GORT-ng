using Gort.Core.Calibration;
using Gort.Core.Model;
using Gort.Core.Structuring;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>RF-152 a RF-156 — construção de linhas.</summary>
public class LineTests
{
    [Fact]
    public void RF_152_o_texto_da_linha_termina_com_espaco()
    {
        var line = LineBuilder.Horizontal("Era uma vez", 0, 0, 20);
        Assert.Equal("Era uma vez ", line.Text);
        Assert.EndsWith(" ", line.Text);
    }

    [Fact]
    public void RF_152_uma_unica_palavra_tambem_recebe_o_espaco_final()
        => Assert.Equal("Olá ", LineBuilder.Horizontal("Olá", 0, 0, 20).Text);

    [Fact]
    public void RF_154_a_caixa_da_linha_e_a_uniao_das_caixas_das_palavras()
    {
        var line = new Line(new[]
        {
            new Word { Text = "a", Box = new Rect(10, 5, 20, 30) },
            new Word { Text = "b", Box = new Rect(40, 10, 20, 15) },
        });
        Assert.Equal(new Rect(10, 5, 50, 30), line.Box);
    }

    [Theory]
    // RF-155 / P-33 — vertical quando altura > largura × 1,5.
    [InlineData(10, 15, false)]   // 15 == 10×1,5 → NÃO é maior que
    [InlineData(10, 16, true)]
    [InlineData(10, 14, false)]
    [InlineData(100, 10, false)]
    public void RF_155_classificacao_de_orientacao_usa_P33(int w, int h, bool vertical)
    {
        var o = Line.ClassifyOrientation(new Rect(0, 0, w, h));
        Assert.Equal(vertical ? Orientation.Vertical : Orientation.Horizontal, o);
    }

    [Fact]
    public void RF_153_a_caixa_da_palavra_e_expandida_para_fora()
    {
        // piso no canto superior esquerdo, teto no canto inferior direito.
        var box = Rect.FromWordBox(10.4, 5.9, 20.2, 30.1);
        Assert.Equal(10, box.Left);
        Assert.Equal(5, box.Top);
        Assert.Equal(31, box.Right);    // teto de 30,6
        Assert.Equal(36, box.Bottom);   // teto de 36,0
    }

    [Fact]
    public void RF_153_largura_e_altura_negativas_viram_zero()
    {
        var box = Rect.FromWordBox(10, 10, -5, -5);
        Assert.Equal(0, box.Width);
        Assert.Equal(0, box.Height);
    }

    [Fact]
    public void RF_142_a_caixa_de_um_quadrilatero_usa_minimo_e_maximo()
    {
        // Texto rotacionado: a diferença direta entre dois pontos daria valor negativo.
        var pts = new (double, double)[] { (30, 10), (10, 20), (20, 40), (40, 30) };
        var box = Rect.FromQuad(pts);
        Assert.Equal(new Rect(10, 10, 30, 30), box);
        Assert.True(box.Width > 0 && box.Height > 0);
    }
}

/// <summary>RF-164 — tamanho de fonte estimado.</summary>
public class FontSizeEstimatorTests
{
    private static Word W(int w, int h) => new() { Text = "x", Box = new Rect(0, 0, w, h) };

    [Fact]
    public void RF_164_sem_caixa_valida_devolve_P38()
    {
        Assert.Equal(P.FontSizeFallback, FontSizeEstimator.Estimate(Array.Empty<Word>()));
        Assert.Equal(P.FontSizeFallback, FontSizeEstimator.Estimate(new[] { W(0, 10), W(10, 0) }));
    }

    [Fact]
    public void RF_164_usa_o_minimo_da_caixa()
        => Assert.Equal(12, FontSizeEstimator.Estimate(new[] { W(40, 12) }));

    [Fact]
    public void RF_164_numero_impar_de_amostras_usa_a_central()
        => Assert.Equal(20, FontSizeEstimator.Estimate(new[] { W(10, 10), W(20, 20), W(90, 90) }));

    [Fact]
    public void RF_164_numero_par_usa_a_media_das_duas_centrais()
        => Assert.Equal(15, FontSizeEstimator.Estimate(
            new[] { W(10, 10), W(14, 14), W(16, 16), W(90, 90) }));

    [Fact]
    public void RF_164_a_mediana_resiste_a_caixas_espurias_de_pontuacao()
    {
        // Uma vírgula minúscula e um artefato enorme não deslocam a estimativa.
        var words = new[] { W(2, 2), W(20, 20), W(20, 20), W(20, 20), W(400, 400) };
        Assert.Equal(20, FontSizeEstimator.Estimate(words));
    }
}

/// <summary>RF-163 — adjacência espacial.</summary>
public class AdjacencyTests
{
    [Fact]
    public void RF_163_1_orientacoes_diferentes_nunca_sao_adjacentes()
    {
        var h = LineBuilder.Horizontal("abc def", 0, 0, 20);
        var v = LineBuilder.Vertical("abc def", 0, 0, 20);
        Assert.False(Adjacency.AreAdjacent(h, v));
    }

    [Fact]
    public void RF_163_2_razao_de_fonte_acima_de_P34_reprova()
    {
        var a = LineBuilder.Horizontal("aaa", 0, 0, 20);
        var b = LineBuilder.Horizontal("bbb", 0, 22, 40);   // 40/20 = 2,0 > 1,3
        Assert.False(Adjacency.AreAdjacent(a, b));
    }

    [Fact]
    public void RF_163_2_razao_de_fonte_dentro_de_P34_aprova()
    {
        var a = LineBuilder.Horizontal("aaa", 0, 0, 20);
        var b = LineBuilder.Horizontal("bbb", 0, 22, 24);   // 24/20 = 1,2 ≤ 1,3
        Assert.True(Adjacency.AreAdjacent(a, b));
    }

    [Fact]
    public void RF_163_3_intervalo_no_eixo_de_leitura_acima_de_P35_reprova()
    {
        var a = LineBuilder.Horizontal("aaa", 0, 0, 20);
        // fonte média 20; limite de intervalo = 20 × 1,25 = 25.
        var perto = LineBuilder.Horizontal("bbb", 0, 20 + 25, 20);    // intervalo 25 → passa
        var longe = LineBuilder.Horizontal("bbb", 0, 20 + 26, 20);    // intervalo 26 → reprova
        Assert.True(Adjacency.AreAdjacent(a, perto));
        Assert.False(Adjacency.AreAdjacent(a, longe));
    }

    [Fact]
    public void RF_163_4_sem_sobreposicao_transversal_mas_com_inicios_proximos_aprova()
    {
        // Duas linhas curtas empilhadas, deslocadas lateralmente menos de 2×fonte.
        var a = LineBuilder.Horizontal("aaaa", 0, 0, 20);
        var b = LineBuilder.Horizontal("bbbb", 30, 22, 20);   // |0 − 30| = 30 ≤ 20×2
        Assert.True(Adjacency.AreAdjacent(a, b));
    }

    [Fact]
    public void RF_163_4_sem_sobreposicao_e_com_inicios_distantes_reprova()
    {
        var a = LineBuilder.Horizontal("aa", 0, 0, 20);
        var b = LineBuilder.Horizontal("bb", 500, 22, 20);    // |0 − 500| ≫ 40
        Assert.False(Adjacency.AreAdjacent(a, b));
    }

    [Fact]
    public void Sobreposicao_relativa_usa_o_menor_comprimento_como_denominador()
    {
        // [0,100] contra [0,10]: interseção 10, menor comprimento 10 → 1,0
        Assert.Equal(1.0, Adjacency.Overlap(0, 100, 0, 10), 6);
        // sem interseção
        Assert.Equal(0.0, Adjacency.Overlap(0, 10, 20, 30), 6);
    }

    [Fact]
    public void Intervalo_de_eixo_e_zero_quando_os_segmentos_se_sobrepoem()
    {
        Assert.Equal(0, Adjacency.AxisGap(0, 10, 5, 15));
        Assert.Equal(5, Adjacency.AxisGap(0, 10, 15, 20));
    }

    [Fact]
    public void RF_163_vertical_troca_os_eixos()
    {
        // Duas colunas lado a lado: adjacentes no eixo horizontal.
        var a = LineBuilder.Vertical("aaa bbb", 100, 0, 20);
        var b = LineBuilder.Vertical("ccc ddd", 100 - 20 - 10, 0, 20);
        Assert.True(Adjacency.AreAdjacent(a, b));
    }
}

/// <summary>RF-165 a RF-177 — classificação de linhas.</summary>
public class LineClassifierTests
{
    [Theory]
    [InlineData("• item ", true)]
    [InlineData("● item ", true)]
    [InlineData("・ item ", true)]
    [InlineData("- item ", false)]    // marcador fraco, não forte
    [InlineData("texto ", false)]
    public void RF_166_marcador_forte_usa_o_conjunto_P39(string texto, bool esperado)
        => Assert.Equal(esperado, LineClassifier.HasStrongListMarker(texto));

    [Theory]
    [InlineData("- item ", true)]
    [InlineData("* item ", true)]
    [InlineData(". item ", true)]
    [InlineData("-item ", true)]      // candidato, ainda que não explícito
    [InlineData("texto ", false)]
    public void RF_167_candidato_a_marcador_fraco(string texto, bool esperado)
        => Assert.Equal(esperado, LineClassifier.HasWeakListMarkerCandidate(texto));

    [Theory]
    [InlineData("- item ", true)]
    [InlineData("-item ", false)]     // segundo caractere não é branco
    public void RF_168_marcador_fraco_explicito_exige_branco_no_segundo_caractere(
        string texto, bool esperado)
        => Assert.Equal(esperado, LineClassifier.HasExplicitWeakListMarker(texto));

    [Theory]
    [InlineData("1. item ", true)]
    [InlineData("12) item ", true)]
    [InlineData("(a) item ", true)]
    [InlineData("abc. item ", true)]     // 3 alfanuméricos — no limite de P-150
    [InlineData("abcd. item ", false)]   // 4 alfanuméricos — acima de P-150
    [InlineData("(a. item ", false)]     // abriu parêntese, fechamento tem de ser ')'
    [InlineData("1.item ", false)]       // falta o branco depois do fechamento
    [InlineData("1. ", false)]           // nada não branco depois
    [InlineData("item ", false)]
    public void RF_169_marcador_numerado(string texto, bool esperado)
        => Assert.Equal(esperado, LineClassifier.HasNumberedListMarker(texto));

    [Fact]
    public void RF_165_uma_linha_com_marcador_forte_basta_para_contexto_de_lista()
    {
        var comp = new[]
        {
            LineBuilder.Horizontal("• primeiro", 0, 0, 20),
            LineBuilder.Horizontal("texto comum", 0, 30, 20),
        };
        Assert.True(LineClassifier.IsListContext(comp));
    }

    [Fact]
    public void RF_165_duas_linhas_com_candidato_fraco_bastam_para_contexto_de_lista()
    {
        var comp = new[]
        {
            LineBuilder.Horizontal("-um", 0, 0, 20),
            LineBuilder.Horizontal("-dois", 0, 30, 20),
        };
        Assert.True(LineClassifier.IsListContext(comp));
    }

    [Fact]
    public void RF_165_uma_unica_linha_com_candidato_fraco_nao_basta()
    {
        var comp = new[]
        {
            LineBuilder.Horizontal("-um", 0, 0, 20),
            LineBuilder.Horizontal("texto comum", 0, 30, 20),
        };
        Assert.False(LineClassifier.IsListContext(comp));
    }

    [Theory]
    [InlineData("[Nome] ", true)]
    [InlineData("「Nome」 ", true)]
    [InlineData("『Nome』 ", true)]
    [InlineData("<Nome> ", true)]
    [InlineData("Nome: ", true)]
    [InlineData("Nome： ", true)]
    [InlineData("Nome ", false)]
    [InlineData("[Nome ", false)]
    public void RF_171_titulo_explicito(string texto, bool esperado)
        => Assert.Equal(esperado, LineClassifier.IsExplicitTitle(texto));

    [Fact]
    public void RF_173_limite_normal_e_P40_caracteres_de_palavra()
    {
        // "abcdefghij" = 10 caracteres, uma palavra → curta pelos dois critérios.
        Assert.True(LineClassifier.IsShortLine(
            LineBuilder.Horizontal("abcdefghij", 0, 0, 20), removeSpaces: false));
    }

    [Fact]
    public void RF_173_ate_P43_palavras_tambem_e_curta_fora_do_modo_sem_espacos()
    {
        var linha = LineBuilder.Horizontal("alpha bravo charlie", 0, 0, 20);   // 17 caracteres
        Assert.True(LineClassifier.IsShortLine(linha, removeSpaces: false));   // 3 palavras ≤ P-43
        Assert.False(LineClassifier.IsShortLine(linha, removeSpaces: true));   // critério some
    }

    [Fact]
    public void RF_173_linha_vertical_desconta_P42_do_limite()
    {
        // 8 caracteres em 4 palavras: cabe em P-40 (10) mas não em P-40 − P-42 (7).
        // Quatro palavras também afastam o critério alternativo de P-43, isolando o
        // desconto de P-42.
        var h = LineBuilder.Horizontal("ab cd ef gh", 0, 0, 20);
        var v = LineBuilder.Vertical("ab cd ef gh", 0, 0, 20);
        Assert.True(LineClassifier.IsShortLine(h, removeSpaces: false));
        Assert.Equal(Orientation.Vertical, v.Orientation);
        Assert.False(LineClassifier.IsShortLine(v, removeSpaces: false));
    }

    [Fact]
    public void RF_172_titulo_por_contexto_exige_linha_seguinte_bem_maior()
    {
        var curta = LineBuilder.Horizontal("Ana", 0, 0, 20);
        var longa = LineBuilder.Horizontal("bom dia como vai voce hoje", 0, 30, 20);
        var quase = LineBuilder.Horizontal("bom dia", 0, 30, 20);

        Assert.True(LineClassifier.IsContextTitle(curta, longa, removeSpaces: false));
        // "Ana" tem 3 não brancos; exige ≥ teto(1,5×3) = 5. "bom dia" tem 6 → também passa.
        Assert.True(LineClassifier.IsContextTitle(curta, quase, removeSpaces: false));
        Assert.False(LineClassifier.IsContextTitle(curta, null, removeSpaces: false));
    }

    [Fact]
    public void RF_172_orientacoes_diferentes_nao_produzem_titulo_por_contexto()
    {
        var curta = LineBuilder.Horizontal("Ana", 0, 0, 20);
        var vertical = LineBuilder.Vertical("um dois tres quatro", 0, 30, 20);
        Assert.False(LineClassifier.IsContextTitle(curta, vertical, removeSpaces: false));
    }

    [Theory]
    [InlineData("Fim. ", true)]
    [InlineData("Fim? ", true)]
    [InlineData("Fim! ", true)]
    [InlineData("終わり。 ", true)]
    [InlineData("終わり？ ", true)]
    [InlineData("終わり！ ", true)]
    [InlineData("Ele disse.\" ", true)]     // fechamento removido antes de checar
    [InlineData("「彼は言った。」 ", true)]
    [InlineData("continua ", false)]
    [InlineData("\" ", false)]              // só fechamento
    [InlineData(" ", false)]                // vazia
    public void RF_177_fim_de_frase(string texto, bool esperado)
        => Assert.Equal(esperado, LineClassifier.EndsSentence(texto));
}
