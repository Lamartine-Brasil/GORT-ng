using Gort.Core.Calibration;
using Gort.Core.Imaging;
using Gort.Core.Model;
using Xunit;

namespace Gort.Core.Tests;

public class PreprocessorTests
{
    /// <summary>Imagem BGR de teste, pintada por uma função de (x, y).</summary>
    private static ImageBuffer Bgr(int w, int h, Func<int, int, Rgba> paint)
    {
        var img = ImageBuffer.Allocate(w, h, PixelFormat.Bgr24);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var c = paint(x, y);
                int o = img.OffsetOf(x, y);
                img.Pixels[o] = c.B; img.Pixels[o + 1] = c.G; img.Pixels[o + 2] = c.R;
            }
        }
        return img;
    }

    private static byte At(ImageBuffer img, int x, int y) => img.Pixels[img.OffsetOf(x, y)];

    [Fact]
    public void RF_104_os_modos_de_filtro_sao_mutuamente_exclusivos_por_construcao()
    {
        // Um único campo de modo torna impossível ter RGB e HSV ligados ao mesmo tempo:
        // "marcar um desmarca os outros dois" é uma propriedade do tipo, não da interface.
        var s = new FilterSettings { Mode = FilterMode.Hsv };
        s.Mode = FilterMode.Rgb;
        Assert.Equal(FilterMode.Rgb, s.Mode);
        Assert.Single(Enum.GetValues<FilterMode>(), m => m == s.Mode);
    }

    [Fact]
    public void RF_105_no_modo_rgb_o_casamento_e_exato()
    {
        var img = Bgr(3, 1, (x, _) => x switch
        {
            0 => new Rgba(10, 20, 30),
            1 => new Rgba(10, 20, 31),   // um componente diferente já reprova
            _ => new Rgba(0, 0, 0),
        });
        var s = new FilterSettings
        {
            Mode = FilterMode.Rgb,
            Groups = { new ColorGroup { R = 10, G = 20, B = 30 } },
        };

        var outp = Preprocessor.Binarize(img, s);
        Assert.Equal(Preprocessor.TextValue, At(outp, 0, 0));
        Assert.Equal(Preprocessor.BackgroundValue, At(outp, 1, 0));
        Assert.Equal(Preprocessor.BackgroundValue, At(outp, 2, 0));
    }

    [Fact]
    public void RF_105_o_pixel_passa_se_satisfizer_QUALQUER_grupo_ativo()
    {
        var img = Bgr(2, 1, (x, _) => x == 0 ? new Rgba(1, 2, 3) : new Rgba(9, 8, 7));
        var s = new FilterSettings
        {
            Mode = FilterMode.Rgb,
            Groups =
            {
                new ColorGroup { R = 1, G = 2, B = 3 },
                new ColorGroup { R = 9, G = 8, B = 7 },
            },
        };
        var outp = Preprocessor.Binarize(img, s);
        Assert.Equal(Preprocessor.TextValue, At(outp, 0, 0));
        Assert.Equal(Preprocessor.TextValue, At(outp, 1, 0));
    }

    /// <summary>
    /// Critério de aceite do capítulo 13: "Com um grupo HSV configurado para texto branco
    /// sobre fundo escuro, a pré-visualização mostra as letras em preto e o resto em branco."
    /// O grupo usado é exatamente P-28, o que o assistente configura para texto claro.
    /// </summary>
    [Fact]
    public void RF_082_e_RF_119_texto_branco_sobre_fundo_escuro()
    {
        // Coluna 1 = letra branca; colunas 0 e 2 = fundo azul-escuro.
        var img = Bgr(3, 1, (x, _) => x == 1 ? new Rgba(255, 255, 255) : new Rgba(10, 15, 40));

        var s = new FilterSettings
        {
            Mode = FilterMode.Hsv,
            Groups = FilterSettings.WizardGroups(darkText: false),   // P-28: S 0–10, V 75–100
        };

        var preview = Preprocessor.Preview(img, s);
        Assert.Equal(Preprocessor.TextValue, At(preview, 1, 0));         // letra em preto
        Assert.Equal(Preprocessor.BackgroundValue, At(preview, 0, 0));   // resto em branco
        Assert.Equal(Preprocessor.BackgroundValue, At(preview, 2, 0));
    }

    [Fact]
    public void RF_119_o_assistente_para_texto_escuro_produz_os_dois_grupos_de_P26_e_P27()
    {
        var g = FilterSettings.WizardGroups(darkText: true);
        Assert.Equal(2, g.Count);
        Assert.Equal((0, 8, 0, 32), (g[0].S1, g[0].S2, g[0].V1, g[0].V2));
        Assert.Equal((95, 100, 0, 32), (g[1].S1, g[1].S2, g[1].V1, g[1].V2));

        var claro = FilterSettings.WizardGroups(darkText: false);
        Assert.Single(claro);
        Assert.Equal((0, 10, 75, 100), (claro[0].S1, claro[0].S2, claro[0].V1, claro[0].V2));
    }

    [Fact]
    public void RF_108_o_limiar_usa_a_matriz_de_luminancia_P146()
    {
        // 0,30·R + 0,59·G + 0,11·B
        Assert.Equal(255 * 0.59, Preprocessor.Luminance(0, 255, 0), 6);
        Assert.Equal(0, Preprocessor.Luminance(0, 0, 0), 6);

        var img = Bgr(2, 1, (x, _) => x == 0 ? new Rgba(0, 0, 0) : new Rgba(255, 255, 255));
        var s = new FilterSettings { Mode = FilterMode.Threshold, Threshold = P.DefaultThreshold };
        var outp = Preprocessor.Binarize(img, s);
        Assert.Equal(Preprocessor.TextValue, At(outp, 0, 0));         // escuro é texto
        Assert.Equal(Preprocessor.BackgroundValue, At(outp, 1, 0));
    }

    [Fact]
    public void RF_110_sem_filtro_a_imagem_nao_e_binarizada()
    {
        var img = Bgr(4, 4, (_, _) => new Rgba(12, 34, 56));
        var s = new FilterSettings { Mode = FilterMode.None, Scale = 1.0 };
        var outp = Preprocessor.Process(img, Array.Empty<Rect>(), s);
        Assert.Equal(PixelFormat.Bgr24, outp.Format);
    }

    [Fact]
    public void RF_103_a_exclusao_nao_muda_a_geometria_da_imagem()
    {
        var img = Bgr(10, 10, (_, _) => new Rgba(0, 0, 0));
        var s = new FilterSettings { Mode = FilterMode.Threshold, Scale = 1.0 };
        var outp = Preprocessor.Process(img, new[] { new Rect(2, 2, 3, 3) }, s);
        Assert.Equal(10, outp.Width);
        Assert.Equal(10, outp.Height);
    }

    /// <summary>
    /// RF-102 — A região excluída recebe o valor de FUNDO do filtro ativo, nunca preto,
    /// branco fixo ou qualquer cor de alto contraste. Aqui a imagem inteira passaria no
    /// filtro; a exclusão tem de virar fundo mesmo assim.
    /// </summary>
    [Fact]
    public void RF_102_a_exclusao_vira_fundo_e_nao_uma_aresta_de_contraste()
    {
        var img = Bgr(10, 10, (_, _) => new Rgba(0, 0, 0));    // tudo é "texto" no limiar
        var s = new FilterSettings { Mode = FilterMode.Threshold, Scale = 1.0 };
        var outp = Preprocessor.Process(img, new[] { new Rect(2, 2, 3, 3) }, s);

        Assert.Equal(Preprocessor.TextValue, At(outp, 0, 0));           // fora da exclusão
        Assert.Equal(Preprocessor.BackgroundValue, At(outp, 3, 3));     // dentro da exclusão
        Assert.Equal(Preprocessor.BackgroundValue, At(outp, 2, 2));
        Assert.Equal(Preprocessor.TextValue, At(outp, 5, 5));           // borda exclusiva
    }

    [Fact]
    public void RF_102_sem_filtro_a_exclusao_recebe_a_cor_dominante_da_borda()
    {
        // Fundo bege uniforme com um "ícone" vermelho no meio, que será excluído.
        var bege = new Rgba(200, 190, 170);
        var img = Bgr(10, 10, (x, y) =>
            x >= 3 && x < 7 && y >= 3 && y < 7 ? new Rgba(255, 0, 0) : bege);

        var s = new FilterSettings { Mode = FilterMode.None, Scale = 1.0 };
        var outp = Preprocessor.Process(img, new[] { new Rect(3, 3, 4, 4) }, s);

        int o = outp.OffsetOf(5, 5);
        Assert.Equal(bege.B, outp.Pixels[o]);
        Assert.Equal(bege.G, outp.Pixels[o + 1]);
        Assert.Equal(bege.R, outp.Pixels[o + 2]);
    }

    [Fact]
    public void RF_112_a_erosao_e_3x3_de_uma_iteracao_e_afina_o_traco()
    {
        // Bloco 3×3 de texto: só o pixel central sobrevive à erosão.
        var bin = ImageBuffer.Allocate(5, 5, PixelFormat.Gray8);
        for (int i = 0; i < bin.Pixels.Length; i++) bin.Pixels[i] = Preprocessor.BackgroundValue;
        for (int y = 1; y <= 3; y++)
        {
            for (int x = 1; x <= 3; x++) bin.Pixels[bin.OffsetOf(x, y)] = Preprocessor.TextValue;
        }

        var eroded = Preprocessor.Erode(bin);
        Assert.Equal(Preprocessor.TextValue, At(eroded, 2, 2));
        Assert.Equal(Preprocessor.BackgroundValue, At(eroded, 1, 1));
        Assert.Equal(Preprocessor.BackgroundValue, At(eroded, 3, 3));
    }

    [Fact]
    public void RF_111_a_erosao_remove_ruido_de_ponto_isolado()
    {
        var bin = ImageBuffer.Allocate(5, 5, PixelFormat.Gray8);
        for (int i = 0; i < bin.Pixels.Length; i++) bin.Pixels[i] = Preprocessor.BackgroundValue;
        bin.Pixels[bin.OffsetOf(2, 2)] = Preprocessor.TextValue;

        var eroded = Preprocessor.Erode(bin);
        Assert.All(eroded.Pixels, p => Assert.Equal(Preprocessor.BackgroundValue, p));
    }

    [Fact]
    public void RF_112_a_erosao_acontece_antes_da_ampliacao()
    {
        // Um ponto isolado ampliado 3× viraria um bloco 3×3 que a erosão NÃO apagaria.
        // Como a erosão vem antes, o ponto some e a imagem final fica limpa.
        var img = Bgr(5, 5, (x, y) => x == 2 && y == 2 ? new Rgba(0, 0, 0) : new Rgba(255, 255, 255));
        var s = new FilterSettings
        {
            Mode = FilterMode.Threshold, Erosion = true, Scale = 3.0,
        };
        var outp = Preprocessor.Process(img, Array.Empty<Rect>(), s);
        Assert.All(outp.Pixels, p => Assert.Equal(Preprocessor.BackgroundValue, p));
    }

    [Fact]
    public void RF_113_a_ampliacao_multiplica_as_dimensoes()
    {
        var img = Bgr(10, 20, (_, _) => new Rgba(0, 0, 0));
        var outp = Preprocessor.Scale(img, 2.0);
        Assert.Equal(20, outp.Width);
        Assert.Equal(40, outp.Height);
    }

    /// <summary>
    /// Critério de aceite do capítulo 13: "Uma caixa de palavra devolvida pelo OCR com
    /// ampliação 2× resulta, ao ser convertida, exatamente sobre o texto original na tela."
    /// </summary>
    [Fact]
    public void RF_116_a_conversao_de_volta_usa_piso_e_teto()
    {
        var scaled = new Rect(20, 40, 60, 30);                       // espaço ampliado 2×
        var source = Preprocessor.ToSourceSpace(scaled, P.DefaultScale);
        Assert.Equal(new Rect(10, 20, 30, 15), source);

        // Piso nos cantos superior/esquerdo e teto nos inferior/direito: a caixa convertida
        // nunca perde pixel de borda do glifo.
        var impar = Preprocessor.ToSourceSpace(new Rect(21, 41, 61, 31), 2.0);
        Assert.Equal(10, impar.Left);
        Assert.Equal(20, impar.Top);
        Assert.Equal(41, impar.Right);    // teto de 82/2
        Assert.Equal(36, impar.Bottom);   // teto de 72/2
    }

    [Fact]
    public void RF_117_imagens_de_1_3_e_4_canais_sao_aceitas()
    {
        foreach (var fmt in new[] { PixelFormat.Gray8, PixelFormat.Bgr24, PixelFormat.Bgra32 })
        {
            var img = ImageBuffer.Allocate(4, 4, fmt);
            var s = new FilterSettings { Mode = FilterMode.Threshold, Scale = 1.0 };
            var outp = Preprocessor.Process(img, Array.Empty<Rect>(), s);
            Assert.Equal(4, outp.Width);
        }
    }

    [Fact]
    public void RF_117_o_canal_unico_e_replicado_nos_tres_canais_de_cor()
    {
        var img = ImageBuffer.Allocate(1, 1, PixelFormat.Gray8);
        img.Pixels[0] = 77;
        var (b, g, r, a) = img.GetPixel(0, 0);
        Assert.Equal(77, b); Assert.Equal(77, g); Assert.Equal(77, r); Assert.Equal(255, a);
    }

    [Fact]
    public void RF_042_e_RF_114_valores_fora_de_faixa_sao_saturados_nao_rejeitados()
    {
        var s = new FilterSettings { Threshold = 300, Scale = 11.0 };
        s.Groups.Add(new ColorGroup { R = 300, S1 = 90, S2 = 10, V1 = 200, V2 = 5 });
        s.Normalize();

        Assert.Equal(255, s.Threshold);
        Assert.Equal(P.DefaultScale, s.Scale);        // acima de P-24 volta ao padrão
        Assert.Equal(255, s.Groups[0].R);
        Assert.Equal((10, 90), (s.Groups[0].S1, s.Groups[0].S2));   // RF-043 — trocados
        Assert.Equal((5, 100), (s.Groups[0].V1, s.Groups[0].V2));
    }

    [Fact]
    public void Caso_de_erro_do_capitulo_13_todos_os_pixels_filtrados_produz_imagem_em_branco()
    {
        var img = Bgr(8, 8, (_, _) => new Rgba(255, 255, 255));
        var s = new FilterSettings
        {
            Mode = FilterMode.Rgb, Scale = 1.0,
            Groups = { new ColorGroup { R = 1, G = 2, B = 3 } },   // nada casa
        };
        var outp = Preprocessor.Process(img, Array.Empty<Rect>(), s);
        Assert.All(outp.Pixels, p => Assert.Equal(Preprocessor.BackgroundValue, p));
    }
}
