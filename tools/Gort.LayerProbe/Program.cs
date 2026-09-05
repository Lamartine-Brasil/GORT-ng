using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Gort.App;
using Gort.App.Windows;
using Gort.Core.Calibration;

// ─────────────────────────────────────────────────────────────────────────────
// Teste visual da ETAPA 11 — modo camada.
//
// Renderiza a superfície do modo camada FORA DA TELA e grava em PNG, o que torna
// verificável o que normalmente só se vê a olho: o contorno duplo de RF-336, o
// retângulo de fundo de RF-337, e a diferença entre a janela parada (RF-333) e
// traduzindo (RF-334).
// ─────────────────────────────────────────────────────────────────────────────

string outputDir = args.FirstOrDefault() ?? Path.Combine(Path.GetTempPath(), "gort-camada");
Directory.CreateDirectory(outputDir);

// O desenho "headless" puro não produz pixels; o Skia é o mesmo motor de desenho que a
// aplicação usa em tela, então o que sai daqui é o que o usuário veria.
AppBuilder.Configure<Application>()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .UseSkia()
    .SetupWithoutStarting();

// RF-007 — a verificação de desenho vetorial, que decide se o contorno é possível.
string? failure = VectorTextCheck.Apply();
Console.WriteLine($"RF-007 — desenho vetorial: " +
                  (failure is null ? "funciona" : "FALHOU — " + failure));
Console.WriteLine($"          cadeia de teste: \"{VectorTextCheck.Probe}\"");
Console.WriteLine();

const string Texto = "O sol nascia devagar sobre a colina.\nこんにちは、世界。";

void Render(string name, Action<LayerTextSurface> configure)
{
    var surface = new LayerTextSurface
    {
        Width = 720,
        Height = 180,
        FontSizePoints = 26,
    };
    surface.SetText(Texto);
    configure(surface);

    surface.Measure(new Size(720, 180));
    surface.Arrange(new Rect(0, 0, 720, 180));

    var bitmap = new RenderTargetBitmap(new PixelSize(720, 180), new Vector(96, 96));
    bitmap.Render(surface);

    string path = Path.Combine(outputDir, name + ".png");
    bitmap.Save(path);
    Console.WriteLine($"  {name,-22} → {path}");
}

Console.WriteLine("Renderizando:");

// RF-333 — parada: fundo semitransparente (P-79) e borda de destaque para o usuário
// localizar e mover a janela.
Render("01-parada", s => s.Translating = false);

// RF-334 — traduzindo: fundo totalmente transparente; só o texto aparece.
// RF-336 — contorno duplo; RF-337 — retângulo de fundo atrás do texto.
Render("02-traduzindo", s => s.Translating = true);

// RF-336 sem contorno, para comparar: é o que RF-007 produz quando o desenho
// vetorial não funciona.
Render("03-sem-contorno", s => { s.Translating = true; s.UseStroke = false; });

// RF-337 desligado: o texto sobre o jogo, sem retângulo atrás.
Render("04-sem-fundo", s => { s.Translating = true; s.UseTextBackground = false; });

Console.WriteLine();
Console.WriteLine($"Contorno externo P-80 = {P.OuterStrokeWidth} px na cor de contorno 2");
Console.WriteLine($"Contorno interno P-81 = {P.InnerStrokeWidth} px na cor de contorno 1");
Console.WriteLine($"Fundo expandido em {P.LayerBackgroundExpandLeft}/{P.LayerBackgroundExpandTop}/" +
                  $"{P.LayerBackgroundExpandWidth}/{P.LayerBackgroundExpandHeight} (P-82 a P-85)");

// ─────────────────────────────────────────────────────────────────────────────
// ETAPA 12 — modo sobreposição.
//
// Exercita, juntos, a resolução de colisões (RF-355 a RF-358), o tamanho
// automático de fonte (RF-360 a RF-363), a quebra por caractere (RF-369) e o
// desenho com contorno duplo — sobre um cenário que reproduz o caso mais comum
// do produto: um nome de personagem curto acima de uma fala longa.
// ─────────────────────────────────────────────────────────────────────────────

Console.WriteLine();
Console.WriteLine("Sobreposição (Etapa 12)");
Console.WriteLine(new string('─', 70));

