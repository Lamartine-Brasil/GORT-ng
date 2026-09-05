using Gort.Core.Caching;
using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Configuration;

/// <summary>RF-281 — Presets de geração do modelo de linguagem.</summary>
public enum LlmPreset { Standard, Economy, Custom }

/// <summary>
/// RF-031 / RF-032 — Opções avançadas, em arquivo separado do perfil.
///
/// São GLOBAIS: não mudam quando o usuário troca de perfil, porque são preferências da
/// PESSOA, não do jogo.
/// RF-033 — Se o arquivo estiver ausente ou vazio, todos os valores assumem seus padrões e
/// o arquivo é criado.
/// </summary>
public sealed class AdvancedOptions
{
    public const int CurrentSchemaVersion = 1;

    // ── Aplicativo ────────────────────────────────────────────────────────────
    public bool TrayMode { get; set; }
    /// <summary>RF-324 — Ligada automaticamente quando o idioma de destino declara a propriedade.</summary>
    public bool RightToLeft { get; set; }
    /// <summary>RF-520 — Controle remoto sempre no topo.</summary>
    public bool RemoteAlwaysOnTop { get; set; }

    // ── Área que segue o mouse ────────────────────────────────────────────────
    /// <summary>RF-460 — Modo compatível: move a área rápida em vez de criar uma dedicada.</summary>
    public bool MouseFollowCompatible { get; set; }
    /// <summary>RF-459 — Usar somente a área que segue o mouse. É o padrão.</summary>
    public bool MouseFollowOnly { get; set; } = true;

    // ── Captura de janela ─────────────────────────────────────────────────────
    /// <summary>RF-091 — Exibir a borda amarela que o sistema desenha em janelas capturadas.</summary>
    public bool ShowCaptureBorder { get; set; }

    // ── Cores da camada de seleção ────────────────────────────────────────────
    /// <summary>RF-049 — Cor de destaque do retângulo em construção. IV.12: preto.</summary>
    public Rgba SelectionHighlight { get; set; } = Rgba.Black;
    /// <summary>RF-049 / RF-050 — Cor de fundo; o alfa dela alimenta P-10. IV.12: branco.</summary>
    public Rgba SelectionBackground { get; set; } = Rgba.White;

    // ── Sobreposição ──────────────────────────────────────────────────────────
    /// <summary>RF-360 — Tamanho automático de fonte.</summary>
    public bool AutoFontSize { get; set; }
    /// <summary>RF-158 — Fusão automática de linhas em blocos.</summary>
    public bool MergeLines { get; set; }
    /// <summary>RF-375 — Preservar a direção do original (vertical).</summary>
    public bool PreserveOrientation { get; set; }
    /// <summary>RF-336 / RF-392 — Usar contorno de fonte.</summary>
    public bool FontStroke { get; set; }
    /// <summary>RF-378 — Usar a transparência do fundo.</summary>
    public bool UseBackgroundTransparency { get; set; }
    /// <summary>RF-413 — Caixa mestre da cor automática.</summary>
    public bool AutoColor { get; set; } = true;
    public bool AutoFontColor { get; set; } = true;
    public bool AutoBackgroundColor { get; set; } = true;
    /// <summary>P-129 / P-130.</summary>
    public double AutoFontSizeMin { get; set; } = P.AutoFontSizeMinDefault;
    public double AutoFontSizeMax { get; set; } = P.AutoFontSizeMaxDefault;
    /// <summary>P-131 — Permanência do resultado após um ciclo pontual, em segundos.</summary>
    public int OneShotHoldSeconds { get; set; } = P.OneShotResultHoldSecondsDefault;

    // ── Modo escuro e camada ──────────────────────────────────────────────────
    /// <summary>RF-330 — Fonte própria do modo escuro; vazio cai para a fonte do sistema.</summary>
    public string DarkModeFont { get; set; } = "";
    /// <summary>RF-341 — Alinhamento vertical na base.</summary>
    public bool LayerAlignBottom { get; set; }
    /// <summary>RF-341 — Alinhamento horizontal à direita.</summary>
    public bool LayerAlignRight { get; set; }

    // ── Geral ─────────────────────────────────────────────────────────────────
    /// <summary>RF-320 — Sempre no topo apenas durante a tradução.</summary>
    public bool AlwaysOnTopOnlyWhileTranslating { get; set; }
    /// <summary>RF-240 — Ignorar tradução vazia.</summary>
    public bool IgnoreEmptyTranslation { get; set; }
    /// <summary>RF-322 — O atalho de ocultar também inicia/para a tradução.</summary>
    public bool HideAlsoTranslates { get; set; }

