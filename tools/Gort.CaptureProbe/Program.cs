using Gort.Core.Model;
using Gort.Platform;
using Gort.Platform.Capabilities;
using Gort.Platform.Capture;
using Gort.Platform.Diagnostics;
using Gort.Platform.Monitors;
using Gort.Core.Imaging;
using Gort.Core.Regions;

// ─────────────────────────────────────────────────────────────────────────────
// Teste visual da ETAPA 2 da PARTE X.
//
//   Entregável:  "uma função que recebe um retângulo em coordenadas globais e
//                 devolve a imagem. Um teste visual que salva a imagem em arquivo."
//   Como testar: "capturar uma região em cada monitor, incluindo coordenadas
//                 negativas, e conferir o conteúdo."
//
// Também exercita RF-576: todas as capacidades são apuradas na inicialização e
// impressas aqui — é este relatório que a interface vai consumir para ocultar ou
// desabilitar controles.
// ─────────────────────────────────────────────────────────────────────────────

// --ignorar-permissao existe só nesta FERRAMENTA de diagnóstico, para distinguir
// "a ligação nativa está errada" de "falta a permissão do sistema". O programa em si
// obedece a RF-569 e não inicia sem a permissão.
bool ignorePermission = args.Contains("--ignorar-permissao");

string outputDir = args.FirstOrDefault(a => !a.StartsWith("--"))
    ?? Path.Combine(Path.GetTempPath(), "gort-captura");

using var platform = PlatformServices.Create();

Console.WriteLine($"Plataforma: {platform.PlatformName}");
Console.WriteLine();

// ── RF-576: capacidades apuradas na inicialização ────────────────────────────
Console.WriteLine("Capacidades (PARTE IX.1)");
Console.WriteLine(new string('─', 78));
foreach (var status in platform.Capabilities.All)
{
    string mark = status.Available ? "  ok  " : " FALTA";
    Console.WriteLine($"[{mark}] {CapabilityInfo.Name(status.Capability)}");
    if (!status.Available)
    {
        Console.WriteLine($"         {status.Kind}: {status.Explanation}");
        if (status.RemediationHint is not null)
            Console.WriteLine($"         → {status.RemediationHint}");
    }
}
Console.WriteLine();

// ── RF-569 / PARTE VIII: sem capacidade essencial, não iniciar ───────────────
if (!platform.Capabilities.CanTranslate)
{
    Console.WriteLine("Nenhuma tradução é possível neste estado:");
    Console.WriteLine(platform.Capabilities.BlockingExplanation());
    Console.WriteLine();

    var screen = platform.Capabilities[Capability.ScreenRegionCapture];
    if (screen.Kind == UnavailabilityKind.PermissionRequired)
    {
        Console.WriteLine("Solicitando a permissão ao sistema...");
        var updated = platform.RequestPermission(Capability.ScreenRegionCapture);
        if (!updated.Available && !ignorePermission)
        {
            Console.WriteLine("Permissão ainda ausente. Abrindo a tela de configuração.");
            platform.OpenPermissionSettings(Capability.ScreenRegionCapture);
            Console.WriteLine();
            Console.WriteLine("Conceda a permissão e rode de novo. Para conferir apenas a");
            Console.WriteLine("ligação nativa, rode com --ignorar-permissao: sem a permissão");
            Console.WriteLine("o sistema devolve só o papel de parede, sem as janelas.");
            return 1;
        }
    }
    else if (!ignorePermission)
    {
        return 1;
    }

    if (ignorePermission)
        Console.WriteLine("(--ignorar-permissao) Seguindo mesmo assim, só para diagnóstico.");
    Console.WriteLine();
}

// ── C18: monitores ───────────────────────────────────────────────────────────
var monitors = platform.Monitors.Monitors;
Console.WriteLine($"Monitores (C18): {monitors.Count}");
Console.WriteLine(new string('─', 78));
foreach (var m in monitors) Console.WriteLine($"  {m}");

var desktop = MonitorGeometry.VirtualDesktop(monitors);
Console.WriteLine($"  área de trabalho virtual: {desktop}");
Console.WriteLine();

