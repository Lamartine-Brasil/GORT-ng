using Tomlyn;
using Tomlyn.Model;

namespace Gort.Core.Catalog;

/// <summary>
/// RF-029 — Os conjuntos de valores (idiomas, motores de OCR, serviços de tradução e seus
/// parâmetros) são descritos como DADOS, não como código. Incluir um item novo é uma
/// alteração de dados mais a implementação do adaptador correspondente, nunca uma
/// alteração no núcleo do pipeline (RF-566).
///
/// RF-567 — Nenhum ponto do programa pode assumir um conjunto fixo de idiomas, motores ou
/// serviços — nem por quantidade, nem por ordem, nem por identificador literal espalhado
/// pelo código.
/// </summary>
public sealed class AppCatalog
{
    private readonly Dictionary<string, LanguageInfo> _languages;
    private readonly Dictionary<string, OcrEngineInfo> _ocrEngines;
    private readonly Dictionary<string, TranslationServiceInfo> _services;

    private AppCatalog(
        IReadOnlyList<LanguageInfo> languages,
        IReadOnlyList<OcrEngineInfo> engines,
        IReadOnlyList<TranslationServiceInfo> services,
        string defaultTarget,
        string defaultService,
        string bridgeLanguage,
        LlmCatalog llm,
        IReadOnlyList<string> fontFallbacks,
        IReadOnlyDictionary<string, string> links,
        ModernOcrModels? modernOcrModels,
        Translation.Services.FreeWebTranslatorOptions? freeWebTranslator)
    {
        ModernOcrModels = modernOcrModels;
        FreeWebTranslator = freeWebTranslator;
        Languages = languages;
        OcrEngines = engines;
        TranslationServices = services;
        DefaultTargetLanguage = defaultTarget;
        DefaultTranslationService = defaultService;
        BridgeLanguage = bridgeLanguage;
        Llm = llm;
        FontFallbacks = fontFallbacks;
        Links = links;

        _languages = languages.ToDictionary(l => l.Key, StringComparer.OrdinalIgnoreCase);
        _ocrEngines = engines.ToDictionary(e => e.Key, StringComparer.OrdinalIgnoreCase);
        _services = services.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<LanguageInfo> Languages { get; }
    public IReadOnlyList<OcrEngineInfo> OcrEngines { get; }
    public IReadOnlyList<TranslationServiceInfo> TranslationServices { get; }

    /// <summary>RF-314 — Idioma de destino padrão (português do Brasil nesta versão).</summary>
    public string DefaultTargetLanguage { get; }

    /// <summary>RF-225 — Serviço de tradução padrão (tradutor web gratuito).</summary>
    public string DefaultTranslationService { get; }

    /// <summary>
    /// RF-239 — Idioma-ponte da tradução em duas etapas.
    ///
    /// RF-567 — vem dos dados, e não de um literal no código: a especificação nomeia o
    /// japonês, mas nomear é decisão de produto, e produto muda em arquivo.
    /// </summary>
    public string BridgeLanguage { get; }

    public LlmCatalog Llm { get; }

    /// <summary>RF-029 — Modelos do motor de reconhecimento moderno, vindos dos dados.</summary>
    public ModernOcrModels? ModernOcrModels { get; }

    /// <summary>
    /// VI.1 — Configuração do tradutor web gratuito. O endereço é dado, não endereço
    /// embutido no código.
    /// </summary>
    public Translation.Services.FreeWebTranslatorOptions? FreeWebTranslator { get; }

    /// <summary>RF-387 — Lista de reserva de famílias de fonte.</summary>
    public IReadOnlyList<string> FontFallbacks { get; }

    /// <summary>Endereços externos como dado, nunca embutidos (RF-513, RF-544).</summary>
    public IReadOnlyDictionary<string, string> Links { get; }

    public LanguageInfo? Language(string? key)
        => key is not null && _languages.TryGetValue(key, out var l) ? l : null;

    public OcrEngineInfo? OcrEngine(string? key)
        => key is not null && _ocrEngines.TryGetValue(key, out var e) ? e : null;

    public TranslationServiceInfo? Service(string? key)
        => key is not null && _services.TryGetValue(key, out var s) ? s : null;

    /// <summary>
    /// RF-151 — Idiomas oferecidos por um motor de OCR: a interseção entre os idiomas que o
    /// motor sabe reconhecer e os idiomas previstos na tabela.
    /// <paramref name="installed"/>, quando fornecido, restringe ainda mais a lista aos
    /// idiomas efetivamente instalados no sistema (RF-136).
    /// </summary>
    public IReadOnlyList<LanguageInfo> LanguagesFor(OcrEngineInfo engine, IEnumerable<string>? installed = null)
    {
        var installedSet = installed is null
            ? null
            : new HashSet<string>(installed, StringComparer.OrdinalIgnoreCase);

        return engine.Languages
            .Select(Language)
            .OfType<LanguageInfo>()
            .Where(l => installedSet is null
                        || installedSet.Contains(l.Key)
                        || installedSet.Contains(l.OcrCode))
            .ToList();
    }

    /// <summary>
    /// RF-511 — As listas de idioma de um serviço contêm apenas os idiomas que ele suporta:
    /// um idioma sem código para aquele serviço não aparece na lista dele.
    /// RF-313 — Nas listas de destino, o idioma de destino padrão aparece em primeiro lugar.
    /// </summary>
    public IReadOnlyList<LanguageInfo> LanguagesFor(TranslationServiceInfo service, bool targetList)
    {
        var list = Languages.Where(l => l.CodeFor(service.Key) is not null).ToList();
        if (!targetList) return list;

        int i = list.FindIndex(l => l.Key == DefaultTargetLanguage);
        if (i > 0)
        {
            var def = list[i];
            list.RemoveAt(i);
            list.Insert(0, def);
        }
        return list;
    }

    /// <summary>
    /// RF-316 — A comparação de códigos de idioma trata "en" e "en-US" como equivalentes:
    /// compara-se a subetiqueta primária, sem diferenciar maiúsculas.
    /// </summary>
    public static bool CodesEquivalent(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
        return string.Equals(PrimarySubtag(a), PrimarySubtag(b), StringComparison.OrdinalIgnoreCase);
    }

    private static string PrimarySubtag(string code)
    {
        int i = code.IndexOfAny(new[] { '-', '_' });
        return i < 0 ? code : code[..i];
    }

    /// <summary>
    /// RF-147 / RF-315 — Ao trocar o idioma de OCR, procura o idioma correspondente na
    /// lista de um serviço para selecioná-lo automaticamente.
    /// </summary>
    public LanguageInfo? MatchForService(LanguageInfo ocrLanguage, TranslationServiceInfo service)
    {
        if (ocrLanguage.CodeFor(service.Key) is not null) return ocrLanguage;
        return Languages.FirstOrDefault(l =>
            l.CodeFor(service.Key) is not null && CodesEquivalent(l.Key, ocrLanguage.Key));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Carregamento
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Carrega o catálogo a partir dos arquivos de dados. Um arquivo ausente ou ilegível
    /// não impede a abertura: o catálogo cai para o conjunto embutido mínimo e o ocorrido
    /// é registrado (P7, P8).
    /// </summary>
    public static AppCatalog Load(string dataDirectory, Action<string>? log = null)
    {
        var languages = new List<LanguageInfo>();
        var engines = new List<OcrEngineInfo>();
        var services = new List<TranslationServiceInfo>();
        string defaultTarget = "pt-BR";
        string defaultService = "webfree";
        string bridgeLanguage = "";
        LlmCatalog? llm = null;
        ModernOcrModels? modernModels = null;
        Translation.Services.FreeWebTranslatorOptions? freeWeb = null;
        var fonts = new List<string>();
        var links = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        TomlTable? langTable = TryRead(Path.Combine(dataDirectory, "languages.toml"), log);
        if (langTable is not null)
        {
            defaultTarget = GetString(langTable, "default_target") ?? defaultTarget;
            foreach (var item in GetArray(langTable, "language"))
            {
                var codes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (item.TryGetValue("codes", out var c) && c is TomlTable ct)
                {
                    foreach (var kv in ct)
                    {
                        if (kv.Value is string s && !string.IsNullOrWhiteSpace(s)) codes[kv.Key] = s;
                    }
                }
                string? key = GetString(item, "key");
                if (key is null) continue;
                languages.Add(new LanguageInfo
                {
                    Key = key,
                    NameKey = GetString(item, "name_key") ?? $"lang.{key}",
                    OcrCode = GetString(item, "ocr") ?? key,
                    Codes = codes,
                    SeparatesWordsBySpace = GetBool(item, "separates_words_by_space") ?? true,
                    SupportsVertical = GetBool(item, "supports_vertical") ?? false,
                    RightToLeft = GetBool(item, "right_to_left") ?? false,
                });
            }
        }

        TomlTable? engTable = TryRead(Path.Combine(dataDirectory, "engines.toml"), log);
        if (engTable is not null)
        {
            defaultService = GetString(engTable, "default_translation_service") ?? defaultService;
            bridgeLanguage = GetString(engTable, "bridge_language") ?? bridgeLanguage;

            foreach (var item in GetArray(engTable, "ocr_engine"))
            {
                string? key = GetString(item, "key");
                if (key is null) continue;
                engines.Add(new OcrEngineInfo
                {
                    Key = key,
                    NameKey = GetString(item, "name_key") ?? $"ocr.{key}",
                    NeedsNetwork = GetBool(item, "needs_network") ?? false,
                    WordPositions = GetBool(item, "word_positions") ?? false,
                    LinePositions = GetBool(item, "line_positions") ?? true,
                    Realtime = GetBool(item, "realtime") ?? true,
                    Languages = GetStringList(item, "languages"),
                });
            }

            foreach (var item in GetArray(engTable, "translation_service"))
            {
                string? key = GetString(item, "key");
                if (key is null) continue;
                services.Add(new TranslationServiceInfo
                {
                    Key = key,
                    NameKey = GetString(item, "name_key") ?? $"svc.{key}",
                    NeedsNetwork = GetBool(item, "needs_network") ?? true,
                    UsesResultMemory = GetBool(item, "uses_result_memory") ?? true,
                    ShortcutSwitchable = GetBool(item, "shortcut_switchable") ?? false,
                    SupportsBridge = GetBool(item, "supports_bridge") ?? false,
                    UsesCollection = GetBool(item, "uses_collection") ?? true,
                    SeparatorToken = GetString(item, "separator_token") ?? Calibration.P.SeparatorToken,
                    MultipleKeys = GetBool(item, "multiple_keys") ?? false,
                    Secondary = GetBool(item, "secondary") ?? false,
                });
            }

            if (engTable.TryGetValue("llm", out var l) && l is TomlTable lt)
            {
                llm = new LlmCatalog
                {
                    DefaultModel = GetString(lt, "default_model") ?? "",
                    Models = GetStringList(lt, "models"),
                    LegacyFamilyPrefix = GetString(lt, "legacy_family_prefix") ?? "",
                    ProMarker = GetString(lt, "pro_marker") ?? "pro",
                };
            }

            if (engTable.TryGetValue("modern_ocr", out var mo) && mo is TomlTable mot)
            {
                var recognition = new Dictionary<string, RecognitionModel>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in GetArray(mot, "recognition"))
                {
                    string? lang = GetString(item, "language");
                    string? model = GetString(item, "model");
                    if (lang is null || model is null) continue;
                    recognition[lang] = new RecognitionModel(model, GetString(item, "dictionary"));
                }

                string? detection = GetString(mot, "detection");
                if (detection is not null)
                {
                    modernModels = new ModernOcrModels
                    {
                        Detection = detection,
                        Recognition = recognition,
                    };
                }
            }

            if (engTable.TryGetValue("webfree", out var wf) && wf is TomlTable wft)
            {
                string? endpoint = GetString(wft, "endpoint");
                string? high = GetString(wft, "client_high_quality");
                string? low = GetString(wft, "client_low_quality");
                if (endpoint is not null && high is not null && low is not null)
                {
                    freeWeb = new Translation.Services.FreeWebTranslatorOptions
                    {
                        Endpoint = endpoint,
                        HighQualityClient = high,
                        LowQualityClient = low,
                        LowQualityMarker = GetString(wft, "low_quality_marker")
                                           ?? "[qualidade reduzida] ",
                    };
                }
            }

            if (engTable.TryGetValue("fonts", out var f) && f is TomlTable ft)
                fonts.AddRange(GetStringList(ft, "fallback"));

            if (engTable.TryGetValue("links", out var k) && k is TomlTable kt)
            {
                foreach (var kv in kt)
                {
                    if (kv.Value is string s) links[kv.Key] = s;
                }
            }
        }

        llm ??= new LlmCatalog
        {
            DefaultModel = "",
            Models = Array.Empty<string>(),
            LegacyFamilyPrefix = "",
            ProMarker = "pro",
        };

        return new AppCatalog(languages, engines, services, defaultTarget, defaultService,
                              bridgeLanguage, llm, fonts, links, modernModels, freeWeb);
    }

    private static TomlTable? TryRead(string path, Action<string>? log)
    {
        try
        {
            if (!File.Exists(path))
            {
                log?.Invoke($"Arquivo de catálogo ausente: {path}");
                return null;
            }
            return Toml.ToModel(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            // P8 — degradação silenciosa: registra e segue com o que houver.
            log?.Invoke($"Falha ao ler {path}: {ex.Message}");
            return null;
        }
    }

    private static IEnumerable<TomlTable> GetArray(TomlTable table, string key)
    {
        if (!table.TryGetValue(key, out var value)) yield break;
        if (value is TomlTableArray arr)
        {
            foreach (var t in arr) yield return t;
        }
        else if (value is TomlArray a)
        {
            foreach (var t in a.OfType<TomlTable>()) yield return t;
        }
    }

    private static string? GetString(TomlTable t, string key)
        => t.TryGetValue(key, out var v) && v is string s && s.Length > 0 ? s : null;

    private static bool? GetBool(TomlTable t, string key)
        => t.TryGetValue(key, out var v) && v is bool b ? b : null;

    private static IReadOnlyList<string> GetStringList(TomlTable t, string key)
    {
        if (t.TryGetValue(key, out var v) && v is TomlArray arr)
            return arr.OfType<string>().ToList();
        return Array.Empty<string>();
    }
}
