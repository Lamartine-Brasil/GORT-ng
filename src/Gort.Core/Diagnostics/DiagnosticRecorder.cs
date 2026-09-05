using Gort.Core.Model;

namespace Gort.Core.Diagnostics;

/// <summary>
/// RF-490 / RF-491 — Sinalizadores do modo de depuração.
/// </summary>
public sealed class DebugOptions
{
    /// <summary>RF-490 — O modo de depuração está ativo.</summary>
    public bool Enabled { get; set; }

    /// <summary>RF-491 — Ignora o intervalo entre ciclos e roda o mais rápido possível.</summary>
    public bool UnlockSpeed { get; set; }

    /// <summary>RF-491 — Prefixa as traduções vindas do cache com um marcador e a contagem.</summary>
    public bool ShowCacheResults { get; set; }

    /// <summary>RF-157 / RF-491 — Desativa o agrupamento de linhas em blocos.</summary>
    public bool OneLinePerTranslation { get; set; }

    /// <summary>
    /// RF-491 — Desenha retângulos semitransparentes sobre as caixas de origem em vez do
    /// fundo normal, e emite registros detalhados das decisões.
    /// </summary>
    public bool ShowWordAreas { get; set; }

    /// <summary>RF-491 / RF-492 — Grava um retrato completo do ciclo.</summary>
    public bool SaveAnalysis { get; set; }

    /// <summary>RF-496 — Grava o resultado em arquivo de texto a cada ciclo.</summary>
    public bool WriteResultFile { get; set; }

    /// <summary>
    /// RF-500 — Sinalizadores repassados ao mecanismo nativo de pré-processamento.
    /// </summary>
    public bool NativeDebug { get; set; }
    public bool NativeShowReplacements { get; set; }
    public bool NativeSaveCapture { get; set; }
    public bool NativeSaveResult { get; set; }
}

/// <summary>
/// RF-498 — Contadores internos de tentativas de OCR e de traduções, com um registro de
/// mensagens acessível.
/// </summary>
public sealed class DiagnosticCounters
{
    private readonly object _gate = new();
    private readonly List<string> _messages = new();

    /// <summary>Teto do registro, para que ele não cresça sem limite numa sessão longa.</summary>
    public int MaxMessages { get; init; } = 500;

    public int OcrAttempts { get; private set; }
    public int Translations { get; private set; }
    public int NetworkCalls { get; private set; }
    public int Errors { get; private set; }

    public void RecordOcr() { lock (_gate) OcrAttempts++; }

    public void RecordTranslation(int networkCalls)
    {
        lock (_gate)
        {
            Translations++;
            NetworkCalls += networkCalls;
        }
    }

    public void RecordError(string message)
    {
        lock (_gate)
        {
            Errors++;
            Add($"erro: {message}");
        }
    }

    public void Log(string message) { lock (_gate) Add(message); }

    private void Add(string message)
    {
        _messages.Add($"{DateTime.Now:HH:mm:ss.fff}  {message}");
        while (_messages.Count > MaxMessages) _messages.RemoveAt(0);
    }

    public IReadOnlyList<string> Messages
    {
        get { lock (_gate) return _messages.ToList(); }
    }

    public void Reset()
    {
        lock (_gate)
        {
            OcrAttempts = Translations = NetworkCalls = Errors = 0;
            _messages.Clear();
        }
    }

    public override string ToString()
        => $"OCR: {OcrAttempts} · traduções: {Translations} · rede: {NetworkCalls} · erros: {Errors}";
}

/// <summary>
/// Monta o retrato de análise a partir do resultado de um ciclo, e cuida da regra de
/// RF-495: um retrato pendente cujo desenho não completou é gravado SEM a parte de desenho,
/// e não descartado.
/// </summary>
public sealed class DiagnosticRecorder
{
    private readonly string _directory;
    private AnalysisSnapshot? _pending;

    public DiagnosticRecorder(string directory) => _directory = directory;

