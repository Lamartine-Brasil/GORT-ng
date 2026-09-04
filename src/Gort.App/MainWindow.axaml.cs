using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Gort.App.Windows;
using Gort.Core.Regions;
using Gort.Core.Structuring;
using Gort.Platform.Capabilities;
using Gort.Platform.Monitors;

namespace Gort.App;

/// <summary>
/// V.1 — Janela principal.
///
/// Esta é a versão da Etapa 7: o suficiente para o produto ser utilizável de ponta a ponta.
/// As sete abas completas de V.1 são da Etapa 17.
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppSession _session;
    private DarkTranslationWindow? _translationWindow;
    private bool _busy;

    public MainWindow() : this(AppSession.Create()) { }

    public MainWindow(AppSession session)
    {
        _session = session;
        InitializeComponent();

        Subtitle.Text = $"{_session.Platform.PlatformName} · " +
                        $"{_session.Platform.Monitors.Monitors.Count} monitor(es)";

        ShowCapabilities();
        FillChoices();
        ShowAreas();

        DefineAreaButton.Click += async (_, _) => await DefineAreaAsync();
        ClearAreasButton.Click += (_, _) => { _session.Regions.ClearAll(); ShowAreas(); };
        TranslateOnceButton.Click += async (_, _) => await TranslateOnceAsync();
        ApplyButton.Click += (_, _) => Apply();
        ShowWindowButton.Click += (_, _) => TranslationWindow().Show();

        // RF-560 — o indicador de memória é amostrado em intervalo fixo e NUNCA dentro do
        // ciclo de tradução: lê-lo no ciclo custaria latência justamente onde ela importa.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => ShowMemory();
        timer.Start();
        ShowMemory();
    }

    /// <summary>
    /// RF-576 — As capacidades indisponíveis aparecem explicadas, e a tela de configuração
    /// do sistema é oferecida quando há uma.
    /// </summary>
    private void ShowCapabilities()
    {
        var report = _session.Platform.Capabilities;

        if (report.CanTranslate)
        {
            var missing = report.Unavailable.ToList();
            CapabilityText.Text = missing.Count == 0
                ? "Todas as capacidades necessárias estão disponíveis."
                : "Pronto para traduzir. Indisponíveis: " +
                  string.Join("; ", missing.Select(m => CapabilityInfo.Name(m.Capability)));
            return;
        }

        // RF-569 — sem capacidade essencial, o programa diz isso e não inicia a tradução.
        CapabilityText.Text = report.BlockingExplanation();
        TranslateOnceButton.IsEnabled = false;

        var blocking = report.Unavailable.FirstOrDefault(
            s => s.Kind == UnavailabilityKind.PermissionRequired && s.RemediationHint is not null);

        if (blocking is not null)
        {
            PermissionButton.IsVisible = true;
            PermissionButton.Content = blocking.RemediationHint;
            PermissionButton.Click += (_, _) =>
                _session.Platform.OpenPermissionSettings(blocking.Capability);
        }
    }

    /// <summary>
    /// RF-120 / RF-511 — As listas contêm apenas o que está disponível e o que cada serviço
    /// suporta. RF-313 — o idioma de destino padrão aparece em primeiro lugar.
    /// </summary>
    private void FillChoices()
    {
        EngineBox.ItemsSource = _session.Engines.Available.Select(e => e.Key).ToList();
        EngineBox.SelectedItem = _session.Engines.Resolve(_session.Profile.OcrEngine)?.Key;

        ServiceBox.ItemsSource = _session.Catalog.TranslationServices.Select(s => s.Key).ToList();
        ServiceBox.SelectedItem = _session.Profile.TranslationService;

        var service = _session.Catalog.Service(_session.Profile.TranslationService)
                      ?? _session.Catalog.TranslationServices[0];

        SourceBox.ItemsSource = _session.Catalog
            .LanguagesFor(service, targetList: false).Select(l => l.Key).ToList();
        SourceBox.SelectedItem = _session.Profile.OcrLanguage;

        TargetBox.ItemsSource = _session.Catalog
            .LanguagesFor(service, targetList: true).Select(l => l.Key).ToList();
        TargetBox.SelectedItem = _session.Profile.TargetLanguage;
    }

    private void ShowAreas()
    {
        var built = _session.Regions.Build();
        AreaText.Text = built.Captures.Count == 0
            // RF-065 — sem área, o programa explica que é preciso defini-la primeiro.
            ? "Nenhuma área definida. É preciso marcar ao menos uma para traduzir."
            : string.Join("\n", built.Captures.Select((r, i) => $"{i + 1}: {r}"));

        TranslateOnceButton.IsEnabled =
            _session.Regions.HasAnyIncrementalArea && _session.Platform.Capabilities.CanTranslate;
    }

    /// <summary>RF-558 — O consumo de memória fica à vista, sem abrir diálogo.</summary>
    private void ShowMemory()
    {
        long bytes = Process.GetCurrentProcess().PrivateMemorySize64;
        MemoryIndicator.Text = $"memória: {bytes / 1024 / 1024} MB";
    }

    /// <summary>RF-047 — Abre a camada de seleção sobre toda a área de trabalho virtual.</summary>
    private async Task DefineAreaAsync()
    {
        var monitors = _session.Platform.Monitors.Monitors;
        if (monitors.Count == 0) return;

        var desktop = MonitorGeometry.VirtualDesktop(monitors);

        Hide();   // a janela principal sairia na captura da própria área
        await Task.Delay(150);

        var overlay = new AreaSelectionOverlay(desktop,
                                               _session.Advanced.SelectionHighlight,
                                               _session.Advanced.SelectionBackground);
        var drawn = await overlay.SelectAsync();

        Show();
        Activate();

        if (drawn is null) return;

        // O que o usuário desenha é o RETÂNGULO DE CAPTURA; a moldura é maior, porque
        // desconta borda e barra de título (RF-073).
        double scale = MonitorGeometry.ScaleOf(monitors, drawn.Value);
        var metrics = FrameGeometry.MetricsFor(scale);
        _session.Regions.AddArea(FrameGeometry.ToFrameRect(drawn.Value, metrics));

        ShowAreas();
    }

    /// <summary>
    /// RF-202 — "Traduzir uma vez": executa um único ciclo nas áreas atuais, pelo mesmo
    /// caminho da tradução contínua.
    /// </summary>
    private async Task TranslateOnceAsync()
    {
        if (_busy) return;
        _busy = true;
        TranslateOnceButton.IsEnabled = false;
        ResultText.Text = "traduzindo…";

        try
        {
            var window = TranslationWindow();
            window.Show();
            window.SetRunning(true);

            var areas = _session.Regions.Build();
            var settings = _session.BuildCycleSettings();

            var watch = Stopwatch.StartNew();
            var result = await _session.Cycle.RunAsync(areas, settings);
            watch.Stop();

            // RF-240 — com "ignorar tradução vazia" ativa, um resultado vazio não substitui
            // o que já está na tela.
            bool empty = string.IsNullOrWhiteSpace(result.DisplayText);
            if (!empty || !_session.Advanced.IgnoreEmptyTranslation)
                window.Show(result.DisplayText, result.RecognizedText);

            window.SetRunning(false);

            ResultText.Text = result.Error is not null
                ? $"erro: {result.Error}"
                : $"{watch.ElapsedMilliseconds} ms · " +
                  $"{result.Regions.Sum(r => r.Blocks.Count)} bloco(s) · " +
                  $"{result.NetworkCount} ida(s) à rede";

            await (_session.Memory?.FlushAsync() ?? Task.CompletedTask);
        }
        catch (Exception ex)
        {
            // RF-561 — nenhuma falha encerra o programa; ela vira mensagem visível.
            ResultText.Text = $"erro: {ex.Message}";
        }
        finally
        {
            _busy = false;
            TranslateOnceButton.IsEnabled = true;
        }
    }

    private DarkTranslationWindow TranslationWindow()
    {
        if (_translationWindow is null)
        {
            _translationWindow = new DarkTranslationWindow
            {
                ShowRecognizedText = _session.Profile.ShowRecognizedText,
            };
            _translationWindow.SetAlwaysOnTop(_session.Options.TranslationWindowAlwaysOnTop);
            _translationWindow.SetFont(_session.Advanced.DarkModeFont, _session.Profile.FontSize);
        }
        return _translationWindow;
    }

    /// <summary>
    /// RF-504 — Aplicar: leva os valores da interface à configuração e salva o perfil
    /// principal.
    /// </summary>
    private void Apply()
    {
        if (EngineBox.SelectedItem is string engine) _session.Profile.OcrEngine = engine;
        if (ServiceBox.SelectedItem is string service) _session.Profile.TranslationService = service;
        if (SourceBox.SelectedItem is string source) _session.Profile.OcrLanguage = source;
        if (TargetBox.SelectedItem is string target) _session.Profile.TargetLanguage = target;

        // RF-148 — os ajustes automáticos vêm das PROPRIEDADES do idioma, não do seu nome.
        var language = _session.Catalog.Language(_session.Profile.OcrLanguage);
        if (language is not null) _session.Profile.ApplyLanguageProperties(language);

        _session.ApplyConfiguration();
        _session.SaveProfile();

        FillChoices();
        ShowAreas();
        ResultText.Text = "configuração aplicada e perfil salvo.";
    }

    protected override void OnClosed(EventArgs e)
    {
        // A janela de tradução se recusa a fechar por conta de RF-326; ao encerrar o
        // programa de verdade, essa recusa tem de ser suspensa.
        if (_translationWindow is not null)
        {
            _translationWindow.HideInsteadOfClose = false;
            _translationWindow.Close();
        }
        _session.Dispose();
        base.OnClosed(e);
    }
}
