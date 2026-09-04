using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Gort.App.Windows;
using Gort.Core.Caching;
using Gort.Core.Regions;
using Gort.Core.Shortcuts;
using Gort.Platform.Input;

// Só o tipo de que este arquivo precisa de Gort.Core.Model: importar o espaço inteiro
// traria `Rect`, que colide com o do Avalonia.
using ShortcutAction = Gort.Core.Model.ShortcutAction;
using Gort.Engine;
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
    private readonly TranslationLoop _loop;
    private readonly DisplayMemory _displayMemory = new();
    private RemoteControlWindow? _remote;
    private bool _busy;

    public MainWindow() : this(AppSession.Create()) { }

    public MainWindow(AppSession session)
    {
        _session = session;
        InitializeComponent();

        _loop = BuildLoop();
        SetUpShortcuts();

        Subtitle.Text = $"{_session.Platform.PlatformName} · " +
                        $"{_session.Platform.Monitors.Monitors.Count} monitor(es)";

        ShowCapabilities();
        ShowNotices();
        FillChoices();
        ShowAreas();

        FillSpeeds();

        DefineAreaButton.Click += async (_, _) => await DefineAreaAsync();
        TranslateLoopButton.Click += (_, _) => ToggleLoop();
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
    /// RF-569 / RF-028 — Os avisos da inicialização são exibidos UMA VEZ.
    /// </summary>
    private void ShowNotices()
    {
        if (_session.Notices.Count == 0) return;
        CapabilityText.Text += "\n\n" + string.Join("\n", _session.Notices);
        _session.Notices.Clear();
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

    /// <summary>
    /// RF-558 — O consumo de memória fica à vista, sem abrir diálogo.
    ///
    /// A medida é o CONJUNTO DE TRABALHO, e não a memória privada: fora do Windows, a
    /// memória privada é devolvida como zero pelo runtime, e um indicador que marca zero
    /// para sempre é pior que indicador nenhum — ele daria a impressão de que não há
    /// consumo a acompanhar, que é justamente o contrário do que RF-558 quer.
    /// </summary>
    private void ShowMemory()
    {
        using var process = Process.GetCurrentProcess();
        long bytes = process.WorkingSet64;
        if (bytes <= 0) bytes = GC.GetTotalMemory(false);

        MemoryIndicator.Text = $"memória: {bytes / 1024 / 1024} MB";
    }

    /// <summary>RF-047 — Abre a camada de seleção sobre toda a área de trabalho virtual.</summary>
    private async Task DefineAreaAsync()
    {
        var monitors = _session.Platform.Monitors.Monitors;
        if (monitors.Count == 0) return;

        var desktop = MonitorGeometry.VirtualDesktop(monitors);

        Hide();   // a janela principal sairia na captura da própria área

        // RF-053 / RF-443 — enquanto a camada de seleção está aberta, os atalhos globais
        // ficam inertes: o usuário está usando o teclado e o mouse para marcar a área.
        _session.Dispatcher.Suspended = true;
        await Task.Delay(150);

        var overlay = new AreaSelectionOverlay(desktop,
                                               _session.Advanced.SelectionHighlight,
                                               _session.Advanced.SelectionBackground);
        var drawn = await overlay.SelectAsync();

        _session.Dispatcher.Suspended = false;
        _session.Dispatcher.Reset();

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

    /// <summary>
    /// RF-436 — Instala o interceptador global e liga o controle remoto.
    ///
    /// RF-569 — Quando os atalhos globais não estão disponíveis, o usuário opera pelo
    /// controle remoto, e o programa informa isso UMA VEZ.
    /// </summary>
    private void SetUpShortcuts()
    {
        var keyboard = _session.Platform.Keyboard;

        if (keyboard.Start())
        {
            keyboard.KeyChanged += OnGlobalKey;
        }
        else if (keyboard.UnavailableReason is not null)
        {
            _session.Notices.Add(keyboard.UnavailableReason);
        }

        // O controle remoto existe sempre: ele é a alternativa aos atalhos, e também o modo
        // normal de operar sem voltar à janela principal.
        _remote = new RemoteControlWindow
        {
            DefineArea = () => _ = DefineAreaAsync(),
            Snapshot = () => _ = TranslateOnceAsync(),
            Start = ToggleLoop,
            Stop = ToggleLoop,
            OpenSettings = () => { Show(); Activate(); },
        };
        _remote.SetAlwaysOnTop(_session.Advanced.RemoteAlwaysOnTop);
        PlaceRemote(_remote);
        _remote.Show();
    }

    /// <summary>
    /// RF-517 — O controle remoto tem de estar SEMPRE ACESSÍVEL. Sem uma posição explícita
    /// ele nasce no canto superior esquerdo, onde costuma ficar debaixo da janela que o
    /// usuário está traduzindo. A posição inicial é a base do monitor principal, longe do
    /// conteúdo e perto de onde a mão já está.
    /// </summary>
    private void PlaceRemote(RemoteControlWindow remote)
    {
        var monitors = _session.Platform.Monitors.Monitors;
        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
        if (primary is null) return;

        int width = (int)remote.Width;
        int height = (int)remote.Height;

        remote.Position = new PixelPoint(
            primary.Bounds.Left + (primary.Bounds.Width - width) / 2,
            primary.Bounds.Bottom - height - 90);
    }

    /// <summary>
    /// Uma tecla do sistema inteiro. O tratamento tem de devolver DEPRESSA: o sistema remove
    /// um interceptador que fique preso, e isso mataria todos os atalhos (RF-011).
    /// </summary>
    private void OnGlobalKey(KeyEvent e)
    {
        if (!e.IsDown)
        {
            _session.Dispatcher.KeyUp(e.Key);
            return;
        }

        var shortcut = _session.Dispatcher.KeyDown(e.Key);
        if (shortcut is null) return;

        // A ação vai para a thread de interface; o gancho volta imediatamente.
        Dispatcher.UIThread.Post(() => Run(shortcut.Action));
    }

    /// <summary>RF-444 / RF-447 — O que cada atalho dispara.</summary>
    private void Run(ShortcutAction action)
    {
        switch (action)
        {
            case ShortcutAction.ToggleRealtimeTranslation:
                // RF-450 — vindo do interceptador, a parada usa o prazo curto P-04.
                if (_loop.IsRunning) { _loop.StopFromKeyboardHook(); ShowLoopState(); }
                else ToggleLoop();
                break;

            case ShortcutAction.TranslateOnce:
                // RF-451 — com o laço rodando, pausa e executa um ciclo pontual, também com
                // prazo curto.
                if (_loop.IsRunning) _loop.StopFromKeyboardHook();
                _ = TranslateOnceAsync();
                break;

            case ShortcutAction.QuickArea:
            case ShortcutAction.SnapshotArea:
                _ = DefineAreaAsync();
                break;

            case ShortcutAction.ToggleTranslationWindow:
                if (_translationWindow is { IsVisible: true }) _translationWindow.Hide();
                else TranslationWindow().Show();
                break;
        }
    }

    /// <summary>P-05 a P-09 — As cinco velocidades, da mais rápida à mais lenta.</summary>
    private void FillSpeeds()
    {
        SpeedBox.ItemsSource = Enumerable.Range(1, 5)
            .Select(i => $"{i} — {Gort.Core.Calibration.P.CycleIntervalMs(i)} ms")
            .ToList();
        SpeedBox.SelectedIndex = Math.Clamp(_session.Profile.Speed, 1, 5) - 1;
        SpeedBox.SelectionChanged += (_, _) =>
        {
            if (SpeedBox.SelectedIndex >= 0) _session.Profile.Speed = SpeedBox.SelectedIndex + 1;
        };
    }

    /// <summary>
    /// RF-008 / Passo 1 do fluxo — O mesmo acionamento inicia e para: se já estiver
    /// traduzindo, ele para.
    /// </summary>
    private void ToggleLoop()
    {
        if (_loop.IsRunning)
        {
            // RF-010 — se a thread não parar no prazo, o sinalizador NÃO é revertido e o
            // usuário é informado, em vez de o programa fingir que parou.
            if (!_loop.Stop())
            {
                ResultText.Text = "o laço não parou no prazo; tentando de novo no próximo comando.";
                return;
            }
            ShowLoopState();
            return;
        }

        // Passo 2 — verificações de pré-condição.
        if (!_session.Regions.HasAnyIncrementalArea)
        {
            // RF-065 — sem área, a tradução não começa e o programa explica.
            ResultText.Text = "É preciso definir ao menos uma área de OCR antes de traduzir.";
            return;
        }

        var engine = _session.Engines.Resolve(_session.Profile.OcrEngine);
        if (engine is null)
        {
            ResultText.Text = "Nenhum motor de OCR disponível.";
            return;
        }

        // RF-122 — o motor de nuvem não pode ser usado em tradução em tempo real.
        var info = _session.Catalog.OcrEngine(engine.Key);
        if (info is { Realtime: false })
        {
            ResultText.Text = $"O motor '{engine.Key}' só funciona em modo pontual.";
            return;
        }

        // RF-351 — a sobreposição exige motor que devolva posição de palavra.
        if (_session.Profile.WindowMode == Gort.Core.Structuring.WindowMode.Overlay
            && !engine.ProvidesWordPositions)
        {
            ResultText.Text = $"O motor '{engine.Key}' não devolve posição de palavra; " +
                              "a sobreposição não pode ser usada com ele.";
            return;
        }

        // Passo 3 — a janela de tradução é preparada.
        var window = TranslationWindow();
        window.Show();
        window.Clear();

        // RF-071 — ao iniciar uma tradução que não é instantânea, a memória de "último
        // instantâneo" é apagada.
        _session.Regions.ForgetLastSnapshot();

        if (!_loop.Start(LoopMode.Realtime))
        {
            ResultText.Text = "não foi possível iniciar: o laço anterior não parou.";
            return;
        }
        ShowLoopState();
    }

    private TranslationLoop BuildLoop()
    {
        var host = new LoopHost
        {
            Areas = () => _session.Regions.Build(),
            Settings = () => _session.BuildCycleSettings(),
            RunCycle = (areas, settings) => _session.Cycle.RunAsync(areas, settings),
            HasTranslationWindow = () => _translationWindow is not null,
            CycleIntervalMs = () => _session.Profile.CycleIntervalMs,

            // Passo 18 — o desenho é DESPACHADO para a thread de interface. O laço nunca
            // desenha, e P2 proíbe que ele abra diálogo.
            Draw = result => Dispatcher.UIThread.Post(() =>
            {
                // Passo 17 — a memória de exibição é aplicada ao texto final.
                string text = _displayMemory.Apply(result.DisplayText);

                // RF-240 — com "ignorar tradução vazia", um resultado vazio não substitui
                // o que está na tela.
                if (string.IsNullOrWhiteSpace(text) && _session.Advanced.IgnoreEmptyTranslation)
                    return;

                _translationWindow?.Show(text, result.RecognizedText);
            }),

            Repaint = () => Dispatcher.UIThread.Post(() => _translationWindow?.InvalidateVisual()),

            ReportError = message => Dispatcher.UIThread.Post(() =>
                ResultText.Text = $"erro no laço: {message}"),

            FlushMemory = () => _session.Memory?.FlushAsync(),

            Stopped = () => Dispatcher.UIThread.Post(ShowLoopState),
        };

        return new TranslationLoop(host);
    }

    private void ShowLoopState()
    {
        bool running = _loop.IsRunning;
        TranslateLoopButton.Content = running ? "Parar tradução" : "Iniciar tradução";
        TranslateOnceButton.IsEnabled = !running && _session.Regions.HasAnyIncrementalArea;
        _translationWindow?.SetRunning(running);
        _remote?.SetRunning(running);

        // RF-320 — "sempre no topo apenas durante a tradução".
        if (_session.Advanced.AlwaysOnTopOnlyWhileTranslating)
            _translationWindow?.SetAlwaysOnTop(running);
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

            // RF-222 / RF-223 — memória de exibição, conforme as opções avançadas.
            _displayMemory.Enabled = _session.Advanced.DisplayMemoryEnabled;
            _displayMemory.Capacity = _session.Advanced.DisplayMemoryCount;
            _displayMemory.LifetimeSeconds = _session.Advanced.DisplayMemoryLifetimeSeconds;
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

        // RF-012 — a mudança passa pelo protocolo "pausar → aplicar → retomar". Se a
        // parada falhar por tempo, NADA é aplicado e o usuário é informado.
        var outcome = _loop.PauseAndResume(() =>
        {
            _session.ApplyConfiguration();
            _session.SaveProfile();
        });

        if (outcome == ApplyResult.Aborted)
        {
            ResultText.Text = "o laço não parou no prazo: NADA foi aplicado.";
            return;
        }

        FillChoices();
        ShowAreas();
        ShowLoopState();
        ResultText.Text = outcome == ApplyResult.AppliedAndResumed
            ? "configuração aplicada, perfil salvo, tradução retomada."
            : "configuração aplicada e perfil salvo.";
    }

    protected override void OnClosed(EventArgs e)
    {
        // RF-016 — ao encerrar de fato: parar o laço, liberar o interceptador global de
        // teclado e fechar as janelas auxiliares.
        _loop.Stop();
        _session.Platform.Keyboard.Stop();

        if (_remote is not null)
        {
            _remote.AllowClose = true;
            _remote.Close();
        }

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
