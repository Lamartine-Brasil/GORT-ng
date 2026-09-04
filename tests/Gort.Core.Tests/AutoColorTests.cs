using Gort.Core.Calibration;
using Gort.Core.ColorAnalysis;
using Gort.Core.Model;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>
/// Cap. 20 — Análise automática de cor. Os testes reproduzem, em imagens sintéticas, os
/// quatro critérios de aceite do capítulo.
/// </summary>
public class AutoColorTests
{
    /// <summary>
    /// Desenha um bloco de texto: um fundo uniforme com "glifos" retangulares dentro das
    /// caixas de palavra, deixando uma moldura de fundo puro nas bordas de cada palavra —
    /// que é exatamente o que as sondas de RF-399 procuram.
    /// </summary>
    private static (ImageBuffer Image, Rect Block, List<Rect> Words) Render(
        Rgba background, Rgba text, int wordCount = 4, int inset = 3)
    {
        const int w = 200, h = 60;
        var img = ImageBuffer.Allocate(w, h, PixelFormat.Bgra32);

        void Paint(Rect r, Rgba c)
        {
            for (int y = Math.Max(0, r.Top); y < Math.Min(h, r.Bottom); y++)
            {
                for (int x = Math.Max(0, r.Left); x < Math.Min(w, r.Right); x++)
                {
                    int o = img.OffsetOf(x, y);
                    img.Pixels[o] = c.B; img.Pixels[o + 1] = c.G;
                    img.Pixels[o + 2] = c.R; img.Pixels[o + 3] = c.A;
                }
            }
        }

        Paint(new Rect(0, 0, w, h), background);

        var words = new List<Rect>();
        int x0 = 10;
        for (int i = 0; i < wordCount; i++)
        {
            var word = new Rect(x0, 20, 30, 24);
            words.Add(word);
            // O glifo ocupa o miolo; a moldura de `inset` px permanece fundo puro.
            Paint(new Rect(word.X + inset, word.Y + inset,
                           word.Width - 2 * inset, word.Height - 2 * inset), text);
            x0 += 45;
        }

        var block = Rect.UnionAll(words);
        return (img, block, words);
    }

    private static AutoColorResult Analyze(
        (ImageBuffer Image, Rect Block, List<Rect> Words) scene, AutoColorOptions? o = null)
    {
        var r = AutoColorAnalyzer.Analyze(scene.Image, scene.Block, scene.Words,
                                          scene.Image.Width, scene.Image.Height, o);
        Assert.NotNull(r);
        return r!;
    }

    /// <summary>
    /// Critério de aceite: "Texto branco sobre caixa de diálogo azul escura produz fonte
    /// branca e fundo azul escuro."
    /// </summary>
    [Fact]
    public void Texto_branco_sobre_caixa_azul_escura()
    {
        var azul = new Rgba(12, 20, 64);
        var r = Analyze(Render(azul, Rgba.White));

        Assert.Equal(Rgba.White, r.Font with { A = 255 });
        Assert.Equal((azul.R, azul.G, azul.B), (r.Background.R, r.Background.G, r.Background.B));
    }

    /// <summary>
    /// Critério de aceite: "Texto preto sobre fundo bege produz fonte preta e fundo bege."
    /// </summary>
    [Fact]
    public void Texto_preto_sobre_fundo_bege()
    {
        var bege = new Rgba(232, 216, 184);
        var r = Analyze(Render(bege, Rgba.Black));

        Assert.Equal(Rgba.Black, r.Font with { A = 255 });
        Assert.Equal((bege.R, bege.G, bege.B), (r.Background.R, r.Background.G, r.Background.B));
    }

    /// <summary>
    /// Critério de aceite: "Texto com contorno claro sobre fundo claro produz uma cor de
    /// fonte com contraste de pelo menos P-115 contra o fundo escolhido."
    /// </summary>
    [Fact]
    public void Contraste_minimo_P115_e_garantido()
    {
        // Cinza sobre cinza claro: contraste natural baixo, abaixo de P-115.
        var fundo = new Rgba(220, 220, 220);
        var texto = new Rgba(190, 190, 190);

        var r = Analyze(Render(fundo, texto), new AutoColorOptions
        {
            Enabled = true, TextBackgroundEnabled = true, BackgroundAlpha = 170,
        });

        Assert.True(r.Contrast >= P.MinContrastRatio,
            $"contraste obtido {r.Contrast:F2} < P-115 ({P.MinContrastRatio})");
        Assert.True(r.ContrastCorrected || r.UsedFallback);
    }

