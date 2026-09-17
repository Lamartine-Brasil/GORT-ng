using Gort.Core.Imaging;
using Gort.Core.Model;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>
/// RF-570 — Detecção de quadros em branco: o sintoma do jogo em tela cheia exclusiva.
/// </summary>
public class BlankFrameTests
{
    private static ImageBuffer Uniform(byte value, int w = 200, int h = 120)
    {
        var image = ImageBuffer.Allocate(w, h, PixelFormat.Bgra32);
        for (int i = 0; i < image.Pixels.Length; i++) image.Pixels[i] = value;
        return image;
    }

    private static ImageBuffer WithText()
    {
        var image = Uniform(20);
        for (int y = 40; y < 70; y++)
        {
            for (int x = 30; x < 170; x += 3)
            {
                int o = image.OffsetOf(x, y);
                image.Pixels[o] = image.Pixels[o + 1] = image.Pixels[o + 2] = 240;
            }
        }
        return image;
    }

    [Fact]
    public void RF_570_um_quadro_preto_e_reconhecido_como_em_branco()
        => Assert.True(BlankFrameDetector.IsBlank(Uniform(0)));

    /// <summary>
    /// Branco uniforme tem a mesma causa e o mesmo remédio, e o OCR não acha texto em
    /// nenhum dos dois.
    /// </summary>
    [Fact]
    public void Branco_uniforme_tambem_conta_como_em_branco()
        => Assert.True(BlankFrameDetector.IsBlank(Uniform(255)));

    [Fact]
    public void Um_quadro_com_texto_nao_e_em_branco()
        => Assert.False(BlankFrameDetector.IsBlank(WithText()));

    /// <summary>
    /// A tolerância não é zero: compressão de vídeo e escalonamento de tela deixam ruído, e
    /// um preto de verdade numa captura real chega com valores de 0 a 2.
    /// </summary>
    [Fact]
    public void RF_570_o_ruido_de_captura_nao_desfaz_a_deteccao()
    {
        var image = Uniform(0);
        var random = new Random(7);

        for (int i = 0; i < image.Pixels.Length; i++)
            image.Pixels[i] = (byte)random.Next(0, BlankFrameDetector.Tolerance);

        Assert.True(BlankFrameDetector.IsBlank(image));
    }

    [Fact]
    public void Uma_imagem_vazia_conta_como_em_branco()
        => Assert.True(BlankFrameDetector.IsBlank(ImageBuffer.Allocate(0, 0, PixelFormat.Bgra32)));

    /// <summary>
    /// RF-570 — A sugestão só aparece depois de alguns quadros seguidos: um quadro preto
    /// isolado é comum e inócuo — uma transição de cena, um carregamento —, e sugerir na
    /// primeira vez seria acusar o usuário de algo que se resolve no ciclo seguinte.
    /// </summary>
    [Fact]
    public void RF_570_a_sugestao_espera_alguns_quadros_seguidos()
    {
        var watch = new BlankFrameWatch();

        Assert.False(watch.Observe(blank: true));
        Assert.False(watch.Observe(blank: true));
        Assert.True(watch.Observe(blank: true));
    }

    /// <summary>
    /// A sugestão sai UMA VEZ por sequência: repeti-la a cada ciclo transformaria um aviso
    /// útil em ruído, e o usuário aprenderia a ignorá-lo justamente quando ele importa.
    /// </summary>
    [Fact]
    public void RF_570_a_sugestao_nao_se_repete_na_mesma_sequencia()
    {
        var watch = new BlankFrameWatch();

        for (int i = 0; i < BlankFrameDetector.FramesBeforeSuggesting - 1; i++)
            watch.Observe(true);

        Assert.True(watch.Observe(true));

        for (int i = 0; i < 20; i++) Assert.False(watch.Observe(true));
    }

    /// <summary>
    /// Um quadro com conteúdo zera tudo, inclusive a memória de já ter sugerido: se o
    /// problema voltar depois, ele merece um aviso novo.
    /// </summary>
    [Fact]
    public void Um_quadro_com_conteudo_rearma_a_sugestao()
    {
        var watch = new BlankFrameWatch();

        for (int i = 0; i < BlankFrameDetector.FramesBeforeSuggesting; i++) watch.Observe(true);
        Assert.Equal(BlankFrameDetector.FramesBeforeSuggesting, watch.Streak);

        Assert.False(watch.Observe(blank: false));
        Assert.Equal(0, watch.Streak);

        for (int i = 0; i < BlankFrameDetector.FramesBeforeSuggesting - 1; i++)
            Assert.False(watch.Observe(true));

        Assert.True(watch.Observe(true));
    }
}
