using Gort.Core.Auxiliary;
using Gort.Core.Caching;
using Gort.Core.Catalog;
using Gort.Core.Configuration;
using Gort.Core.Imaging;
using Gort.Core.Ocr;
using Gort.Core.Regions;
using Gort.Core.Shortcuts;
using Gort.Core.Structuring;
using Gort.Core.Translation;
using Gort.Core.Translation.Services;
using Gort.Engine;
using Gort.Ocr.Rapid;
using Gort.Platform;
using Gort.Platform.Monitors;

namespace Gort.App;

/// <summary>
/// Reúne o que o programa precisa para funcionar e mantém a coerência entre as partes.
///
/// RF-005 — Tudo isto é montado durante a tela de abertura: catálogo, perfil do usuário,
/// capacidades do sistema, motores de OCR disponíveis e serviço de tradução.
/// RF-576 — As capacidades são apuradas UMA VEZ, aqui, e a interface reage a elas; nada é
/// descoberto no meio de uma tradução.
/// </summary>
public sealed class AppSession : IDisposable
{
    private AppSession(AppCatalog catalog, UserPaths paths, Profile profile,
                       AdvancedOptions advanced, AppOptions appOptions,
                       IPlatformServices platform, OcrEngineRegistry engines)
    {
        Catalog = catalog;
        Paths = paths;
        Profile = profile;
        Advanced = advanced;
        Options = appOptions;
        Platform = platform;
        Engines = engines;

        // RF-075 — a escala vem sempre do monitor que contém a moldura, resolvida no
        // momento da conversão.
        Regions = new RegionManager(frame => MonitorGeometry.ScaleOf(Platform.Monitors.Monitors, frame));
        Regions.SetColorGroupCount(profile.ColorGroups.Count);
        Regions.LoadFrom(profile.Areas, profile.Exclusions,
                         profile.AreaColorGroups.Select(g => (IReadOnlyList<bool>)g).ToList());
        Regions.MouseFollowOnly = advanced.MouseFollowOnly;

        // RF-037 / RF-453 — os atalhos vêm do seu próprio arquivo.
        Shortcuts = ShortcutStore.Load(paths.Shortcuts);
        Dispatcher = new ShortcutDispatcher(Shortcuts);

        // Cap. 24 e 25 — recursos auxiliares.
        Clipboard = new ClipboardTranslationGate();
        ClipboardOutput = new ClipboardWriter();
        Speech = new SpeechQueue(() => platform.Speech.IsSpeaking);

        Pipeline = new TranslationPipeline();
        Cycle = new TranslationCycle(Platform.Capture, Pipeline);

        ApplyConfiguration();
    }

    public AppCatalog Catalog { get; }
    public UserPaths Paths { get; }
    public Profile Profile { get; }
    public AdvancedOptions Advanced { get; }
    public AppOptions Options { get; }
    public IPlatformServices Platform { get; }
    public OcrEngineRegistry Engines { get; }
    public RegionManager Regions { get; }
    public ClipboardTranslationGate Clipboard { get; }
    public ClipboardWriter ClipboardOutput { get; }
    public SpeechQueue Speech { get; }
    public ShortcutSet Shortcuts { get; }
    public ShortcutDispatcher Dispatcher { get; }
    public TranslationPipeline Pipeline { get; }
    public TranslationCycle Cycle { get; }

    /// <summary>Serviço de tradução ativo, recriado ao aplicar configuração.</summary>
    public ITranslationService? Service { get; private set; }

    /// <summary>Memória de resultados do serviço ativo (RF-206).</summary>
    public ResultMemory? Memory { get; private set; }

    /// <summary>Dicionário de correção carregado (RF-181).</summary>
    public CorrectionDictionary? Dictionary { get; private set; }

    /// <summary>Mensagens de diagnóstico da inicialização, para exibir ao usuário uma vez.</summary>
    public List<string> Notices { get; } = new();

