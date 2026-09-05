using System.Diagnostics;
using Gort.Core.Caching;
using Gort.Core.Calibration;
using Gort.Core.Catalog;
using Gort.Core.Configuration;
using Gort.Core.Imaging;
using Gort.Core.Model;
using Gort.Core.Ocr;
using Gort.Core.Regions;
using Gort.Core.Structuring;
using Gort.Core.Translation;
using Gort.Core.Translation.Services;
using Gort.Engine;
using Gort.Ocr.Rapid;
using Gort.Platform;
using Gort.Platform.Monitors;

// ─────────────────────────────────────────────────────────────────────────────
// ETAPA 7 — "primeiro produto utilizável de ponta a ponta: captura, reconhece,
// traduz e mostra em uma janela."
//
// Esta ferramenta faz tudo menos a janela: exercita o ciclo completo do capítulo
// 8, passos 7 a 13, e imprime o que a janela de tradução receberia.
// ─────────────────────────────────────────────────────────────────────────────

static string RepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gort.sln"))) dir = dir.Parent;
    return dir?.FullName ?? Directory.GetCurrentDirectory();
}

var catalogo = AppCatalog.Load(Path.Combine(RepoRoot(), "data"));
var perfil = Profile.Defaults();

using var plataforma = PlatformServices.Create();
if (!plataforma.Capabilities.CanTranslate)
{
    Console.WriteLine(plataforma.Capabilities.BlockingExplanation());
    return 1;
}

var monitores = plataforma.Monitors.Monitors;

// ── Áreas (Etapa 3) ─────────────────────────────────────────────────────────
var regioes = new RegionManager(q => MonitorGeometry.ScaleOf(monitores, q));
var tela = monitores[0].Bounds;

// A área pode vir da linha de comando: "x y largura altura" da MOLDURA.
// Sem argumentos, usa a metade superior do monitor principal, que é onde a maior
// parte das janelas mostra texto.
var numeros = args.Where(a => int.TryParse(a, out _)).Select(int.Parse).ToArray();
var moldura = numeros.Length >= 4
    ? new Rect(numeros[0], numeros[1], numeros[2], numeros[3])
    : new Rect(tela.Left, tela.Top, tela.Width, tela.Height / 2);

regioes.AddArea(moldura);

// RF-065 — sem área incremental, a tradução não começa.
if (!regioes.HasAnyIncrementalArea)
{
    Console.WriteLine("É preciso definir ao menos uma área de OCR antes de traduzir.");
    return 1;
}

var areas = regioes.Build();

// ── Motor de OCR (Etapa 5) ──────────────────────────────────────────────────
using var motores = new OcrEngineRegistry();
motores.Register(new RapidOcrEngine(models: catalogo.ModernOcrModels));

var ocr = motores.Resolve(perfil.OcrEngine);
if (ocr is null)
{
    Console.WriteLine("Nenhum motor de OCR disponível.");
    return 1;
}

// ── Serviço de tradução (Etapa 7) ───────────────────────────────────────────
var servicoInfo = catalogo.Service(perfil.TranslationService)!;
using var servico = new FreeWebTranslator(catalogo.FreeWebTranslator!);

var paths = new UserPaths(Path.Combine(Path.GetTempPath(), "gort-ciclo"));
var memoria = new ResultMemory(servicoInfo.Key, paths.ResultMemoryFor(servicoInfo.Key));
memoria.Load();

using var pipeline = new TranslationPipeline
{
    SeparatorToken = servicoInfo.SeparatorToken,
    Memory = servicoInfo.UsesResultMemory ? memoria : null,
};

var origem = catalogo.Language(perfil.OcrLanguage)!;
var destino = catalogo.Language(perfil.TargetLanguage)!;

Console.WriteLine($"OCR: {ocr.Key} ({origem.Key})   Tradutor: {servicoInfo.Key} " +
                  $"({origem.CodeFor(servicoInfo.Key)} → {destino.CodeFor(servicoInfo.Key)})");