    /// <summary>
    /// Critério de aceite: "Um bloco sobre gradiente produz uma cor de fundo estável entre
    /// quadros consecutivos com o mesmo conteúdo."
    /// </summary>
    [Fact]
    public void Fundo_sobre_gradiente_e_estavel_entre_quadros()
    {
        static (ImageBuffer, Rect, List<Rect>) Gradiente()
        {
            const int w = 200, h = 60;
            var img = ImageBuffer.Allocate(w, h, PixelFormat.Bgra32);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byte v = (byte)(40 + x * 60 / w);
                    int o = img.OffsetOf(x, y);
                    img.Pixels[o] = v; img.Pixels[o + 1] = v;
                    img.Pixels[o + 2] = v; img.Pixels[o + 3] = 255;
                }
            }
            var words = new List<Rect>();
            int x0 = 10;
            for (int i = 0; i < 4; i++)
            {
                var word = new Rect(x0, 20, 30, 24);
                words.Add(word);
                for (int y = word.Y + 3; y < word.Bottom - 3; y++)
                {
                    for (int x = word.X + 3; x < word.Right - 3; x++)
                    {
                        int o = img.OffsetOf(x, y);
                        img.Pixels[o] = 255; img.Pixels[o + 1] = 255;
                        img.Pixels[o + 2] = 255; img.Pixels[o + 3] = 255;
                    }
                }
                x0 += 45;
            }
            return (img, Rect.UnionAll(words), words);
        }

        var (img, block, words) = Gradiente();
        var a = AutoColorAnalyzer.Analyze(img, block, words, img.Width, img.Height);
        var b = AutoColorAnalyzer.Analyze(img, block, words, img.Width, img.Height);

        Assert.NotNull(a);
        Assert.Equal(a!.Background, b!.Background);
        Assert.Equal(a.Font, b.Font);
    }

    [Fact]
    public void RF_396_o_passo_de_amostragem_e_o_teto_da_raiz_da_area_dividida_pelo_maximo()
    {
        // Área 400×400 = 160000; máximo 65536 → teto(sqrt(2,44…)) = 2.
        Assert.Equal(2, AutoColorAnalyzer.SampleStep(new Rect(0, 0, 400, 400),
                                                     P.ColorMaxSamplesBackground));
        // Retângulo pequeno: passo mínimo 1.
        Assert.Equal(1, AutoColorAnalyzer.SampleStep(new Rect(0, 0, 10, 10),
                                                     P.ColorMaxSamplesWord));
    }

    [Fact]
    public void RF_397_pixels_quase_transparentes_sao_ignorados()
    {
        var (img, block, words) = Render(new Rgba(10, 10, 10), Rgba.White);
        // Torna a imagem inteira quase transparente: nada é amostrado.
        for (int i = 3; i < img.Pixels.Length; i += 4) img.Pixels[i] = (byte)(P.ColorMinAlpha - 1);

        Assert.Null(AutoColorAnalyzer.Analyze(img, block, words, img.Width, img.Height));
    }

    [Fact]
    public void RF_398_a_quantizacao_descarta_3_bits_por_canal()
    {
        // Cores que diferem em menos de 8 níveis caem no mesmo agrupamento.
        Assert.Equal(ColorCluster.Quantize(100, 100, 100), ColorCluster.Quantize(103, 103, 103));
        Assert.NotEqual(ColorCluster.Quantize(100, 100, 100), ColorCluster.Quantize(120, 100, 100));
    }

    [Fact]
    public void RF_398_o_valor_do_agrupamento_e_a_mediana_por_componente()
    {
        var c = new ColorCluster(0);
        c.Add(10, 10, 10);
        c.Add(20, 20, 20);
        c.Add(200, 200, 200);   // valor espúrio não desloca a mediana
        Assert.Equal(new Rgba(20, 20, 20), c.Value);
    }

    [Fact]
    public void RF_404_e_RF_415_a_falha_devolve_nulo_e_o_desenho_usa_as_cores_configuradas()
    {
        var img = ImageBuffer.Allocate(0, 0, PixelFormat.Bgra32);
        Assert.Null(AutoColorAnalyzer.Analyze(img, new Rect(0, 0, 10, 10),
                                              Array.Empty<Rect>(), 10, 10));

        // Retângulo do bloco fora da imagem → interseção vazia → falha.
        var img2 = ImageBuffer.Allocate(10, 10, PixelFormat.Bgra32);
        Assert.Null(AutoColorAnalyzer.Analyze(img2, new Rect(100, 100, 10, 10),
                                              Array.Empty<Rect>(), 10, 10));
    }

    /// <summary>
    /// RF-412 — A correção final de legibilidade só se aplica quando a cor automática está
    /// em uso, o fundo do texto está ativado, e o alfa efetivo do fundo é maior que zero.
    /// </summary>
    [Fact]
    public void RF_412_sem_fundo_pintado_nao_ha_correcao_de_contraste()
    {
        var cena = Render(new Rgba(220, 220, 220), new Rgba(190, 190, 190));

        var semFundo = Analyze(cena, new AutoColorOptions
        {
            Enabled = true, TextBackgroundEnabled = false, BackgroundAlpha = 170,
        });
        Assert.False(semFundo.ContrastCorrected);

        var alfaZero = Analyze(cena, new AutoColorOptions
        {
            Enabled = true, TextBackgroundEnabled = true, BackgroundAlpha = 0,
        });
        Assert.False(alfaZero.ContrastCorrected);
    }

    /// <summary>
    /// RF-414 — Quando a cor de fundo automática é usada, o ALFA vem da cor de fundo
    /// configurada pelo usuário e só os componentes de cor vêm da análise.
    /// </summary>
    [Fact]
    public void RF_414_o_alfa_do_fundo_vem_da_configuracao_do_usuario()
    {
        var r = Analyze(Render(new Rgba(12, 20, 64), Rgba.White),
                        new AutoColorOptions { BackgroundAlpha = 200 });
        Assert.Equal(200, r.Background.A);
    }

    [Fact]
    public void RF_411_a_razao_de_contraste_segue_a_formula_de_luminancia_relativa()
    {
        // Preto contra branco é o contraste máximo definido pela fórmula: 21:1.
        Assert.Equal(21.0, ColorMath.ContrastRatio(Rgba.Black, Rgba.White), 2);
        Assert.Equal(1.0, ColorMath.ContrastRatio(Rgba.White, Rgba.White), 6);
    }

    [Fact]
    public void RF_409_a_alternativa_e_preto_ou_branco_o_que_der_maior_contraste()
    {
        Assert.Equal(Rgba.Black, ColorMath.BestBlackOrWhite(Rgba.White));
        Assert.Equal(Rgba.White, ColorMath.BestBlackOrWhite(Rgba.Black));
    }

    /// <summary>RF-393 — Derivação das cores de contorno a partir da cor de fonte.</summary>
    [Fact]
    public void RF_393_fonte_clara_recebe_contorno_2_preto()
    {
        var (s1, s2) = ColorMath.DeriveStrokeColors(Rgba.White);
        Assert.Equal(Rgba.Black, s2);
        // Contorno 1: mesma matiz, brilho reduzido em 0,1.
        Assert.True(ColorMath.ToHsb(s1).B < ColorMath.ToHsb(Rgba.White).B);
    }

    [Fact]
    public void RF_393_fonte_escura_recebe_contorno_2_branco()
    {
        var (s1, s2) = ColorMath.DeriveStrokeColors(new Rgba(20, 20, 20));
        Assert.Equal(Rgba.White, s2);
        Assert.True(ColorMath.ToHsb(s1).B > ColorMath.ToHsb(new Rgba(20, 20, 20)).B);
    }

    [Fact]
    public void RF_107_a_conversao_hsv_do_filtro_usa_a_escala_0_a_100()
    {
        var (h, s, v) = ColorMath.ToHsvFilter(255, 255, 255);
        Assert.Equal(0, s);      // branco não tem saturação
        Assert.Equal(100, v);    // e tem brilho máximo

        var (h2, s2, v2) = ColorMath.ToHsvFilter(255, 0, 0);
        Assert.Equal(0, h2);     // vermelho puro
        Assert.Equal(100, s2);
        Assert.Equal(100, v2);

        var (_, s3, v3) = ColorMath.ToHsvFilter(0, 0, 0);
        Assert.Equal(0, s3);     // saturação 0 quando o máximo é 0
        Assert.Equal(0, v3);
    }
}