void RenderOverlay(string name, Action<OverlaySurface> configure,
                   Func<IReadOnlyList<OverlayBlock>> blocks)
{
    var surface = new OverlaySurface
    {
        Width = 760,
        Height = 260,
        Translating = true,
        FontStroke = true,
        AutoFontSize = true,
        Scale = 2.0,
        VerticalDpi = 96,
    };
    configure(surface);
    surface.SetBlocks(blocks());

    surface.Measure(new Size(760, 260));
    surface.Arrange(new Rect(0, 0, 760, 260));

    var bitmap = new RenderTargetBitmap(new PixelSize(760, 260), new Vector(96, 96));
    bitmap.Render(surface);

    string path = Path.Combine(outputDir, name + ".png");
    bitmap.Save(path);

    var lista = surface.Blocks;
    Console.WriteLine($"  {name}");
    foreach (var b in lista)
    {
        Console.WriteLine($"     {(b.IsTitle ? "título " : "bloco  ")}{b.ViewRect} " +
                          $"fonte {b.FinalFontSize:0.0} pt · {b.Lines.Count} linha(s)" +
                          $"{(b.Clipped ? " · RECORTADO" : "")}");
    }
    Console.WriteLine($"     layout {surface.LastLayoutMs:0.0} ms · desenho {surface.LastDrawMs:0.0} ms · " +
                      $"cache {surface.LastCacheHits} acertos / {surface.LastCacheMisses} erros");
}

// Nome de personagem curto sobre fala longa, com os retângulos SE SOBREPONDO — é o
// caso que RF-357 protege: o título preserva o retângulo e a fala cede.
IReadOnlyList<OverlayBlock> Cena() => new[]
{
    new OverlayBlock
    {
        Text = "Ana",
        ViewRect = new Gort.Core.Model.Rect(30, 24, 190, 46),
        IsTitle = true,
        Orientation = Gort.Core.Model.Orientation.Horizontal,
        OwnMedianSize = 44,
    },
    new OverlayBlock
    {
        Text = "Não consigo acreditar no que acabou de acontecer aqui. Precisamos sair agora.",
        ViewRect = new Gort.Core.Model.Rect(150, 30, 560, 120),
        IsTitle = false,
        Orientation = Gort.Core.Model.Orientation.Horizontal,
        OwnMedianSize = 40,
    },
    new OverlayBlock
    {
        Text = "Pressione qualquer tecla para continuar",
        ViewRect = new Gort.Core.Model.Rect(30, 180, 400, 50),
        IsTitle = false,
        Orientation = Gort.Core.Model.Orientation.Horizontal,
        OwnMedianSize = 28,
    },
};

RenderOverlay("05-sobreposicao", _ => { }, Cena);

// RF-413 / RF-393 — com cor automática, a fonte vem da análise e os contornos são
// derivados dela.
RenderOverlay("06-sobreposicao-cor-automatica", _ => { }, () => Cena()
    .Select(b => new OverlayBlock
    {
        Text = b.Text,
        ViewRect = b.ViewRect,
        IsTitle = b.IsTitle,
        Orientation = b.Orientation,
        OwnMedianSize = b.OwnMedianSize,
        AutoColor = new Gort.Core.Model.AutoColorResult(
            Font: new Gort.Core.Model.Rgba(255, 232, 120),
            Background: new Gort.Core.Model.Rgba(16, 24, 64, 200),
            SupportingWords: 4, Contrast: 8.2,
            UsedFallback: false, ContrastCorrected: false),
    })
    .ToList());

// ─────────────────────────────────────────────────────────────────────────────
// A SOBREPOSIÇÃO ALIMENTADA POR UM CICLO REAL.
//
// Tudo acima usa cenas sintéticas: blocos escritos à mão, com retângulos e cores
// escolhidos para exercitar o desenho. Isto aqui é diferente — captura a tela de
// verdade, reconhece, traduz, analisa a cor da própria imagem e desenha. É a única
// forma de ver os capítulos 19 e 20 se encontrando: o tamanho de fonte derivado do
// original (RF-360 🔒), a resolução de colisões (RF-355 🔒) e as cores extraídas da
// imagem (cap. 20 🔒), todos sobre dados que ninguém escolheu.
//
// Uso:  dotnet run --project tools/Gort.LayerProbe -- <pasta> --real X Y L A
// ─────────────────────────────────────────────────────────────────────────────
if (args.Contains("--real"))
{
    // Sem `await`: o Avalonia instala um contexto de sincronização ao ser configurado, e
    // aguardar nele a partir do fluxo principal trava — a primeira versão desta sonda ficou
    // pendurada sem imprimir nada.
    RenderFromRealCycle();
}