if (monitors.Count == 0)
{
    Console.WriteLine("Sem monitores; nada a capturar.");
    return 1;
}

// ── C1: uma região por monitor ───────────────────────────────────────────────
Directory.CreateDirectory(outputDir);
Console.WriteLine($"Capturando em {outputDir}");
Console.WriteLine(new string('─', 78));

var rects = new List<(string Name, Rect Rect)>();

for (int i = 0; i < monitors.Count; i++)
{
    var m = monitors[i];
    // Uma faixa do canto superior esquerdo de cada monitor, que é onde as coordenadas
    // negativas aparecem quando o monitor fica à esquerda ou acima do principal.
    int w = Math.Min(480, m.Bounds.Width);
    int h = Math.Min(320, m.Bounds.Height);
    rects.Add(($"monitor-{i}-canto", new Rect(m.Bounds.Left, m.Bounds.Top, w, h)));

    // E o centro, para conferir o conteúdo com algo mais reconhecível.
    rects.Add(($"monitor-{i}-centro", new Rect(
        m.Bounds.Left + (m.Bounds.Width - w) / 2,
        m.Bounds.Top + (m.Bounds.Height - h) / 2, w, h)));
}

// Um retângulo que atravessa a fronteira entre dois monitores, quando houver.
if (monitors.Count > 1)
{
    var a = monitors[0].Bounds;
    rects.Add(("entre-monitores", new Rect(a.Right - 200, a.Top + 100, 400, 200)));
}

// Um retângulo completamente fora de qualquer monitor: PARTE VIII manda que o índice
// seja pulado silenciosamente, sem erro.
rects.Add(("fora-da-tela", new Rect(desktop.Right + 5000, desktop.Bottom + 5000, 100, 100)));

var request = new CaptureRequest
{
    Rects = rects.Select(r => r.Rect).ToList(),
    Source = CaptureSource.Screen,
};

var sw = System.Diagnostics.Stopwatch.StartNew();
var regions = platform.Capture.Capture(request);
sw.Stop();

var byIndex = regions.ToDictionary(r => r.Index);

for (int i = 0; i < rects.Count; i++)
{
    var (name, rect) = rects[i];
    if (!byIndex.TryGetValue(i, out var region))
    {
        Console.WriteLine($"  {name,-22} {rect}  → sem imagem (índice pulado)");
        continue;
    }

    string file = Path.Combine(outputDir, $"{name}.png");
    PngWriter.Save(region.Image, file);

    Console.WriteLine($"  {name,-22} {rect}  → {region.Image.Width}x{region.Image.Height} " +
                      $"{region.Image.Format} ({region.Image.ByteCount / 1024} KiB)  {file}");
}

Console.WriteLine();
Console.WriteLine($"{regions.Count} de {rects.Count} regiões capturadas em {sw.ElapsedMilliseconds} ms.");
Console.WriteLine();

// ── VII.1 / RF-547: a captura tem de caber no orçamento do ciclo ─────────────
//
// "O ciclo completo, do início da captura até o desenho, deve caber dentro do
//  intervalo configurado. Com o intervalo mínimo P-05 [...] esse é o alvo:
//  300 ms para capturar, pré-processar, reconhecer, agrupar, consultar cache e
//  desenhar."
//
// A primeira captura carrega JIT e inicialização do sistema gráfico; o que
// interessa ao laço é o custo em REGIME.
{
    var caixaTipica = new Rect(
        monitors[0].Bounds.Left + 100, monitors[0].Bounds.Top + 100, 800, 200);
    var pedido = new CaptureRequest { Rects = new[] { caixaTipica }, Source = CaptureSource.Screen };

    for (int i = 0; i < 5; i++) platform.Capture.Capture(pedido);   // aquecimento

    var amostras = new List<double>();
    for (int i = 0; i < 30; i++)
    {
        var t = System.Diagnostics.Stopwatch.StartNew();
        platform.Capture.Capture(pedido);
        t.Stop();
        amostras.Add(t.Elapsed.TotalMilliseconds);
    }
    amostras.Sort();

    int orcamento = Gort.Core.Calibration.P.CycleIntervalSpeed1Ms;
    double mediana = amostras[amostras.Count / 2];
    double pior = amostras[^1];

    Console.WriteLine($"Latência da captura em regime, uma caixa de diálogo típica {caixaTipica}:");
    Console.WriteLine($"  mediana {mediana:0.0} ms · pior {pior:0.0} ms · " +
                      $"{mediana / orcamento * 100:0.#}% do orçamento de {orcamento} ms (P-05)");
}

