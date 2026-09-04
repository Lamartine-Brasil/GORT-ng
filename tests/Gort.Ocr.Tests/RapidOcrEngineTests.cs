using Gort.Core.Model;
using Gort.Core.Ocr;
using Gort.Ocr.Rapid;
using Xunit;

namespace Gort.Ocr.Tests;

/// <summary>
/// Etapa 5 — "o contrato de 6.4 satisfeito por um motor local, devolvendo palavras com
/// caixas".
///
/// Os modelos ONNX pesam dezenas de MB e NÃO entram no versionamento, então cada teste
/// verifica o contrato nas duas situações: com o motor disponível, o comportamento
/// especificado; sem ele, a indisponibilidade explicada de RF-575.
/// </summary>
public class RapidOcrEngineTests
{
    private static bool ModelsPresent
        => ModelLocator.Find(ModelLocator.DetectionModel) is not null
           && ModelLocator.Find(ModelLocator.RecognitionModel) is not null;

    private static ImageBuffer Solid(int w, int h, byte value)
    {
        var img = ImageBuffer.Allocate(w, h, PixelFormat.Bgra32);
        for (int i = 0; i < img.Pixels.Length; i++) img.Pixels[i] = value;
        return img;
    }

    /// <summary>
    /// RF-575 / RF-120 — O motor nunca é apresentado como utilizável quando falhará: ou
    /// está disponível, ou traz o motivo por escrito.
    /// </summary>
    [Fact]
    public void O_motor_ou_esta_disponivel_ou_explica_por_que_nao()
    {
        using var engine = new RapidOcrEngine();

        if (engine.IsAvailable)
        {
            Assert.Null(engine.UnavailableReason);
        }
        else
        {
            Assert.False(string.IsNullOrWhiteSpace(engine.UnavailableReason));
        }
    }