    /// <summary>
    /// RF-492 — Monta o retrato de um ciclo, com tudo o que o programa decidiu.
    /// </summary>
    public static AnalysisSnapshot Build(
        IReadOnlyList<RegionResult> regions, string recognizedText, string translatedText,
        string windowMode, string ocrEngine, string translationService)
    {
        var snapshot = new AnalysisSnapshot
        {
            WindowMode = windowMode,
            OcrEngine = ocrEngine,
            TranslationService = translationService,
            RecognizedText = recognizedText,
            TranslatedText = translatedText,
        };

        foreach (var region in regions)
        {
            var area = new SnapshotArea
            {
                Index = region.Index,
                IsSnapshot = region.IsSnapshot,
                AreaRect = SnapshotRect.From(region.ScreenRect),
                ResultRect = SnapshotRect.From(region.ResultBox),
                RecognizedText = string.Concat(region.Lines.Select(l => l.Text)),
                TranslatedText = region.RawTranslatedText ?? "",
            };

            foreach (var line in region.Lines)
            {
                var snapshotLine = new SnapshotLine
                {
                    Text = line.Text,
                    Orientation = line.Orientation.ToString(),
                    Box = SnapshotRect.From(line.Box),
                };

                foreach (var word in line.Words)
                {
                    snapshotLine.Words.Add(new SnapshotWord
                    {
                        Text = word.Text,
                        Box = SnapshotRect.From(word.Box),
                    });
                }
                area.Lines.Add(snapshotLine);
            }

            foreach (var block in region.Blocks)
            {
                area.Blocks.Add(new SnapshotBlock
                {
                    Text = block.SourceText,
                    Translated = block.TranslatedText,
                    IsTitle = block.IsTitle,
                    Orientation = block.Orientation.ToString(),
                    SourceBox = SnapshotRect.From(block.SourceBox),
                    ViewBox = SnapshotRect.From(block.ViewBox),
                    ContentBox = SnapshotRect.From(block.ContentBox),
                    LinesBox = SnapshotRect.From(Rect.UnionAll(block.Lines.Select(l => l.Box))),
                });
            }

            if (region.UsesAutoColor)
            {
                foreach (var color in region.AutoColors)
                {
                    area.AutoColors.Add(new SnapshotColors
                    {
                        Font = color?.Font.ToString(),
                        Background = color?.Background.ToString(),
                        SupportingWords = color?.SupportingWords ?? 0,
                        Contrast = color?.Contrast ?? 0,
                        UsedFallback = color?.UsedFallback ?? false,
                        ContrastCorrected = color?.ContrastCorrected ?? false,
                    });
                }
            }

            snapshot.Areas.Add(area);
        }

        return snapshot;
    }

    /// <summary>
    /// Registra um ciclo.
    ///
    /// RF-493 — No modo sobreposição o retrato só é gravado DEPOIS que o desenho terminou;
    /// até lá ele fica pendente.
    /// RF-495 — Se um ciclo seguinte começar antes disso, o pendente é gravado SEM a parte
    /// de desenho, e não descartado: perder o retrato justamente do quadro em que o desenho
    /// demorou seria perder a evidência do problema que se quer investigar.
    /// </summary>
    public string? Record(AnalysisSnapshot snapshot, bool waitsForDrawing)
    {
        string? flushed = FlushPending();

        if (waitsForDrawing)
        {
            _pending = snapshot;
            return flushed;
        }

        snapshot.Save(_directory);
        return flushed;
    }

    /// <summary>RF-493 — O desenho terminou: completa o retrato pendente e grava.</summary>
    public string? CompleteDrawing(SnapshotDrawing drawing)
    {
        if (_pending is null) return null;

        _pending.Drawing = drawing;
        string path = _pending.Save(_directory);
        _pending = null;
        return path;
    }

    /// <summary>RF-495 — Grava um pendente sem a parte de desenho.</summary>
    public string? FlushPending()
    {
        if (_pending is null) return null;

        string path = _pending.Save(_directory);
        _pending = null;
        return path;
    }

    public bool HasPending => _pending is not null;
}
