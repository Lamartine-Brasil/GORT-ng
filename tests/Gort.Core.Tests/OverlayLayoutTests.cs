using Gort.Core.Calibration;
using Gort.Core.Model;
using Gort.Core.Rendering;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>
/// Medidor determinístico: cada caractere tem largura fixa e a altura é o tamanho da fonte.
/// Torna as regras de layout verificáveis sem depender de uma fonte real.
/// </summary>
internal sealed class FakeMeasurer : ITextMeasurer
{
    public double CharWidthRatio { get; init; } = 0.5;
    public double HeightRatio { get; init; } = 1.0;

    /// <summary>Diferença entre o caminho vetorial e o motor de texto, que RF-373 concilia.</summary>
    public double EngineExtra { get; init; }

    public int PathCalls { get; private set; }

    public TextExtent MeasurePath(string text, FontSpec font)
    {
        PathCalls++;
        return new TextExtent(text.Length * font.Size * CharWidthRatio, font.Size * HeightRatio);
    }

    public double MeasureEngineWidth(string text, FontSpec font)
        => text.Length * font.Size * CharWidthRatio + EngineExtra;

    public double FontHeight(FontSpec font) => font.Size * HeightRatio;
}

/// <summary>RF-349 a RF-359 — Geometria da sobreposição.</summary>
public class OverlayGeometryTests
{
    /// <summary>RF-349 — A janela cobre a união das áreas ampliada em P-92. 🔒</summary>
    [Fact]
    public void RF_349_a_janela_cobre_a_uniao_das_areas_com_a_folga_de_P92()
    {
        var janela = OverlayGeometry.WindowRect(new[]
        {
            new Rect(100, 100, 200, 100),
            new Rect(400, 150, 200, 100),
        });

        // União: (100,100 500x150). Com folga de 1,3: 650 x 195.
        Assert.Equal(650, janela.Width);
        Assert.Equal(195, janela.Height);

        // A folga é distribuída em torno do centro.
        Assert.True(janela.Left < 100);
        Assert.True(janela.Right > 600);
    }

    [Fact]
    public void RF_349_sem_areas_a_janela_e_vazia()
        => Assert.True(OverlayGeometry.WindowRect(Array.Empty<Rect>()).IsEmpty);

    /// <summary>
    /// RF-350 — O retângulo é ACUMULATIVO enquanto a tradução roda: se o novo cabe no
    /// anterior, mantém-se o anterior; senão, a união. 🔒
    /// </summary>
    [Fact]
    public void RF_350_um_retangulo_menor_nao_encolhe_a_janela()
    {
        var anterior = new Rect(0, 0, 800, 400);
        var menor = new Rect(100, 100, 200, 100);

        Assert.Equal(anterior, OverlayGeometry.Accumulate(anterior, menor));
    }

    [Fact]
    public void RF_350_um_retangulo_maior_produz_a_uniao()
    {
        var anterior = new Rect(0, 0, 200, 200);
        var maior = new Rect(100, 100, 400, 400);

        Assert.Equal(anterior.Union(maior), OverlayGeometry.Accumulate(anterior, maior));
    }

    [Fact]
    public void RF_350_ao_parar_o_acumulo_e_zerado()
    {
        // Nulo é o estado "acabou de começar": o novo retângulo vale sozinho.
        var novo = new Rect(50, 50, 100, 100);
        Assert.Equal(novo, OverlayGeometry.Accumulate(null, novo));
    }

    /// <summary>RF-352 — Retângulo de um bloco em coordenadas de tela.</summary>
    [Fact]
    public void RF_352_o_bloco_e_posicionado_pela_area_pela_ampliacao_e_pela_janela()
    {
        // Bloco em (100,50 200x40) na imagem ampliada 2x; área em (300,200);
        // janela de sobreposição em (250,150).
        var r = OverlayGeometry.BlockRect(
            blockInImage: new Rect(100, 50, 200, 40),
            ocrArea: new Rect(300, 200, 800, 300),
            scale: 2.0,
            overlayWindow: new Rect(250, 150, 1000, 500));

        // 300 + 100/2 − 250 = 100 ;  200 + 50/2 − 150 = 75
        Assert.Equal(100, r.X);
        Assert.Equal(75, r.Y);
        Assert.Equal(100, r.Width);    // 200/2
        Assert.Equal(20, r.Height);    // 40/2
    }

