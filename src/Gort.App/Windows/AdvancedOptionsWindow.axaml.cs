using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Gort.Core.Caching;
using Gort.Core.Calibration;
using Gort.Core.Configuration;
using Gort.Core.Localization;
using Gort.Core.Shortcuts;
using Gort.Core.Translation.Presets;
using Avalonia;
using ShortcutAction = Gort.Core.Model.ShortcutAction;

namespace Gort.App.Windows;

/// <summary>
/// V.3 — Janela de opções avançadas: sete abas.
///
/// "Abrir esta janela deve BLOQUEAR OS ATALHOS GLOBAIS até que ela seja fechada." Não é
/// detalhe de conforto: a aba de atalhos avançados captura combinações, e um atalho global
/// disparando no meio da captura executaria a ação em vez de registrá-la.
///
/// A janela trabalha sobre uma CÓPIA das opções. Fechar sem aplicar não muda nada — é o que
/// permite a RF-530 ter um botão "aplicar" com significado, e ao botão "restaurar padrões"
/// ser reversível enquanto não se aplica.
/// </summary>
public partial class AdvancedOptionsWindow : Window
{
    private readonly Localizer _loc;
    private readonly ShortcutSet _shortcuts;
    private readonly ShortcutDispatcher _dispatcher;
    private readonly ApiPresetStore _presets;
    private readonly string _collectionDirectory;
    private readonly Func<IEnumerable<string>> _fonts;

    private AdvancedOptions _options;
    private Gort.Core.Configuration.ClipboardCopyFormat _copyFormat;
    private Gort.Core.Catalog.LanguageInfo? _targetLanguage;

    /// <summary>
    /// Enquanto verdadeiro, os manipuladores não escrevem nas opções: eles estão sendo
    /// disparados pelo próprio carregamento dos controles, não pelo usuário.
    /// </summary>
    private bool _loading;

    private ApiPreset? _selectedPreset;
    private readonly List<string> _capturing = new();

    /// <summary>RF-447 — Os serviços que o catálogo marca como trocáveis por atalho.</summary>
    private readonly List<Gort.Core.Catalog.TranslationServiceInfo> _switchableServices;

    /// <summary>RF-530 / RF-531 — Chamado quando o usuário aplica.</summary>
    public Action<AdvancedOptions, Gort.Core.Configuration.ClipboardCopyFormat>? Applied
    { get; set; }

    public AdvancedOptionsWindow() : this(
        new Localizer(), AdvancedOptions.Defaults(),
        Gort.Core.Configuration.ClipboardCopyFormat.Ocr,
        ShortcutSet.WithDefaults(), new ShortcutDispatcher(ShortcutSet.WithDefaults()),
        new ApiPresetStore(), "", () => Array.Empty<string>(), null)
    {
    }