    // ── Memória de exibição ───────────────────────────────────────────────────
    public bool DisplayMemoryEnabled { get; set; }
    public int DisplayMemoryCount { get; set; } = P.DisplayMemoryCountDefault;
    public int DisplayMemoryLifetimeSeconds { get; set; } = P.DisplayMemoryLifetimeSecondsDefault;

    // ── Coletânea de tradução ─────────────────────────────────────────────────
    /// <summary>RF-216 — Lista de arquivos ativos, persistida aqui.</summary>
    public List<string> CollectionFiles { get; set; } = new();
    public CollectionLookupMode CollectionMode { get; set; } = CollectionLookupMode.Database;
    public bool CollectionIgnoreCase { get; set; } = true;

    // ── Tradução ──────────────────────────────────────────────────────────────
    /// <summary>RF-239 — Tradução ponte.</summary>
    public bool BridgeTranslation { get; set; }
    /// <summary>RF-267 — Usar tradutor alternativo em caso de erro.</summary>
    public bool FallbackTranslator { get; set; } = true;

    /// <summary>RF-292 — API personalizada.</summary>
    public string CustomApiUrl { get; set; } = "http://localhost:8080/translator";
    public bool CustomApiSameLanguageCodesAsWeb { get; set; } = true;
    public string CustomApiSource { get; set; } = "en";
    public string CustomApiTarget { get; set; } = "pt-BR";

    /// <summary>RF-275 — Instrução própria do usuário para o modelo de linguagem.</summary>
    public string LlmCustomInstruction { get; set; } = "";
    /// <summary>RF-275 — Não enviar a instrução padrão junto.</summary>
    public bool LlmDisableDefaultInstruction { get; set; }
    /// <summary>RF-278 — Nome do modelo quando "personalizado".</summary>
    public string LlmCustomModel { get; set; } = "gemini-2.0-flash";
    public LlmPreset LlmPreset { get; set; } = LlmPreset.Standard;
    public int LlmTemperature { get; set; } = P.LlmTemperatureDefault;
    public int LlmThinking { get; set; } = P.LlmThinkingDefault;
    public int LlmMaxOutput { get; set; } = P.LlmMaxOutputDefault;

    // ── Área de transferência ─────────────────────────────────────────────────
    /// <summary>RF-464 — Monitorar a área de transferência.</summary>
    public bool ClipboardTranslation { get; set; }
    /// <summary>RF-470 — Anexar o texto original ao resultado.</summary>
    public bool ClipboardShowOriginal { get; set; }
    /// <summary>RF-469 — Exibir "detectado — traduzindo".</summary>
    public bool ClipboardShowTranslating { get; set; }

    // ── OCR ───────────────────────────────────────────────────────────────────
    /// <summary>RF-123 — Priorizar o motor de nuvem em modo pontual.</summary>
    public bool PreferCloudOcrOneShot { get; set; }
    /// <summary>P-29 — Limite mensal do OCR de nuvem.</summary>
    public int CloudOcrMonthlyLimit { get; set; } = P.CloudOcrMonthlyLimit;

    // ── Dicionário ────────────────────────────────────────────────────────────
    /// <summary>RF-182 / P-46 — Passagens adicionais do dicionário.</summary>
    public int DictionaryExtraPasses { get; set; } = P.DictionaryExtraPassesDefault;

    /// <summary>RF-042 — Saturação; RF-524 — mínimo nunca acima do máximo, e vice-versa.</summary>
    public void Normalize()
    {
        DisplayMemoryCount = Math.Clamp(DisplayMemoryCount,
            P.DisplayMemoryCountMin, P.DisplayMemoryCountMax);
        DisplayMemoryLifetimeSeconds = Math.Clamp(DisplayMemoryLifetimeSeconds,
            0, P.DisplayMemoryLifetimeSecondsMax);

        AutoFontSizeMin = Math.Max(P.AutoFontSizeControlMin, AutoFontSizeMin);
        AutoFontSizeMax = Math.Max(P.AutoFontSizeControlMin, AutoFontSizeMax);
        // RF-524 — alterar um ajusta o outro automaticamente.
        if (AutoFontSizeMin > AutoFontSizeMax) AutoFontSizeMax = AutoFontSizeMin;

        OneShotHoldSeconds = Math.Max(0, OneShotHoldSeconds);
        CloudOcrMonthlyLimit = Math.Max(0, CloudOcrMonthlyLimit);
        DictionaryExtraPasses = Math.Clamp(DictionaryExtraPasses,
            P.DictionaryExtraPassesMin, P.DictionaryExtraPassesMax);

        LlmTemperature = Math.Clamp(LlmTemperature, P.LlmTemperatureMin, P.LlmTemperatureMax);
        LlmThinking = Math.Clamp(LlmThinking, P.LlmThinkingMin, P.LlmThinkingMax);
        LlmMaxOutput = Math.Clamp(LlmMaxOutput, P.LlmMaxOutputMin, P.LlmMaxOutputMax);
    }