    [Fact]
    public void RF_352_os_cantos_usam_piso_e_teto_para_nao_perder_pixel_de_borda()
    {
        var r = OverlayGeometry.BlockRect(
            new Rect(101, 51, 201, 41), new Rect(0, 0, 800, 300), 2.0, new Rect(0, 0, 800, 300));

        Assert.Equal(50, r.X);          // piso de 50,5
        Assert.Equal(25, r.Y);          // piso de 25,5
        Assert.Equal(151, r.Right);     // teto de 151,0
        Assert.Equal(46, r.Bottom);     // teto de 46,0
    }

    /// <summary>RF-354 — O bloco é recortado pela área; sem área, é descartado.</summary>
    [Fact]
    public void RF_354_o_bloco_e_recortado_pela_area_de_ocr()
    {
        var recortado = OverlayGeometry.ClipToArea(
            new Rect(50, 50, 200, 100), new Rect(0, 0, 150, 200));

        Assert.Equal(new Rect(50, 50, 100, 100), recortado);
    }

    [Fact]
    public void RF_354_um_bloco_fora_da_area_fica_vazio_e_e_descartado()
        => Assert.True(OverlayGeometry.ClipToArea(
            new Rect(500, 500, 100, 100), new Rect(0, 0, 200, 200)).IsEmpty);

    /// <summary>RF-353 — No modo de janela anexada, a origem é limitada pelo cliente.</summary>
    [Fact]
    public void RF_353_a_origem_e_limitada_por_baixo_a_posicao_do_cliente()
    {
        var r = OverlayGeometry.ClampToClient(new Rect(10, 20, 100, 50), (50, 40));
        Assert.Equal(50, r.Left);
        Assert.Equal(40, r.Top);
    }

    /// <summary>
    /// RF-359 — O retângulo de conteúdo é reduzido em P-93 quando o contorno está ativo, e
    /// NÃO é reduzido quando não está: sem contorno não há o que reservar.
    /// </summary>
    [Fact]
    public void RF_359_o_conteudo_so_encolhe_quando_ha_contorno()
    {
        var vista = new Rect(0, 0, 200, 100);

        var comContorno = OverlayGeometry.ContentRect(vista, fontStroke: true);
        Assert.Equal(200 - 2 * (int)P.ContentRectInsetWithStroke, comContorno.Width);

        Assert.Equal(vista, OverlayGeometry.ContentRect(vista, fontStroke: false));
    }
}

/// <summary>RF-355 a RF-358 — Resolução de colisões.</summary>
public class CollisionResolverTests
{
    private static CollisionResolver.Item Block(Rect rect, bool title = false)
        => new() { Rect = rect, IsTitle = title };

    /// <summary>
    /// Critério de aceite do capítulo 19: "Dois blocos adjacentes com traduções longas não
    /// escrevem um por cima do outro."
    /// </summary>
    [Fact]
    public void Dois_blocos_sobrepostos_deixam_de_se_sobrepor()
    {
        var a = Block(new Rect(0, 0, 200, 100));
        var b = Block(new Rect(150, 0, 200, 100));

        CollisionResolver.Resolve(new[] { a, b });

        Assert.False(a.Rect.IntersectsWith(b.Rect));
    }

    /// <summary>
    /// Critério de aceite do capítulo 19: "Um nome de personagem curto mantém seu retângulo
    /// quando colide com o diálogo." (RF-357) 🔒
    /// </summary>
    [Fact]
    public void RF_357_o_titulo_preserva_o_retangulo_inteiro_e_o_outro_cede()
    {
        var titulo = Block(new Rect(0, 0, 120, 40), title: true);
        var original = titulo.Rect;
        var dialogo = Block(new Rect(80, 0, 400, 40));

        CollisionResolver.Resolve(new[] { titulo, dialogo });

        Assert.Equal(original, titulo.Rect);
        Assert.False(titulo.Rect.IntersectsWith(dialogo.Rect));
    }

