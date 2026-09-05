using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Gort.App.Windows;
using Gort.Core.Catalog;
using Gort.Core.Configuration;
using Gort.Core.Localization;
using Gort.Core.Shortcuts;
using Gort.Core.Translation.Presets;

// ─────────────────────────────────────────────────────────────────────────────
// Teste visual da ETAPA 17b — janela de opções avançadas (V.3).
//
// A janela é revelada por um botão, e conferi-la a olho exige clicar nele. Aqui ela
// é montada FORA DA TELA, com o mesmo motor de desenho da aplicação, e cada aba é
// gravada em PNG — o que torna verificável o que normalmente só se vê clicando:
// os textos vindos da tabela (RF-481), o grupo aninhado desabilitado de RF-523 e
// os rótulos dos três controles deslizantes de RF-526.
// ─────────────────────────────────────────────────────────────────────────────

string outputDir = args.FirstOrDefault() ?? Path.Combine(Path.GetTempPath(), "gort-opcoes");
Directory.CreateDirectory(outputDir);

AppBuilder.Configure<Gort.App.App>()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .UseSkia()
    .SetupWithoutStarting();

string data = LocateData();
var catalog = AppCatalog.Load(data);
var loc = Localizer.Load(Path.Combine(data, "localizacao.csv"));

var shortcuts = ShortcutSet.WithDefaults();
var presets = new ApiPresetStore();
presets.Add("servidor de casa");
presets.Add("servidor de casa");            // RF-529 — vira "servidor de casa (2)"

var window = new AdvancedOptionsWindow(
    loc, AdvancedOptions.Defaults(), ClipboardCopyFormat.Ocr,
    shortcuts, new ShortcutDispatcher(shortcuts), presets,
    Path.Combine(Path.GetTempPath(), "gort-coletanea-inexistente"),
    () => new[] { "Helvetica", "Menlo" },
    catalog.Language("pt-BR"),
    catalog.TranslationServices.Where(s => s.ShortcutSwitchable));

var tabs = window.GetControl<TabControl>("Tabs");
var items = tabs.Items.Cast<TabItem>().ToList();

Console.WriteLine($"Abas: {items.Count}   (V.3 pede sete)");
Console.WriteLine();

// Um controle com modelo — TabControl, CheckBox, Slider — só materializa o seu visual
// quando pertence a uma janela apresentada. Desenhá-lo solto devolve uma imagem em branco.
// A plataforma "headless" com Skia dá justamente isso: uma janela de verdade, sem tela, cujo
// quadro pode ser capturado.
window.Show();

for (int i = 0; i < items.Count; i++)
{
    tabs.SelectedIndex = i;

    // Duas voltas do laço de despacho: uma para a seleção materializar o painel, outra
    // para ele acomodar o seu próprio layout.
    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    window.GetControl<Control>("Tabs").UpdateLayout();
    Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    using var frame = window.CaptureRenderedFrame();
    string name = $"aba-{i + 1}-{Slug(items[i].Header?.ToString() ?? $"{i}")}.png";
    frame?.Save(Path.Combine(outputDir, name));
    Console.WriteLine($"  {items[i].Header}  →  {name}");
}

Console.WriteLine();
Report(window, presets);

RenderAuxiliaryWindows(loc, outputDir);

Console.WriteLine();
Console.WriteLine($"Arquivos em {outputDir}");

// ─────────────────────────────────────────────────────────────────────────────