    public AdvancedOptionsWindow(
        Localizer loc, AdvancedOptions options,
        Gort.Core.Configuration.ClipboardCopyFormat copyFormat,
        ShortcutSet shortcuts, ShortcutDispatcher dispatcher, ApiPresetStore presets,
        string collectionDirectory, Func<IEnumerable<string>> fonts,
        Gort.Core.Catalog.LanguageInfo? targetLanguage,
        IEnumerable<Gort.Core.Catalog.TranslationServiceInfo>? switchableServices = null)
    {
        InitializeComponent();

        _loc = loc;
        _shortcuts = shortcuts;
        _dispatcher = dispatcher;
        _presets = presets;
        _collectionDirectory = collectionDirectory;
        _fonts = fonts;
        _targetLanguage = targetLanguage;
        _switchableServices = (switchableServices ??
            Array.Empty<Gort.Core.Catalog.TranslationServiceInfo>()).ToList();

        // A janela edita uma cópia; o original só muda em "aplicar".
        _options = options.CloneForEditing();
        _copyFormat = copyFormat;

        ApplyTexts();
        WireEvents();
        LoadFromOptions();

        // V.3 — os atalhos globais ficam bloqueados enquanto a janela existe.
        _dispatcher.Suspended = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _dispatcher.Suspended = false;
        _dispatcher.Reset();
        base.OnClosed(e);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Textos (RF-481: tudo vem da tabela)
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyTexts()
    {
        Title = _loc["advanced.title"];
        TitleText.Text = _loc["advanced.title"];

        TabApp.Header = _loc["advanced.tab.app"];
        TabShortcuts.Header = _loc["advanced.tab.shortcuts"];
        TabWindow.Header = _loc["advanced.tab.window"];
        TabCollection.Header = _loc["advanced.tab.collection"];
        TabTranslation.Header = _loc["advanced.tab.translation"];
        TabOcr.Header = _loc["advanced.tab.ocr"];
        TabDictionary.Header = _loc["advanced.tab.dictionary"];

        ApplyButton.Content = _loc["advanced.apply"];
        RestoreButton.Content = _loc["advanced.restore"];
        CloseButton.Content = _loc["advanced.close"];

        AppGeneralLabel.Text = _loc["app.general"];
        TrayModeCheck.Content = _loc["app.tray_mode"];
        RightToLeftCheck.Content = _loc["app.right_to_left"];
        RemoteAlwaysOnTopCheck.Content = _loc["app.remote_always_on_top"];
        MouseFollowLabel.Text = _loc["app.mouse_follow"];
        MouseCompatibleCheck.Content = _loc["app.mouse_compatible"];
        MouseOnlyCheck.Content = _loc["app.mouse_only"];
        WindowCaptureLabel.Text = _loc["app.window_capture"];
        CaptureBorderCheck.Content = _loc["app.capture_border"];
        SelectionColorsLabel.Text = _loc["app.selection_colors"];
        SelectionBackgroundLabel.Text = _loc["app.selection_background"];
        SelectionHighlightLabel.Text = _loc["app.selection_highlight"];
        SelectionPreviewButton.Content = _loc["app.selection_preview"];
        SelectionRestoreButton.Content = _loc["app.selection_restore"];

        OverlayLabel.Text = _loc["window.overlay"];
        AutoFontSizeCheck.Content = _loc["window.auto_font_size"];
        MergeLinesCheck.Content = _loc["window.merge_lines"];
        PreserveOrientationCheck.Content = _loc["window.preserve_orientation"];
        FontStrokeCheck.Content = _loc["window.font_stroke"];
        BackgroundTransparencyCheck.Content = _loc["window.background_transparency"];
        AutoColorCheck.Content = _loc["window.auto_color"];
        AutoFontColorCheck.Content = _loc["window.auto_font_color"];
        AutoBackgroundColorCheck.Content = _loc["window.auto_background_color"];
        MinSizeLabel.Text = _loc["window.min_size"];
        MaxSizeLabel.Text = _loc["window.max_size"];
        SnapshotHoldLabel.Text = _loc["window.snapshot_hold"];
        DarkLabel.Text = _loc["window.dark"];
        DarkFontClearButton.Content = _loc["window.dark_font_clear"];
        LayerLabel.Text = _loc["window.layer"];
        LayerBottomCheck.Content = _loc["window.layer_bottom"];
        LayerRightCheck.Content = _loc["window.layer_right"];
        WindowGeneralLabel.Text = _loc["window.general"];
        TopOnlyWhileTranslatingCheck.Content = _loc["window.top_only_translating"];
        IgnoreEmptyCheck.Content = _loc["window.ignore_empty"];
        HideAlsoTranslatesCheck.Content = _loc["window.hide_also_translates"];
        DisplayMemoryLabel.Text = _loc["window.display_memory"];
        DisplayMemoryCheck.Content = _loc["window.display_memory_enable"];
        DisplayMemoryCountLabel.Text = _loc["window.display_memory_count"];
        DisplayMemoryTimeLabel.Text = _loc["window.display_memory_time"];
        DisplayMemoryHelp.Text = _loc["window.display_memory_help"];

        CollectionCheckAll.Content = _loc["collection.check_all"];
        CollectionUncheckAll.Content = _loc["collection.uncheck_all"];
        CollectionRefresh.Content = _loc["collection.refresh"];
        CollectionDatabaseCheck.Content = _loc["collection.database_mode"];
        CollectionIgnoreCaseCheck.Content = _loc["collection.ignore_case"];

        BridgeCheck.Content = _loc["translation.bridge"];
        FallbackCheck.Content = _loc["translation.fallback"];
        ApiLabel.Text = _loc["translation.api"];
        ApiAddButton.Content = _loc["translation.api_add"];
        ApiRemoveButton.Content = _loc["translation.api_remove"];
        ApiSameCodesCheck.Content = _loc["translation.api_same_codes"];
        LlmLabel.Text = _loc["translation.llm"];
        LlmInstructionBox.Watermark = _loc["translation.llm_instruction"];
        LlmModelBox.Watermark = _loc["translation.llm_model"];
        LlmNoDefaultInstructionCheck.Content = _loc["translation.llm_no_default"];
        LlmStandardRadio.Content = _loc["translation.llm_standard"];
        LlmEconomyRadio.Content = _loc["translation.llm_economy"];
        LlmCustomRadio.Content = _loc["translation.llm_custom"];
        LlmTemperatureLabel.Text = _loc["translation.llm_temperature"];
        LlmThinkingLabel.Text = _loc["translation.llm_thinking"];
        LlmMaxOutputLabel.Text = _loc["translation.llm_max_output"];
        ClipboardLabel.Text = _loc["translation.clipboard"];
        ClipboardTranslationCheck.Content = _loc["translation.clipboard_use"];
        ClipboardShowOriginalCheck.Content = _loc["translation.clipboard_original"];
        ClipboardShowTranslatingCheck.Content = _loc["translation.clipboard_translating"];
        ClipboardWriteLabel.Text = _loc["translation.clipboard_write"];
        ClipboardFormatTranslated.Content = _loc["translation.clipboard_translated_only"];
        ClipboardFormatBoth.Content = _loc["translation.clipboard_both"];
        ClipboardFormatRecognized.Content = _loc["translation.clipboard_recognized_only"];

        PreferCloudOneShotCheck.Content = _loc["ocr.prefer_cloud_one_shot"];
        CloudLimitLabel.Text = _loc["ocr.cloud_limit"];
        OcrHelp1.Text = _loc["ocr.help1"];
        OcrHelp2.Text = _loc["ocr.help2"];
        OcrHelp3.Text = _loc["ocr.help3"];

        DictionaryPassesLabel.Text = _loc["dictionary.passes"];
        DictionaryHelp.Text = _loc["dictionary.help"];
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ligações
    // ─────────────────────────────────────────────────────────────────────────

    private void WireEvents()
    {
        // RF-523 — o grupo aninhado só existe enquanto a caixa mestre está marcada.
        AutoColorCheck.IsCheckedChanged += (_, _) => RefreshDependencies();
        DisplayMemoryCheck.IsCheckedChanged += (_, _) => RefreshDependencies();
        CollectionDatabaseCheck.IsCheckedChanged += (_, _) => RefreshDependencies();
        ApiSameCodesCheck.IsCheckedChanged += (_, _) => RefreshDependencies();

        // RF-524 — alterar um ajusta o outro.
        MinSizeBox.ValueChanged += (_, _) => ClampFontSizes(movedMinimum: true);
        MaxSizeBox.ValueChanged += (_, _) => ClampFontSizes(movedMinimum: false);

        // RF-525 — trocar o preset atualiza os três controles imediatamente.
        LlmStandardRadio.IsCheckedChanged += (_, _) => OnPresetChosen(LlmPreset.Standard);
        LlmEconomyRadio.IsCheckedChanged += (_, _) => OnPresetChosen(LlmPreset.Economy);
        LlmCustomRadio.IsCheckedChanged += (_, _) => OnPresetChosen(LlmPreset.Custom);

        // RF-526 — os rótulos dos três controles.
        LlmTemperatureSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(Slider.Value)) RefreshSliderLabels();
        };
        LlmThinkingSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(Slider.Value)) RefreshSliderLabels();
        };
        LlmMaxOutputSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(Slider.Value)) RefreshSliderLabels();
        };

        // RF-527 — trocar de preset salva o anterior ANTES de carregar o novo.
        ApiList.SelectionChanged += (_, _) => OnPresetSelectionChanged();
        ApiAddButton.Click += (_, _) => AddPreset();
        ApiRemoveButton.Click += (_, _) => RemovePreset();

        CollectionCheckAll.Click += (_, _) => SetAllCollectionFiles(true);
        CollectionUncheckAll.Click += (_, _) => SetAllCollectionFiles(false);
        CollectionRefresh.Click += (_, _) => LoadCollectionFiles();
        CollectionList.SelectionChanged += (_, _) => ShowCollectionInfo();

        DarkFontClearButton.Click += (_, _) => DarkFontBox.SelectedItem = null;

        SelectionRestoreButton.Click += (_, _) =>
        {
            var padrao = AdvancedOptions.Defaults();
            _options.SelectionBackground = padrao.SelectionBackground;
            _options.SelectionHighlight = padrao.SelectionHighlight;
            RefreshColorSamples();
        };

        ApplyButton.Click += (_, _) => Apply();
        RestoreButton.Click += (_, _) => RestoreDefaults();
        CloseButton.Click += (_, _) => Close();
    }

    /// <summary>
    /// RF-523 e as demais dependências entre controles.
    ///
    /// Todas juntas, num lugar só: a regra é sempre "este grupo existe enquanto aquela caixa
    /// estiver marcada", e espalhá-la faria cada caixa ter de lembrar dos seus dependentes.
    /// </summary>
    private void RefreshDependencies()
    {
        AutoColorGroup.IsEnabled = AutoColorCheck.IsChecked == true;
        DisplayMemoryGroup.IsEnabled = DisplayMemoryCheck.IsChecked == true;
        CollectionDatabaseGroup.IsEnabled = CollectionDatabaseCheck.IsChecked == true;

        // V.3 — "usar os mesmos códigos do tradutor web" desabilita origem e destino.
        bool ownCodes = ApiSameCodesCheck.IsChecked != true;
        ApiSourceBox.IsEnabled = ownCodes;
        ApiTargetBox.IsEnabled = ownCodes;

        // RF-303 — preset de arquivo: nome somente-leitura e remover desabilitado (RF-528).
        bool fromFile = _selectedPreset?.IsFromFile == true;
        ApiNameBox.IsReadOnly = fromFile;
        ApiRemoveButton.IsEnabled = _selectedPreset is not null && !fromFile;

        // RF-525 — os três controles só são editáveis no preset personalizado.
        bool custom = LlmCustomRadio.IsChecked == true;
        LlmTemperatureSlider.IsEnabled = custom;
        LlmThinkingSlider.IsEnabled = custom;
        LlmMaxOutputSlider.IsEnabled = custom;
    }

    /// <summary>
    /// RF-524 — O mínimo nunca fica acima do máximo, e vice-versa: alterar um ajusta o
    /// OUTRO. Qual dos dois cede depende de qual o usuário mexeu — empurrar de volta o
    /// campo que ele acabou de digitar seria desfazer a digitação diante dele.
    /// </summary>
    private void ClampFontSizes(bool movedMinimum)
    {
        if (_loading) return;

        double min = MinSizeBox.Value.HasValue ? (double)MinSizeBox.Value.Value : 0;
        double max = MaxSizeBox.Value.HasValue ? (double)MaxSizeBox.Value.Value : 0;
        if (min <= max) return;

        _loading = true;
        if (movedMinimum) MaxSizeBox.Value = (decimal)min;
        else MinSizeBox.Value = (decimal)max;
        _loading = false;
    }

    /// <summary>
    /// RF-525 — Trocar o preset atualiza IMEDIATAMENTE os três controles com os valores do
    /// preset e os desabilita; "personalizado" os habilita MANTENDO os valores atuais.
    /// </summary>
    private void OnPresetChosen(LlmPreset preset)
    {
        if (_loading) return;

        var radio = preset switch
        {
            LlmPreset.Standard => LlmStandardRadio,
            LlmPreset.Economy => LlmEconomyRadio,
            _ => LlmCustomRadio,
        };
        if (radio.IsChecked != true) return;

        if (preset != LlmPreset.Custom)
        {
            _options.ApplyPreset(preset);

            _loading = true;
            LlmTemperatureSlider.Value = _options.LlmTemperature;
            LlmThinkingSlider.Value = _options.LlmThinking;
            LlmMaxOutputSlider.Value = _options.LlmMaxOutput;
            _loading = false;
        }
        else
        {
            _options.LlmPreset = LlmPreset.Custom;
        }

        RefreshSliderLabels();
        RefreshDependencies();
    }

    /// <summary>RF-526 — Os três rótulos, cada um no seu formato.</summary>
    private void RefreshSliderLabels()
    {
        LlmTemperatureValue.Text =
            AdvancedLabels.Temperature((int)LlmTemperatureSlider.Value);
        LlmThinkingValue.Text =
            _loc[AdvancedLabels.ThinkingKey((int)LlmThinkingSlider.Value)];
        LlmMaxOutputValue.Text =
            AdvancedLabels.MaxOutput((int)LlmMaxOutputSlider.Value);
    }

    private void RefreshColorSamples()
    {
        SelectionBackgroundSample.Background = Brush(_options.SelectionBackground);
        SelectionHighlightSample.Background = Brush(_options.SelectionHighlight);
    }

    private static IBrush Brush(Gort.Core.Model.Rgba c)
        => new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));

    // ─────────────────────────────────────────────────────────────────────────
    // Carregar e recolher
    // ─────────────────────────────────────────────────────────────────────────

    private void LoadFromOptions()
    {
        _loading = true;
        var o = _options;

        TrayModeCheck.IsChecked = o.TrayMode;
        RightToLeftCheck.IsChecked = o.RightToLeft;
        RemoteAlwaysOnTopCheck.IsChecked = o.RemoteAlwaysOnTop;
        MouseCompatibleCheck.IsChecked = o.MouseFollowCompatible;
        MouseOnlyCheck.IsChecked = o.MouseFollowOnly;
        CaptureBorderCheck.IsChecked = o.ShowCaptureBorder;

        AutoFontSizeCheck.IsChecked = o.AutoFontSize;
        MergeLinesCheck.IsChecked = o.MergeLines;
        PreserveOrientationCheck.IsChecked = o.PreserveOrientation;
        FontStrokeCheck.IsChecked = o.FontStroke;
        BackgroundTransparencyCheck.IsChecked = o.UseBackgroundTransparency;
        AutoColorCheck.IsChecked = o.AutoColor;
        AutoFontColorCheck.IsChecked = o.AutoFontColor;
        AutoBackgroundColorCheck.IsChecked = o.AutoBackgroundColor;
        MinSizeBox.Value = (decimal)o.AutoFontSizeMin;
        MaxSizeBox.Value = (decimal)o.AutoFontSizeMax;
        SnapshotHoldBox.Value = o.OneShotHoldSeconds;

        DarkFontBox.ItemsSource = _fonts().ToList();
        DarkFontBox.SelectedItem = o.DarkModeFont.Length == 0 ? null : o.DarkModeFont;

        LayerBottomCheck.IsChecked = o.LayerAlignBottom;
        LayerRightCheck.IsChecked = o.LayerAlignRight;
        TopOnlyWhileTranslatingCheck.IsChecked = o.AlwaysOnTopOnlyWhileTranslating;
        IgnoreEmptyCheck.IsChecked = o.IgnoreEmptyTranslation;
        HideAlsoTranslatesCheck.IsChecked = o.HideAlsoTranslates;
        DisplayMemoryCheck.IsChecked = o.DisplayMemoryEnabled;
        DisplayMemoryCountBox.Value = o.DisplayMemoryCount;
        DisplayMemoryTimeBox.Value = o.DisplayMemoryLifetimeSeconds;

        CollectionDatabaseCheck.IsChecked = o.CollectionMode == CollectionLookupMode.Database;
        CollectionIgnoreCaseCheck.IsChecked = o.CollectionIgnoreCase;

        BridgeCheck.IsChecked = o.BridgeTranslation;
        FallbackCheck.IsChecked = o.FallbackTranslator;

        LlmInstructionBox.Text = o.LlmCustomInstruction;
        LlmModelBox.Text = o.LlmCustomModel;
        LlmNoDefaultInstructionCheck.IsChecked = o.LlmDisableDefaultInstruction;
        LlmStandardRadio.IsChecked = o.LlmPreset == LlmPreset.Standard;
        LlmEconomyRadio.IsChecked = o.LlmPreset == LlmPreset.Economy;
        LlmCustomRadio.IsChecked = o.LlmPreset == LlmPreset.Custom;
        LlmTemperatureSlider.Value = o.LlmTemperature;
        LlmThinkingSlider.Value = o.LlmThinking;
        LlmMaxOutputSlider.Value = o.LlmMaxOutput;

        ClipboardTranslationCheck.IsChecked = o.ClipboardTranslation;
        ClipboardShowOriginalCheck.IsChecked = o.ClipboardShowOriginal;
        ClipboardShowTranslatingCheck.IsChecked = o.ClipboardShowTranslating;
        ClipboardFormatTranslated.IsChecked =
            _copyFormat == Gort.Core.Configuration.ClipboardCopyFormat.Translation;
        ClipboardFormatBoth.IsChecked =
            _copyFormat == Gort.Core.Configuration.ClipboardCopyFormat.Both;
        ClipboardFormatRecognized.IsChecked =
            _copyFormat == Gort.Core.Configuration.ClipboardCopyFormat.Ocr;

        PreferCloudOneShotCheck.IsChecked = o.PreferCloudOcrOneShot;
        CloudLimitBox.Value = o.CloudOcrMonthlyLimit;
        DictionaryPassesBox.Value = o.DictionaryExtraPasses;

        _loading = false;

        RefreshColorSamples();
        RefreshSliderLabels();
        BuildShortcutRows();
        LoadCollectionFiles();
        LoadPresetList();
        RefreshDependencies();
    }

    private void CollectIntoOptions()
    {
        var o = _options;

        o.TrayMode = TrayModeCheck.IsChecked == true;
        o.RightToLeft = RightToLeftCheck.IsChecked == true;
        o.RemoteAlwaysOnTop = RemoteAlwaysOnTopCheck.IsChecked == true;
        o.MouseFollowCompatible = MouseCompatibleCheck.IsChecked == true;
        o.MouseFollowOnly = MouseOnlyCheck.IsChecked == true;
        o.ShowCaptureBorder = CaptureBorderCheck.IsChecked == true;

        o.AutoFontSize = AutoFontSizeCheck.IsChecked == true;
        o.MergeLines = MergeLinesCheck.IsChecked == true;
        o.PreserveOrientation = PreserveOrientationCheck.IsChecked == true;
        o.FontStroke = FontStrokeCheck.IsChecked == true;
        o.UseBackgroundTransparency = BackgroundTransparencyCheck.IsChecked == true;
        o.AutoColor = AutoColorCheck.IsChecked == true;
        o.AutoFontColor = AutoFontColorCheck.IsChecked == true;
        o.AutoBackgroundColor = AutoBackgroundColorCheck.IsChecked == true;
        o.AutoFontSizeMin = (double)(MinSizeBox.Value ?? (decimal)o.AutoFontSizeMin);
        o.AutoFontSizeMax = (double)(MaxSizeBox.Value ?? (decimal)o.AutoFontSizeMax);
        o.OneShotHoldSeconds = (int)(SnapshotHoldBox.Value ?? o.OneShotHoldSeconds);

        o.DarkModeFont = DarkFontBox.SelectedItem as string ?? "";
        o.LayerAlignBottom = LayerBottomCheck.IsChecked == true;
        o.LayerAlignRight = LayerRightCheck.IsChecked == true;
        o.AlwaysOnTopOnlyWhileTranslating = TopOnlyWhileTranslatingCheck.IsChecked == true;
        o.IgnoreEmptyTranslation = IgnoreEmptyCheck.IsChecked == true;
        o.HideAlsoTranslates = HideAlsoTranslatesCheck.IsChecked == true;
        o.DisplayMemoryEnabled = DisplayMemoryCheck.IsChecked == true;
        o.DisplayMemoryCount = (int)(DisplayMemoryCountBox.Value ?? o.DisplayMemoryCount);
        o.DisplayMemoryLifetimeSeconds =
            (int)(DisplayMemoryTimeBox.Value ?? o.DisplayMemoryLifetimeSeconds);

        o.CollectionMode = CollectionDatabaseCheck.IsChecked == true
            ? CollectionLookupMode.Database : CollectionLookupMode.Exact;
        o.CollectionIgnoreCase = CollectionIgnoreCaseCheck.IsChecked == true;
        o.CollectionFiles = CheckedCollectionFiles();

        o.BridgeTranslation = BridgeCheck.IsChecked == true;
        o.FallbackTranslator = FallbackCheck.IsChecked == true;

        o.LlmCustomInstruction = LlmInstructionBox.Text ?? "";
        o.LlmCustomModel = LlmModelBox.Text ?? "";
        o.LlmDisableDefaultInstruction = LlmNoDefaultInstructionCheck.IsChecked == true;
        o.LlmPreset = LlmEconomyRadio.IsChecked == true ? LlmPreset.Economy
            : LlmCustomRadio.IsChecked == true ? LlmPreset.Custom
            : LlmPreset.Standard;
        o.LlmTemperature = (int)LlmTemperatureSlider.Value;
        o.LlmThinking = (int)LlmThinkingSlider.Value;
        o.LlmMaxOutput = (int)LlmMaxOutputSlider.Value;

        o.ClipboardTranslation = ClipboardTranslationCheck.IsChecked == true;
        o.ClipboardShowOriginal = ClipboardShowOriginalCheck.IsChecked == true;
        o.ClipboardShowTranslating = ClipboardShowTranslatingCheck.IsChecked == true;

        _copyFormat = ClipboardFormatTranslated.IsChecked == true
            ? Gort.Core.Configuration.ClipboardCopyFormat.Translation
            : ClipboardFormatBoth.IsChecked == true
                ? Gort.Core.Configuration.ClipboardCopyFormat.Both
                : Gort.Core.Configuration.ClipboardCopyFormat.Ocr;

        o.PreferCloudOcrOneShot = PreferCloudOneShotCheck.IsChecked == true;
        o.CloudOcrMonthlyLimit = (int)(CloudLimitBox.Value ?? o.CloudOcrMonthlyLimit);
        o.DictionaryExtraPasses = (int)(DictionaryPassesBox.Value ?? o.DictionaryExtraPasses);

        o.Normalize();
    }

    /// <summary>
    /// RF-530 — Aplicar grava tudo e reaplica ao programa. RF-527 — o preset em edição é
    /// salvo antes, senão a última digitação se perderia sem aviso.
    /// </summary>
    private void Apply()
    {
        SaveSelectedPreset();
        CollectIntoOptions();
        Applied?.Invoke(_options, _copyFormat);
        StatusText.Text = _loc["advanced.applied"];
    }

    /// <summary>
    /// RF-530 — Restaurar padrões, COM CONFIRMAÇÃO.
    ///
    /// RF-532 — a direção do texto vem da propriedade do idioma de destino, não de uma lista
    /// embutida. Restaurar não grava: o usuário ainda pode fechar sem aplicar.
    /// </summary>
    private async void RestoreDefaults()
    {
        if (!await Confirm(_loc["advanced.restore_confirm"])) return;

        _options = AdvancedOptions.Defaults(_targetLanguage);
        LoadFromOptions();
        StatusText.Text = _loc["advanced.restored"];
    }

    /// <summary>Confirmação simples, sem depender de caixa de diálogo do sistema.</summary>
    private async Task<bool> Confirm(string message)
    {
        var yes = new Button { Content = _loc["advanced.apply"], Padding = new Thickness(16, 6) };
        var no = new Button { Content = _loc["advanced.close"], Padding = new Thickness(16, 6) };

        var dialog = new Window
        {
            Title = _loc["advanced.title"],
            Width = 420,
            SizeToContent = SizeToContent.Height,
            Background = new SolidColorBrush(Color.Parse("#F4F4F6")),
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(18),
                Spacing = 14,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { yes, no },
                    },
                },
            },
        };

        bool confirmed = false;
        yes.Click += (_, _) => { confirmed = true; dialog.Close(); };
        no.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
        return confirmed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Aba de atalhos avançados (RF-447)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// V.3 — Quatro blocos de "abrir perfil", um de transparência forçada e sete de troca
    /// de serviço. As quantidades vêm de P-116 e P-117, não de números soltos aqui.
    /// </summary>
    private void BuildShortcutRows()
    {
        ShortcutPanel.Children.Clear();

        // P-119 — quantos perfis podem ter atalho dedicado.
        for (int i = 0; i < P.ProfileShortcutCount; i++)
            ShortcutPanel.Children.Add(ProfileShortcutRow(i));

        ShortcutPanel.Children.Add(ShortcutRow(ShortcutAction.ToggleForcedTransparency, 0));

        ShortcutPanel.Children.Add(new TextBlock
        {
            Text = _loc["shortcut.SwitchTranslationService"],
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 12, 0, 2),
        });

        // RF-447 — um bloco por serviço que o catálogo marca como trocável por atalho.
        // A especificação nomeia sete; QUAIS são eles é dado, não um número aqui.
        for (int i = 0; i < _switchableServices.Count; i++)
        {
            ShortcutPanel.Children.Add(ShortcutRow(
                ShortcutAction.SwitchTranslationService, i,
                _loc[_switchableServices[i].NameKey]));
        }
    }

    private Control ProfileShortcutRow(int index)
    {
        var row = (StackPanel)ShortcutRow(ShortcutAction.OpenProfile, index);

        var config = _shortcuts.Find(ShortcutAction.OpenProfile, index);
        var file = new TextBox { Text = config?.Data ?? "", Width = 150, IsReadOnly = true };

        var choose = new Button { Content = "…", Padding = new Thickness(10, 4) };
        choose.Click += async (_, _) =>
        {
            var picked = await StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions { AllowMultiple = false });

            string? path = picked.FirstOrDefault()?.Path.LocalPath;
            if (path is null) return;

            file.Text = path;
            EnsureShortcut(ShortcutAction.OpenProfile, index).Data = path;
        };

        var clear = new Button { Content = "×", Padding = new Thickness(10, 4) };
        clear.Click += (_, _) =>
        {
            file.Text = "";
            EnsureShortcut(ShortcutAction.OpenProfile, index).Data = null;
        };

        row.Children.Add(file);
        row.Children.Add(choose);
        row.Children.Add(clear);
        return row;
    }

    private Control ShortcutRow(ShortcutAction action, int index, string? suffix = null)
    {
        var config = _shortcuts.Find(action, index);
        string name = _loc[$"shortcut.{action}"];

        // O rótulo do bloco de serviço é o NOME DO SERVIÇO: repetir "Trocar serviço" em
        // cada uma das sete linhas gasta a largura toda e não distingue uma da outra.
        var label = new TextBlock
        {
            Text = suffix ?? (index == 0 ? name : $"{name} {index + 1}"),
            Width = 250,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var field = new TextBox { Text = config?.ToString() ?? "", Width = 150, IsReadOnly = true };

        // RF-514 — enquanto o campo tem foco, os atalhos ficam inertes. Aqui eles já estão
        // suspensos pela janela inteira; a captura mesmo assim reinicia o acumulador.
        field.GotFocus += (_, _) => _capturing.Clear();
        field.KeyDown += (_, e) => CaptureShortcut(action, index, field, e);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 3),
            Children = { label, field },
        };
    }

    private Gort.Core.Model.ShortcutConfig EnsureShortcut(ShortcutAction action, int index)
    {
        var existing = _shortcuts.Find(action, index);
        if (existing is not null) return existing;

        return _shortcuts.Set(action, Array.Empty<string>(), index);
    }

    private void CaptureShortcut(ShortcutAction action, int index, TextBox field, KeyEventArgs e)
    {
        e.Handled = true;

        if (e.Key is Key.Escape or Key.Back)
        {
            _capturing.Clear();
            _shortcuts.Clear(action, index);
            field.Text = "";
            return;
        }

        string name = KeyNames.Normalize(e.Key.ToString());
        if (_capturing.Contains(name)) return;
        if (_capturing.Count >= P.MaxShortcutKeys) _capturing.Clear();

        _capturing.Add(name);
        _shortcuts.Set(action, _capturing, index);
        field.Text = _shortcuts.Find(action, index)?.ToString() ?? "";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Aba da coletânea (RF-215 a RF-218)
    // ─────────────────────────────────────────────────────────────────────────

    private void LoadCollectionFiles()
    {
        var boxes = new List<CheckBox>();

        if (Directory.Exists(_collectionDirectory))
        {
            foreach (string file in Directory.GetFiles(_collectionDirectory, "*.txt")
                                             .OrderBy(f => f))
            {
                boxes.Add(new CheckBox
                {
                    Content = Path.GetFileName(file),
                    Tag = file,
                    IsChecked = _options.CollectionFiles.Contains(file),
                });
            }
        }

        CollectionList.ItemsSource = boxes;
        CollectionInfo.Text = boxes.Count == 0 ? _loc["collection.empty"] : "";
    }

    private IEnumerable<CheckBox> CollectionBoxes()
        => (CollectionList.ItemsSource as IEnumerable<CheckBox>) ?? Array.Empty<CheckBox>();

    private void SetAllCollectionFiles(bool value)
    {
        foreach (var box in CollectionBoxes()) box.IsChecked = value;
    }

    private List<string> CheckedCollectionFiles()
        => CollectionBoxes().Where(b => b.IsChecked == true)
                            .Select(b => (string)b.Tag!)
                            .ToList();

    /// <summary>RF-217 — O painel de informação do arquivo selecionado.</summary>
    private void ShowCollectionInfo()
    {
        if (CollectionList.SelectedItem is not CheckBox box) return;

        string file = (string)box.Tag!;
        try
        {
            string info = TranslationCollection.ReadInfo(file);
            int pairs = PairFile.Load(file).Count;

            CollectionInfo.Text = info.Length > 0
                ? info + "\n" + _loc.Format("collection.info", Path.GetFileName(file), pairs)
                : _loc.Format("collection.info", Path.GetFileName(file), pairs);
        }
        catch
        {
            CollectionInfo.Text = _loc.Format("collection.info_unreadable", Path.GetFileName(file));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Presets de API personalizada (RF-527 a RF-529)
    // ─────────────────────────────────────────────────────────────────────────

    private void LoadPresetList()
    {
        var previous = _selectedPreset;
        _selectedPreset = null;                 // evita salvar por cima ao recarregar

        ApiList.ItemsSource = _presets.Presets
            .Select(p => new ListBoxItem { Content = p.DisplayName, Tag = p })
            .ToList();

        var item = ApiItems().FirstOrDefault(i => ReferenceEquals(i.Tag, previous))
                   ?? ApiItems().FirstOrDefault();
        ApiList.SelectedItem = item;
    }

    private IEnumerable<ListBoxItem> ApiItems()
        => (ApiList.ItemsSource as IEnumerable<ListBoxItem>) ?? Array.Empty<ListBoxItem>();

    /// <summary>
    /// RF-527 — Selecionar um preset SALVA as alterações do anterior antes de carregar o
    /// novo. Sem isso, trocar de item na lista descartaria a digitação em silêncio, que é o
    /// tipo de perda que o usuário só descobre muito depois.
    /// </summary>
    private void OnPresetSelectionChanged()
    {
        SaveSelectedPreset();

        _selectedPreset = (ApiList.SelectedItem as ListBoxItem)?.Tag as ApiPreset;
        LoadSelectedPreset();
    }

    private void SaveSelectedPreset()
    {
        if (_selectedPreset is null || _loading) return;

        var p = _selectedPreset;

        // RF-303 — o nome de um preset de arquivo não é editável, então não é recolhido.
        if (!p.IsFromFile)
        {
            string typed = ApiNameBox.Text ?? "";
            // RF-529 — nome duplicado ganha sufixo "(n)" AO SALVAR.
            if (typed != p.Name) _presets.Rename(p, typed);
        }

        p.Url = ApiUrlBox.Text ?? "";
        p.Headers = ApiHeadersBox.Text ?? "";
        p.RequestTemplate = ApiRequestBox.Text ?? "";
        p.ResponseTemplate = ApiResponseBox.Text ?? "";
        p.SameLanguageCodesAsWeb = ApiSameCodesCheck.IsChecked == true;
        p.SourceCode = ApiSourceBox.Text ?? "";
        p.TargetCode = ApiTargetBox.Text ?? "";
    }

    private void LoadSelectedPreset()
    {
        _loading = true;
        var p = _selectedPreset;

        ApiNameBox.Text = p?.Name ?? "";
        ApiUrlBox.Text = p?.Url ?? "";
        ApiHeadersBox.Text = p?.Headers ?? "";
        ApiRequestBox.Text = p?.RequestTemplate ?? "";
        ApiResponseBox.Text = p?.ResponseTemplate ?? "";
        ApiSameCodesCheck.IsChecked = p?.SameLanguageCodesAsWeb ?? true;
        ApiSourceBox.Text = p?.SourceCode ?? "";
        ApiTargetBox.Text = p?.TargetCode ?? "";
        _loading = false;

        RefreshDependencies();
    }

    private void AddPreset()
    {
        SaveSelectedPreset();

        // RF-529 — o nome nasce único; a loja de presets é quem sabe disso.
        var novo = _presets.Add(_loc["translation.api"]);
        LoadPresetList();
        ApiList.SelectedItem = ApiItems().FirstOrDefault(i => ReferenceEquals(i.Tag, novo));
    }

    /// <summary>RF-303 / RF-528 — Presets de arquivo não são removidos pela interface.</summary>
    private void RemovePreset()
    {
        if (_selectedPreset is null) return;
        if (!_presets.Remove(_selectedPreset)) return;

        _selectedPreset = null;
        LoadPresetList();
    }
}
