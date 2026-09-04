using Gort.Core.Calibration;
using Gort.Core.Imaging;
using Gort.Core.Model;
using Gort.Core.Structuring;
using Tomlyn.Model;

namespace Gort.Core.Configuration;

/// <summary>RF-473 — Formato da cópia para a área de transferência.</summary>
public enum ClipboardCopyFormat { Ocr, Translation, Both }

/// <summary>RF-323 — Ordenação (alinhamento horizontal) do texto exibido.</summary>
public enum TextOrder { Left, Center, Right }

/// <summary>
/// Cap. 10 — Perfil principal do usuário. Guarda a configuração POR JOGO.
///
/// Os valores iniciais são exatamente os da tabela IV.12 (padrões de configuração).
/// RF-025 — Os padrões são aplicados ANTES de interpretar o arquivo, de modo que um perfil
/// parcial produza um estado completo e coerente.
/// RF-026 — Todo valor de conjunto fechado é persistido pelo seu IDENTIFICADOR TEXTUAL,
/// nunca pela posição numérica: é isso que permite acrescentar, remover ou reordenar itens
/// do conjunto sem invalidar os arquivos já gravados pelo usuário.
/// </summary>
public sealed class Profile
{
    /// <summary>Versão de esquema atual deste arquivo (RF-038).</summary>
    public const int CurrentSchemaVersion = 1;

    // ── Motor de OCR e idiomas ────────────────────────────────────────────────
    public string OcrEngine { get; set; } = "modern";
    public string OcrLanguage { get; set; } = "en";
    public string TargetLanguage { get; set; } = "pt-BR";
    /// <summary>RF-150 — Conjunto de dados de idioma do motor clássico.</summary>
    public string ClassicDataset { get; set; } = "eng";
    /// <summary>RF-150 — Modo rápido do motor clássico.</summary>
    public bool ClassicFastMode { get; set; }

    // ── Serviço de tradução ───────────────────────────────────────────────────
    public string TranslationService { get; set; } = "webfree";
    /// <summary>RF-030 — Subchave que identifica qual preset de API personalizada está selecionado.</summary>
    public string? CustomApiPreset { get; set; }
    /// <summary>Idioma de origem por serviço, resolvido pela tabela de idiomas (RF-308).</summary>
    public Dictionary<string, string> ServiceSourceLanguage { get; } = new();
    public Dictionary<string, string> ServiceTargetLanguage { get; } = new();

    // ── Saídas ────────────────────────────────────────────────────────────────
    /// <summary>RF-497 — Exibir o texto reconhecido junto da tradução.</summary>
    public bool ShowRecognizedText { get; set; } = true;
    /// <summary>RF-496 — Gravar o resultado em arquivo a cada ciclo.</summary>
    public bool WriteResultToFile { get; set; }
    /// <summary>RF-473 — Copiar o resultado para a área de transferência.</summary>
    public bool CopyToClipboard { get; set; }
    public ClipboardCopyFormat CopyFormat { get; set; } = ClipboardCopyFormat.Ocr;

    // ── Banco de dados local ──────────────────────────────────────────────────
    public string DatabaseFile { get; set; } = "empty.txt";
    /// <summary>RF-242 — Ignorar maiúsculas/minúsculas.</summary>
    public bool DatabaseIgnoreCase { get; set; }
    /// <summary>RF-242 — Correspondência parcial em múltiplas linhas.</summary>
    public bool DatabasePartialMultiline { get; set; }

    // ── Dicionário de correção ────────────────────────────────────────────────
    public string DictionaryFile { get; set; } = "myDic.txt";
    public bool UseDictionary { get; set; } = true;
    /// <summary>
    /// RF-183 / RF-044 — Modo "por palavra". Em um perfil NOVO o padrão segue a propriedade
    /// "separa palavras por espaço" do idioma de OCR escolhido (RF-311), nunca uma
    /// comparação com um identificador de idioma (RF-567).
    /// </summary>
    public bool DictionaryWholeWord { get; set; } = true;

    // ── Velocidade ────────────────────────────────────────────────────────────
    /// <summary>Índice 1..5, resolvido para o intervalo P-05..P-09.</summary>
    public int Speed { get; set; } = 2;
    public int CycleIntervalMs => P.CycleIntervalMs(Speed);

    // ── Pré-processamento ─────────────────────────────────────────────────────
    public FilterMode FilterMode { get; set; } = FilterMode.None;
    public int Threshold { get; set; } = P.DefaultThreshold;
    public bool Erosion { get; set; }
    public double Scale { get; set; } = P.DefaultScale;
    /// <summary>IV.12 — Um grupo, com todos os valores zerados.</summary>
    public List<ColorGroup> ColorGroups { get; set; } = new() { new ColorGroup() };