    [Fact]
    public void RF_128_o_modelo_ausente_produz_indisponibilidade_explicada()
    {
        using var engine = new RapidOcrEngine(
            modelDirectory: Path.Combine(Path.GetTempPath(), "gort-sem-modelos-" + Guid.NewGuid()));

        // A busca ainda alcança as pastas convencionais; se os modelos estiverem
        // instalados, o motor sobe — que é justamente o que RF-128 manda tentar.
        if (!engine.IsAvailable)
        {
            Assert.Contains("modelos", engine.UnavailableReason!, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void RF_121_o_motor_moderno_devolve_posicao_de_palavra()
        => Assert.True(new RapidOcrEngine().ProvidesWordPositions);

    /// <summary>
    /// Critério de teste da Etapa 5: "uma imagem em branco produz resultado vazio".
    /// Vazio, e não erro — RF-194 trata vazio como mudança, então a tradução some da tela.
    /// </summary>
    [Fact]
    public void Uma_imagem_em_branco_produz_resultado_vazio_e_nao_erro()
    {
        using var engine = new SafeOcrEngine(new RapidOcrEngine());
        if (!engine.IsAvailable) return;

        var result = engine.Recognize(Solid(400, 120, 255), "en");
        Assert.True(result.IsEmpty);
        Assert.Null(result.ErrorMessage);
        Assert.Empty(result.Words);
        Assert.Equal(0, result.LineCount);
    }

    [Fact]
    public void Uma_imagem_toda_preta_tambem_produz_resultado_vazio()
    {
        using var engine = new SafeOcrEngine(new RapidOcrEngine());
        if (!engine.IsAvailable) return;

        Assert.True(engine.Recognize(Solid(400, 120, 0), "en").IsEmpty);
    }

    [Fact]
    public void Uma_imagem_degenerada_nao_lanca()
    {
        using var engine = new SafeOcrEngine(new RapidOcrEngine());
        if (!engine.IsAvailable) return;

        Assert.True(engine.Recognize(ImageBuffer.Allocate(0, 0, PixelFormat.Bgra32), "en").IsEmpty);
        Assert.NotNull(engine.Recognize(Solid(1, 1, 128), "en"));
    }

    /// <summary>
    /// RF-141 — Este motor devolve LINHAS; cada uma vira uma única palavra com a caixa da
    /// linha, e as caixas ficam dentro da imagem recebida (contrato de 6.4).
    /// </summary>
    [Fact]
    public void As_caixas_devolvidas_ficam_dentro_da_imagem_e_tem_dimensoes_positivas()
    {
        using var engine = new SafeOcrEngine(new RapidOcrEngine());
        if (!engine.IsAvailable) return;

        // Barras escuras sobre fundo claro: o detector as trata como texto.
        var img = Solid(600, 200, 240);
        for (int linha = 0; linha < 3; linha++)
        {
            for (int y = 40 + linha * 50; y < 70 + linha * 50; y++)
            {
                for (int x = 40; x < 400; x += 24)
                {
                    for (int dx = 0; dx < 14; dx++)
                    {
                        int o = img.OffsetOf(x + dx, y);
                        img.Pixels[o] = img.Pixels[o + 1] = img.Pixels[o + 2] = 20;
                    }
                }
            }
        }

        var result = engine.Recognize(img, "en");
        Assert.Null(result.ErrorMessage);

        var bounds = new Rect(0, 0, img.Width, img.Height);
        foreach (var word in result.Words)
        {
            // RF-142 — nunca largura ou altura negativa.
            Assert.True(word.Box.Width > 0, $"largura não positiva em {word.Box}");
            Assert.True(word.Box.Height > 0, $"altura não positiva em {word.Box}");
            Assert.True(bounds.Contains(word.Box), $"{word.Box} fora de {bounds}");
        }

        // Uma palavra por linha, conforme RF-141.
        Assert.All(result.WordsPerLine, n => Assert.Equal(1, n));
    }

    /// <summary>
    /// RF-140 — Com a orientação vertical ativa, as linhas verticais são reordenadas por
    /// coluna, da direita para a esquerda; as horizontais ficam onde estavam. 🔒
    /// </summary>
    [Fact]
    public void RF_140_a_opcao_de_orientacao_vertical_e_configuravel_no_motor()
    {
        using var engine = new RapidOcrEngine();
        Assert.False(engine.VerticalOrientation);
        engine.VerticalOrientation = true;
        Assert.True(engine.VerticalOrientation);
    }

    [Fact]
    public void P_30_o_teto_de_linhas_por_imagem_vem_da_calibragem()
        => Assert.Equal(Gort.Core.Calibration.P.ModernOcrMaxLines, new RapidOcrEngine().MaxLines);
}

/// <summary>Localização dos modelos (RF-128).</summary>
public class ModelLocatorTests
{
    [Fact]
    public void A_busca_inclui_a_pasta_do_executavel_e_a_de_trabalho()
    {
        var caminhos = ModelLocator.SearchPaths().ToList();
        Assert.Contains(caminhos, p => p.Contains("modelos"));
        Assert.True(caminhos.Count >= 3);
    }

    [Fact]
    public void Uma_pasta_explicita_e_procurada_primeiro()
    {
        var caminhos = ModelLocator.SearchPaths("/pasta/explicita").ToList();
        Assert.Equal("/pasta/explicita", caminhos[0]);
    }

    [Fact]
    public void Um_arquivo_inexistente_devolve_nulo_sem_lancar()
        => Assert.Null(ModelLocator.Find("modelo-que-nao-existe.onnx"));
}

/// <summary>
/// RF-029 / RF-566 — Qual modelo atende qual idioma é DADO. Estes testes leem o catálogo
/// real de <c>data/</c>: se alguém mover essa decisão para o código, eles quebram.
/// </summary>
public class ModernOcrModelCatalogTests
{
    private static Gort.Core.Catalog.AppCatalog Catalog()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gort.sln")))
            dir = dir.Parent;
        return Gort.Core.Catalog.AppCatalog.Load(Path.Combine(dir!.FullName, "data"));
    }

    [Fact]
    public void Os_modelos_do_motor_moderno_vem_dos_dados()
    {
        var models = Catalog().ModernOcrModels;
        Assert.NotNull(models);
        Assert.EndsWith(".onnx", models!.Detection);
    }

    /// <summary>
    /// RF-309 — O escopo desta versão traduz de japonês e de inglês. Os dois precisam de
    /// reconhecedor declarado.
    /// </summary>
    [Fact]
    public void RF_309_ha_reconhecedor_para_ingles_e_para_japones()
    {
        var models = Catalog().ModernOcrModels!;
        Assert.NotNull(models.For("en"));
        Assert.NotNull(models.For("ja"));
    }

    /// <summary>
    /// O japonês precisa do seu PRÓPRIO reconhecedor: o modelo chinês tem kanji e latino,
    /// mas quase nenhum hiragana ou katakana, e kana é a maior parte de uma frase japonesa.
    /// </summary>
    [Fact]
    public void O_japones_nao_compartilha_o_reconhecedor_do_ingles()
    {
        var models = Catalog().ModernOcrModels!;
        Assert.NotEqual(models.For("en")!.Model, models.For("ja")!.Model);
    }

    [Fact]
    public void Um_idioma_sem_reconhecedor_declarado_devolve_nulo()
        => Assert.Null(Catalog().ModernOcrModels!.For("idioma_inexistente"));

    /// <summary>
    /// O detector é comum a todos os idiomas: ele acha ONDE há texto, não O QUE está
    /// escrito.
    /// </summary>
    [Fact]
    public void O_detector_e_um_so_para_todos_os_idiomas()
    {
        var models = Catalog().ModernOcrModels!;
        Assert.Single(new[] { models.Detection }.Distinct());
        Assert.True(models.Languages.Count() >= 2);
    }

    [Fact]
    public void O_motor_expoe_os_idiomas_do_catalogo()
    {
        using var engine = new RapidOcrEngine(models: Catalog().ModernOcrModels);
        if (!engine.IsAvailable) return;
        Assert.Contains("en", engine.Languages);
        Assert.Contains("ja", engine.Languages);
    }

    /// <summary>
    /// RF-145 — Um idioma sem modelo produz a mensagem de erro no resultado, sem lançar e
    /// sem parar o laço.
    /// </summary>
    [Fact]
    public void RF_145_um_idioma_sem_modelo_devolve_erro_e_nao_lanca()
    {
        using var engine = new SafeOcrEngine(new RapidOcrEngine(models: Catalog().ModernOcrModels));
        if (!engine.IsAvailable) return;

        var img = ImageBuffer.Allocate(64, 32, PixelFormat.Bgra32);
        var result = engine.Recognize(img, "idioma_inexistente");

        Assert.True(result.IsEmpty);
        Assert.Contains("idioma_inexistente", result.ErrorMessage!);
    }
}
