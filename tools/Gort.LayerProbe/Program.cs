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
