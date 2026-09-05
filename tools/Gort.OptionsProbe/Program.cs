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