    /// <summary>
    /// RF-281 — Aplica os valores de um preset. RF-525 — trocar o preset atualiza
    /// imediatamente os três controles; "personalizado" mantém os valores atuais.
    /// </summary>
    public void ApplyPreset(LlmPreset preset)
    {
        LlmPreset = preset;
        switch (preset)
        {
            case LlmPreset.Standard:
                LlmTemperature = P.LlmTemperatureDefault;
                LlmThinking = P.LlmThinkingDefault;
                LlmMaxOutput = P.LlmMaxOutputDefault;
                break;
            case LlmPreset.Economy:
                LlmTemperature = P.LlmTemperatureEconomy;
                LlmThinking = P.LlmThinkingEconomy;
                LlmMaxOutput = P.LlmMaxOutputEconomy;
                break;
            // P-153 — o preset personalizado nasce com os valores do padrão; depois disso o
            // usuário manda.
        }
    }

    public static AdvancedOptions Defaults() => new();

    /// <summary>
    /// RF-530 / RF-532 — Restaura os padrões.
    ///
    /// A direção do texto é DERIVADA da propriedade de direção do idioma de destino
    /// (RF-311), e não de uma lista de idiomas embutida — RF-567 proíbe o programa de
    /// comparar com identificadores de idioma. Acrescentar um idioma da direita para a
    /// esquerda passa a ser acrescentar uma linha em `data/languages.toml`.
    /// </summary>
    public static AdvancedOptions Defaults(Catalog.LanguageInfo? targetLanguage)
    {
        var options = new AdvancedOptions();
        if (targetLanguage is not null) options.RightToLeft = targetLanguage.RightToLeft;
        return options;
    }

    public static AdvancedOptions Load(string path, out TomlStore store)
    {
        store = TomlStore.Load(path);
        var o = new AdvancedOptions();
        try
        {
            Migrations.Migrate(store, CurrentSchemaVersion);
            o.Read(store);
        }
        catch
        {
            o = new AdvancedOptions();   // RF-024 / RF-033
        }
        o.Normalize();
        return o;
    }