static void Report(AdvancedOptionsWindow window, ApiPresetStore presets)
{
    // RF-523 — o grupo aninhado fica desabilitado quando a caixa mestre está desmarcada.
    var master = window.GetControl<CheckBox>("AutoColorCheck");
    var group = window.GetControl<StackPanel>("AutoColorGroup");

    Console.WriteLine("RF-523 — grupo de cor automática:");
    Console.WriteLine($"  mestre marcada   → grupo habilitado: {group.IsEnabled}");

    master.IsChecked = false;
    window.GetControl<Control>("Tabs").UpdateLayout();
    Console.WriteLine($"  mestre desmarcada → grupo habilitado: {group.IsEnabled}");
    master.IsChecked = true;

    // RF-526 — os três rótulos, cada um no seu formato.
    Console.WriteLine();
    Console.WriteLine("RF-526 — rótulos dos controles deslizantes:");
    Console.WriteLine($"  temperatura      = {window.GetControl<TextBlock>("LlmTemperatureValue").Text}");
    Console.WriteLine($"  raciocínio       = {window.GetControl<TextBlock>("LlmThinkingValue").Text}");
    Console.WriteLine($"  limite de saída  = {window.GetControl<TextBlock>("LlmMaxOutputValue").Text}");

    // RF-525 — no preset padrão os três ficam desabilitados.
    Console.WriteLine();
    Console.WriteLine("RF-525 — controles no preset padrão:");
    Console.WriteLine($"  habilitados: {window.GetControl<Slider>("LlmTemperatureSlider").IsEnabled}");
    window.GetControl<RadioButton>("LlmCustomRadio").IsChecked = true;
    Console.WriteLine($"  no personalizado: {window.GetControl<Slider>("LlmTemperatureSlider").IsEnabled}");

    // RF-529 — nomes duplicados criados pela interface.
    Console.WriteLine();
    Console.WriteLine("RF-529 — presets criados com o mesmo nome:");
    foreach (var preset in presets.Presets) Console.WriteLine($"  {preset.DisplayName}");
}

/// <summary>V.4 — As janelas auxiliares, pelo mesmo caminho.</summary>
static void RenderAuxiliaryWindows(Localizer loc, string outputDir)
{
    Console.WriteLine();
    Console.WriteLine("V.4 — janelas auxiliares:");

    var dictionary = new Gort.App.Windows.DictionaryEditorWindow(
        loc, Path.Combine(Path.GetTempPath(), "gort-dic-inexistente.txt"), null,
        "Pressione qualquer tecla");
    Capture(dictionary, "janela-dicionario.png", outputDir);

    // Uma imagem sintética: faixas de cor com um texto escuro por cima, que é o que a
    // binarização precisa separar.
    var picker = new Gort.App.Windows.ColorPickerWindow(
        loc, new Gort.Core.Model.ColorGroup { R = 240, G = 240, B = 240, S2 = 100, V2 = 100 },
        new Gort.Core.Imaging.FilterSettings
        {
            Mode = Gort.Core.Imaging.FilterMode.Threshold,
            Threshold = 128,
        },
        SyntheticImage);
    Capture(picker, "janela-conta-gotas.png", outputDir, keepOpen: true);

    // RF-536 — "transformar" mostra a binarização, com o mesmo critério do
    // pré-processamento. É a razão de a janela existir: ver o que o OCR vai receber.
    Click(picker, "TransformButton");
    Capture(picker, "janela-conta-gotas-binarizada.png", outputDir, keepOpen: true);

    // RF-536 — alterar o limiar REPROCESSA automaticamente, sem passar pelo botão.
    picker.GetControl<Slider>("ThresholdSlider").Value = 200;
    Capture(picker, "janela-conta-gotas-limiar-200.png", outputDir);

    // RF-533 / RF-534 — gerenciamento de áreas e grupos de cor.
    var regions = new Gort.Core.Regions.RegionManager();
    var area = regions.AddArea(new Gort.Core.Model.Rect(200, 150, 420, 260));
    regions.AddArea(new Gort.Core.Model.Rect(700, 400, 300, 180));
    regions.AddExclusion(new Gort.Core.Model.Rect(260, 200, 120, 60));

    Capture(new Gort.App.Windows.AreaManagerWindow(loc, regions),
            "janela-areas.png", outputDir);

    var groups = new[]
    {
        new Gort.Core.Model.ColorGroup { R = 255, G = 255, B = 255, S2 = 20, V1 = 80, V2 = 100 },
        new Gort.Core.Model.ColorGroup { R = 250, G = 220, B = 90, S1 = 40, S2 = 100, V1 = 60, V2 = 100 },
        new Gort.Core.Model.ColorGroup { R = 40, G = 40, B = 40, S2 = 30, V2 = 25 },
    };
    Capture(new Gort.App.Windows.ColorGroupsWindow(loc, area, groups),
            "janela-grupos-de-cor.png", outputDir);

    // RF-054 / RF-055 / RF-063 — as molduras: normal e de exclusão.
    RenderFrame(loc, regions.Areas[0], 1, "areas.kind_normal", "moldura-area.png", outputDir);
    RenderFrame(loc, regions.Exclusions[0], 1, "areas.kind_exclusion",
                "moldura-exclusao.png", outputDir);

    // RF-538 — gerenciamento de chaves, com os três estados de RF-252 na lista.
    var keys = new Gort.Core.Translation.Keys.TranslationKeyStore();
    keys.Set("conta-pessoal", "s1", isFree: true);
    keys.Set("conta-de-teste", "s2", isFree: true);
    keys.Set("conta-paga", "s3", isFree: false);
    keys.Find("conta-de-teste")!.State = Gort.Core.Translation.Keys.KeyState.Limit;

    Capture(new Gort.App.Windows.KeyManagerWindow(loc, keys, ""),
            "janela-chaves.png", outputDir);

    // RF-543 — sobre.
    Capture(new Gort.App.Windows.AboutWindow(loc, new[] { ("myDic.txt", 128) }),
            "janela-sobre.png", outputDir);

    // O controle remoto tem a mesma moldura escura da janela de áreas; os dois passaram a
    // fixar o tema escuro, para não herdarem texto claro sobre botão claro num sistema
    // configurado em tema claro.
    Capture(new Gort.App.Windows.RemoteControlWindow(), "janela-controle-remoto.png", outputDir);

    // Controle: a janela de sobreposição já foi verificada TRANSPARENTE na tela. Se ela
    // sair preta aqui também, o preto é da captura fora da tela, não das janelas.
    var overlay = new Gort.App.Windows.OverlayWindow();
    overlay.Width = 300; overlay.Height = 120;
    Capture(overlay, "controle-sobreposicao.png", outputDir);
}