Console.WriteLine($"Área: {areas.Captures[0]}   Ampliação: {perfil.Scale}x");
Console.WriteLine(new string('─', 78));

var settings = new CycleSettings
{
    Service = servico,
    TranslationContext = new TranslationContext
    {
        SourceCode = origem.CodeFor(servicoInfo.Key)!,
        TargetCode = destino.CodeFor(servicoInfo.Key)!,
    },
    Ocr = ocr,
    OcrLanguage = origem.Key,
    Filter = new FilterSettings { Mode = perfil.FilterMode, Scale = perfil.Scale },
    MergeLines = true,
    Text = new TextProcessingOptions
    {
        WindowMode = WindowMode.Dark,
        RemoveSpaces = perfil.RemoveSpaces,
    },
    NumberAreas = perfil.NumberAreas,
};

// Diagnóstico: grava o que a região capturou, para distinguir "não há texto na área"
// de "o pipeline perdeu o texto".
{
    var amostra = plataforma.Capture.Capture(new Gort.Platform.Capture.CaptureRequest
    {
        Rects = areas.Captures,
        Source = Gort.Platform.Capture.CaptureSource.Screen,
    });
    if (amostra.Count > 0)
    {
        string caminho = Path.Combine(Path.GetTempPath(), "gort-ciclo-entrada.png");
        Gort.Platform.Diagnostics.PngWriter.Save(amostra[0].Image, caminho);
        Console.WriteLine($"(diagnóstico: entrada gravada em {caminho})");

        var direto = ocr.Recognize(
            Preprocessor.Process(amostra[0].Image, areas.ExclusionsIn(0), settings.Filter),
            settings.OcrLanguage);
        Console.WriteLine($"(diagnóstico: OCR direto → {direto.LineCount} linhas, " +
                          $"vazio={direto.IsEmpty}, erro={direto.ErrorMessage ?? "nenhum"})");
    }
    else
    {
        Console.WriteLine("(diagnóstico: a captura não produziu imagem)");
    }
}

var ciclo = new TranslationCycle(plataforma.Capture, pipeline);

for (int rodada = 1; rodada <= 2; rodada++)
{
    var t = Stopwatch.StartNew();
    var r = await ciclo.RunAsync(areas, settings);
    t.Stop();

    Console.WriteLine($"Ciclo {rodada}: {t.ElapsedMilliseconds} ms " +
                      $"({t.ElapsedMilliseconds * 100.0 / P.CycleIntervalSpeed1Ms:0}% de P-05), " +
                      $"{r.Regions.Sum(x => x.Blocks.Count)} blocos, " +
                      $"{r.NetworkCount} textos numa requisição (RF-231)" +
                      $"{(r.Error is null ? "" : "  ERRO: " + r.Error)}");

    if (rodada == 1)
    {
        Console.WriteLine();
        Console.WriteLine("Texto reconhecido:");
        foreach (var linha in r.RecognizedText.Split('\n').Take(8))
            if (linha.Trim().Length > 0) Console.WriteLine($"   {linha.Trim()}");

        Console.WriteLine();
        Console.WriteLine("O que a janela de tradução recebe:");
        Console.WriteLine("   ┌" + new string('─', 60));
        foreach (var linha in r.DisplayText.Split('\n').Take(10))
            Console.WriteLine($"   │ {linha}");
        Console.WriteLine("   └" + new string('─', 60));

        // RF-328 — com a exibição do texto reconhecido ativa, o modo escuro mostra a
        // tradução, duas quebras de linha, o prefixo "OCR : " e o texto reconhecido.
        Console.WriteLine();
    }
}

await memoria.FlushAsync();
Console.WriteLine();
Console.WriteLine("A segunda rodada não vai à rede: é o descarte de RF-207 e RF-230.");
return 0;