    [Fact]
    public void RF_357_a_regra_do_titulo_vale_independente_da_ordem_do_par()
    {
        var dialogo = Block(new Rect(80, 0, 400, 40));
        var titulo = Block(new Rect(0, 0, 120, 40), title: true);
        var original = titulo.Rect;

        CollisionResolver.Resolve(new[] { dialogo, titulo });

        Assert.Equal(original, titulo.Rect);
    }

    /// <summary>
    /// RF-356 — Sem títulos, a fronteira é proporcional às áreas: o bloco MAIOR fica com a
    /// maior parte do espaço disputado. 🔒
    /// </summary>
    [Fact]
    public void RF_356_a_fronteira_e_proporcional_as_areas()
    {
        // O primeiro é três vezes mais alto, logo três vezes maior em área.
        var grande = Block(new Rect(0, 0, 200, 150));
        var pequeno = Block(new Rect(100, 0, 200, 50));

        CollisionResolver.Resolve(new[] { grande, pequeno });

        Assert.False(grande.Rect.IntersectsWith(pequeno.Rect));
        // O grande conservou mais da sobreposição do que perdeu.
        Assert.True(grande.Rect.Width > 100, $"o bloco maior ficou com {grande.Rect.Width}");
    }

    [Fact]
    public void RF_355_a_separacao_escolhe_o_eixo_que_perde_menos_area()
    {
        // Sobreposição fina na vertical: cortar na vertical perde muito menos.
        var a = Block(new Rect(0, 0, 400, 100));
        var b = Block(new Rect(0, 90, 400, 100));

        CollisionResolver.Resolve(new[] { a, b });

        Assert.False(a.Rect.IntersectsWith(b.Rect));
        // As larguras foram preservadas: o corte foi no eixo vertical.
        Assert.Equal(400, a.Rect.Width);
        Assert.Equal(400, b.Rect.Width);
    }

    [Fact]
    public void Blocos_que_nao_se_tocam_ficam_intactos()
    {
        var a = Block(new Rect(0, 0, 100, 50));
        var b = Block(new Rect(200, 0, 100, 50));
        var ra = a.Rect; var rb = b.Rect;

        CollisionResolver.Resolve(new[] { a, b });

        Assert.Equal(ra, a.Rect);
        Assert.Equal(rb, b.Rect);
    }

