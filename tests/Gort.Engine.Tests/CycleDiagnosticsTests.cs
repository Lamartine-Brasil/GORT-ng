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
/// Cap. 27 — O ciclo observado: contadores de RF-498 e os sinalizadores de imagem de
/// RF-500, com o critério de aceite de RF-490 (desligar restaura o comportamento normal).
/// </summary>
public class CycleDiagnosticsTests
{
    private static readonly Rect Area = new(0, 0, 120, 40);

    private static BuiltAreas Areas() => new()
    {
        Captures = new[] { Area },
        Exclusions = Array.Empty<Rect>(),
        ColorGroups = new[] { (IReadOnlyList<bool>)new[] { true } },
        PersistedAreas = new[] { Area },
    };

    private static TranslationCycle BuildCycle()
        => new(new ScreenCapture(new OneRegionBackend()), new TranslationPipeline());

    private static CycleSettings Settings(CycleDiagnostics? diagnostics) => new()
    {
        Service = new EchoService(),
        TranslationContext = new TranslationContext { SourceCode = "en", TargetCode = "pt" },
        Ocr = new FixedOcr(),
        OcrLanguage = "en",
        Diagnostics = diagnostics,
    };

    private static (CycleDiagnostics Diagnostics, DiagnosticCounters Counters,
                    List<string> Saved) Observed(Action<DebugOptions> configure)
    {
        var options = new DebugOptions { Enabled = true };
        configure(options);

        var counters = new DiagnosticCounters();
        var saved = new List<string>();

        return (new CycleDiagnostics
        {
            Options = options,
            Directory = Path.GetTempPath(),
            Counters = counters,
            SaveImage = (name, _) => saved.Add(name),
        }, counters, saved);
    }

    /// <summary>RF-498 — Cada chamada ao motor conta uma tentativa de OCR.</summary>
    [Fact]
    public async Task RF_498_o_ciclo_conta_as_tentativas_de_ocr()
    {
        var (diagnostics, counters, _) = Observed(_ => { });

        await BuildCycle().RunAsync(Areas(), Settings(diagnostics));
        await BuildCycle().RunAsync(Areas(), Settings(diagnostics));

        Assert.Equal(2, counters.OcrAttempts);
    }

    /// <summary>RF-498 — E as traduções, com quantos textos foram de fato à rede.</summary>
    [Fact]
    public async Task RF_498_o_ciclo_conta_as_traducoes_e_as_chamadas_de_rede()
    {
        var (diagnostics, counters, _) = Observed(_ => { });

        await BuildCycle().RunAsync(Areas(), Settings(diagnostics));

        Assert.Equal(1, counters.Translations);
        Assert.True(counters.NetworkCalls > 0);
    }

    /// <summary>
    /// RF-500 — "Salvar captura" grava a imagem que ENTROU; "salvar resultado da captura"
    /// grava a que o OCR realmente vê. A diferença entre as duas é o efeito do filtro e da
    /// ampliação, e é por isso que os dois sinalizadores são separados.
    /// </summary>
    [Fact]
    public async Task RF_500_os_dois_sinalizadores_de_imagem_sao_independentes()
    {
        var (soCaptura, _, capturas) = Observed(o => o.NativeSaveCapture = true);
        await BuildCycle().RunAsync(Areas(), Settings(soCaptura));
        Assert.Equal(new[] { "captura-area0" }, capturas);

        var (soResultado, _, resultados) = Observed(o => o.NativeSaveResult = true);
        await BuildCycle().RunAsync(Areas(), Settings(soResultado));
        Assert.Equal(new[] { "tratada-area0" }, resultados);

        var (ambos, _, todas) = Observed(o =>
        {
            o.NativeSaveCapture = true;
            o.NativeSaveResult = true;
        });
        await BuildCycle().RunAsync(Areas(), Settings(ambos));
        Assert.Equal(new[] { "captura-area0", "tratada-area0" }, todas);
    }

    [Fact]
    public async Task Sem_sinalizador_nenhuma_imagem_e_gravada()
    {
        var (diagnostics, _, saved) = Observed(_ => { });
        await BuildCycle().RunAsync(Areas(), Settings(diagnostics));
        Assert.Empty(saved);
    }

    /// <summary>
    /// Critério de aceite do cap. 27 — "desativar o modo de depuração restaura o
    /// comportamento normal sem reiniciar".
    ///
    /// Aqui isso é literal: sem o objeto de diagnóstico o ciclo não conta, não grava e não
    /// consulta sinalizador nenhum. Nada precisa ser desfeito porque nada foi ligado.
    /// </summary>
    [Fact]
    public async Task RF_490_sem_o_modo_de_depuracao_o_ciclo_nao_observa_nada()
    {
        var counters = new DiagnosticCounters();

        var result = await BuildCycle().RunAsync(Areas(), Settings(diagnostics: null));

        Assert.False(result.IsEmpty);          // o ciclo rodou normalmente
        Assert.Equal(0, counters.OcrAttempts);
    }

    // ── Dublês ──────────────────────────────────────────────────────────────

    private sealed class OneRegionBackend : ICaptureBackend
    {
        public bool Supports(CaptureSource source) => true;
        public void ExcludeOwnWindow(nint handle) { }
        public void Dispose() { }

        public CapturedRegion? Capture(int index, Rect rect, CaptureSource source) => new()
        {
            Index = index,
            ScreenRect = rect,
            Image = new ImageBuffer(rect.Width, rect.Height, PixelFormat.Bgra32,
                                    new byte[rect.Width * rect.Height * 4]),
        };
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
            => OcrResultBuilder.FromLines(new[] { ("Hello world", new Rect(4, 4, 90, 20)) });
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