    public static AppSession Create(string? dataDirectory = null, string? userRoot = null)
    {
        var notices = new List<string>();

        var catalog = AppCatalog.Load(dataDirectory ?? LocateDataDirectory(), notices.Add);
        var paths = new UserPaths(userRoot);

        // RF-025 — os padrões são aplicados antes de interpretar o arquivo, então um perfil
        // parcial produz um estado completo e coerente.
        var profile = Profile.Load(paths.MainProfile, out _);
        var advanced = AdvancedOptions.Load(paths.AdvancedOptions, out _);
        var appOptions = AppOptions.Load(paths.AppOptions, out _);

        var platform = PlatformServices.Create();

        var engines = new OcrEngineRegistry();
        engines.Register(new RapidOcrEngine(models: catalog.ModernOcrModels));

        // RF-575 — o motor do sistema só é registrado onde ele existe; o registro filtra
        // pelos disponíveis, então um motor indisponível nunca chega à lista do usuário.
        if (OperatingSystem.IsMacOS())
        {
            engines.Register(new Gort.Platform.MacOS.MacVisionOcr());
        }

        var session = new AppSession(catalog, paths, profile, advanced, appOptions,
                                     platform, engines);
        session.Notices.AddRange(notices);
        return session;
    }

    /// <summary>
    /// Cap. 10 — Aplicar configuração. Resolve o serviço de tradução, a memória de
    /// resultados, o dicionário e o token separador a partir do perfil e do catálogo.
    /// </summary>
    public void ApplyConfiguration()
    {
        Profile.Normalize();

        // RF-307 — um serviço salvo no perfil que não exista mais cai para o banco de dados
        // local, em vez de impedir o funcionamento.
        var info = Catalog.Service(Profile.TranslationService)
                   ?? Catalog.Service("localdb")
                   ?? Catalog.TranslationServices.FirstOrDefault();

        Service?.Dispose();
        Service = CreateService(info?.Key ?? "localdb");

        Memory = info is { UsesResultMemory: true }
            ? new ResultMemory(info.Key, Paths.ResultMemoryFor(info.Key))
            : null;
        Memory?.Load();

        Pipeline.SeparatorToken = info?.SeparatorToken ?? Gort.Core.Calibration.P.SeparatorToken;
        Pipeline.Memory = Memory;
        Pipeline.IgnoreEmptyTranslation = Advanced.IgnoreEmptyTranslation;

        // RF-465 / RF-473 — os recursos auxiliares seguem o perfil e as opções avançadas.
        Clipboard.Enabled = Advanced.ClipboardTranslation;
        Clipboard.ShowOriginal = Advanced.ClipboardShowOriginal;
        Clipboard.ShowTranslating = Advanced.ClipboardShowTranslating;

        // RF-472 — aplicar configurações limpa o estado de "traduzindo pela área de
        // transferência".
        Clipboard.Reset();

        ClipboardOutput.Enabled = Profile.CopyToClipboard;
        ClipboardOutput.Format = Profile.CopyFormat;

        Speech.Enabled = Profile.SpeakResult;
        Speech.WaitForPrevious = Profile.SpeakWaitForPrevious;
        Speech.SynthesizerAvailable = Platform.Speech.IsAvailable;

        Dictionary = Profile.UseDictionary
            ? CorrectionDictionary.Load(Path.Combine(Paths.DataDirectory, Profile.DictionaryFile))
            : null;

        if (Dictionary is not null)
        {
            Dictionary.WholeWord = Profile.DictionaryWholeWord;
            Dictionary.ExtraPasses = Advanced.DictionaryExtraPasses;
        }
    }