static void RenderFrame(Localizer loc, Gort.Core.Regions.CaptureFrame frame, int index,
                        string kindKey, string name, string outputDir)
{
    var window = new Gort.App.Windows.AreaFrameWindow(
        frame, index, () => new Gort.Core.Model.Rect(0, 0, 1920, 1080))
    {
        KindName = loc[kindKey],
    };
    Capture(window, name, outputDir);
}

static void Click(Window window, string name)
{
    var button = window.GetControl<Button>(name);
    button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
}

static Gort.Core.Model.ImageBuffer SyntheticImage()
{
    const int W = 320, H = 120;
    var pixels = new byte[W * H * 4];

    for (int y = 0; y < H; y++)
    {
        for (int x = 0; x < W; x++)
        {
            int i = (y * W + x) * 4;
            bool stripe = (x / 40) % 2 == 0;
            bool ink = y is > 40 and < 80 && x % 40 is > 8 and < 26;

            byte tone = ink ? (byte)30 : stripe ? (byte)220 : (byte)170;
            pixels[i + 0] = tone;
            pixels[i + 1] = tone;
            pixels[i + 2] = ink ? (byte)30 : (byte)255;
            pixels[i + 3] = 255;
        }
    }
    return new Gort.Core.Model.ImageBuffer(W, H, Gort.Core.Model.PixelFormat.Bgra32, pixels);
}

static void Capture(Window window, string name, string outputDir, bool keepOpen = false)
{
    if (!window.IsVisible) window.Show();
    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    ((Control)window.Content!).UpdateLayout();
    Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    using var frame = window.CaptureRenderedFrame();
    frame?.Save(Path.Combine(outputDir, name));
    Console.WriteLine($"  {window.Title}  →  {name}");
    if (!keepOpen) window.Close();
}

static string Slug(string text)
{
    var chars = text.ToLowerInvariant()
        .Select(c => char.IsLetterOrDigit(c) ? c : '-')
        .ToArray();
    return new string(chars).Trim('-');
}

static string LocateData()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (; dir is not null; dir = dir.Parent)
    {
        string candidate = Path.Combine(dir.FullName, "data");
        if (File.Exists(Path.Combine(candidate, "languages.toml"))) return candidate;
    }
    return "data";
}
