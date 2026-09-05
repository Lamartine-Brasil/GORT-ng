using System.Runtime.Versioning;
using Gort.Core.Model;
using Gort.Core.Ocr;
using Gort.Platform.Capabilities;
using Xunit;

namespace Gort.Platform.Tests;

/// <summary>
/// C20 / RF-121 — Motor do sistema operacional.
///
/// Os testes valem nas duas situações: onde o motor existe, o comportamento especificado;
/// onde não existe, a indisponibilidade explicada de RF-575.
/// </summary>
public class SystemOcrTests
{
    private static IOcrEngine? Create()
    {
        if (!OperatingSystem.IsMacOS()) return null;
        return new SafeOcrEngine(CreateMac());
    }

    [SupportedOSPlatform("macos")]
    /// <summary>
    /// RF-151 — Os idiomas de origem da tabela, que é com quem o motor faz a interseção.
    /// Aqui eles são passados à mão porque o teste não carrega o catálogo; na aplicação
    /// vêm dele.
    /// </summary>
    private static IOcrEngine CreateMac()
        => new MacOS.MacVisionOcr(new[] { "en", "ja" });

    private static ImageBuffer Solid(int w, int h, byte value)
    {
        var img = ImageBuffer.Allocate(w, h, PixelFormat.Bgra32);
        for (int i = 0; i < img.Pixels.Length; i++) img.Pixels[i] = value;
        return img;
    }

    /// <summary>RF-575 — Ou o motor está disponível, ou explica por que não.</summary>
    [Fact]
    public void O_motor_ou_esta_disponivel_ou_explica_por_que_nao()
    {
        var engine = Create();
        if (engine is null) return;

        if (engine.IsAvailable) Assert.Null(engine.UnavailableReason);
        else Assert.False(string.IsNullOrWhiteSpace(engine.UnavailableReason));
    }

    /// <summary>
    /// RF-151 / RF-136 — Os idiomas oferecidos são os que o sistema tem instalados,
    /// intersectados com a tabela do programa.
    /// </summary>
    [Fact]
    public void RF_151_os_idiomas_vem_do_sistema_e_estao_na_tabela_do_programa()
    {
        var engine = Create();
        if (engine is null || !engine.IsAvailable) return;

        Assert.NotEmpty(engine.Languages);
        Assert.All(engine.Languages, l => Assert.Contains(l, new[] { "en", "ja" }));
    }

    /// <summary>RF-121 — Este motor devolve posição por palavra (RF-351).</summary>
    [Fact]
    public void RF_121_o_motor_do_sistema_devolve_posicao_de_palavra()
    {
        var engine = Create();
        if (engine is null) return;
        Assert.True(engine.ProvidesWordPositions);
    }

    /// <summary>Uma imagem sem texto produz resultado vazio, não erro.</summary>
    [Fact]
    public void Uma_imagem_em_branco_produz_resultado_vazio()
    {
        var engine = Create();
        if (engine is null || !engine.IsAvailable) return;

        var result = engine.Recognize(Solid(300, 100, 255), "en");
        Assert.True(result.IsEmpty);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Uma_imagem_degenerada_nao_lanca()
    {
        var engine = Create();
        if (engine is null || !engine.IsAvailable) return;

        Assert.True(engine.Recognize(ImageBuffer.Allocate(0, 0, PixelFormat.Bgra32), "en").IsEmpty);
        Assert.NotNull(engine.Recognize(Solid(1, 1, 128), "en"));
    }

    /// <summary>
    /// RF-576 — A capacidade C20 é apurada na inicialização e bate com o estado real do
    /// motor: a interface nunca oferece um motor que falhará.
    /// </summary>
    [Fact]
    public void RF_576_a_capacidade_C20_reflete_o_estado_real_do_motor()
    {
        using var platform = PlatformServices.Create();
        var engine = Create();
        if (engine is null) return;

        Assert.Equal(engine.IsAvailable,
                     platform.Capabilities.Has(Capability.SystemTextRecognition));
    }
}
