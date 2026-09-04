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