void RenderFromRealCycle()
{
    var numeros = args.SkipWhile(a => a != "--real").Skip(1)
                      .Where(a => int.TryParse(a, out _)).Select(int.Parse).ToArray();

    using var sessao = AppSession.Create();
    sessao.ApplyConfiguration();

    var monitores = sessao.Platform.Monitors.Monitors;
    var area = Gort.Platform.Monitors.MonitorGeometry.VirtualDesktop(monitores);

    var moldura = numeros.Length >= 4
        ? new Gort.Core.Model.Rect(numeros[0], numeros[1], numeros[2], numeros[3])
        : new Gort.Core.Model.Rect(area.X, area.Y, area.Width, area.Height / 2);

    sessao.Regions.AddArea(moldura);

    // RF-098 — a imagem original só é pedida no modo sobreposição com cor automática; é
    // ela que a análise do capítulo 20 lê.
    sessao.Profile.WindowMode = Gort.Core.Structuring.WindowMode.Overlay;
    sessao.Advanced.AutoColor = true;

    var ajustes = sessao.BuildCycleSettings();
    var construidas = sessao.Regions.Build();

    Console.WriteLine();
    Console.WriteLine("Sobreposição a partir de um ciclo REAL");
    Console.WriteLine(new string('─', 78));
    Console.WriteLine($"  área: {construidas.Captures[0]}   ampliação: {sessao.Profile.Scale}x");

    var relogio = System.Diagnostics.Stopwatch.StartNew();
    var resultado = Task.Run(() => sessao.Cycle.RunAsync(construidas, ajustes))
                        .GetAwaiter().GetResult();
    relogio.Stop();

    if (resultado.Regions.Count == 0)
    {
        Console.WriteLine("  a captura não produziu imagem.");
        return;
    }

    Console.WriteLine($"  ciclo: {relogio.ElapsedMilliseconds} ms · "
                      + $"{resultado.Regions.Sum(r => r.Blocks.Count)} blocos");

    // A conversão de bloco de região em bloco de sobreposição é a mesma que a janela
    // principal faz (RF-352 a RF-354); aqui a janela é o próprio retângulo da área.
    var janela = construidas.Captures[0];
    var blocos = new List<OverlayBlock>();

    foreach (var regiao in resultado.Regions)
    {
        var metrics = Gort.Core.Regions.FrameGeometry.MetricsFor(
            Gort.Platform.Monitors.MonitorGeometry.ScaleOf(monitores, regiao.ScreenRect));

        for (int i = 0; i < regiao.Blocks.Count; i++)
        {
            var bloco = regiao.Blocks[i];
            if (string.IsNullOrWhiteSpace(bloco.TranslatedText)) continue;

            var rect = Gort.Core.Rendering.OverlayGeometry.BlockRect(
                bloco.SourceBox, regiao.ScreenRect, sessao.Profile.Scale, janela, metrics.Border);

            var areaNaJanela = regiao.ScreenRect.Offset(-janela.X, -janela.Y);
            rect = Gort.Core.Rendering.OverlayGeometry.ClipToArea(rect, areaNaJanela);
            if (rect.IsEmpty) continue;

            blocos.Add(new OverlayBlock
            {
                Text = bloco.TranslatedText!,
                ViewRect = rect,
                SourceRect = bloco.SourceBox,
                IsTitle = bloco.IsTitle,
                Orientation = bloco.Orientation,
                OwnMedianSize = Gort.Core.Rendering.OverlayTextLayout.MedianLineSize(
                    bloco.Lines, bloco.Orientation),
                AutoColor = regiao.UsesAutoColor && i < regiao.AutoColors.Count
                    ? regiao.AutoColors[i] : null,
            });
        }
    }

    var superficie = new OverlaySurface
    {
        Width = janela.Width,
        Height = janela.Height,
        Translating = true,
        FontStroke = true,
        AutoFontSize = true,
        Scale = sessao.Profile.Scale,
        VerticalDpi = monitores.FirstOrDefault(m => m.IsPrimary)?.Dpi ?? P.ReferenceDpi,
        FontFamilyName = "",
    };

    superficie.SetBlocks(blocos);
    superficie.Measure(new Size(janela.Width, janela.Height));
    superficie.Arrange(new Rect(0, 0, janela.Width, janela.Height));

    var mapa = new RenderTargetBitmap(
        new PixelSize(janela.Width, janela.Height), new Vector(96, 96));
    mapa.Render(superficie);

    string arquivo = Path.Combine(outputDir, "07-sobreposicao-real.png");
    mapa.Save(arquivo);

    foreach (var b in superficie.Blocks)
    {
        Console.WriteLine($"     {(b.IsTitle ? "título " : "bloco  ")}fonte "
                          + $"{b.FinalFontSize:0.0} pt (preferido {b.PreferredFontSize:0.0}) · "
                          + $"{b.Lines.Count} linha(s)"
                          + $"{(b.Clipped ? " · RECORTADO" : "")}"
                          + $"{(b.AutoColor is null ? "" : $" · cor auto {b.AutoColor.Font}")}");
        Console.WriteLine($"             \"{b.Text}\"");
    }

    Console.WriteLine($"     layout {superficie.LastLayoutMs:0.0} ms · "
                      + $"desenho {superficie.LastDrawMs:0.0} ms");
    Console.WriteLine($"  → {arquivo}");
}
