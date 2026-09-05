using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Gort.App.Windows;
using Gort.Core.Caching;
using Gort.Core.Configuration;
using Gort.Core.Diagnostics;
using Gort.Core.Imaging;
using Gort.Core.Localization;
using Gort.Core.Regions;
using Gort.Core.Shortcuts;
using Gort.Engine;
using Gort.Platform.Capabilities;
using Gort.Platform.Input;
using Gort.Platform.Monitors;

using ShortcutAction = Gort.Core.Model.ShortcutAction;
using WindowMode = Gort.Core.Structuring.WindowMode;

namespace Gort.App;

/// <summary>
/// V.1 — Janela principal, com as sete abas em ordem fixa.
///
/// RF-481 — Todo texto exibido vem da tabela de localização, não de literais no código.
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppSession _session;
    private readonly Localizer _loc;
    private readonly TranslationLoop _loop;
    private readonly DisplayMemory _displayMemory = new();

    private ITranslationWindow? _translationWindow;
    private RemoteControlWindow? _remote;
    private WindowMode _windowMode;
    private int _quickStep;
    private bool _busy;

    public MainWindow() : this(AppSession.Create()) { }

    public MainWindow(AppSession session)
    {
        _session = session;
        _loc = session.Localizer;

        InitializeComponent();

        _loop = BuildLoop();

        Localize();
        FillChoices();
        LoadValues();
        WireEvents();
        SetUpShortcuts();

        ShowCapabilities();
        ShowNotices();
        UpdatePreview();
        ShowQuickStep();

        // RF-501 — ao abrir, seleciona a aba de configuração rápida, a menos que o usuário
        // tenha marcado "abrir na aba básica".
        Tabs.SelectedIndex = _session.Options.StartOnBasicTab ? 0 : 5;

        // RF-560 — o indicador de memória é amostrado em intervalo fixo e NUNCA dentro do
        // ciclo de tradução. O mesmo tique confere a disposição dos monitores (RF-086): são
        // duas leituras baratas, e um segundo temporizador só para isso custaria o dobro
        // por nada.
        //
        // RF-552 — em ociosidade não deve haver temporizador ativo além do da área que
        // segue o mouse. Este só corre enquanto a JANELA ESTÁ VISÍVEL: escondida, não há
        // indicador para atualizar, e o usuário que escondeu a janela é justamente o que
        // está jogando.
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _statusTimer.Tick += (_, _) => { ShowMemory(); CheckMonitorLayout(); };

        IsVisibleChanged();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty) IsVisibleChanged();
        };

        ShowMemory();
        RememberMonitorLayout();

        StartMouseFollow();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RF-481 — Localização de todos os rótulos
    // ─────────────────────────────────────────────────────────────────────────

    private void Localize()
    {
        Title = _loc["app.title"];
        TitleText.Text = _loc["app.title"];
        DonateButton.Content = _loc["app.donate"];
        ApplyButton.Content = _loc["app.apply"];
        ShowWindowButton.Content = _loc["translate.start"];

        TabBasic.Header = _loc["tab.basic"];
        TabText.Header = _loc["tab.text"];
        TabAdditional.Header = _loc["tab.additional"];
        TabTranslation.Header = _loc["tab.translation"];
        TabOther.Header = _loc["tab.other"];
        TabQuick.Header = _loc["tab.quick"];
        TabDebug.Header = _loc["tab.debug"];

        DebugUnlockSpeed.Content = _loc["debug.unlock_speed"];
        DebugShowCache.Content = _loc["debug.show_cache"];
        DebugOneLine.Content = _loc["debug.one_line"];
        DebugWordAreas.Content = _loc["debug.word_areas"];
        DebugSaveSnapshot.Content = _loc["debug.save_analysis"];
        DebugWriteResult.Content = _loc["debug.write_result"];
        DebugNativeHeader.Text = _loc["debug.native_debug"];
        DebugNativeDebug.Content = _loc["debug.native_debug"];
        DebugNativeReplacements.Content = _loc["debug.native_replacements"];
        DebugNativeSaveCapture.Content = _loc["debug.native_save_capture"];
        DebugNativeSaveResult.Content = _loc["debug.native_save_result"];
        DebugClearMemory.Content = _loc["debug.clear_memory"];
        DebugOpenFolder.Content = _loc["debug.open_folder"];

        OcrGroupLabel.Text = _loc["basic.ocr"];
        EngineLabel.Text = _loc["basic.ocr"];
        SourceLabel.Text = _loc["translation.source"];
        ShowOcrCheck.Content = _loc["basic.show_ocr"];
        WriteFileCheck.Content = _loc["basic.write_file"];
        CopyClipboardCheck.Content = _loc["basic.copy_clipboard"];

        ServiceGroupLabel.Text = _loc["basic.service"];
        ServiceLabel.Text = _loc["basic.service"];
        TargetLabel.Text = _loc["translation.target"];

        DictionaryGroupLabel.Text = _loc["basic.dictionary"];
        UseDictionaryCheck.Content = _loc["basic.dictionary"];
        DictionaryWordCheck.Content = _loc["basic.dictionary_word"];

        ImageGroupLabel.Text = _loc["basic.image_correction"];
        FilterRgbRadio.Content = _loc["basic.filter_rgb"];
        FilterHsvRadio.Content = _loc["basic.filter_hsv"];
        FilterThresholdRadio.Content = _loc["basic.filter_threshold"];
        ErosionCheck.Content = _loc["basic.erosion"];
        PreviewButton.Content = _loc["basic.preview"];

        FontLabel.Text = _loc["text.font"];
        FontSizeLabel.Text = _loc["text.size"];
        TextColorButton.Content = _loc["text.color"];
        Stroke1Button.Content = _loc["text.stroke1"];
        Stroke2Button.Content = _loc["text.stroke2"];
        BackgroundButton.Content = _loc["text.background"];
        RestoreColorsButton.Content = _loc["text.restore_colors"];
        CenterCheck.Content = _loc["text.center"];
        RemoveSpacesCheck.Content = _loc["text.remove_spaces"];
        UseBackgroundCheck.Content = _loc["text.use_background"];
        NumberAreasCheck.Content = _loc["text.number_areas"];

        CaptureGroupLabel.Text = _loc["additional.capture"];
        ActiveWindowCheck.Content = _loc["additional.active_window"];
        ScaleLabel.Text = _loc["additional.scale"];
        AttachedWindowButton.Content = _loc["additional.attached_window"];
        SpeedGroupLabel.Text = _loc["additional.speed"];
        WindowGroupLabel.Text = _loc["additional.window"];
        AlwaysOnTopCheck.Content = _loc["additional.always_on_top"];
        ProfileGroupLabel.Text = _loc["additional.profile"];
        LoadProfileButton.Content = _loc["additional.load"];
        SaveProfileButton.Content = _loc["additional.save"];
        RestoreDefaultsButton.Content = _loc["additional.restore"];
        CheckUpdatesCheck.Content = _loc["additional.check_updates"];
        StartBasicCheck.Content = _loc["additional.start_basic"];

        ServiceLanguagesLabel.Text = _loc["tab.translation"];
        SpeakCheck.Content = _loc["translation.speak"];
        SpeakWaitCheck.Content = _loc["translation.speak_wait"];

        ShortcutsLabel.Text = _loc["other.shortcuts"];
        AdvancedButton.Content = _loc["other.advanced"];
        DictionaryEditorButton.Content = _loc["dict.title"];
        KeysButton.Content = _loc["keys.title"];
        AboutButton.Content = _loc["about.title"];
        HelpLabel.Text = _loc["other.help"];
        ManualButton.Content = _loc["other.manual"];
        KnownErrorsButton.Content = _loc["other.known_errors"];

        QuickDarkRadio.Content = _loc["quick.dark_text"];
        QuickLightRadio.Content = _loc["quick.light_text"];
        QuickUnknownRadio.Content = _loc["quick.unknown"];

        Subtitle.Text = $"{_session.Platform.PlatformName} · " +
                        $"{_session.Platform.Monitors.Monitors.Count} monitor(es)";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Preenchimento e leitura dos controles
    // ─────────────────────────────────────────────────────────────────────────

    private void FillChoices()
    {
        EngineBox.ItemsSource = _session.Engines.Available.Select(e => e.Key).ToList();
        ServiceBox.ItemsSource = _session.Catalog.TranslationServices.Select(s => s.Key).ToList();

        var service = _session.Catalog.Service(_session.Profile.TranslationService)
                      ?? _session.Catalog.TranslationServices[0];

        SourceBox.ItemsSource = _session.Catalog
            .LanguagesFor(service, targetList: false).Select(l => l.Key).ToList();
        TargetBox.ItemsSource = _session.Catalog
            .LanguagesFor(service, targetList: true).Select(l => l.Key).ToList();

        // RF-317 — os três modos de janela.
        WindowModeBox.ItemsSource = new[] { "escuro", "camada", "sobreposição" };

        // P-05 a P-09 — as cinco velocidades, da mais rápida à mais lenta.
        SpeedBox.ItemsSource = Enumerable.Range(1, 5)
            .Select(i => $"{i} — {Gort.Core.Calibration.P.CycleIntervalMs(i)} ms").ToList();

        // RF-387 — a lista de fontes começa pelo vazio, que significa "resolver em tempo de
        // execução"; fixar um nome é sempre uma escolha do usuário.
        var fonts = new List<string> { "" };
        fonts.AddRange(FontManager.Current.SystemFonts.Select(f => f.Name).OrderBy(n => n));
        FontBox.ItemsSource = fonts;

        BuildShortcutList();
    }

    private void LoadValues()
    {
        var p = _session.Profile;

        EngineBox.SelectedItem = _session.Engines.Resolve(p.OcrEngine)?.Key;
        ServiceBox.SelectedItem = p.TranslationService;
        SourceBox.SelectedItem = p.OcrLanguage;
        TargetBox.SelectedItem = p.TargetLanguage;

        ShowOcrCheck.IsChecked = p.ShowRecognizedText;
        WriteFileCheck.IsChecked = p.WriteResultToFile;
        CopyClipboardCheck.IsChecked = p.CopyToClipboard;

        UseDictionaryCheck.IsChecked = p.UseDictionary;
        DictionaryWordCheck.IsChecked = p.DictionaryWholeWord;

        // RF-104 — os três modos de filtro são mutuamente exclusivos por construção.
        FilterNoneRadio.IsChecked = p.FilterMode == FilterMode.None;
        FilterRgbRadio.IsChecked = p.FilterMode == FilterMode.Rgb;
        FilterHsvRadio.IsChecked = p.FilterMode == FilterMode.Hsv;
        FilterThresholdRadio.IsChecked = p.FilterMode == FilterMode.Threshold;
        ThresholdBox.Value = p.Threshold;
        ErosionCheck.IsChecked = p.Erosion;

        FontBox.SelectedItem = p.FontFamily;
        FontSizeBox.Value = (decimal)p.FontSize;
        CenterCheck.IsChecked = p.TextOrder == TextOrder.Center;
        RemoveSpacesCheck.IsChecked = p.RemoveSpaces;
        UseBackgroundCheck.IsChecked = p.TextBackground;
        NumberAreasCheck.IsChecked = p.NumberAreas;

        ActiveWindowCheck.IsChecked = p.CaptureActiveWindow;
        ScaleBox.Value = (decimal)p.Scale;
        SpeedBox.SelectedIndex = Math.Clamp(p.Speed, 1, 5) - 1;
        WindowModeBox.SelectedItem = p.WindowMode switch
        {
            WindowMode.Layer => "camada",
            WindowMode.Overlay => "sobreposição",
            _ => "escuro",
        };
        AlwaysOnTopCheck.IsChecked = _session.Options.TranslationWindowAlwaysOnTop;
        CheckUpdatesCheck.IsChecked = _session.Options.CheckForUpdates;
        StartBasicCheck.IsChecked = _session.Options.StartOnBasicTab;

        SpeakCheck.IsChecked = p.SpeakResult;
        SpeakWaitCheck.IsChecked = p.SpeakWaitForPrevious;

        // C15 — sem sintetizador, a opção fica DESABILITADA com a explicação (RF-573).
        if (!_session.Platform.Speech.IsAvailable)
        {
            SpeakCheck.IsEnabled = false;
            SpeakWaitCheck.IsEnabled = false;
            ToolTip.SetTip(SpeakCheck, _session.Platform.Speech.UnavailableReason);
        }

        // C2/C3 — sem captura de janela anexada, o botão fica desabilitado com explicação.
        if (!_session.Platform.Capabilities.Has(Capability.WindowCapture))
        {
            AttachedWindowButton.IsEnabled = false;
            ToolTip.SetTip(AttachedWindowButton,
                _session.Platform.Capabilities[Capability.WindowCapture].Explanation);
        }

        UpdateColorButtons();
    }

    private void WireEvents()
    {
        ApplyButton.Click += (_, _) => Apply();
        ShowWindowButton.Click += (_, _) => { TranslationWindow(); ShowTranslationWindow(); };
        PreviewButton.Click += (_, _) => OpenColorPicker();
        RestoreColorsButton.Click += (_, _) => RestoreColors();
        QuickNextButton.Click += (_, _) => _ = AdvanceQuickAsync();

        RestoreDefaultsButton.Click += (_, _) => RestoreDefaults();
        SaveProfileButton.Click += (_, _) => { CaptureLayerPlacement(); _session.SaveProfile(); Say("msg.applied"); };

        DonateButton.Click += (_, _) => OpenLink("donation");
        ManualButton.Click += (_, _) => OpenLink("manual");
        KnownErrorsButton.Click += (_, _) => OpenLink("known_errors");

        // RF-508 — a pré-visualização reflete IMEDIATAMENTE qualquer mudança nos controles
        // desta aba, sem exigir "aplicar".
        FontBox.SelectionChanged += (_, _) => UpdatePreview();
        FontSizeBox.ValueChanged += (_, _) => UpdatePreview();
        CenterCheck.IsCheckedChanged += (_, _) => UpdatePreview();
        RemoveSpacesCheck.IsCheckedChanged += (_, _) => UpdatePreview();
        UseBackgroundCheck.IsCheckedChanged += (_, _) => UpdatePreview();
        NumberAreasCheck.IsCheckedChanged += (_, _) => UpdatePreview();

        // RF-490 — o modo de depuração é revelado por um controle escondido: um clique
        // longo no título.
        TitleText.DoubleTapped += (_, _) =>
        {
            TabDebug.IsVisible = !TabDebug.IsVisible;

            // Critério de aceite do cap. 27: desativar o modo restaura o comportamento
            // normal SEM REINICIAR. Como o ciclo relê a configuração a cada volta, apagar
            // o sinalizador basta — a próxima volta já não recebe o objeto de diagnóstico.
            _session.Debug.Enabled = TabDebug.IsVisible;
            ApplyDebugOptions();
        };

        WireDebugOptions();

        AdvancedButton.Click += (_, _) => OpenAdvancedOptions();
        DictionaryEditorButton.Click += (_, _) => OpenDictionaryEditor();

        // RF-538 — o rodízio de chaves do serviço ativo.
        KeysButton.Click += (_, _) =>
            new KeyManagerWindow(_loc, _session.Keys, _session.KeysFile).Show(this);

        // RF-543 — sobre, com as versões dos dicionários carregados.
        AboutButton.Click += (_, _) => OpenAbout();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // V.3 — Janela de opções avançadas
    // ─────────────────────────────────────────────────────────────────────────

    private AdvancedOptionsWindow? _advancedWindow;

    /// <summary>
    /// V.3 — Uma instância só. Reabrir com a janela já aberta a traz para a frente em vez
    /// de criar uma segunda, que teria a sua própria cópia das opções e desfaria a da
    /// primeira ao aplicar.
    /// </summary>
    private void OpenAdvancedOptions()
    {
        if (_advancedWindow is not null) { _advancedWindow.Activate(); return; }

        var window = new AdvancedOptionsWindow(
            _loc, _session.Advanced, _session.Profile.CopyFormat,
            _session.Shortcuts, _session.Dispatcher, _session.ApiPresets,
            _session.Paths.CollectionDirectory,
            () => FontManager.Current.SystemFonts.Select(f => f.Name).OrderBy(n => n),
            _session.Catalog.Language(_session.Profile.TargetLanguage),
            _session.Catalog.TranslationServices.Where(s => s.ShortcutSwitchable))
        {
            Applied = ApplyAdvancedOptions,
        };

        _advancedWindow = window;
        window.Closed += (_, _) => _advancedWindow = null;
        window.Show(this);
    }

    /// <summary>
    /// RF-530 / RF-531 — Aplicar grava tudo e reaplica ao programa SEM REINICIAR.
    ///
    /// RF-012 — a aplicação passa pelo protocolo de pausa: o laço para, a mudança acontece
    /// e ele é retomado. Se a thread não parar no prazo, NADA é aplicado e o usuário é
    /// avisado — é a única forma de não ter um ciclo lendo configuração pela metade.
    /// </summary>
    private void ApplyAdvancedOptions(
        Gort.Core.Configuration.AdvancedOptions options,
        Gort.Core.Configuration.ClipboardCopyFormat copyFormat)
    {
        var result = _loop.PauseAndResume(() =>
        {
            _session.Advanced.CopyFrom(options);
            _session.Profile.CopyFormat = copyFormat;

            _session.SaveAdvanced();
            _session.SaveShortcuts();
            _session.ApiPresets.Save(_session.Paths.ApiPresetsFile);

            // RF-531 — tudo o que a aplicação precisa reconfigurar, sem reiniciar.
            _session.ApplyConfiguration();
        });

        if (result == ApplyResult.Aborted) { Say("msg.loop_stop_failed"); return; }

        // O que depende de janelas vivas é reaplicado aqui, fora da pausa.
        _remote?.SetAlwaysOnTop(_session.Advanced.RemoteAlwaysOnTop);
        _displayMemory.Enabled = _session.Advanced.DisplayMemoryEnabled;
        _displayMemory.Capacity = _session.Advanced.DisplayMemoryCount;
        _displayMemory.LifetimeSeconds = _session.Advanced.DisplayMemoryLifetimeSeconds;

        LoadValues();
        UpdatePreview();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cap. 27 — Depuração e diagnóstico
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Cronômetro da parte de desenho do retrato (RF-494).</summary>
    private System.Diagnostics.Stopwatch? _drawWatch;
    private double _sizeAndPositionMs;

    private void WireDebugOptions()
    {
        foreach (var box in new[]
                 {
                     DebugUnlockSpeed, DebugShowCache, DebugOneLine, DebugWordAreas,
                     DebugSaveSnapshot, DebugWriteResult, DebugNativeDebug,
                     DebugNativeReplacements, DebugNativeSaveCapture, DebugNativeSaveResult,
                 })
        {
            box.IsCheckedChanged += (_, _) => ApplyDebugOptions();
        }

        DebugClearMemory.Click += (_, _) => ClearResultMemory();
        // RF-492 — a pasta dos retratos, aberta no navegador de arquivos do sistema.
        DebugOpenFolder.Click += (_, _) => OpenExternal(_session.Paths.DiagnosticsDirectory);

        // RF-552 — o temporizador dos contadores só corre com a aba de depuração à vista.
        // Deixá-lo correndo e não fazer nada é um temporizador ativo do mesmo jeito.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => ShowCounters();

        TabDebug.PropertyChanged += (_, e) =>
        {
            if (e.Property != IsVisibleProperty) return;
            if (TabDebug.IsVisible) { timer.Start(); ShowCounters(); }
            else timer.Stop();
        };
    }

    /// <summary>
    /// RF-491 / RF-500 — Os sinalizadores vão para o objeto que o ciclo lê. Não há cópia
    /// espalhada pela interface: a caixa é a fonte, o objeto é o destino.
    /// </summary>
    private void ApplyDebugOptions()
    {
        var d = _session.Debug;
        d.UnlockSpeed = DebugUnlockSpeed.IsChecked == true;
        d.ShowCacheResults = DebugShowCache.IsChecked == true;
        d.OneLinePerTranslation = DebugOneLine.IsChecked == true;
        d.ShowWordAreas = DebugWordAreas.IsChecked == true;
        d.SaveAnalysis = DebugSaveSnapshot.IsChecked == true;
        d.WriteResultFile = DebugWriteResult.IsChecked == true;
        d.NativeDebug = DebugNativeDebug.IsChecked == true;
        d.NativeShowReplacements = DebugNativeReplacements.IsChecked == true;
        d.NativeSaveCapture = DebugNativeSaveCapture.IsChecked == true;
        d.NativeSaveResult = DebugNativeSaveResult.IsChecked == true;

        // RF-496 — a gravação só acontece com o modo de depuração ligado.
        _session.ResultFile.Enabled = d.Enabled && d.WriteResultFile;

        // RF-491 — "mostrar áreas de palavra" vale para a sobreposição já viva.
        if (_translationWindow is OverlayWindow overlay)
            overlay.Canvas.ShowWordAreas = d.Enabled && d.ShowWordAreas;
    }

    private void ShowCounters()
    {
        DebugCounters.Text = _loc.Format("debug.counters",
            _session.Counters.OcrAttempts, _session.Counters.Translations,
            _session.Counters.NetworkCalls, _session.Counters.Errors);

        // RF-498 — o registro é acessível; as mais recentes ficam embaixo, como num log.
        DebugLog.Text = string.Join('\n', _session.Counters.Messages);
    }

    /// <summary>
    /// RF-499 — Limpa toda a memória de resultados anteriores.
    ///
    /// RF-212 — Enquanto uma gravação está em curso a memória está sendo serializada;
    /// apagá-la no meio corromperia o arquivo. O comando recusa em vez de esperar: o
    /// usuário repete o clique, e ninguém fica com a interface travada.
    /// </summary>
    private void ClearResultMemory()
    {
        var memory = _session.Memory;
        if (memory is null) return;

        if (memory.IsWriting) { Say("msg.memory_busy"); return; }

        memory.Clear();
        _session.ResultFile.Reset();
        Say("msg.memory_cleared");
    }

    /// <summary>
    /// RF-492 — Monta e registra o retrato do ciclo, na thread do laço, antes do desenho.
    ///
    /// RF-493 — No modo sobreposição o gravador segura o retrato até o desenho terminar; em
    /// qualquer outro modo ele grava na hora, porque não há parte de desenho a esperar.
    /// </summary>
    private void RecordAnalysis(CycleResult result)
    {
        if (!_session.Debug.Enabled || !_session.Debug.SaveAnalysis) return;

        try
        {
            bool overlay = _session.Profile.WindowMode == WindowMode.Overlay;

            var snapshot = DiagnosticRecorder.Build(
                result.Regions, result.RecognizedText, result.DisplayText,
                _session.Profile.WindowMode.ToString(),
                _session.Profile.OcrEngine,
                _session.Profile.TranslationService);

            _drawWatch = overlay ? System.Diagnostics.Stopwatch.StartNew() : null;

            // RF-495 — se havia um retrato pendente, ele sai daqui GRAVADO sem a parte de
            // desenho. É o gravador que cuida disso; a interface não decide descartar nada.
            _session.Diagnostics.Record(snapshot, waitsForDrawing: overlay);
        }
        catch (Exception ex)
        {
            // P8 — o diagnóstico nunca derruba o laço.
            _session.Counters.RecordError(ex.Message);
        }
    }

    /// <summary>
    /// RF-493 / RF-494 — O desenho da sobreposição terminou: completa o retrato pendente
    /// com o que só se sabe depois de desenhar, e grava.
    /// </summary>
    private void CompleteAnalysisDrawing(OverlayWindow window)
    {
        if (_session.Diagnostics is null || !_session.Diagnostics.HasPending) return;

        try
        {
            var surface = window.Canvas;
            double total = _drawWatch?.Elapsed.TotalMilliseconds ?? 0;
            double layoutAndDraw = surface.LastLayoutMs + surface.LastDrawMs;

            var drawing = new SnapshotDrawing
            {
                WindowRect = SnapshotRect.From(new Gort.Core.Model.Rect(
                    window.Position.X, window.Position.Y,
                    (int)window.Bounds.Width, (int)window.Bounds.Height)),
                TotalMs = total,
                SizeAndPositionMs = _sizeAndPositionMs,
                LayoutAndDrawMs = layoutAndDraw,

                // O que sobra do total depois do dimensionamento e do layout é o tempo em
                // que o quadro esperou pelo compositor: é essa a parcela de apresentação
                // que RF-494 pede, e ela é medida, não estimada.
                PresentMs = Math.Max(0, total - _sizeAndPositionMs - layoutAndDraw),

                CacheHits = surface.LastCacheHits,
                CacheMisses = surface.LastCacheMisses,
                Options = RenderingOptionsSnapshot(surface),
            };

            foreach (var block in surface.Blocks)
                drawing.Blocks.Add(DescribeDrawnBlock(surface, block));

            _session.Diagnostics.CompleteDrawing(drawing);
        }
        catch (Exception ex)
        {
            _session.Counters.RecordError(ex.Message);
        }
        finally
        {
            _drawWatch = null;
        }
    }

    /// <summary>Abre um caminho no navegador de arquivos do sistema.</summary>
    private void OpenExternal(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Say(_loc.Format("msg.error", ex.Message));
        }
    }

    /// <summary>RF-493 — As opções de renderização em vigor no momento do desenho.</summary>
    private static Dictionary<string, object> RenderingOptionsSnapshot(OverlaySurface s) => new()
    {
        ["fonte"] = s.FontFamilyName,
        ["estilo"] = s.FontStyle.ToString(),
        ["contorno"] = s.FontStroke,
        ["fundo"] = s.UseBackground,
        ["transparencia_do_fundo"] = s.UseBackgroundTransparency,
        ["tamanho_automatico"] = s.AutoFontSize,
        ["tamanho_minimo"] = s.MinFontSize,
        ["tamanho_maximo"] = s.MaxFontSize,
        ["tamanho_fixo"] = s.FixedFontSize,
        ["preservar_orientacao"] = s.PreserveOrientation,
        ["ampliacao"] = s.Scale,
        ["dpi_vertical"] = s.VerticalDpi,
        ["areas_de_palavra"] = s.ShowWordAreas,
    };

    /// <summary>RF-493 — Tudo o que se sabe sobre um bloco depois de desenhado.</summary>
    private static SnapshotDrawnBlock DescribeDrawnBlock(OverlaySurface s, OverlayBlock b)
    {
        var textColor = b.AutoColor?.Font ?? s.TextColor;
        var (stroke1, stroke2) = b.AutoColor is not null
            ? Gort.Core.Model.ColorMath.DeriveStrokeColors(textColor)
            : (s.Stroke1Color, s.Stroke2Color);

        return new SnapshotDrawnBlock
        {
            Text = b.Text,
            IsTitle = b.IsTitle,
            Orientation = b.Orientation.ToString(),

            SourceRect = SnapshotRect.From(b.SourceRect),
            ViewRect = SnapshotRect.From(b.ViewRect),
            ContentRect = SnapshotRect.From(b.ContentRect),
            DrawnRect = SnapshotRect.From(b.DrawnRect),

            FontFamily = s.FontFamilyName,
            FontStyle = s.FontStyle.ToString(),
            FontSize = b.FinalFontSize,
            PreferredSize = b.PreferredFontSize,
            MinimumSize = s.MinFontSize,

            // RF-360 passo 1 — o tamanho estimado do texto ORIGINAL, em pixels de imagem.
            EstimatedOriginalSize = b.OwnMedianSize,

            FontColor = textColor.ToString(),
            BackgroundColor = (b.AutoColor?.Background ?? s.BackgroundColor).ToString(),
            Stroke1Color = stroke1.ToString(),
            Stroke2Color = stroke2.ToString(),

            UsedAutoColor = b.AutoColor is not null,
            ContrastCorrected = b.AutoColor?.ContrastCorrected ?? false,

            Lines = b.Lines.ToList(),
            LineAdvance = b.LineAdvance,
            Clipped = b.Clipped,
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RF-508 a RF-510 — Pré-visualização ao vivo
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RF-509 — O texto de exemplo tem caracteres latinos, japoneses e numerais, e um trecho
    /// que demonstra o formato de múltiplas áreas: com prefixo numérico quando a numeração
    /// está ativa e com "-" quando não está.
    /// </summary>
    private void UpdatePreview()
    {
        bool numbered = NumberAreasCheck.IsChecked == true;
        string prefix = numbered ? "1 : " : "- ";

        string sample = $"{prefix}O sol nascia devagar. 12345\n{prefix}こんにちは、世界。";

        // RF-510 — com a remoção de espaços marcada, a pré-visualização mostra o texto SEM
        // espaços: o usuário vê o efeito da opção antes de aplicá-la.
        if (RemoveSpacesCheck.IsChecked == true)
            sample = Gort.Core.Structuring.TextPostProcessor.RemoveAllSpaces(sample);

        Preview.Translating = true;
        Preview.FontFamilyName = FontBox.SelectedItem as string ?? "";
        Preview.FontSizePoints = (double)(FontSizeBox.Value ?? 15);
        Preview.TextColor = _session.Profile.TextColor;
        Preview.Stroke1Color = _session.Profile.Stroke1Color;
        Preview.Stroke2Color = _session.Profile.Stroke2Color;
        Preview.BackgroundColor = _session.Profile.BackgroundColor;
        Preview.UseTextBackground = UseBackgroundCheck.IsChecked == true;
        Preview.TextHorizontalAlignment = CenterCheck.IsChecked == true
            ? TextAlignment.Center : TextAlignment.Left;

        Preview.SetText(sample);
    }

    /// <summary>RF-390 — Restaurar as cores padrão (P-101 a P-104).</summary>
    private void RestoreColors()
    {
        var (text, s1, s2, background) = Gort.Core.Rendering.TextColors.Defaults();
        _session.Profile.TextColor = text;
        _session.Profile.Stroke1Color = s1;
        _session.Profile.Stroke2Color = s2;
        _session.Profile.BackgroundColor = background;

        UpdateColorButtons();
        UpdatePreview();
    }

    /// <summary>
    /// RF-391 — A caixa de amostra NUNCA exibe componente zero: 0 é exibido como 1, para que
    /// a amostra não seja interpretada como transparente.
    /// </summary>
    private void UpdateColorButtons()
    {
        void Paint(Button button, Gort.Core.Model.Rgba color)
        {
            var swatch = Gort.Core.Rendering.TextColors.ForSwatch(color);
            button.Background = new SolidColorBrush(
                Color.FromArgb(255, swatch.R, swatch.G, swatch.B));
        }

        Paint(TextColorButton, _session.Profile.TextColor);
        Paint(Stroke1Button, _session.Profile.Stroke1Color);
        Paint(Stroke2Button, _session.Profile.Stroke2Color);
        Paint(BackgroundButton, _session.Profile.BackgroundColor);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RF-513 — Atalhos
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildShortcutList()
    {
        var rows = new List<Control>();

        foreach (var (action, _) in ShortcutSet.Defaults)
        {
            var config = _session.Shortcuts.Find(action);

            var label = new TextBlock
            {
                Text = _loc[$"shortcut.{action}"],
                Width = 260,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };

            var field = new TextBox
            {
                Text = config?.ToString() ?? "",
                Width = 180,
                IsReadOnly = true,
            };

            // RF-514 — enquanto um campo de captura está com foco, os atalhos globais ficam
            // inertes: o usuário está justamente digitando uma combinação.
            field.GotFocus += (_, _) => _session.Dispatcher.Suspended = true;
            field.LostFocus += (_, _) =>
            {
                _session.Dispatcher.Suspended = false;
                _session.Dispatcher.Reset();
            };

            field.KeyDown += (_, e) => CaptureShortcut(action, field, e);

            var restore = new Button { Content = _loc["other.default"], Padding = new Thickness(10, 4) };
            restore.Click += (_, _) =>
            {
                _session.Shortcuts.RestoreDefault(action);
                field.Text = _session.Shortcuts.Find(action)?.ToString() ?? "";
            };

            var clear = new Button { Content = _loc["other.clear"], Padding = new Thickness(10, 4) };
            clear.Click += (_, _) =>
            {
                _session.Shortcuts.Clear(action);
                field.Text = "";
            };

            rows.Add(new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 3),
                Children = { label, field, restore, clear },
            });
        }

        ShortcutList.ItemsSource = rows;
        ShortcutHint.Text = _loc["other.shortcuts"];
    }

    /// <summary>
    /// RF-513 — O campo registra as teclas na ordem em que são pressionadas, separadas por
    /// "+", limitado a três; escape e retrocesso limpam; teclas repetidas são ignoradas.
    /// </summary>
    private readonly List<string> _capturing = new();

    private void CaptureShortcut(ShortcutAction action, TextBox field,
                                 Avalonia.Input.KeyEventArgs e)
    {
        e.Handled = true;

        if (e.Key is Avalonia.Input.Key.Escape or Avalonia.Input.Key.Back)
        {
            _capturing.Clear();
            _session.Shortcuts.Clear(action);
            field.Text = "";
            return;
        }

        string name = KeyNames.Normalize(e.Key.ToString());
        if (_capturing.Contains(name)) return;                     // repetidas são ignoradas
        if (_capturing.Count >= Gort.Core.Calibration.P.MaxShortcutKeys) _capturing.Clear();

        _capturing.Add(name);
        _session.Shortcuts.Set(action, _capturing);
        field.Text = _session.Shortcuts.Find(action)?.ToString() ?? "";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RF-515 / RF-516 — Assistente de configuração rápida
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowQuickStep()
    {
        QuickColorChoices.IsVisible = _quickStep == 0;
        QuickSummary.IsVisible = _quickStep >= 2;

        QuickStepText.Text = _quickStep switch
        {
            0 => _loc["quick.step_color"],
            1 => _loc["quick.step_area"],
            2 => _loc["quick.step_confirm"],
            _ => _loc["quick.done"],
        };

        QuickNextButton.Content = _quickStep >= 3 ? _loc["quick.done"] : _loc["quick.next"];

        if (_quickStep >= 2)
        {
            var built = _session.Regions.Build();
            QuickSummary.Text = built.Captures.Count == 0
                ? _loc["area.none"]
                : string.Join("\n", built.Captures.Select((r, i) => $"{i + 1}: {r}"));
        }
    }

    private async Task AdvanceQuickAsync()
    {
        switch (_quickStep)
        {
            case 0:
                _quickStep = 1;
                break;

            case 1:
                // Passo 2 — o botão abre a camada de seleção.
                await DefineAreaAsync();
                _quickStep = 2;
                break;

            case 2:
                _quickStep = 3;
                ApplyQuickConfiguration();
                break;

            default:
                Tabs.SelectedIndex = 0;
                _quickStep = 0;
                break;
        }

        ShowQuickStep();
    }

    /// <summary>
    /// RF-516 — Ao concluir, o assistente aplica TUDO de uma vez.
    ///
    /// A ordem importa: parar a tradução primeiro, porque tudo o que vem depois mexe em
    /// configuração que o laço estaria usando.
    /// </summary>
    private void ApplyQuickConfiguration()
    {
        _loop.Stop();

        var p = _session.Profile;

        // Escolhe o motor: o moderno se disponível; senão o do sistema, se tiver o idioma
        // pedido; senão o clássico.
        var engine = _session.Engines.Find("modern") is { IsAvailable: true }
            ? "modern"
            : _session.Engines.Find("system") is { IsAvailable: true } sys
              && sys.Languages.Contains(p.OcrLanguage)
                ? "system"
                : "classic";

        if (_session.Engines.Resolve(engine) is { } resolved) p.OcrEngine = resolved.Key;

        // O serviço é o padrão do catálogo (RF-225). Antes havia aqui uma escolha por
        // idioma cujos dois ramos devolviam o mesmo valor — e que comparava com "ja",
        // que é justamente o que RF-567 proíbe.
        p.TranslationService = _session.Catalog.DefaultTranslationService;

        // Define os códigos de idioma de cada serviço, e o destino padrão (RF-314).
        var source = _session.Catalog.Language(p.OcrLanguage);
        if (source is not null)
        {
            foreach (var (service, key) in Gort.Core.Ocr.EngineSelection
                         .PropagateSourceLanguage(_session.Catalog, source))
            {
                p.ServiceSourceLanguage[service] = key;
            }

            // RF-148 — os ajustes automáticos vêm das PROPRIEDADES do idioma.
            p.ApplyLanguageProperties(source);
        }
        p.TargetLanguage = _session.Catalog.DefaultTargetLanguage;

        // RF-119 — ativa o filtro HSV com os grupos da cor escolhida, ou desativa todos os
        // filtros se o usuário escolheu "não sei".
        if (QuickDarkRadio.IsChecked == true)
        {
            p.FilterMode = FilterMode.Hsv;
            p.ColorGroups = FilterSettings.WizardGroups(darkText: true);
        }
        else if (QuickLightRadio.IsChecked == true)
        {
            p.FilterMode = FilterMode.Hsv;
            p.ColorGroups = FilterSettings.WizardGroups(darkText: false);
        }
        else
        {
            p.FilterMode = FilterMode.None;
        }

        p.CaptureActiveWindow = false;   // desativa a captura da janela ativa
        p.WindowMode = WindowMode.Layer; // força o modo camada
        p.Speed = 1;                     // força a velocidade mais rápida

        _session.Regions.SetColorGroupCount(p.ColorGroups.Count);
        _session.ApplyConfiguration();
        _session.SaveProfile();

        LoadValues();
        ShowLoopState();
        Say("msg.applied");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Capacidades, memória e mensagens
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowCapabilities()
    {
        var report = _session.Platform.Capabilities;

        if (!report.CanTranslate)
        {
            // RF-569 — sem capacidade essencial, o programa diz isso e não inicia.
            Say(report.BlockingExplanation());
            return;
        }

        var missing = report.Unavailable.ToList();
        if (missing.Count > 0)
        {
            _session.Notices.Add(_loc.Format("system.unavailable",
                string.Join("; ", missing.Select(m => CapabilityInfo.Name(m.Capability)))));
        }
    }

    /// <summary>RF-569 / RF-028 — Os avisos da inicialização são exibidos UMA VEZ.</summary>
    private void ShowNotices()
    {
        if (_session.Notices.Count == 0) return;
        Say(string.Join("  ·  ", _session.Notices));
        _session.Notices.Clear();
    }

    /// <summary>
    /// RF-558 — O consumo de memória fica à vista. A medida é o CONJUNTO DE TRABALHO: fora
    /// do Windows o runtime devolve zero para a memória privada, e um indicador que marca
    /// zero para sempre é pior que indicador nenhum.
    /// </summary>
    private void ShowMemory()
    {
        var report = _session.MemorySnapshot(OverlayBitmapBytes());
        MemoryIndicator.Text = _loc.Format("app.memory", report.ProcessBytes / 1024 / 1024);

        // RF-559 — o detalhamento fica a um passe de mouse, sem abrir diálogo: só o total
        // diz que há um problema, e o detalhamento diz qual configuração o causou.
        ToolTip.SetTip(MemoryIndicator, string.Join('\n', new[]
        {
            _loc.Format("memory.regions", MemoryReport.Megabytes(report.RegionImageBytes)),
            _loc.Format("memory.cache", MemoryReport.Megabytes(report.TranslationCacheBytes)),
            _loc.Format("memory.overlay", MemoryReport.Megabytes(report.OverlayBitmapBytes)),
        }));
    }

    /// <summary>RF-552 — O temporizador de estado só corre com a janela visível.</summary>
    private DispatcherTimer? _statusTimer;

    private void IsVisibleChanged()
    {
        if (_statusTimer is null) return;

        if (IsVisible)
        {
            _statusTimer.Start();
            ShowMemory();
        }
        else
        {
            _statusTimer.Stop();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RF-086 / RF-087 — Mudança de monitor
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Assinatura da disposição atual, para reconhecer que ela mudou.</summary>
    private string _monitorLayout = "";

    /// <summary>Índices já avisados, para não repetir o aviso a cada dois segundos.</summary>
    private string _warnedLayout = "";

    private string LayoutSignature()
    {
        var monitors = _session.Platform.Monitors.Monitors;
        return string.Join('|', monitors.Select(
            m => $"{m.Bounds.X},{m.Bounds.Y},{m.Bounds.Width},{m.Bounds.Height},{m.Scale}"));
    }

    private void RememberMonitorLayout() => _monitorLayout = LayoutSignature();

    /// <summary>
    /// RF-086 — Se a resolução mudar, um monitor for removido ou a disposição for alterada
    /// e alguma área ficar fora da área de trabalho, o programa AVISA e aponta quais.
    ///
    /// RF-087 — e oferece abrir o gerenciamento de áreas, com as molduras visíveis.
    ///
    /// Nenhuma área é reposicionada: o programa não tem como saber onde o conteúdo do jogo
    /// foi parar, e mover a área produziria uma região errada em silêncio — o usuário só
    /// descobriria pelo resultado da tradução.
    /// </summary>
    private void CheckMonitorLayout()
    {
        _session.Platform.Monitors.Refresh();

        string signature = LayoutSignature();
        if (signature == _monitorLayout) return;

        _monitorLayout = signature;

        var monitors = _session.Platform.Monitors.Monitors;
        var areas = _session.Regions.Areas.Select(a => a.FrameRect).ToList();
        var invalid = MonitorGeometry.InvalidAreas(monitors, areas);

        if (invalid.Count == 0) { _warnedLayout = ""; return; }

        // O aviso é por DISPOSIÇÃO, não por tique: sem isto ele se repetiria a cada dois
        // segundos enquanto o usuário não corrigisse, e viraria ruído.
        if (_warnedLayout == signature) return;
        _warnedLayout = signature;

        Say(_loc.Format("msg.areas_invalid",
                        string.Join(", ", invalid.Select(i => i + 1))));

        // RF-087 — o gerenciamento abre com as molduras visíveis, que é onde a correção
        // acontece. Abrir direto poupa o usuário de procurar onde era.
        OpenAreaManager();
    }

    /// <summary>
    /// RF-559 — O mapa de bits da sobreposição. RF-556 — ele só é recriado quando as
    /// dimensões da janela mudam, então o tamanho atual descreve o que está alocado.
    /// </summary>
    private long OverlayBitmapBytes()
    {
        if (_translationWindow is not OverlayWindow overlay) return 0;

        return (long)Math.Max(0, overlay.Bounds.Width)
             * (long)Math.Max(0, overlay.Bounds.Height) * 4;
    }

    private void Say(string keyOrText)
        => ResultText.Text = _loc.Has(keyOrText) ? _loc[keyOrText] : keyOrText;

    private void OpenLink(string key)
    {
        // RF-513 / RF-544 — os endereços são DADOS de configuração, nunca embutidos.
        if (!_session.Catalog.Links.TryGetValue(key, out var url) || url.Length == 0)
        {
            Say($"O endereço '{key}' ainda não está configurado em data/engines.toml.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Say(_loc.Format("msg.error", ex.Message));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Atalhos e controle remoto
    // ─────────────────────────────────────────────────────────────────────────

    private void SetUpShortcuts()
    {
        var keyboard = _session.Platform.Keyboard;

        if (keyboard.Start()) keyboard.KeyChanged += OnGlobalKey;
        else if (keyboard.UnavailableReason is not null)
            _session.Notices.Add(keyboard.UnavailableReason);

        _remote = new RemoteControlWindow
        {
            DefineArea = OpenAreaManager,
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
    /// RF-517 — O controle remoto tem de estar SEMPRE ACESSÍVEL. Sem posição explícita ele
    /// nasce no canto superior esquerdo, onde costuma ficar debaixo da janela traduzida.
    /// </summary>
    private void PlaceRemote(RemoteControlWindow remote)
    {
        var monitors = _session.Platform.Monitors.Monitors;
        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
        if (primary is null) return;

        remote.Position = new PixelPoint(
            primary.Bounds.Left + (primary.Bounds.Width - (int)remote.Width) / 2,
            primary.Bounds.Bottom - (int)remote.Height - 90);
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
                // RF-451 — com o laço rodando, pausa e executa um ciclo pontual.
                if (_loop.IsRunning) _loop.StopFromKeyboardHook();
                _ = TranslateOnceAsync();
                break;

            case ShortcutAction.QuickArea:
            case ShortcutAction.SnapshotArea:
                _ = DefineAreaAsync();
                break;

            case ShortcutAction.ToggleMouseFollowArea:
                _session.Regions.MouseFollowActive = !_session.Regions.MouseFollowActive;
                break;

            case ShortcutAction.ToggleTranslationWindow:
                if (_translationWindow is Window { IsVisible: true } visible) visible.Hide();
                else ShowTranslationWindow();
                break;

            case ShortcutAction.OpenDictionaryEditor:
                OpenDictionaryEditor();
                break;
        }
    }

    /// <summary>
    /// RF-543 — Sobre. As "versões dos dicionários" são o que o programa realmente sabe
    /// deles: o nome do arquivo e quantas entradas foram carregadas. Um número de versão
    /// que o dicionário não declara seria invenção.
    /// </summary>
    private void OpenAbout()
    {
        var dictionaries = new List<(string, int)>();

        if (_session.Dictionary is { } dictionary)
            dictionaries.Add((_session.Profile.DictionaryFile, dictionary.Count));

        new AboutWindow(_loc, dictionaries)
        {
            OpenLink = OpenLink,
            ShowSplash = () => new SplashWindow().Show(this),
        }.Show(this);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RF-537 — Editor de dicionário
    // ─────────────────────────────────────────────────────────────────────────

    private DictionaryEditorWindow? _dictionaryEditor;

    /// <summary>
    /// RF-537 — Abre o editor com o texto reconhecido ATUAL já preenchido.
    ///
    /// É o ponto do recurso: o usuário abre o editor no instante em que viu o OCR errar, e
    /// o texto errado já está lá. Enquanto ele está aberto, a cópia automática para a área
    /// de transferência fica SUSPENSA — o usuário está justamente selecionando e colando
    /// dentro do editor, e um ciclo sobrescrevendo a área de transferência no meio disso
    /// apagaria o que ele acabou de recortar.
    /// </summary>
    private void OpenDictionaryEditor()
    {
        if (_dictionaryEditor is not null) { _dictionaryEditor.Activate(); return; }

        string path = Path.Combine(
            _session.Paths.DataDirectory, _session.Profile.DictionaryFile);

        var window = new DictionaryEditorWindow(
            _loc, path, _session.Dictionary, _lastRecognizedText)
        {
            Accepted = () => Say(_loc.Format("dict.added", _session.Dictionary?.Count ?? 0)),
        };

        _dictionaryEditor = window;
        _session.ClipboardOutput.Suspended = true;

        window.Closed += (_, _) =>
        {
            _dictionaryEditor = null;
            _session.ClipboardOutput.Suspended = false;
        };

        window.Show(this);
    }

    /// <summary>
    /// RF-454 a RF-457 — A área que segue o mouse reposiciona-se a cada P-122, e só dispara
    /// o recálculo quando a posição EFETIVAMENTE mudou, no máximo uma vez a cada P-123.
    /// </summary>
    private void StartMouseFollow()
    {
        var gate = RecalculationGate.ForMouseFollow();
        gate.Enabled = true;

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Gort.Core.Calibration.P.MouseFollowTimerMs),
        };

        timer.Tick += (_, _) =>
        {
            if (!_session.Regions.MouseFollowActive) return;
            if (!_session.Platform.Cursor.TryGet(out int x, out int y)) return;
            if (!_session.Regions.MoveMouseFollowTo(x, y)) return;
            gate.ShouldRecalculate();
        };

        timer.Start();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Áreas e tradução
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>RF-047 — Abre a camada de seleção sobre toda a área de trabalho virtual.</summary>
    private async Task DefineAreaAsync(AreaKind kind = AreaKind.Normal)
    {
        var monitors = _session.Platform.Monitors.Monitors;
        if (monitors.Count == 0) return;

        var desktop = MonitorGeometry.VirtualDesktop(monitors);

        Hide();   // a janela principal sairia na captura da própria área

        // RF-053 / RF-443 — enquanto a camada está aberta, os atalhos globais ficam inertes.
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

        // O usuário desenha o RETÂNGULO DE CAPTURA; a moldura é maior, porque desconta
        // borda e barra de título (RF-073).
        double scale = MonitorGeometry.ScaleOf(monitors, drawn.Value);
        var metrics = FrameGeometry.MetricsFor(scale);
        var frameRect = FrameGeometry.ToFrameRect(drawn.Value, metrics);

        var frame = kind == AreaKind.Exclusion
            ? _session.Regions.AddExclusion(frameRect)
            : _session.Regions.AddArea(frameRect);

        // Se o gerenciamento está aberto, a área nova ganha moldura na hora.
        if (_areaManager is not null) ShowFrames();

        ShowQuickStep();
        ShowLoopState();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RF-054 a RF-064, RF-533, RF-534 — Molduras e gerenciamento de áreas
    // ─────────────────────────────────────────────────────────────────────────

    private AreaManagerWindow? _areaManager;
    private readonly List<AreaFrameWindow> _frames = new();

    /// <summary>
    /// RF-533 — Abre o gerenciamento de áreas, com as molduras visíveis.
    ///
    /// A janela fica onde o controle remoto está: é dali que o usuário vem, e as molduras
    /// ocupam a tela toda, então uma janela centralizada cairia por cima justamente do que
    /// se está ajustando.
    /// </summary>
    private void OpenAreaManager()
    {
        if (_areaManager is not null) { _areaManager.Activate(); return; }

        var window = new AreaManagerWindow(_loc, _session.Regions)
        {
            AddArea = kind => _ = DefineAreaAsync(kind),
            OpenColorGroups = OpenColorGroupsForFirstArea,
            Changed = ShowFrames,
        };

        window.Finished = _ =>
        {
            _areaManager = null;
            CloseFrames();

            // Aplicar ou cancelar, os dois mexem na lista de áreas: o laço precisa relê-la.
            ShowLoopState();
        };

        _areaManager = window;

        if (_remote is not null)
            window.Position = new PixelPoint(_remote.Position.X, _remote.Position.Y - 170);

        window.Show();
        ShowFrames();
    }

    /// <summary>
    /// RF-054 / RF-064 — Uma moldura por área, reconstruídas quando a lista muda.
    ///
    /// Reconstruir em vez de reconciliar é deliberado: a reindexação de RF-064 renumera
    /// todas as áreas depois da removida, e casar janelas com áreas por posição na lista
    /// daria exatamente o mesmo trabalho com mais chance de errar.
    /// </summary>
    private void ShowFrames()
    {
        CloseFrames();

        _session.Regions.SetFramesVisible(true);
        var monitors = _session.Platform.Monitors.Monitors;

        Gort.Core.Model.Rect Desktop() => MonitorGeometry.VirtualDesktop(monitors);

        int index = 1;
        foreach (var area in _session.Regions.Areas) Add(area, index++, "areas.kind_normal");

        index = 1;
        foreach (var e in _session.Regions.Exclusions) Add(e, index++, "areas.kind_exclusion");

        _areaManager?.Refresh();

        void Add(CaptureFrame frame, int number, string kindKey)
        {
            var window = new AreaFrameWindow(frame, number, Desktop)
            {
                KindName = _loc[kindKey],

                // RF-059 / RF-060 — o recálculo só acontece se o programa já terminou de
                // inicializar e não está aplicando configuração. A moldura só avisa.
                AreasChanged = () =>
                {
                    if (_busy) return;
                    _areaManager?.Refresh();
                },
            };

            _frames.Add(window);
            window.Show();
        }
    }

    private void CloseFrames()
    {
        foreach (var frame in _frames) frame.Close();
        _frames.Clear();
    }

    /// <summary>RF-534 — Os grupos de cor da primeira área.</summary>
    private void OpenColorGroupsForFirstArea()
    {
        // RF-063 — áreas de exclusão não oferecem os botões de cor; por isso só as normais
        // entram aqui.
        var area = _session.Regions.Areas.FirstOrDefault();
        if (area is null) { Say("area.none"); return; }

        new ColorGroupsWindow(_loc, area, _session.Profile.ColorGroups).Show(this);
    }

    /// <summary>
    /// RF-081 / RF-083 — Pré-visualização binarizada: aplica exatamente o mesmo critério de
    /// filtro que o pré-processamento usaria, para que o usuário veja o que o OCR vai
    /// receber.
    /// </summary>
    /// <summary>
    /// RF-535 / RF-536 — Abre o conta-gotas com a pré-visualização binarizada.
    ///
    /// A janela é ÚNICA: reabrir com ela já aberta a traz para a frente. Uma segunda
    /// instância editaria o mesmo grupo de cor por dois caminhos, e a última a fechar
    /// venceria sem que o usuário soubesse por quê.
    ///
    /// RF-535 pede também que, enquanto ela está aberta, as molduras deixem de ser "sempre
    /// no topo". As molduras ainda não existem como janela (RF-047 a RF-056 pendentes);
    /// quando existirem, é aqui que elas serão rebaixadas.
    /// </summary>
    private ColorPickerWindow? _picker;

    private void OpenColorPicker()
    {
        if (_picker is not null) { _picker.Activate(); return; }

        // RF-084 — sem nenhuma área, o programa INFORMA em vez de falhar.
        if (_session.Regions.Build().Captures.Count == 0) { Say("area.none"); return; }

        var settings = _session.BuildCycleSettings().Filter;

        // RF-105 — o grupo editado é o PRIMEIRO; os demais continuam valendo no filtro.
        var group = _session.Profile.ColorGroups.Count > 0
            ? _session.Profile.ColorGroups[0]
            : AddFirstColorGroup();

        var window = new ColorPickerWindow(_loc, group, settings, CaptureFirstArea)
        {
            Changed = () =>
            {
                // RF-508 — a mudança vale imediatamente, sem passar por "aplicar".
                _session.Profile.FilterMode = settings.Mode;
                _session.Profile.Threshold = settings.Threshold;
                _session.Profile.Erosion = settings.Erosion;
                LoadValues();
            },
        };

        _picker = window;
        window.Closed += (_, _) => _picker = null;
        window.Show(this);
    }

    private Gort.Core.Model.ColorGroup AddFirstColorGroup()
    {
        var group = new Gort.Core.Model.ColorGroup();
        _session.Profile.ColorGroups.Add(group);
        return group;
    }

    /// <summary>Recaptura a primeira área, que é o que o conta-gotas examina.</summary>
    private Gort.Core.Model.ImageBuffer? CaptureFirstArea()
    {
        var built = _session.Regions.Build();
        if (built.Captures.Count == 0) return null;

        var captured = _session.Platform.Capture.Capture(new Gort.Platform.Capture.CaptureRequest
        {
            Rects = new[] { built.Captures[0] },
            Source = Gort.Platform.Capture.CaptureSource.Screen,
        });

        return captured.Count == 0 ? null : captured[0].Image;
    }

    /// <summary>
    /// RF-008 / Passo 1 — O mesmo acionamento inicia e para: se já estiver traduzindo, para.
    /// </summary>
    private void ToggleLoop()
    {
        if (_loop.IsRunning)
        {
            // RF-010 — se a thread não parar no prazo, o sinalizador NÃO é revertido e o
            // usuário é informado.
            if (!_loop.Stop()) { Say("msg.loop_stop_failed"); return; }
            ShowLoopState();
            return;
        }

        // Passo 2 — verificações de pré-condição.
        if (!_session.Regions.HasAnyIncrementalArea) { Say("msg.no_area"); return; }

        var engine = _session.Engines.Resolve(_session.Profile.OcrEngine);
        if (engine is null) { Say("msg.no_engine"); return; }

        var rejection = Gort.Core.Ocr.EngineSelection.CanStart(
            engine, _session.Catalog.OcrEngine(engine.Key),
            realtime: true, _session.Profile.WindowMode);

        if (rejection != Gort.Core.Ocr.EngineRejection.None)
        {
            Say(Gort.Core.Ocr.EngineSelection.Explain(rejection, engine.Key));
            return;
        }

        // Passo 3 — a janela de tradução é preparada.
        var window = TranslationWindow();
        ShowTranslationWindow();
        window.Clear();

        // RF-383 — preparar a sobreposição antes do primeiro quadro.
        if (window is OverlayWindow prepared) prepared.PrepareForTranslation();

        // RF-343 — a janela sobre uma área de OCR seria traduzida a si mesma.
        WarnIfWindowOverlapsAreas();

        // RF-071 — ao iniciar uma tradução não instantânea, a memória do último instantâneo
        // é apagada.
        _session.Regions.ForgetLastSnapshot();

        if (!_loop.Start(LoopMode.Realtime)) { Say("msg.loop_stop_failed"); return; }
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

            // RF-491 — "destravar velocidade", só no modo de depuração. Lido do objeto de
            // sinalizadores, e não da caixa: o laço roda em outra thread, e tocar num
            // controle de interface a partir dela é o que P2 proíbe.
            UnlockSpeed = () => _session.Debug.Enabled && _session.Debug.UnlockSpeed,

            // Passo 18 — o desenho é DESPACHADO para a thread de interface. O laço nunca
            // desenha, e P2 proíbe que ele abra diálogo.
            //
            // RF-493 — o retrato é registrado AQUI, antes do despacho: no modo sobreposição
            // ele fica pendente até o desenho terminar, e é o gravador que decide o que
            // fazer com um pendente que ficou para trás (RF-495).
            Draw = result =>
            {
                RecordAnalysis(result);
                Dispatcher.UIThread.Post(() => DrawResult(result));
            },

            Repaint = () => Dispatcher.UIThread.Post(() => _translationWindow?.Repaint()),

            ReportError = message =>
            {
                // RF-498 — o erro entra no registro de mensagens antes de virar aviso.
                _session.Counters.RecordError(message);
                Dispatcher.UIThread.Post(() => Say(_loc.Format("msg.error", message)));
            },

            // Passo 16 — cópia para a área de transferência.
            CopyToClipboard = result => Dispatcher.UIThread.Post(() => CopyResult(result)),

            // Passo 19 — efeitos colaterais: gravação em arquivo e leitura em voz alta.
            SideEffects = result =>
            {
                // RF-496 — o par vai para o arquivo no formato do banco de dados. Fora da
                // thread de interface de propósito: é disco, e a fala já é assíncrona.
                try { _session.ResultFile.Write(result.RecognizedText, result.DisplayText); }
                catch { /* P8 */ }

                Dispatcher.UIThread.Post(() => SpeakResult(result));
            },

            FlushMemory = () => _session.Memory?.FlushAsync(),
            Stopped = () => Dispatcher.UIThread.Post(ShowLoopState),
        };

        return new TranslationLoop(host);
    }

    /// <summary>
    /// RF-537 — O último texto reconhecido, para pré-preencher o editor de dicionário.
    /// Escrito na thread de interface, que é a única que desenha.
    /// </summary>
    private string _lastRecognizedText = "";

    private void DrawResult(CycleResult result)
    {
        _lastRecognizedText = result.RecognizedText;

        if (_translationWindow is OverlayWindow overlay)
        {
            // RF-494 — o cálculo de tamanho e posição é uma das parcelas medidas.
            var watch = System.Diagnostics.Stopwatch.StartNew();

            // RF-349 / RF-350 — a janela acompanha a união das áreas, sem encolher no meio
            // da tradução.
            overlay.FitTo(_session.Regions.Build().Captures);
            var blocks = BuildOverlayBlocks(result, overlay);

            watch.Stop();
            _sizeAndPositionMs = watch.Elapsed.TotalMilliseconds;

            overlay.SetBlocks(blocks);
            return;
        }

        // Passo 17 — a memória de exibição é aplicada ao texto final.
        string text = _displayMemory.Apply(result.DisplayText);

        // RF-240 — com "ignorar tradução vazia", um resultado vazio não substitui o que
        // está na tela.
        if (string.IsNullOrWhiteSpace(text) && _session.Advanced.IgnoreEmptyTranslation) return;

        _translationWindow?.Show(text, result.RecognizedText);
    }

    private void ShowLoopState()
    {
        bool running = _loop.IsRunning;
        ShowWindowButton.Content = running ? _loc["translate.stop"] : _loc["translate.start"];
        _translationWindow?.SetRunning(running);
        _remote?.SetRunning(running);

        // RF-320 — "sempre no topo apenas durante a tradução".
        if (_session.Advanced.AlwaysOnTopOnlyWhileTranslating)
            _translationWindow?.SetAlwaysOnTop(running);
    }

    /// <summary>RF-202 — "Traduzir uma vez": um único ciclo, pelo mesmo caminho.</summary>
    private async Task TranslateOnceAsync()
    {
        if (_busy) return;
        _busy = true;
        Say("msg.translating");

        try
        {
            var window = TranslationWindow();
            ShowTranslationWindow();
            window.SetRunning(true);

            var watch = Stopwatch.StartNew();
            var result = await _session.Cycle.RunAsync(
                _session.Regions.Build(), _session.BuildCycleSettings());
            watch.Stop();

            DrawResult(result);
            window.SetRunning(false);

            Say(result.Error is not null
                ? _loc.Format("msg.error", result.Error)
                : _loc.Format("msg.cycle_result", watch.ElapsedMilliseconds,
                              result.Regions.Sum(r => r.Blocks.Count), result.NetworkCount));

            await (_session.Memory?.FlushAsync() ?? Task.CompletedTask);
        }
        catch (Exception ex)
        {
            // RF-561 — nenhuma falha encerra o programa; vira mensagem visível.
            Say(_loc.Format("msg.error", ex.Message));
        }
        finally
        {
            _busy = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Janelas de tradução (RF-317, RF-318)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RF-317 / RF-318 — Os modos são trocáveis a qualquer momento, e trocar DESTRÓI a
    /// janela anterior e cria a nova: elas não compartilham estado visual.
    /// </summary>
    private ITranslationWindow TranslationWindow()
    {
        var mode = _session.Profile.WindowMode;
        if (_translationWindow is not null && _windowMode == mode) return _translationWindow;

        DestroyTranslationWindow();
        _windowMode = mode;

        // RF-222 / RF-223 — memória de exibição, conforme as opções avançadas.
        _displayMemory.Enabled = _session.Advanced.DisplayMemoryEnabled;
        _displayMemory.Capacity = _session.Advanced.DisplayMemoryCount;
        _displayMemory.LifetimeSeconds = _session.Advanced.DisplayMemoryLifetimeSeconds;

        _translationWindow = mode switch
        {
            WindowMode.Layer => CreateLayerWindow(),
            WindowMode.Overlay => CreateOverlayWindow(),
            _ => CreateDarkWindow(),
        };

        _translationWindow.SetAlwaysOnTop(_session.Options.TranslationWindowAlwaysOnTop);
        return _translationWindow;
    }

    private ITranslationWindow CreateDarkWindow()
    {
        var window = new DarkTranslationWindow
        {
            ShowRecognizedText = _session.Profile.ShowRecognizedText,
        };
        window.SetFont(_session.Advanced.DarkModeFont, _session.Profile.FontSize);
        return window;
    }

    private ITranslationWindow CreateLayerWindow()
    {
        var window = new LayerTranslationWindow(_session.Platform.WindowEffects)
        {
            ShowRecognizedText = _session.Profile.ShowRecognizedText,

            // RF-481 — o menu de contexto tira o texto da tabela, como o resto da interface.
            Text = key => _loc[key],

            // RF-546 — o menu LÊ o estado do perfil e o DEVOLVE para lá; ele não guarda
            // cópia, senão discordaria da aba de texto assim que uma das duas mudasse.
            ReadRemoveSpaces = () => _session.Profile.RemoveSpaces,
            RemoveSpacesChanged = value =>
            {
                _session.Profile.RemoveSpaces = value;
                RemoveSpacesCheck.IsChecked = value;
            },
        };

        // RF-041 / RF-340 — a posição salva é validada contra os monitores presentes.
        var monitors = _session.Platform.Monitors.Monitors.Select(m => m.Bounds).ToList();
        int screenHeight = _session.Platform.Monitors.Monitors
            .FirstOrDefault(m => m.IsPrimary)?.Bounds.Height ?? 1080;

        var placement = _session.Profile.ResolveLayerPlacement(monitors, screenHeight);
        window.Position = new PixelPoint(placement.X, placement.Y);
        window.Width = placement.Width;
        window.Height = placement.Height;

        window.Configure(
            ResolveFont(), _session.Profile.FontSize,
            _session.Profile.TextColor, _session.Profile.Stroke1Color,
            _session.Profile.Stroke2Color, _session.Profile.BackgroundColor,
            useStroke: true,
            useBackground: _session.Profile.TextBackground,
            horizontal: _session.Profile.TextOrder == TextOrder.Center
                ? TextAlignment.Center
                : _session.Profile.TextOrder == TextOrder.Right
                    ? TextAlignment.Right : TextAlignment.Left,
            vertical: _session.Advanced.LayerAlignBottom
                ? Gort.Core.Rendering.VerticalAlignment.Bottom
                : Gort.Core.Rendering.VerticalAlignment.Top);

        return window;
    }

    private ITranslationWindow CreateOverlayWindow()
    {
        var window = new OverlayWindow(_session.Platform.WindowEffects);
        var surface = window.Canvas;

        surface.FontFamilyName = ResolveFont();
        surface.FixedFontSize = _session.Profile.FontSize;
        surface.TextColor = _session.Profile.TextColor;
        surface.Stroke1Color = _session.Profile.Stroke1Color;
        surface.Stroke2Color = _session.Profile.Stroke2Color;
        surface.BackgroundColor = _session.Profile.BackgroundColor;

        surface.FontStroke = _session.Advanced.FontStroke;
        surface.UseBackground = _session.Profile.TextBackground;
        surface.UseBackgroundTransparency = _session.Advanced.UseBackgroundTransparency;
        surface.AutoFontSize = _session.Advanced.AutoFontSize;
        surface.MinFontSize = _session.Advanced.AutoFontSizeMin;
        surface.MaxFontSize = _session.Advanced.AutoFontSizeMax;
        surface.PreserveOrientation = _session.Advanced.PreserveOrientation;
        surface.Scale = _session.Profile.Scale;

        // RF-491 — em depuração, as caixas de origem aparecem no lugar do fundo normal.
        surface.ShowWordAreas = _session.Debug.Enabled && _session.Debug.ShowWordAreas;

        // RF-493 — o retrato pendente só se completa depois do desenho.
        surface.Drawn += () => CompleteAnalysisDrawing(window);

        var primary = _session.Platform.Monitors.Monitors.FirstOrDefault(m => m.IsPrimary);
        surface.VerticalDpi = primary?.Dpi ?? Gort.Core.Calibration.P.ReferenceDpi;

        return window;
    }

    /// <summary>RF-387 — A família é resolvida em tempo de execução, nunca fixada por nome.</summary>
    private string ResolveFont()
        => Gort.Core.Rendering.FontResolution.Resolve(
            _session.Profile.FontFamily, null, _session.Catalog.FontFallbacks, IsFontAvailable);

    private static bool IsFontAvailable(string family)
        => !string.IsNullOrWhiteSpace(family)
           && FontManager.Current.SystemFonts.Any(
               f => string.Equals(f.Name, family, StringComparison.OrdinalIgnoreCase));

    /// <summary>RF-318 — Trocar de modo destrói a janela anterior.</summary>
    private void DestroyTranslationWindow()
    {
        switch (_translationWindow)
        {
            case DarkTranslationWindow dark:
                dark.HideInsteadOfClose = false;
                dark.Close();
                break;
            case LayerTranslationWindow layer:
                layer.HideInsteadOfClose = false;
                layer.Close();
                break;
            case OverlayWindow overlay:
                overlay.Close();
                break;
        }
        _translationWindow = null;
    }

    private void ShowTranslationWindow()
    {
        if (_translationWindow is Window window) window.Show();
    }

    /// <summary>
    /// RF-343 — Avisa quando a janela intersecta uma área de OCR: ela estaria sendo
    /// capturada e traduzindo a si mesma. Só vale nos modos escuro e camada, com captura de
    /// tela; na sobreposição a janela é excluída da captura.
    /// </summary>
    private void WarnIfWindowOverlapsAreas()
    {
        if (_session.Profile.WindowMode == WindowMode.Overlay) return;
        if (_session.Profile.CaptureActiveWindow) return;
        if (_translationWindow is not Window window) return;

        var rect = new Gort.Core.Model.Rect(
            window.Position.X, window.Position.Y,
            (int)window.Bounds.Width, (int)window.Bounds.Height);

        if (!Gort.Core.Rendering.LayerLayout.WouldCaptureItself(
                rect, _session.Regions.Build().Captures))
        {
            return;
        }

        string aviso = _loc["msg.self_capture"];
        if (_translationWindow is LayerTranslationWindow layer) layer.WarnAboutSelfCapture(aviso);
        Say(aviso);
    }

    /// <summary>
    /// RF-352 a RF-354 — Converte o resultado do ciclo nos blocos que a sobreposição desenha.
    /// </summary>
    private List<OverlayBlock> BuildOverlayBlocks(CycleResult result, OverlayWindow window)
    {
        var blocks = new List<OverlayBlock>();

        var windowRect = new Gort.Core.Model.Rect(
            window.Position.X, window.Position.Y,
            (int)window.Bounds.Width, (int)window.Bounds.Height);

        double scale = _session.Profile.Scale;
        var monitors = _session.Platform.Monitors.Monitors;

        foreach (var region in result.Regions)
        {
            var metrics = FrameGeometry.MetricsFor(MonitorGeometry.ScaleOf(monitors, region.ScreenRect));

            for (int i = 0; i < region.Blocks.Count; i++)
            {
                var block = region.Blocks[i];
                if (string.IsNullOrWhiteSpace(block.TranslatedText)) continue;

                var rect = Gort.Core.Rendering.OverlayGeometry.BlockRect(
                    block.SourceBox, region.ScreenRect, scale, windowRect, metrics.Border);

                // RF-354 — recortado pela área; sem área, descartado.
                var areaInWindow = region.ScreenRect.Offset(-windowRect.X, -windowRect.Y);
                rect = Gort.Core.Rendering.OverlayGeometry.ClipToArea(rect, areaInWindow);
                if (rect.IsEmpty) continue;

                blocks.Add(new OverlayBlock
                {
                    Text = block.TranslatedText!,
                    ViewRect = rect,
                    SourceRect = block.SourceBox,   // RF-493
                    IsTitle = block.IsTitle,
                    Orientation = block.Orientation,
                    OwnMedianSize = Gort.Core.Rendering.OverlayTextLayout.MedianLineSize(
                        block.Lines, block.Orientation),
                    AutoColor = region.UsesAutoColor && i < region.AutoColors.Count
                        ? region.AutoColors[i] : null,
                });
            }
        }

        return blocks;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Efeitos colaterais do ciclo
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RF-473 / RF-474 — Copia o resultado. Falhas de acesso à área de transferência são
    /// ignoradas SILENCIOSAMENTE: perder uma cópia é melhor que interromper a tradução.
    /// </summary>
    private void CopyResult(CycleResult result)
    {
        if (!_session.ClipboardOutput.ShouldCopy()) return;

        try
        {
            string text = _session.ClipboardOutput.Compose(result.RecognizedText, result.DisplayText);
            if (!string.IsNullOrWhiteSpace(text)) Clipboard?.SetTextAsync(text);
        }
        catch
        {
            // RF-474 — silêncio.
        }
    }

    /// <summary>RF-476 a RF-480 — Leitura em voz alta do resultado.</summary>
    private void SpeakResult(CycleResult result)
    {
        // RF-478 — no modo sobreposição, os tokens separadores saem antes da leitura.
        string text = Gort.Core.Auxiliary.SpeechQueue.Clean(
            result.DisplayText, _session.Profile.WindowMode, _session.Pipeline.SeparatorToken);

        switch (_session.Speech.Decide(text))
        {
            case Gort.Core.Auxiliary.SpeechQueue.Decision.Speak:
                _session.Platform.Speech.Speak(text, interrupt: false);
                break;
            case Gort.Core.Auxiliary.SpeechQueue.Decision.SpeakInterrupting:
                _session.Platform.Speech.Speak(text, interrupt: true);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Aplicar e encerrar
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>RF-340 / RF-045 — A posição da camada só é salva ao aplicar ou salvar.</summary>
    private void CaptureLayerPlacement()
    {
        if (_translationWindow is not LayerTranslationWindow layer) return;

        _session.Profile.LayerX = layer.Position.X;
        _session.Profile.LayerY = layer.Position.Y;
        _session.Profile.LayerWidth = (int)layer.Bounds.Width;
        _session.Profile.LayerHeight = (int)layer.Bounds.Height;
    }

    /// <summary>RF-022 — Restaurar todos os valores para os padrões, com confirmação.</summary>
    private void RestoreDefaults()
    {
        var defaults = Profile.Defaults();
        var p = _session.Profile;

        p.OcrEngine = defaults.OcrEngine;
        p.TranslationService = defaults.TranslationService;
        p.OcrLanguage = defaults.OcrLanguage;
        p.TargetLanguage = defaults.TargetLanguage;
        p.FilterMode = defaults.FilterMode;
        p.Threshold = defaults.Threshold;
        p.Erosion = defaults.Erosion;
        p.Scale = defaults.Scale;
        p.Speed = defaults.Speed;
        p.WindowMode = defaults.WindowMode;
        p.FontFamily = defaults.FontFamily;
        p.FontSize = defaults.FontSize;
        p.TextColor = defaults.TextColor;
        p.Stroke1Color = defaults.Stroke1Color;
        p.Stroke2Color = defaults.Stroke2Color;
        p.BackgroundColor = defaults.BackgroundColor;

        // RF-022 — a restauração também descarta a posição e o tamanho salvos da janela.
        p.LayerX = p.LayerY = p.LayerWidth = p.LayerHeight = -1;

        _session.ApplyConfiguration();
        LoadValues();
        UpdatePreview();
        Say("msg.applied");
    }

    /// <summary>
    /// RF-504 — Aplicar: leva os valores da interface à configuração e salva o perfil.
    /// </summary>
    private void Apply()
    {
        // RF-504 — limpa o estado de teclas pressionadas.
        _session.Dispatcher.Reset();

        var p = _session.Profile;

        if (EngineBox.SelectedItem is string engine) p.OcrEngine = engine;
        if (ServiceBox.SelectedItem is string service) p.TranslationService = service;
        if (SourceBox.SelectedItem is string source) p.OcrLanguage = source;
        if (TargetBox.SelectedItem is string target) p.TargetLanguage = target;

        p.ShowRecognizedText = ShowOcrCheck.IsChecked == true;
        p.WriteResultToFile = WriteFileCheck.IsChecked == true;
        p.CopyToClipboard = CopyClipboardCheck.IsChecked == true;
        p.UseDictionary = UseDictionaryCheck.IsChecked == true;
        p.DictionaryWholeWord = DictionaryWordCheck.IsChecked == true;

        // RF-104 — os três modos são mutuamente exclusivos.
        p.FilterMode = FilterRgbRadio.IsChecked == true ? FilterMode.Rgb
                     : FilterHsvRadio.IsChecked == true ? FilterMode.Hsv
                     : FilterThresholdRadio.IsChecked == true ? FilterMode.Threshold
                     : FilterMode.None;

        p.Threshold = (int)(ThresholdBox.Value ?? 127);
        p.Erosion = ErosionCheck.IsChecked == true;

        p.FontFamily = FontBox.SelectedItem as string ?? "";
        p.FontSize = (double)(FontSizeBox.Value ?? 15);
        p.TextOrder = CenterCheck.IsChecked == true ? TextOrder.Center : TextOrder.Left;
        p.RemoveSpaces = RemoveSpacesCheck.IsChecked == true;
        p.TextBackground = UseBackgroundCheck.IsChecked == true;
        p.NumberAreas = NumberAreasCheck.IsChecked == true;

        p.CaptureActiveWindow = ActiveWindowCheck.IsChecked == true;
        p.Scale = (double)(ScaleBox.Value ?? 2);
        if (SpeedBox.SelectedIndex >= 0) p.Speed = SpeedBox.SelectedIndex + 1;

        if (WindowModeBox.SelectedItem is string mode)
        {
            p.WindowMode = mode switch
            {
                "camada" => WindowMode.Layer,
                "sobreposição" => WindowMode.Overlay,
                _ => WindowMode.Dark,
            };
        }

        p.SpeakResult = SpeakCheck.IsChecked == true;
        p.SpeakWaitForPrevious = SpeakWaitCheck.IsChecked == true;

        _session.Options.TranslationWindowAlwaysOnTop = AlwaysOnTopCheck.IsChecked == true;
        _session.Options.CheckForUpdates = CheckUpdatesCheck.IsChecked == true;
        _session.Options.StartOnBasicTab = StartBasicCheck.IsChecked == true;

        // RF-148 — os ajustes automáticos vêm das PROPRIEDADES do idioma, não do seu nome.
        if (_session.Catalog.Language(p.OcrLanguage) is { } language)
            p.ApplyLanguageProperties(language);

        CaptureLayerPlacement();

        // RF-012 — a mudança passa pelo protocolo "pausar → aplicar → retomar". Se a parada
        // falhar por tempo, NADA é aplicado e o usuário é informado.
        var outcome = _loop.PauseAndResume(() =>
        {
            _session.ApplyConfiguration();
            _session.SaveShortcuts();
            _session.SaveProfile();
            _session.Options.Save(_session.Paths.AppOptions);
        });

        if (outcome == ApplyResult.Aborted) { Say("msg.apply_aborted"); return; }

        FillChoices();
        LoadValues();
        UpdatePreview();
        ShowLoopState();

        Say(outcome == ApplyResult.AppliedAndResumed ? "msg.applied_resumed" : "msg.applied");
    }

    protected override void OnClosed(EventArgs e)
    {
        // RF-016 — ao encerrar de fato: parar o laço, liberar o interceptador de teclado e
        // fechar as janelas auxiliares.
        _loop.Stop();
        _session.Platform.Keyboard.Stop();

        DestroyTranslationWindow();

        if (_remote is not null)
        {
            _remote.AllowClose = true;
            _remote.Close();
        }

        _session.Dispose();
        base.OnClosed(e);
    }
}