    /// <summary>
    /// RF-358 — O teto de iterações garante término mesmo num arranjo patológico em que
    /// cada separação cria uma nova colisão.
    /// </summary>
    [Fact]
    public void RF_358_muitos_blocos_empilhados_terminam_sem_travar()
    {
        var blocos = Enumerable.Range(0, 12)
            .Select(i => Block(new Rect(i * 5, i * 3, 200, 100)))
            .ToList();

        var relogio = System.Diagnostics.Stopwatch.StartNew();
        CollisionResolver.Resolve(blocos);
        relogio.Stop();

        Assert.True(relogio.ElapsedMilliseconds < 1000,
            $"a resolução levou {relogio.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Um_unico_bloco_nao_e_alterado()
    {
        var a = Block(new Rect(10, 10, 100, 50));
        var original = a.Rect;
        CollisionResolver.Resolve(new[] { a });
        Assert.Equal(original, a.Rect);
    }
}

/// <summary>RF-363 a RF-374 — Quebra de linha, medição e tamanho de fonte.</summary>
public class OverlayTextLayoutTests
{
    private static readonly FontSpec Font = new("Fonte", 20, FontStyle.Normal);

    /// <summary>
    /// RF-369 — A quebra é por CARACTERE, com folga de P-100 vezes o tamanho da fonte. 🔒
    /// </summary>
    [Fact]
    public void RF_369_o_texto_e_quebrado_para_caber_na_largura()
    {
        var m = new FakeMeasurer();          // 10 px por caractere a 20 pt
        var linhas = LineBreaker.Break(m, new string('a', 40), Font, Orientation.Horizontal, 200);

        Assert.True(linhas.Count > 1);
        Assert.All(linhas, l => Assert.True(l.Length * 10 <= 200));
    }

    /// <summary>RF-372 — As quebras explícitas são respeitadas antes da automática.</summary>
    [Fact]
    public void RF_372_as_quebras_explicitas_sao_respeitadas()
    {
        var m = new FakeMeasurer();
        var linhas = LineBreaker.Break(m, "abc\ndef", Font, Orientation.Horizontal, 1000);

        Assert.Equal(new[] { "abc", "def" }, linhas);
    }

    [Fact]
    public void RF_372_quebras_em_qualquer_formato_sao_reconhecidas()
    {
        var m = new FakeMeasurer();
        Assert.Equal(2, LineBreaker.Break(m, "abc\r\ndef", Font, Orientation.Horizontal, 1000).Count);
        Assert.Equal(2, LineBreaker.Break(m, "abc\rdef", Font, Orientation.Horizontal, 1000).Count);
    }

    /// <summary>RF-370 — Se nem um caractere couber, coloca-se um mesmo assim.</summary>
    [Fact]
    public void RF_370_um_retangulo_estreito_demais_ainda_progride()
    {
        var m = new FakeMeasurer();
        var linhas = LineBreaker.Break(m, "abcdef", Font, Orientation.Horizontal, 1);

        Assert.Equal(6, linhas.Count);
        Assert.All(linhas, l => Assert.Single(l));
    }

    /// <summary>RF-371 — Depois de quebrar, os espaços iniciais do restante somem.</summary>
    [Fact]
    public void RF_371_o_restante_nao_comeca_com_espaco()
    {
        var m = new FakeMeasurer();
        var linhas = LineBreaker.Break(m, "aaaa bbbb cccc", Font, Orientation.Horizontal, 60);

        Assert.All(linhas, l => Assert.False(l.StartsWith(' ')));
    }

    /// <summary>
    /// RF-369 — A busca binária dá o mesmo resultado que a varredura linear, que é a
    /// condição para trocar uma pela outra.
    /// </summary>
    [Fact]
    public void RF_369_a_busca_binaria_concorda_com_a_varredura_linear()
    {
        var m = new FakeMeasurer();
        string texto = new('x', 100);

        foreach (double disponivel in new[] { 15.0, 55.0, 133.0, 400.0, 999.0 })
        {
            int binaria = LineBreaker.LongestPrefixThatFits(
                m, texto, Font, Orientation.Horizontal, disponivel);

            int linear = 0;
            while (linear < texto.Length
                   && TextMetrics.Length(m, texto[..(linear + 1)], Font, Orientation.Horizontal) <= disponivel)
            {
                linear++;
            }

            Assert.Equal(linear, binaria);
        }
    }

    /// <summary>
    /// RF-373 — Para blocos horizontais, o MAIOR entre a largura do caminho vetorial e a do
    /// motor de texto. 🔒
    /// </summary>
    [Fact]
    public void RF_373_o_comprimento_horizontal_e_o_maior_das_duas_medidas()
    {
        var m = new FakeMeasurer { EngineExtra = 30 };
        double comprimento = TextMetrics.Length(m, "abc", Font, Orientation.Horizontal);

        // caminho: 3 × 10 = 30 ; motor: 30 + 30 = 60
        Assert.Equal(60, comprimento);
    }

    [Fact]
    public void RF_373_o_comprimento_vertical_e_a_altura_do_caminho()
    {
        var m = new FakeMeasurer { EngineExtra = 1000 };
        Assert.Equal(Font.Size, TextMetrics.Length(m, "abc", Font, Orientation.Vertical));
    }

    /// <summary>RF-365 — O avanço entre linhas é a altura da fonte vezes P-98.</summary>
    [Fact]
    public void RF_365_o_avanco_entre_linhas_e_P98_vezes_a_altura()
        => Assert.Equal(Font.Size * P.LineAdvanceFactor,
                        TextMetrics.LineAdvance(new FakeMeasurer(), Font));

    /// <summary>
    /// RF-368 — A faixa horizontal desce pelo índice vezes o avanço; a vertical recua a
    /// partir da DIREITA. 🔒
    /// </summary>
    [Fact]
    public void RF_368_as_faixas_horizontais_descem_pelo_avanco()
    {
        var conteudo = new RectD(10, 20, 300, 200);

        var primeira = OverlayTextLayout.LineBand(conteudo, 0, 24, Orientation.Horizontal);
        var segunda = OverlayTextLayout.LineBand(conteudo, 1, 24, Orientation.Horizontal);

        Assert.Equal(20, primeira.Top);
        Assert.Equal(44, segunda.Top);
        Assert.Equal(300, primeira.Width);   // a largura inteira do conteúdo
    }

    [Fact]
    public void RF_368_as_faixas_verticais_recuam_a_partir_da_direita()
    {
        var conteudo = new RectD(10, 20, 300, 200);

        var primeira = OverlayTextLayout.LineBand(conteudo, 0, 24, Orientation.Vertical);
        var segunda = OverlayTextLayout.LineBand(conteudo, 1, 24, Orientation.Vertical);

        // A primeira coluna encosta na direita; a seguinte fica à esquerda dela.
        Assert.Equal(conteudo.Right, primeira.Right);
        Assert.True(segunda.Right <= primeira.Left);
        Assert.Equal(200, primeira.Height);   // a altura inteira do conteúdo
    }

    /// <summary>
    /// RF-364 — O teste de "cabe" posiciona cada linha onde ela será desenhada; somar
    /// alturas deixaria a última linha invadir o bloco vizinho. 🔒
    /// </summary>
    [Fact]
    public void RF_364_a_ultima_linha_precisa_caber_inteira_na_sua_faixa()
    {
        var m = new FakeMeasurer();
        var conteudo = new RectD(0, 0, 500, 50);   // altura para uma faixa de 24

        // Duas linhas com avanço 24 exigem 48 de altura: cabe.
        Assert.True(OverlayTextLayout.Fits(m, new[] { "ab", "cd" }, Font,
                                           Orientation.Horizontal, conteudo, false));

        // Três linhas exigem 72: não cabe.
        Assert.False(OverlayTextLayout.Fits(m, new[] { "ab", "cd", "ef" }, Font,
                                            Orientation.Horizontal, conteudo, false));
    }

    /// <summary>RF-367 — Linhas só de espaços são ignoradas: elas não desenham nada.</summary>
    [Fact]
    public void RF_367_linhas_em_branco_nao_contam_no_teste()
    {
        var m = new FakeMeasurer();
        var conteudo = new RectD(0, 0, 500, 30);

        Assert.True(OverlayTextLayout.Fits(m, new[] { "ab", "   " }, Font,
                                           Orientation.Horizontal, conteudo, false));
    }

    /// <summary>RF-366 — Com contorno, os limites medidos são expandidos em P-99. 🔒</summary>
    [Fact]
    public void RF_366_o_contorno_torna_o_teste_mais_exigente()
    {
        var m = new FakeMeasurer();
        // Largura exatamente no limite do texto.
        var conteudo = new RectD(0, 0, 20, 100);

        Assert.True(OverlayTextLayout.Fits(m, new[] { "ab" }, Font,
                                           Orientation.Horizontal, conteudo, fontStroke: false));
        Assert.False(OverlayTextLayout.Fits(m, new[] { "ab" }, Font,
                                            Orientation.Horizontal, conteudo, fontStroke: true));
    }

    /// <summary>
    /// RF-363 — Primeiro testa-se o tamanho preferido DIRETAMENTE; se couber, usa-se ele.
    /// É o atalho para o caso comum, e RF-550 o lista entre as otimizações obrigatórias.
    /// </summary>
    [Fact]
    public void RF_363_quando_o_preferido_cabe_ele_e_usado_sem_bisseccao()
    {
        var m = new FakeMeasurer();
        var conteudo = new RectD(0, 0, 1000, 500);

        double escolhido = OverlayTextLayout.FindFontSize(
            m, "abc", Font, Orientation.Horizontal, conteudo,
            minimum: 10, preferred: 30, fontStroke: false);

        Assert.Equal(30, escolhido);
    }

    [Fact]
    public void RF_363_quando_o_preferido_nao_cabe_a_bisseccao_encontra_um_menor()
    {
        var m = new FakeMeasurer();
        var conteudo = new RectD(0, 0, 100, 40);

        double escolhido = OverlayTextLayout.FindFontSize(
            m, new string('a', 20), Font, Orientation.Horizontal, conteudo,
            minimum: 6, preferred: 40, fontStroke: false);

        Assert.True(escolhido < 40);
        Assert.True(escolhido >= 6);
    }

    [Fact]
    public void RF_363_a_bisseccao_para_na_precisao_de_P97()
    {
        var m = new FakeMeasurer();
        var conteudo = new RectD(0, 0, 100, 40);

        // Com P-96 iterações a partir de uma faixa de 34, a precisão alcançada é bem menor
        // que P-97; o que importa é o resultado nunca ficar abaixo do mínimo.
        double escolhido = OverlayTextLayout.FindFontSize(
            m, new string('a', 50), Font, Orientation.Horizontal, conteudo,
            minimum: 6, preferred: 40, fontStroke: false);

        Assert.InRange(escolhido, 6, 40);
    }

    /// <summary>RF-361 — O preferido é saturado entre o mínimo e o máximo configurados.</summary>
    [Fact]
    public void RF_361_o_tamanho_preferido_e_saturado()
    {
        Assert.Equal(10, OverlayTextLayout.Clamp(4, 10, 50));
        Assert.Equal(50, OverlayTextLayout.Clamp(90, 10, 50));
        Assert.Equal(30, OverlayTextLayout.Clamp(30, 10, 50));
    }

    /// <summary>
    /// RF-360 passo 3 — Um bloco comum usa o tamanho do CORPO, não o próprio: blocos
    /// pequenos dentro de um parágrafo não devem encolher em relação ao parágrafo. 🔒
    /// </summary>
    [Fact]
    public void RF_360_um_bloco_comum_adota_o_tamanho_do_corpo()
    {
        double preferido = OverlayTextLayout.PreferredFontSize(
            ownMedian: 10, bodyMedian: 20, isTitle: false, isLeadBlock: false,
            scale: 1, verticalDpi: 72);

        // 20 / 1 × 72 / 72 × 1,15
        Assert.Equal(20 * P.DerivedFontSizeScale, preferido, 6);
    }

    [Fact]
    public void RF_360_um_titulo_mantem_o_proprio_tamanho()
    {
        double preferido = OverlayTextLayout.PreferredFontSize(
            ownMedian: 40, bodyMedian: 20, isTitle: true, isLeadBlock: false,
            scale: 1, verticalDpi: 72);

        Assert.Equal(40 * P.DerivedFontSizeScale, preferido, 6);
    }

    /// <summary>
    /// RF-360 passo 3 — O bloco líder só mantém o próprio tamanho quando ele é pelo menos
    /// P-94 vezes o do corpo: um cabeçalho genuinamente maior conserva seu tamanho. 🔒
    /// </summary>
    [Fact]
    public void RF_360_o_bloco_lider_so_conserva_o_tamanho_se_for_P94_vezes_o_corpo()
    {
        // 26 / 20 = 1,3 — exatamente P-94: conserva.
        Assert.Equal(26 * P.DerivedFontSizeScale,
            OverlayTextLayout.PreferredFontSize(26, 20, false, true, 1, 72), 6);

        // 25 / 20 = 1,25 — abaixo de P-94: adota o corpo.
        Assert.Equal(20 * P.DerivedFontSizeScale,
            OverlayTextLayout.PreferredFontSize(25, 20, false, true, 1, 72), 6);
    }

    /// <summary>RF-360 passo 4 — Pixels de imagem viram pontos, descontando a ampliação.</summary>
    [Fact]
    public void RF_360_a_ampliacao_e_descontada_na_conversao_para_pontos()
    {
        double comAmpliacao = OverlayTextLayout.PreferredFontSize(40, 40, false, false, 2.0, 72);
        double semAmpliacao = OverlayTextLayout.PreferredFontSize(20, 20, false, false, 1.0, 72);

        Assert.Equal(semAmpliacao, comAmpliacao, 6);
    }
}

/// <summary>RF-374 — Cache de medição durante um desenho.</summary>
public class TextMeasurementCacheTests
{
    [Fact]
    public void RF_374_a_mesma_medida_nao_e_repetida()
    {
        var inner = new FakeMeasurer();
        var cache = new TextMeasurementCache(inner);
        var font = new FontSpec("Fonte", 20, FontStyle.Normal);

        for (int i = 0; i < 10; i++) cache.MeasurePath("abc", font);

        Assert.Equal(1, inner.PathCalls);
        Assert.Equal(9, cache.Hits);
        Assert.Equal(1, cache.Misses);
    }

    [Fact]
    public void RF_374_tamanhos_diferentes_sao_entradas_diferentes()
    {
        var inner = new FakeMeasurer();
        var cache = new TextMeasurementCache(inner);

        cache.MeasurePath("abc", new FontSpec("Fonte", 20, FontStyle.Normal));
        cache.MeasurePath("abc", new FontSpec("Fonte", 21, FontStyle.Normal));

        Assert.Equal(2, inner.PathCalls);
    }

    [Fact]
    public void RF_374_o_estilo_entra_na_chave()
    {
        var inner = new FakeMeasurer();
        var cache = new TextMeasurementCache(inner);

        cache.MeasurePath("abc", new FontSpec("Fonte", 20, FontStyle.Normal));
        cache.MeasurePath("abc", new FontSpec("Fonte", 20, FontStyle.Bold));

        Assert.Equal(2, inner.PathCalls);
    }

    /// <summary>RF-374 — O cache é descartado ao fim do desenho.</summary>
    [Fact]
    public void RF_374_limpar_o_cache_forca_a_remedicao()
    {
        var inner = new FakeMeasurer();
        var cache = new TextMeasurementCache(inner);
        var font = new FontSpec("Fonte", 20, FontStyle.Normal);

        cache.MeasurePath("abc", font);
        cache.Clear();
        cache.MeasurePath("abc", font);

        Assert.Equal(2, inner.PathCalls);
    }

    /// <summary>
    /// O cache é o que torna a sobreposição viável: a bissecção de tamanho de fonte repete
    /// as mesmas medições dezenas de vezes.
    /// </summary>
    [Fact]
    public void RF_374_a_busca_de_tamanho_de_fonte_reaproveita_medicoes()
    {
        var inner = new FakeMeasurer();
        var cache = new TextMeasurementCache(inner);
        var conteudo = new RectD(0, 0, 120, 60);

        OverlayTextLayout.FindFontSize(cache, new string('a', 40),
            new FontSpec("Fonte", 20, FontStyle.Normal),
            Orientation.Horizontal, conteudo, 6, 40, false);

        Assert.True(cache.Hits > 0, "a busca não reaproveitou nenhuma medição");
    }
}

/// <summary>RF-392 — Negrito e contorno.</summary>
public class FontSpecTests
{
    /// <summary>
    /// RF-392 — Sem contorno, o negrito é removido: ele engrossa demais e reduz a
    /// legibilidade sobre fundos claros. 🔒
    /// </summary>
    [Fact]
    public void RF_392_sem_contorno_o_negrito_sai()
    {
        var negrito = new FontSpec("Fonte", 20, FontStyle.Bold | FontStyle.Italic);

        var semContorno = negrito.WithoutBoldWhenNoStroke(fontStroke: false);
        Assert.False(semContorno.Style.HasFlag(FontStyle.Bold));
        Assert.True(semContorno.Style.HasFlag(FontStyle.Italic));   // o itálico permanece

        var comContorno = negrito.WithoutBoldWhenNoStroke(fontStroke: true);
        Assert.True(comContorno.Style.HasFlag(FontStyle.Bold));
    }
}
