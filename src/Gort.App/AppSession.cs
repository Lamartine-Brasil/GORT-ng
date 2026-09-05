using Gort.Core.Auxiliary;
using Gort.Core.Caching;
using Gort.Core.Catalog;
using Gort.Core.Configuration;
using Gort.Core.Diagnostics;
using Gort.Core.Imaging;
using Gort.Core.Localization;
using Gort.Core.Ocr;
using Gort.Core.Regions;
using Gort.Core.Shortcuts;
using Gort.Core.Structuring;
using Gort.Core.Translation;
using Gort.Core.Translation.Keys;
using Gort.Core.Translation.Presets;
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
                       IPlatformServices platform, OcrEngineRegistry engines,
                       string dataDirectory)
    {
        DataDirectory = dataDirectory;
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

        // RF-481 a RF-489 — a tabela de localização é um arquivo de dados externo.
        Localizer = Localizer.Load(Path.Combine(DataDirectory, "localizacao.csv"));

        ApiPresets = ApiPresetStore.Load(paths.ApiPresetsFile, paths.ApiPresetsDirectory);
        Notices.AddRange(ApiPresets.Notices);

        Diagnostics = new DiagnosticRecorder(paths.DiagnosticsDirectory);
        ResultFile = new ResultFileWriter(
            Path.Combine(paths.DataDirectory, "resultado-gravado.txt"));
        Localizer.SelectLanguage(appOptions.InterfaceLanguage);

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
    public Localizer Localizer { get; }
    public string DataDirectory { get; }
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

    // ── Depuração e diagnóstico, cap. 27 ────────────────────────────────────

    /// <summary>RF-490 / RF-491 — Sinalizadores do modo de depuração.</summary>
    public DebugOptions Debug { get; } = new();

    /// <summary>RF-498 — Contadores de OCR e de traduções, com registro de mensagens.</summary>
    public DiagnosticCounters Counters { get; } = new();

    /// <summary>RF-492 a RF-495 — Gravador dos retratos de análise.</summary>
    public DiagnosticRecorder Diagnostics { get; private set; } = null!;

    /// <summary>RF-496 — Gravação do resultado no formato do banco de dados.</summary>
    public ResultFileWriter ResultFile { get; private set; } = null!;

    /// <summary>RF-554 / RF-559 — Imagens de região vivas neste instante.</summary>
    public LiveImageMeter ImageMeter { get; } = new();

    /// <summary>RF-302 — Presets de API personalizada, das duas fontes.</summary>
    public ApiPresetStore ApiPresets { get; private set; } = null!;

    /// <summary>
    /// RF-250 a RF-253 — O rodízio de chaves do serviço ativo. Recarregado ao aplicar
    /// configuração, porque cada serviço tem o seu arquivo.
    /// </summary>
    public TranslationKeyStore Keys { get; private set; } = new();

    /// <summary>Arquivo de chaves do serviço ativo, para a janela gravar nele.</summary>
    public string KeysFile { get; private set; } = "";

    public static AppSession Create(string? dataDirectory = null, string? userRoot = null)
    {
        var notices = new List<string>();

        string data = dataDirectory ?? LocateDataDirectory();
        var catalog = AppCatalog.Load(data, notices.Add);
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
            // RF-151 — a interseção com os idiomas de origem da tabela é feita pelo motor,
            // mas quem são eles vem do catálogo.
            engines.Register(new Gort.Platform.MacOS.MacVisionOcr(
                catalog.Languages.Select(l => l.OcrCode).ToHashSet(
                    StringComparer.OrdinalIgnoreCase)));
        }

        var session = new AppSession(catalog, paths, profile, advanced, appOptions,
                                     platform, engines, data);
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

        // RF-306 — cada preset de API personalizada é uma entrada SEPARADA na lista de
        // serviços, com identificador "customapi:<nome>". Para o resto do programa ele é o
        // serviço "customapi", que é quem descreve as suas propriedades no catálogo.
        string selected = Profile.TranslationService;
        string? presetName = null;

        if (selected.StartsWith(CustomApiPrefix, StringComparison.Ordinal))
        {
            presetName = selected[CustomApiPrefix.Length..];
            selected = "customapi";

            // RF-307 — um preset removido é um serviço que não existe mais.
            if (ApiPresets.Find(presetName) is null) { presetName = null; selected = "localdb"; }
        }

        // RF-307 — um serviço salvo no perfil que não exista mais cai para o banco de dados
        // local, em vez de impedir o funcionamento.
        var info = Catalog.Service(selected)
                   ?? Catalog.Service("localdb")
                   ?? Catalog.TranslationServices.FirstOrDefault();

        Service?.Dispose();
        Service = CreateService(info?.Key ?? "localdb", presetName);

        // RF-250 — o rodízio é por serviço: trocar de serviço troca o arquivo.
        //
        // A chave é a mesma que foi para `CreateService`, e não `info.Key` direto: com um
        // catálogo vazio `info` é nulo, e a linha acima já resolveu isso para "localdb".
        // Repetir a resolução aqui é o que evita que um catálogo ausente — que RF-562 manda
        // sobreviver — derrube a aplicação de configuração.
        string serviceKey = info?.Key ?? "localdb";
        KeysFile = Paths.KeysFor(serviceKey);
        Keys = TranslationKeyStore.Load(KeysFile);

        Memory = info is { UsesResultMemory: true }
            ? new ResultMemory(serviceKey, Paths.ResultMemoryFor(serviceKey))
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

    /// <summary>RF-306 — Prefixo dos identificadores de preset de API personalizada.</summary>
    public const string CustomApiPrefix = "customapi:";

    private ITranslationService CreateService(string key, string? presetName = null)
    {
        if (key == "webfree" && Catalog.FreeWebTranslator is not null)
            return new FreeWebTranslator(Catalog.FreeWebTranslator);

        // VI.5 — a API personalizada é o único serviço da PARTE VI que não depende de
        // credencial de terceiro: quem fornece o endereço é o usuário.
        if (key == "customapi")
        {
            var preset = presetName is null ? null : ApiPresets.Find(presetName);
            string url = preset?.Url ?? Advanced.CustomApiUrl;

            return new CustomApiTranslator(url, preset, log: Notices.Add);
        }

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
                // RF-239 — só se aplica quando a origem NÃO É o idioma-ponte e o serviço
                // declara a tradução ponte suportada. Quem é o idioma-ponte vem do
                // catálogo (RF-567), não de um literal aqui.
                Bridge = Advanced.BridgeTranslation
                         && info.SupportsBridge
                         && Catalog.BridgeLanguage.Length > 0
                         && !string.Equals(source.Key, Catalog.BridgeLanguage,
                                           StringComparison.OrdinalIgnoreCase),
                BridgeCode = Catalog.Language(Catalog.BridgeLanguage)?.CodeFor(info.Key),
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

                // RF-491 — "traduzir uma linha por vez" desativa o agrupamento em blocos.
                OneLinePerTranslation = Debug.Enabled && Debug.OneLinePerTranslation,
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

            // RF-490 — fora do modo de depuração isto é nulo, e o ciclo volta a ser
            // exatamente o que era: desligar o modo restaura o comportamento sem reiniciar.
            Diagnostics = Debug.Enabled ? BuildCycleDiagnostics() : null,

            // RF-554 — o medidor vale sempre, não só em depuração: ele existe para o
            // indicador de RF-558, que é permanente.
            ImageMeter = ImageMeter,
        };
    }

    /// <summary>RF-498 / RF-500 — O que o ciclo recebe para se deixar observar.</summary>
    private CycleDiagnostics BuildCycleDiagnostics() => new()
    {
        Options = Debug,
        Directory = Paths.DiagnosticsDirectory,
        Counters = Counters,

        // RF-500 — o pré-processamento aqui é gerenciado, não uma biblioteca nativa; quem
        // honra os sinalizadores de imagem é o ciclo. O efeito observável é o mesmo.
        SaveImage = (name, image) =>
        {
            if (image is not Gort.Core.Model.ImageBuffer buffer) return;
            try
            {
                string file = Path.Combine(
                    Paths.DiagnosticsDirectory,
                    $"{name}-{DateTime.Now:yyyy-MM-dd-HHmmss-fff}.png");
                Gort.Platform.Diagnostics.PngWriter.Save(buffer, file);
            }
            catch (Exception ex)
            {
                // P8 — uma falha de disco no diagnóstico não pode derrubar o ciclo.
                Counters.RecordError(ex.Message);
            }
        },
    };

    /// <summary>RF-453 — Os atalhos são gravados no seu arquivo ao aplicar (RF-012).</summary>
    public void SaveShortcuts() => ShortcutStore.Save(Paths.Shortcuts, Shortcuts);

    /// <summary>RF-031 / RF-530 — As opções avançadas, no seu arquivo global.</summary>
    public void SaveAdvanced() => Advanced.Save(Paths.AdvancedOptions);

    /// <summary>
    /// RF-558 / RF-559 — O retrato de memória: o total do processo e as três parcelas que o
    /// usuário controla sem saber.
    ///
    /// RF-560 — só soma números que outras partes já mantêm; nada aqui percorre estrutura.
    /// </summary>
    public MemoryReport MemorySnapshot(long overlayBitmapBytes)
    {
        long process;
        try
        {
            using var current = System.Diagnostics.Process.GetCurrentProcess();
            process = current.WorkingSet64;
        }
        catch
        {
            process = 0;
        }
        if (process <= 0) process = GC.GetTotalMemory(false);

        return new MemoryReport
        {
            ProcessBytes = process,
            RegionImageBytes = ImageMeter.Bytes,
            TranslationCacheBytes = (Memory?.ApproximateByteCount ?? 0)
                                    + (Pipeline.Collection?.ApproximateByteCount ?? 0),
            OverlayBitmapBytes = overlayBitmapBytes,
        };
    }

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