    private void Read(TomlStore s)
    {
        TrayMode = s.GetBool("tray_mode", TrayMode);
        RightToLeft = s.GetBool("right_to_left", RightToLeft);
        RemoteAlwaysOnTop = s.GetBool("remote_always_on_top", RemoteAlwaysOnTop);

        MouseFollowCompatible = s.GetBool("mouse_follow_compatible", MouseFollowCompatible);
        MouseFollowOnly = s.GetBool("mouse_follow_only", MouseFollowOnly);
        ShowCaptureBorder = s.GetBool("show_capture_border", ShowCaptureBorder);

        SelectionHighlight = ReadColor(s, "selection_highlight", SelectionHighlight);
        SelectionBackground = ReadColor(s, "selection_background", SelectionBackground);

        AutoFontSize = s.GetBool("auto_font_size", AutoFontSize);
        MergeLines = s.GetBool("merge_lines", MergeLines);
        PreserveOrientation = s.GetBool("preserve_orientation", PreserveOrientation);
        FontStroke = s.GetBool("font_stroke", FontStroke);
        UseBackgroundTransparency = s.GetBool("use_background_transparency", UseBackgroundTransparency);
        AutoColor = s.GetBool("auto_color", AutoColor);
        AutoFontColor = s.GetBool("auto_font_color", AutoFontColor);
        AutoBackgroundColor = s.GetBool("auto_background_color", AutoBackgroundColor);
        AutoFontSizeMin = s.GetDouble("auto_font_size_min", AutoFontSizeMin);
        AutoFontSizeMax = s.GetDouble("auto_font_size_max", AutoFontSizeMax);
        OneShotHoldSeconds = s.GetInt("one_shot_hold_seconds", OneShotHoldSeconds);

        DarkModeFont = s.GetString("dark_mode_font", DarkModeFont);
        LayerAlignBottom = s.GetBool("layer_align_bottom", LayerAlignBottom);
        LayerAlignRight = s.GetBool("layer_align_right", LayerAlignRight);

        AlwaysOnTopOnlyWhileTranslating =
            s.GetBool("always_on_top_only_while_translating", AlwaysOnTopOnlyWhileTranslating);
        IgnoreEmptyTranslation = s.GetBool("ignore_empty_translation", IgnoreEmptyTranslation);
        HideAlsoTranslates = s.GetBool("hide_also_translates", HideAlsoTranslates);

        DisplayMemoryEnabled = s.GetBool("display_memory_enabled", DisplayMemoryEnabled);
        DisplayMemoryCount = s.GetInt("display_memory_count", DisplayMemoryCount);
        DisplayMemoryLifetimeSeconds =
            s.GetInt("display_memory_lifetime_seconds", DisplayMemoryLifetimeSeconds);

        CollectionFiles = s.GetStringList("collection_files").ToList();
        CollectionMode = Profile.ParseEnum(s.GetString("collection_mode", "database"), CollectionMode);
        CollectionIgnoreCase = s.GetBool("collection_ignore_case", CollectionIgnoreCase);

        BridgeTranslation = s.GetBool("bridge_translation", BridgeTranslation);
        FallbackTranslator = s.GetBool("fallback_translator", FallbackTranslator);

        CustomApiUrl = s.GetString("custom_api_url", CustomApiUrl);
        CustomApiSameLanguageCodesAsWeb =
            s.GetBool("custom_api_same_codes", CustomApiSameLanguageCodesAsWeb);
        CustomApiSource = s.GetString("custom_api_source", CustomApiSource);
        CustomApiTarget = s.GetString("custom_api_target", CustomApiTarget);

        LlmCustomInstruction = s.GetString("llm_custom_instruction", LlmCustomInstruction);
        LlmDisableDefaultInstruction =
            s.GetBool("llm_disable_default_instruction", LlmDisableDefaultInstruction);
        LlmCustomModel = s.GetString("llm_custom_model", LlmCustomModel);
        LlmPreset = Profile.ParseEnum(s.GetString("llm_preset", "standard"), LlmPreset);
        LlmTemperature = s.GetInt("llm_temperature", LlmTemperature);
        LlmThinking = s.GetInt("llm_thinking", LlmThinking);
        LlmMaxOutput = s.GetInt("llm_max_output", LlmMaxOutput);

        ClipboardTranslation = s.GetBool("clipboard_translation", ClipboardTranslation);
        ClipboardShowOriginal = s.GetBool("clipboard_show_original", ClipboardShowOriginal);
        ClipboardShowTranslating = s.GetBool("clipboard_show_translating", ClipboardShowTranslating);

        PreferCloudOcrOneShot = s.GetBool("prefer_cloud_ocr_one_shot", PreferCloudOcrOneShot);
        CloudOcrMonthlyLimit = s.GetInt("cloud_ocr_monthly_limit", CloudOcrMonthlyLimit);

        DictionaryExtraPasses = s.GetInt("dictionary_extra_passes", DictionaryExtraPasses);
    }

    public void Save(string path, TomlStore? existing = null)
    {
        var s = existing ?? new TomlStore();
        s.SchemaVersion = CurrentSchemaVersion;

        s.Set("tray_mode", TrayMode);
        s.Set("right_to_left", RightToLeft);
        s.Set("remote_always_on_top", RemoteAlwaysOnTop);

        s.Set("mouse_follow_compatible", MouseFollowCompatible);
        s.Set("mouse_follow_only", MouseFollowOnly);
        s.Set("show_capture_border", ShowCaptureBorder);

        WriteColor(s, "selection_highlight", SelectionHighlight);
        WriteColor(s, "selection_background", SelectionBackground);

        s.Set("auto_font_size", AutoFontSize);
        s.Set("merge_lines", MergeLines);
        s.Set("preserve_orientation", PreserveOrientation);
        s.Set("font_stroke", FontStroke);
        s.Set("use_background_transparency", UseBackgroundTransparency);
        s.Set("auto_color", AutoColor);
        s.Set("auto_font_color", AutoFontColor);
        s.Set("auto_background_color", AutoBackgroundColor);
        s.Set("auto_font_size_min", AutoFontSizeMin);
        s.Set("auto_font_size_max", AutoFontSizeMax);
        s.Set("one_shot_hold_seconds", OneShotHoldSeconds);

        s.Set("dark_mode_font", DarkModeFont);
        s.Set("layer_align_bottom", LayerAlignBottom);
        s.Set("layer_align_right", LayerAlignRight);

        s.Set("always_on_top_only_while_translating", AlwaysOnTopOnlyWhileTranslating);
        s.Set("ignore_empty_translation", IgnoreEmptyTranslation);
        s.Set("hide_also_translates", HideAlsoTranslates);

        s.Set("display_memory_enabled", DisplayMemoryEnabled);
        s.Set("display_memory_count", DisplayMemoryCount);
        s.Set("display_memory_lifetime_seconds", DisplayMemoryLifetimeSeconds);

        s.Set("collection_files", CollectionFiles);
        s.Set("collection_mode", Profile.Identifier(CollectionMode));
        s.Set("collection_ignore_case", CollectionIgnoreCase);

        s.Set("bridge_translation", BridgeTranslation);
        s.Set("fallback_translator", FallbackTranslator);

        s.Set("custom_api_url", CustomApiUrl);
        s.Set("custom_api_same_codes", CustomApiSameLanguageCodesAsWeb);
        s.Set("custom_api_source", CustomApiSource);
        s.Set("custom_api_target", CustomApiTarget);

        s.Set("llm_custom_instruction", LlmCustomInstruction);
        s.Set("llm_disable_default_instruction", LlmDisableDefaultInstruction);
        s.Set("llm_custom_model", LlmCustomModel);
        s.Set("llm_preset", Profile.Identifier(LlmPreset));
        s.Set("llm_temperature", LlmTemperature);
        s.Set("llm_thinking", LlmThinking);
        s.Set("llm_max_output", LlmMaxOutput);

        s.Set("clipboard_translation", ClipboardTranslation);
        s.Set("clipboard_show_original", ClipboardShowOriginal);
        s.Set("clipboard_show_translating", ClipboardShowTranslating);

        s.Set("prefer_cloud_ocr_one_shot", PreferCloudOcrOneShot);
        s.Set("cloud_ocr_monthly_limit", CloudOcrMonthlyLimit);

        s.Set("dictionary_extra_passes", DictionaryExtraPasses);

        s.Save(path);
    }

