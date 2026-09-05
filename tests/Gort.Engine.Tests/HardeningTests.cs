using Gort.Core.Diagnostics;
using Gort.Core.Model;
using Gort.Core.Ocr;
using Gort.Core.Regions;
using Gort.Core.Translation;
using Gort.Engine;
using Gort.Platform.Capture;
using Xunit;

namespace Gort.Engine.Tests;

/// <summary>
/// VII.2 — Memória. RF-554: cada imagem de região é liberada assim que não é mais
/// necessária, e não há mais de um conjunto vivo por vez.
/// </summary>
public class HardeningTests
{
    private static BuiltAreas ThreeAreas()
    {
        var rects = new[]
        {
            new Rect(0, 0, 400, 200),
            new Rect(400, 0, 400, 200),
            new Rect(0, 200, 400, 200),
        };
        return new BuiltAreas
        {
            Captures = rects,
            Exclusions = Array.Empty<Rect>(),
            ColorGroups = rects.Select(_ => (IReadOnlyList<bool>)new[] { true }).ToArray(),
            PersistedAreas = rects,
        };
    }

    private static CycleSettings Settings(LiveImageMeter meter) => new()
    {
        Service = new EchoService(),
        TranslationContext = new TranslationContext { SourceCode = "en", TargetCode = "pt" },
        Ocr = new FixedOcr(),
        OcrLanguage = "en",
        ImageMeter = meter,
    };

    /// <summary>
    /// RF-554 — Ao fim do ciclo, nenhuma imagem de região continua viva. Se o número não
    /// voltar a zero, alguma coisa está segurando um conjunto antigo.
    /// </summary>
    [Fact]
    public async Task RF_554_nenhuma_imagem_de_regiao_sobrevive_ao_ciclo()
    {
        var meter = new LiveImageMeter();
        var backend = new CountingBackend();
        var cycle = new TranslationCycle(new ScreenCapture(backend), new TranslationPipeline());

        await cycle.RunAsync(ThreeAreas(), Settings(meter));

        Assert.Equal(3, backend.Captures);
        Assert.Equal(0, meter.Bytes);
    }

    /// <summary>
    /// RF-554 — E os pixels são SOLTOS, não apenas descontados do medidor: o que ocupa
    /// memória é o vetor, e um contador zerado com o vetor vivo não vale nada.
    /// </summary>
    [Fact]
    public async Task RF_554_os_pixels_sao_efetivamente_soltos()
    {
        var backend = new CountingBackend();
        var cycle = new TranslationCycle(new ScreenCapture(backend), new TranslationPipeline());

        await cycle.RunAsync(ThreeAreas(), Settings(new LiveImageMeter()));

        Assert.All(backend.Handed, region => Assert.Equal(0, region.Image.ByteCount));
    }

    /// <summary>
    /// RF-554 — Não há mais de um conjunto vivo por vez: o ciclo seguinte recomeça a
    /// contagem do zero em vez de somar sobre o anterior.
    /// </summary>
    [Fact]
    public async Task RF_554_o_ciclo_seguinte_nao_soma_sobre_o_anterior()
    {
        var meter = new LiveImageMeter();
        var cycle = new TranslationCycle(
            new ScreenCapture(new CountingBackend()), new TranslationPipeline());

        await cycle.RunAsync(ThreeAreas(), Settings(meter));
        await cycle.RunAsync(ThreeAreas(), Settings(meter));

        Assert.Equal(0, meter.Bytes);
    }

    /// <summary>RF-559 — O detalhamento soma as três parcelas que o usuário controla.</summary>
    [Fact]
    public void RF_559_o_detalhamento_soma_as_tres_parcelas()
    {
        var report = new MemoryReport
        {
            ProcessBytes = 300L * 1024 * 1024,
            RegionImageBytes = 40L * 1024 * 1024,
            TranslationCacheBytes = 5L * 1024 * 1024,
            OverlayBitmapBytes = 8L * 1024 * 1024,
        };

        Assert.Equal(53L * 1024 * 1024, report.DetailedBytes);
        Assert.Equal("40 MB", MemoryReport.Megabytes(report.RegionImageBytes));
    }

    // ── Dublês ──────────────────────────────────────────────────────────────

    private sealed class CountingBackend : ICaptureBackend
    {
        public int Captures { get; private set; }
        public List<CapturedRegion> Handed { get; } = new();

        public bool Supports(CaptureSource source) => true;
        public void ExcludeOwnWindow(nint handle) { }
        public void Dispose() { }

        public CapturedRegion? Capture(int index, Rect rect, CaptureSource source)
        {
            Captures++;
            var region = new CapturedRegion
            {
                Index = index,
                ScreenRect = rect,
                Image = new ImageBuffer(rect.Width, rect.Height, PixelFormat.Bgra32,
                                        new byte[rect.Width * rect.Height * 4]),
            };
            Handed.Add(region);
            return region;
        }
    }

    private sealed class FixedOcr : IOcrEngine
    {
        public string Key => "dublê";
        public bool IsAvailable => true;
        public string? UnavailableReason => null;
        public bool ProvidesWordPositions => true;
        public IReadOnlyList<string> Languages => new[] { "en" };
        public void Dispose() { }

        public OcrResult Recognize(ImageBuffer image, string languageCode)
            => OcrResultBuilder.FromLines(new[] { ("Hello", new Rect(4, 4, 60, 18)) });
    }

    private sealed class EchoService : ITranslationService
    {
        public string Key => "dublê";
        public void Dispose() { }

        public Task<TranslationOutcome> TranslateAsync(
            string text, TranslationContext context, CancellationToken cancellation)
            => Task.FromResult(TranslationOutcome.Ok(text));
    }
}