    // ── Áreas ─────────────────────────────────────────────────────────────────
    /// <summary>RF-066 — Áreas incrementais, persistidas com posição, tamanho e ordem.</summary>
    public List<Rect> Areas { get; set; } = new();
    /// <summary>RF-066 — Áreas decrementais (de exclusão).</summary>
    public List<Rect> Exclusions { get; set; } = new();
    /// <summary>RF-078 — Grupos de cor ativos por área, por índice de área.</summary>
    public List<List<bool>> AreaColorGroups { get; set; } = new();

    // ── Captura ───────────────────────────────────────────────────────────────
    /// <summary>RF-088 — Capturar da janela ativa em vez da tela.</summary>
    public bool CaptureActiveWindow { get; set; }

    // ── Texto e aparência ─────────────────────────────────────────────────────
    public WindowMode WindowMode { get; set; } = WindowMode.Overlay;
    public TextOrder TextOrder { get; set; } = TextOrder.Left;
    /// <summary>RF-180 — Remoção de espaços do resultado do OCR.</summary>
    public bool RemoveSpaces { get; set; }
    /// <summary>RF-337 / RF-377 — Usar cor de fundo atrás do texto.</summary>
    public bool TextBackground { get; set; } = true;
    /// <summary>RF-189 — Exibir o número da área.</summary>
    public bool NumberAreas { get; set; }

    /// <summary>RF-387 — Vazio significa "resolver em tempo de execução"; nunca um nome fixo.</summary>
    public string FontFamily { get; set; } = "";
    /// <summary>P-127.</summary>
    public double FontSize { get; set; } = P.DefaultFontSize;
    public Rgba TextColor { get; set; } = new(P.DefaultTextColor.R, P.DefaultTextColor.G, P.DefaultTextColor.B);
    public Rgba Stroke1Color { get; set; } = new(P.DefaultStroke1Color.R, P.DefaultStroke1Color.G, P.DefaultStroke1Color.B);
    public Rgba Stroke2Color { get; set; } = new(P.DefaultStroke2Color.R, P.DefaultStroke2Color.G, P.DefaultStroke2Color.B);
    public Rgba BackgroundColor { get; set; } = new(
        P.DefaultBackgroundColor.R, P.DefaultBackgroundColor.G,
        P.DefaultBackgroundColor.B, P.DefaultBackgroundColor.A);

    // ── Leitura em voz alta ───────────────────────────────────────────────────
    public bool SpeakResult { get; set; }
    /// <summary>RF-477 — Aguardar o fim da leitura anterior.</summary>
    public bool SpeakWaitForPrevious { get; set; }

    // ── Janela em modo camada ─────────────────────────────────────────────────
    /// <summary>
    /// RF-340 / RF-045 — Posição e tamanho persistidos. −1 significa "não definido", e
    /// nesse caso valem os padrões de P-133. Só são gravados quando o usuário aplica ou
    /// salva explicitamente, nunca durante a inicialização.
    /// </summary>
    public int LayerX { get; set; } = -1;
    public int LayerY { get; set; } = -1;
    public int LayerWidth { get; set; } = -1;
    public int LayerHeight { get; set; } = -1;

    public bool HasLayerPlacement => LayerX >= 0 && LayerY >= 0 && LayerWidth > 0 && LayerHeight > 0;

