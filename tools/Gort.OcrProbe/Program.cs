using System.Diagnostics;
using Gort.Core.Calibration;
using Gort.Core.Imaging;
using Gort.Core.Model;
using Gort.Core.Ocr;
using Gort.Core.Regions;
using Gort.Core.Structuring;
using Gort.Ocr.Rapid;
using Gort.Platform;
using Gort.Platform.Capture;
using Gort.Platform.Diagnostics;
using Gort.Platform.Monitors;

// ─────────────────────────────────────────────────────────────────────────────
// Teste da ETAPA 5: um motor de OCR satisfazendo o contrato de 6.4.
//
//   Como testar: "uma imagem de teste conhecida produz as palavras e caixas
//                 esperadas; uma imagem em branco produz resultado vazio."
//
// Captura uma região da tela, roda o motor nas duas ordens de canal e imprime o
// que cada uma reconhece — a ordem certa é a que produz texto legível, e essa é
// uma questão a verificar, não a adivinhar.
// ─────────────────────────────────────────────────────────────────────────────

string outputDir = args.FirstOrDefault(a => !a.StartsWith("--"))
    ?? Path.Combine(Path.GetTempPath(), "gort-ocr");
Directory.CreateDirectory(outputDir);

using var platform = PlatformServices.Create();
var monitors = platform.Monitors.Monitors;
if (monitors.Count == 0) { Console.WriteLine("Sem monitores."); return 1; }

// ── Imagem em branco: resultado vazio, não erro ──────────────────────────────
using (var engine = new SafeOcrEngine(new RapidOcrEngine()))
{
    Console.WriteLine($"Motor '{engine.Key}': " +
                      (engine.IsAvailable ? "disponível" : $"INDISPONÍVEL — {engine.UnavailableReason}"));
    if (!engine.IsAvailable) return 1;

    var branca = ImageBuffer.Allocate(400, 120, PixelFormat.Bgra32);
    for (int i = 0; i < branca.Pixels.Length; i++) branca.Pixels[i] = 255;

    var vazio = engine.Recognize(branca, "en");
    Console.WriteLine($"Imagem em branco → vazio: {vazio.IsEmpty}, " +
                      $"linhas: {vazio.LineCount}, erro: {vazio.ErrorMessage ?? "nenhum"}");
    Console.WriteLine();
}

// ── Região real da tela ──────────────────────────────────────────────────────
var regioes = new RegionManager(q => MonitorGeometry.ScaleOf(monitors, q));
var tela = monitors[0].Bounds;
regioes.AddArea(new Rect(tela.Left + 40, tela.Top + 40, 806, 323));

var montadas = regioes.Build();
var capturadas = platform.Capture.Capture(new CaptureRequest
{
    Rects = montadas.Captures,
    Source = CaptureSource.Screen,
});

if (capturadas.Count == 0) { Console.WriteLine("Nada capturado."); return 1; }

// RF-118 — motores modernos vão melhor com a imagem colorida original; o filtro existe
// para fundos difíceis. É por isso que IV.12 traz todos os filtros desligados por padrão.
var filtro = new FilterSettings { Mode = FilterMode.None, Scale = P.DefaultScale };
var imagem = Preprocessor.Process(capturadas[0].Image, montadas.ExclusionsIn(0), filtro);

PngWriter.Save(imagem, Path.Combine(outputDir, "ocr-entrada.png"));
Console.WriteLine($"Entrada: {imagem.Width}x{imagem.Height} (ampliação {filtro.Scale}x)");
Console.WriteLine();

// ── RF-029: os modelos vêm do catálogo de DADOS, por idioma ─────────────────
var catalogo = Gort.Core.Catalog.AppCatalog.Load(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data"));
var modelos = catalogo.ModernOcrModels;

Console.WriteLine("Modelos declarados nos dados (RF-029)");
Console.WriteLine(new string('─', 78));
Console.WriteLine($"  detecção: {modelos?.Detection ?? "(catálogo não encontrado)"}");
foreach (var lang in modelos?.Languages ?? Enumerable.Empty<string>())
{
    var m = modelos!.For(lang)!;
    Console.WriteLine($"  {lang,-4} → {m.Model}{(m.Dictionary is null ? "" : $"  + {m.Dictionary}")}");
}
Console.WriteLine();

using (var motor = new SafeOcrEngine(new RapidOcrEngine(models: modelos)))
{
    Console.WriteLine($"Motor por idioma: {(motor.IsAvailable ? "disponível" : motor.UnavailableReason!)}");
    Console.WriteLine($"  idiomas: {string.Join(", ", motor.Languages)}");

    foreach (var lang in motor.Languages)
    {
        var t = Stopwatch.StartNew();
        var r = motor.Recognize(imagem, lang);
        t.Stop();
        Console.WriteLine($"  {lang,-4} → {r.LineCount,2} linhas em {t.ElapsedMilliseconds,4} ms" +
                          $"{(r.ErrorMessage is null ? "" : "  ERRO: " + r.ErrorMessage)}");
        foreach (var linha in r.BuildLines().Take(4))
            Console.WriteLine($"          {linha.Text.TrimEnd()}");
    }
}
Console.WriteLine();

return 0;