    private ITranslationService CreateService(string key)
    {
        if (key == "webfree" && Catalog.FreeWebTranslator is not null)
            return new FreeWebTranslator(Catalog.FreeWebTranslator);

        // Os demais serviços entram na Etapa 15. Até lá, o banco de dados local é o que
        // funciona sem rede e sem credencial.
        var database = new LocalDatabase
        {
            IgnoreCase = Profile.DatabaseIgnoreCase,
            PartialMultiline = Profile.DatabasePartialMultiline,
        };
        database.Load(Path.Combine(Paths.DataDirectory, Profile.DatabaseFile));
        return new LocalDatabaseTranslator(database);
    }

    /// <summary>Monta as configurações de um ciclo a partir do estado atual.</summary>
    public CycleSettings BuildCycleSettings()
    {
        var info = Catalog.Service(Profile.TranslationService) ?? Catalog.TranslationServices[0];
        var source = Catalog.Language(Profile.OcrLanguage) ?? Catalog.Languages[0];
        var target = Catalog.Language(Profile.TargetLanguage) ?? Catalog.Languages[^1];

        var engine = Engines.Resolve(Profile.OcrEngine)
                     ?? throw new InvalidOperationException("Nenhum motor de OCR disponível.");

        return new CycleSettings
        {
            Service = Service!,
            TranslationContext = new TranslationContext
            {
                SourceCode = source.CodeFor(info.Key) ?? source.Key,
                TargetCode = target.CodeFor(info.Key) ?? target.Key,
                // RF-239 — só se aplica quando a origem não é japonês E o serviço declara
                // a tradução ponte suportada.
                Bridge = Advanced.BridgeTranslation && info.SupportsBridge && source.Key != "ja",
                BridgeCode = Catalog.Language("ja")?.CodeFor(info.Key),
            },
            Ocr = engine,
            OcrLanguage = source.Key,
            Filter = new FilterSettings
            {
                Mode = Profile.FilterMode,
                Threshold = Profile.Threshold,
                Erosion = Profile.Erosion,
                Scale = Profile.Scale,
                Groups = Profile.ColorGroups.Select(g => g.Clone()).ToList(),
            },
            MergeLines = Advanced.MergeLines,
            Text = new TextProcessingOptions
            {
                RemoveSpaces = Profile.RemoveSpaces,
                UseDictionary = Profile.UseDictionary,
                WindowMode = Profile.WindowMode,
                ServiceIsLocalDatabase = info.Key == "localdb",
                NumberAreas = Profile.NumberAreas,
            },
            Dictionary = Dictionary,
            NumberAreas = Profile.NumberAreas,
            // RF-098 — a imagem original só é pedida no modo sobreposição com cor automática.
            NeedsOriginalImage = Profile.WindowMode == WindowMode.Overlay && Advanced.AutoColor,

            // RF-413 — as duas cores automáticas são independentes e ficam sob a caixa mestre.
            AutoColor = new Gort.Core.ColorAnalysis.AutoColorOptions
            {
                Enabled = Advanced.AutoColor,
                FontColor = Advanced.AutoFontColor,
                BackgroundColor = Advanced.AutoBackgroundColor,
                TextBackgroundEnabled = Profile.TextBackground,
                BackgroundAlpha = Profile.BackgroundColor.A,
            },
        };
    }

    /// <summary>RF-453 — Os atalhos são gravados no seu arquivo ao aplicar (RF-012).</summary>
    public void SaveShortcuts() => ShortcutStore.Save(Paths.Shortcuts, Shortcuts);

    /// <summary>RF-020 — O perfil principal é salvo sempre que o usuário aplica configurações.</summary>
    public void SaveProfile()
    {
        var (areas, exclusions, groups) = Regions.ToProfile();
        Profile.Areas = areas;
        Profile.Exclusions = exclusions;
        Profile.AreaColorGroups = groups;
        Profile.Save(Paths.MainProfile);
    }

    private static string LocateDataDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "languages.toml"))) return candidate;
        }
        return Path.Combine(AppContext.BaseDirectory, "data");
    }

    public void Dispose()
    {
        Service?.Dispose();
        Pipeline.Dispose();
        Engines.Dispose();
        Platform.Dispose();
    }
}