    /// <summary>
    /// RF-041 — Valida a posição salva contra os monitores presentes: se o retângulo não
    /// intersecta nenhum monitor, usa a posição padrão; se intersecta parcialmente, é
    /// deslocado para dentro dos limites daquele monitor.
    /// </summary>
    public Rect ResolveLayerPlacement(IReadOnlyList<Rect> monitors, int primaryScreenHeight)
    {
        var fallback = new Rect(P.LayerDefaultX,
                                Math.Max(0, primaryScreenHeight - P.LayerDefaultYOffsetFromScreenBottom),
                                P.LayerDefaultWidth, P.LayerDefaultHeight);

        if (!HasLayerPlacement || monitors.Count == 0) return fallback;

        var rect = new Rect(LayerX, LayerY, LayerWidth, LayerHeight);

        Rect? host = null;
        long bestArea = 0;
        foreach (var m in monitors)
        {
            long area = rect.Intersect(m).Area;
            if (area > bestArea) { bestArea = area; host = m; }
        }

        if (host is null) return fallback;                 // não intersecta nenhum monitor
        if (host.Value.Contains(rect)) return rect;        // cabe inteiro

        // Intersecta parcialmente: desloca para dentro dos limites daquele monitor.
        var m2 = host.Value;
        int w = Math.Min(rect.Width, m2.Width);
        int h = Math.Min(rect.Height, m2.Height);
        int x = Math.Clamp(rect.X, m2.Left, Math.Max(m2.Left, m2.Right - w));
        int y = Math.Clamp(rect.Y, m2.Top, Math.Max(m2.Top, m2.Bottom - h));
        return new Rect(x, y, w, h);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RF-042 — Saturação de valores fora de faixa
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RF-042 — Valores fora de faixa são SATURADOS nos limites, nunca rejeitados.
    /// RF-043 — Faixas invertidas em um grupo de cor têm início e fim trocados.
    /// Aplicado ao carregar E ao aplicar.
    /// </summary>
    public void Normalize()
    {
        Speed = Math.Clamp(Speed, 1, 5);
        Threshold = Math.Clamp(Threshold, 0, 255);
        if (Scale > P.ScaleMax) Scale = P.DefaultScale;    // RF-114
        if (Scale < P.ScaleMin) Scale = P.ScaleMin;
        FontSize = Math.Max(P.UiFontSizeMin, FontSize);

        if (ColorGroups.Count == 0) ColorGroups.Add(new ColorGroup());
        foreach (var g in ColorGroups) g.Normalize();

        // RF-079 — toda área conhece todos os grupos; sobras e faltas são normalizadas.
        while (AreaColorGroups.Count < Areas.Count) AreaColorGroups.Add(new List<bool>());
        while (AreaColorGroups.Count > Areas.Count) AreaColorGroups.RemoveAt(AreaColorGroups.Count - 1);
        foreach (var list in AreaColorGroups)
        {
            while (list.Count < ColorGroups.Count) list.Add(true);
            while (list.Count > ColorGroups.Count) list.RemoveAt(list.Count - 1);
        }

        // Áreas de largura ou altura 0 após ajustes são forçadas a 1 px
        // (caso de erro do capítulo 11).
        for (int i = 0; i < Areas.Count; i++)
        {
            var a = Areas[i];
            Areas[i] = new Rect(a.X, a.Y, Math.Max(1, a.Width), Math.Max(1, a.Height));
        }
    }

    /// <summary>
    /// RF-148 / RF-044 — Ajustes automáticos derivados das PROPRIEDADES do idioma (RF-311),
    /// não do seu identificador: quando o idioma NÃO separa palavras por espaço, ativa a
    /// remoção de espaços e desativa o dicionário por palavra; quando separa, o contrário.
    /// </summary>
    public void ApplyLanguageProperties(Catalog.LanguageInfo language)
    {
        RemoveSpaces = !language.SeparatesWordsBySpace;
        DictionaryWholeWord = language.SeparatesWordsBySpace;
    }

    /// <summary>RF-022 — Restaurar todos os valores para os padrões.</summary>
    public static Profile Defaults() => new();

    // ─────────────────────────────────────────────────────────────────────────
    // Persistência
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RF-024 / RF-025 — Aplica os padrões e depois interpreta o arquivo; linhas
    /// desconhecidas são ignoradas e a ausência de uma chave mantém o padrão. Qualquer
    /// exceção restaura TODOS os padrões e continua.
    /// </summary>
    public static Profile Load(string path, out TomlStore store)
    {
        store = TomlStore.Load(path);
        var p = new Profile();   // RF-025 — padrões primeiro
        try
        {
            Migrations.Migrate(store, CurrentSchemaVersion);
            p.Read(store);
        }
        catch
        {
            // RF-024 — qualquer exceção restaura todos os padrões e continua.
            p = new Profile();
        }
        p.Normalize();
        return p;
    }

    private void Read(TomlStore s)
    {
        OcrEngine = s.GetString("ocr_engine", OcrEngine);
        OcrLanguage = s.GetString("ocr_language", OcrLanguage);
        TargetLanguage = s.GetString("target_language", TargetLanguage);
        ClassicDataset = s.GetString("classic_dataset", ClassicDataset);
        ClassicFastMode = s.GetBool("classic_fast_mode", ClassicFastMode);

        TranslationService = s.GetString("translation_service", TranslationService);
        CustomApiPreset = s.Has("custom_api_preset") ? s.GetString("custom_api_preset", "") : null;

        foreach (var t in s.GetTables("service_language"))
        {
            string key = t.TryGetValue("service", out var k) ? (string)k : "";
            if (key.Length == 0) continue;
            if (t.TryGetValue("source", out var src) && src is string ss) ServiceSourceLanguage[key] = ss;
            if (t.TryGetValue("target", out var tgt) && tgt is string ts) ServiceTargetLanguage[key] = ts;
        }

        ShowRecognizedText = s.GetBool("show_recognized_text", ShowRecognizedText);
        WriteResultToFile = s.GetBool("write_result_to_file", WriteResultToFile);
        CopyToClipboard = s.GetBool("copy_to_clipboard", CopyToClipboard);
        CopyFormat = ParseEnum(s.GetString("copy_format", "ocr"), CopyFormat);

        DatabaseFile = s.GetString("database_file", DatabaseFile);
        DatabaseIgnoreCase = s.GetBool("database_ignore_case", DatabaseIgnoreCase);
        DatabasePartialMultiline = s.GetBool("database_partial_multiline", DatabasePartialMultiline);

        DictionaryFile = s.GetString("dictionary_file", DictionaryFile);
        UseDictionary = s.GetBool("use_dictionary", UseDictionary);
        DictionaryWholeWord = s.GetBool("dictionary_whole_word", DictionaryWholeWord);

        Speed = s.GetInt("speed", Speed);

        FilterMode = ParseEnum(s.GetString("filter_mode", "none"), FilterMode);
        Threshold = s.GetInt("threshold", Threshold);
        Erosion = s.GetBool("erosion", Erosion);
        Scale = s.GetDouble("scale", Scale);

        var groups = s.GetTables("color_group");
        if (groups.Count > 0)
        {
            ColorGroups = groups.Select(t => new ColorGroup
            {
                R = TomlInt(t, "r"), G = TomlInt(t, "g"), B = TomlInt(t, "b"),
                S1 = TomlInt(t, "s1"), S2 = TomlInt(t, "s2"),
                V1 = TomlInt(t, "v1"), V2 = TomlInt(t, "v2"),
            }).ToList();
        }

        Areas = ReadRects(s, "area");
        Exclusions = ReadRects(s, "exclusion");

        AreaColorGroups = s.GetTables("area_color_groups")
            .Select(t => t.TryGetValue("active", out var v) && v is TomlArray a
                ? a.Select(Convert.ToBoolean).ToList()
                : new List<bool>())
            .ToList();

        CaptureActiveWindow = s.GetBool("capture_active_window", CaptureActiveWindow);

        WindowMode = ParseEnum(s.GetString("window_mode", "overlay"), WindowMode);
        TextOrder = ParseEnum(s.GetString("text_order", "left"), TextOrder);
        RemoveSpaces = s.GetBool("remove_spaces", RemoveSpaces);
        TextBackground = s.GetBool("text_background", TextBackground);
        NumberAreas = s.GetBool("number_areas", NumberAreas);

        FontFamily = s.GetString("font_family", FontFamily);
        FontSize = s.GetDouble("font_size", FontSize);
        TextColor = ReadColor(s, "text_color", TextColor);
        Stroke1Color = ReadColor(s, "stroke1_color", Stroke1Color);
        Stroke2Color = ReadColor(s, "stroke2_color", Stroke2Color);
        BackgroundColor = ReadColor(s, "background_color", BackgroundColor);

        SpeakResult = s.GetBool("speak_result", SpeakResult);
        SpeakWaitForPrevious = s.GetBool("speak_wait_for_previous", SpeakWaitForPrevious);

        LayerX = s.GetInt("layer_x", LayerX);
        LayerY = s.GetInt("layer_y", LayerY);
        LayerWidth = s.GetInt("layer_width", LayerWidth);
        LayerHeight = s.GetInt("layer_height", LayerHeight);
    }

    /// <summary>
    /// Grava o perfil. RF-038 — só as chaves conhecidas são escritas; qualquer chave gravada
    /// por uma versão mais nova permanece intacta no arquivo.
    /// </summary>
    public void Save(string path, TomlStore? existing = null)
    {
        var s = existing ?? new TomlStore();
        s.SchemaVersion = CurrentSchemaVersion;

        s.Set("ocr_engine", OcrEngine);
        s.Set("ocr_language", OcrLanguage);
        s.Set("target_language", TargetLanguage);
        s.Set("classic_dataset", ClassicDataset);
        s.Set("classic_fast_mode", ClassicFastMode);

        s.Set("translation_service", TranslationService);
        if (CustomApiPreset is not null) s.Set("custom_api_preset", CustomApiPreset);

        var langs = new List<TomlTable>();
        foreach (var key in ServiceSourceLanguage.Keys.Union(ServiceTargetLanguage.Keys).OrderBy(k => k))
        {
            var t = new TomlTable { ["service"] = key };
            if (ServiceSourceLanguage.TryGetValue(key, out var src)) t["source"] = src;
            if (ServiceTargetLanguage.TryGetValue(key, out var tgt)) t["target"] = tgt;
            langs.Add(t);
        }
        s.SetTables("service_language", langs);

        s.Set("show_recognized_text", ShowRecognizedText);
        s.Set("write_result_to_file", WriteResultToFile);
        s.Set("copy_to_clipboard", CopyToClipboard);
        s.Set("copy_format", Identifier(CopyFormat));

        s.Set("database_file", DatabaseFile);
        s.Set("database_ignore_case", DatabaseIgnoreCase);
        s.Set("database_partial_multiline", DatabasePartialMultiline);

        s.Set("dictionary_file", DictionaryFile);
        s.Set("use_dictionary", UseDictionary);
        s.Set("dictionary_whole_word", DictionaryWholeWord);

        s.Set("speed", Speed);

        s.Set("filter_mode", Identifier(FilterMode));
        s.Set("threshold", Threshold);
        s.Set("erosion", Erosion);
        s.Set("scale", Scale);

        s.SetTables("color_group", ColorGroups.Select(g => new TomlTable
        {
            ["r"] = (long)g.R, ["g"] = (long)g.G, ["b"] = (long)g.B,
            ["s1"] = (long)g.S1, ["s2"] = (long)g.S2,
            ["v1"] = (long)g.V1, ["v2"] = (long)g.V2,
        }));

        WriteRects(s, "area", Areas);
        WriteRects(s, "exclusion", Exclusions);

        s.SetTables("area_color_groups", AreaColorGroups.Select(list =>
        {
            var arr = new TomlArray();
            foreach (bool b in list) arr.Add(b);
            return new TomlTable { ["active"] = arr };
        }));

        s.Set("capture_active_window", CaptureActiveWindow);

        s.Set("window_mode", Identifier(WindowMode));
        s.Set("text_order", Identifier(TextOrder));
        s.Set("remove_spaces", RemoveSpaces);
        s.Set("text_background", TextBackground);
        s.Set("number_areas", NumberAreas);

        s.Set("font_family", FontFamily);
        s.Set("font_size", FontSize);
        WriteColor(s, "text_color", TextColor);
        WriteColor(s, "stroke1_color", Stroke1Color);
        WriteColor(s, "stroke2_color", Stroke2Color);
        WriteColor(s, "background_color", BackgroundColor);

        s.Set("speak_result", SpeakResult);
        s.Set("speak_wait_for_previous", SpeakWaitForPrevious);

        // RF-045 — posição e tamanho da camada só chegam aqui quando o usuário aplicou ou
        // salvou explicitamente; a inicialização não os grava.
        s.Set("layer_x", LayerX);
        s.Set("layer_y", LayerY);
        s.Set("layer_width", LayerWidth);
        s.Set("layer_height", LayerHeight);

        s.Save(path);
    }

    // ── Auxiliares ────────────────────────────────────────────────────────────

    private static int TomlInt(TomlTable t, string key)
    {
        if (!t.TryGetValue(key, out var v)) return 0;
        try { return Convert.ToInt32(v); } catch { return 0; }
    }

    private static List<Rect> ReadRects(TomlStore s, string key)
        => s.GetTables(key)
            .Select(t => new Rect(TomlInt(t, "x"), TomlInt(t, "y"),
                                  TomlInt(t, "w"), TomlInt(t, "h")))
            .ToList();

    private static void WriteRects(TomlStore s, string key, List<Rect> rects)
        => s.SetTables(key, rects.Select(r => new TomlTable
        {
            ["x"] = (long)r.X, ["y"] = (long)r.Y,
            ["w"] = (long)r.Width, ["h"] = (long)r.Height,
        }));

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

    /// <summary>
    /// RF-026 / RF-027 — Identificadores textuais estáveis, em minúsculas, sem espaços e
    /// independentes do idioma da interface.
    /// </summary>
    public static string Identifier<T>(T value) where T : struct, Enum
        => value.ToString()!.ToLowerInvariant();

    /// <summary>
    /// RF-028 — Um identificador desconhecido lido de um arquivo não pode impedir o
    /// carregamento: o campo assume o padrão.
    /// </summary>
    public static T ParseEnum<T>(string text, T fallback) where T : struct, Enum
        => Enum.TryParse<T>(text, ignoreCase: true, out var v) ? v : fallback;
}