    private static Rgba ReadColor(TomlStore s, string key, Rgba fallback)
    {
        var t = s.GetSection(key);
        if (!t.Has("r")) return fallback;
        return new Rgba(
            (byte)Math.Clamp(t.GetInt("r", fallback.R), 0, 255),
            (byte)Math.Clamp(t.GetInt("g", fallback.G), 0, 255),
            (byte)Math.Clamp(t.GetInt("b", fallback.B), 0, 255),
            (byte)Math.Clamp(t.GetInt("a", fallback.A), 0, 255));
    }

    private static void WriteColor(TomlStore s, string key, Rgba c)
    {
        var t = s.Section(key);
        t.Set("r", c.R); t.Set("g", c.G); t.Set("b", c.B); t.Set("a", c.A);
    }
}

/// <summary>
/// RF-034 — Opções do aplicativo: idioma da interface, verificação de atualização, aba
/// inicial padrão, e janela de tradução sempre no topo.
/// </summary>
public sealed class AppOptions
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>RF-487 — Idioma inicial da interface: português do Brasil.</summary>
    public string InterfaceLanguage { get; set; } = "pt-BR";
    /// <summary>RF-416 — Verificação de atualização ao iniciar. IV.12: ligada.</summary>
    public bool CheckForUpdates { get; set; } = true;
    /// <summary>RF-501 — "Abrir na aba básica" em vez da aba de configuração rápida.</summary>
    public bool StartOnBasicTab { get; set; }
    /// <summary>RF-319 — Janela de tradução sempre no topo. IV.12: ligada.</summary>
    public bool TranslationWindowAlwaysOnTop { get; set; } = true;

    public static AppOptions Defaults() => new();

    public static AppOptions Load(string path, out TomlStore store)
    {
        store = TomlStore.Load(path);
        var o = new AppOptions();
        try
        {
            Migrations.Migrate(store, CurrentSchemaVersion);
            o.InterfaceLanguage = store.GetString("interface_language", o.InterfaceLanguage);
            o.CheckForUpdates = store.GetBool("check_for_updates", o.CheckForUpdates);
            o.StartOnBasicTab = store.GetBool("start_on_basic_tab", o.StartOnBasicTab);
            o.TranslationWindowAlwaysOnTop =
                store.GetBool("translation_window_always_on_top", o.TranslationWindowAlwaysOnTop);
        }
        catch
        {
            o = new AppOptions();
        }
        return o;
    }

    public void Save(string path, TomlStore? existing = null)
    {
        var s = existing ?? new TomlStore();
        s.SchemaVersion = CurrentSchemaVersion;
        s.Set("interface_language", InterfaceLanguage);
        s.Set("check_for_updates", CheckForUpdates);
        s.Set("start_on_basic_tab", StartOnBasicTab);
        s.Set("translation_window_always_on_top", TranslationWindowAlwaysOnTop);
        s.Save(path);
    }
}