// ── ETAPA 3 + 4: regiões, exclusões e pré-processamento ─────────────────────
//
// Amarra o caminho inteiro: uma moldura vira retângulo de captura (RF-073 a RF-077),
// a exclusão é traduzida para as coordenadas da imagem (RF-068), a imagem é
// capturada (C1) e passa pelo filtro, pela erosão e pela ampliação (cap. 13).
{
    Console.WriteLine();
    Console.WriteLine("Regiões, exclusões e pré-processamento (Etapas 3 e 4)");
    Console.WriteLine(new string('─', 78));

    // RF-075 — a escala vem do monitor que contém a moldura, no momento da conversão.
    var regioes = new RegionManager(quadro => MonitorGeometry.ScaleOf(monitors, quadro));

    var principal = monitors[0].Bounds;
    // Uma moldura sobre a tela e uma exclusão recortando um pedaço de dentro dela —
    // como um retrato ou um contador que o usuário não quer ler.
    regioes.AddArea(new Rect(principal.Left + 40, principal.Top + 40, 806, 323));
    regioes.AddExclusion(new Rect(principal.Left + 60, principal.Top + 80, 206, 123));

    var montadas = regioes.Build();
    int alinhamento = Gort.Core.Calibration.P.CaptureWidthAlignment;
    Console.WriteLine($"  moldura  → captura {montadas.Captures[0]}, largura múltipla de " +
                      $"{alinhamento}: {montadas.Captures[0].Width % alinhamento == 0}");

    var exclusoesLocais = montadas.ExclusionsIn(0);
    Console.WriteLine($"  exclusão → {exclusoesLocais.Count} recortada(s) em coordenadas " +
                      $"da imagem: {string.Join(", ", exclusoesLocais)}");

    var capturadas = platform.Capture.Capture(new CaptureRequest
    {
        Rects = montadas.Captures,
        Source = CaptureSource.Screen,
    });

    if (capturadas.Count > 0)
    {
        var bruta = capturadas[0].Image;
        PngWriter.Save(bruta, Path.Combine(outputDir, "regiao-bruta.png"));

        // RF-119 — o assistente de configuração rápida escolhe os grupos HSV a partir de
        // "texto claro" ou "texto escuro". As interfaces de hoje costumam ser escuras, com
        // texto claro, então usa-se P-28. O limiar simples pressupõe o contrário — texto
        // escuro sobre fundo claro — e inverteria a imagem nesta tela.
        var filtro = new FilterSettings
        {
            Mode = FilterMode.Hsv,
            Groups = FilterSettings.WizardGroups(darkText: false),   // P-28
            Scale = Gort.Core.Calibration.P.DefaultScale,            // P-22
        };

        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        var tratada = Preprocessor.Process(bruta, exclusoesLocais, filtro);
        cronometro.Stop();

        PngWriter.Save(tratada, Path.Combine(outputDir, "regiao-tratada.png"));

        Console.WriteLine($"  bruta    → {bruta.Width}x{bruta.Height} {bruta.Format}");
        Console.WriteLine($"  tratada  → {tratada.Width}x{tratada.Height} {tratada.Format}, " +
                          $"ampliação {filtro.Scale}x, filtro {filtro.Mode}, " +
                          $"em {cronometro.Elapsed.TotalMilliseconds:0.0} ms");
        Console.WriteLine("  arquivos → regiao-bruta.png, regiao-tratada.png");
    }
}

return 0;
