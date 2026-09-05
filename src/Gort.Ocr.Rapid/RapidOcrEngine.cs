using Gort.Core.Calibration;
using Gort.Core.Model;
using Gort.Core.Ocr;
using Gort.Ocr.Rapid.Detection;
using Gort.Ocr.Rapid.Recognition;
using Microsoft.ML.OnnxRuntime;

namespace Gort.Ocr.Rapid;

/// <summary>
/// RF-128 — Localização dos arquivos do motor.
///
/// O motor procura o seu modelo na subpasta de modelos do programa. Se não o encontrar,
/// procura nos locais convencionais do sistema; só então se declara indisponível.
/// </summary>
public static class ModelLocator
{
    /// <summary>
    /// Nomes de reserva, usados só quando o catálogo de dados não está disponível — por
    /// exemplo num teste que instancia o motor sozinho. O caminho normal é o catálogo
    /// (RF-029).
    /// </summary>
    public const string DetectionModel = "ch_PP-OCRv4_det_infer.onnx";
    public const string RecognitionModel = "ch_PP-OCRv4_rec_infer.onnx";

    /// <summary>Pastas onde o modelo é procurado, em ordem.</summary>
    public static IEnumerable<string> SearchPaths(string? explicitDirectory = null)
    {
        if (!string.IsNullOrEmpty(explicitDirectory)) yield return explicitDirectory;

        // RF-003 — a pasta do executável é o diretório de trabalho corrente.
        yield return Path.Combine(AppContext.BaseDirectory, "modelos");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "modelos");

        // Subindo a partir do executável, para o cenário de desenvolvimento em que o
        // binário está em bin/Debug e os modelos na raiz do repositório.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            yield return Path.Combine(dir.FullName, "modelos");
        }
    }

    /// <summary>Devolve o caminho do modelo, ou null quando ele não está em lugar nenhum.</summary>
    public static string? Find(string fileName, string? explicitDirectory = null)
    {
        foreach (var directory in SearchPaths(explicitDirectory))
        {
            try
            {
                string candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch
            {
                // Um caminho inválido não interrompe a busca (P8).
            }
        }
        return null;
    }
}

/// <summary>
/// RF-121 — "Motor de reconhecimento moderno embarcado": não usa rede, devolve posição por
/// palavra e por linha, e é o de melhor qualidade local. Requer modelo e biblioteca nativa
/// presentes.
///
/// É o motor do Apêndice A: ONNX Runtime com RapidOCR.
///
/// O DETECTOR é um só, comum a todos os idiomas — ele acha ONDE há texto, não O QUE está
/// escrito. Já o RECONHECEDOR é específico do idioma, e é carregado sob demanda: manter
/// todos residentes custaria memória e tempo de abertura por idiomas que talvez nunca sejam
/// usados.
/// </summary>
public sealed class RapidOcrEngine : IOcrEngine
{
    private readonly TextDetector? _detector;
    private readonly string? _modelDirectory;
    private readonly ChannelOrder _order;
    private readonly SessionOptions? _sessionOptions;
    private readonly Gort.Core.Catalog.ModernOcrModels? _models;
    private readonly Dictionary<string, TextRecognizer> _recognizers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public RapidOcrEngine(string? modelDirectory = null,
                          ChannelOrder order = ChannelOrder.Rgb,
                          Gort.Core.Catalog.ModernOcrModels? models = null)
    {
        _modelDirectory = modelDirectory;
        _order = order;
        _models = models;

        // RF-029 — quando o catálogo de dados não é fornecido, cai para os nomes de reserva.
        string detectionName = models?.Detection ?? ModelLocator.DetectionModel;
        Languages = models is not null && models.Languages.Any()
            ? models.Languages.ToList()
            : new List<string> { "en" };

        string? detPath = ModelLocator.Find(detectionName, modelDirectory);
        if (detPath is null)
        {
            UnavailableReason =
                $"O modelo de detecção '{detectionName}' não foi encontrado na subpasta " +
                "'modelos' do programa.";
            return;
        }

        // Nenhum reconhecedor localizável significa motor inútil: melhor dizer isso agora
        // (RF-576) do que falhar no meio de uma tradução.
        if (!ResolvableLanguages().Any())
        {
            UnavailableReason =
                "Nenhum modelo de reconhecimento foi encontrado na subpasta 'modelos' " +
                "do programa.";
            return;
        }

        try
        {
            // O laço já divide a máquina com um jogo (RF-227): limitar as threads da
            // inferência evita que o OCR tome todos os núcleos e faça o jogo engasgar.
            _sessionOptions = new SessionOptions
            {
                IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount - 1),
                InterOpNumThreads = 1,
            };

            _detector = new TextDetector(detPath, order: order, sessionOptions: _sessionOptions);
        }
        catch (Exception ex)
        {
            _detector?.Dispose();
            _detector = null;
            UnavailableReason = $"Falha ao inicializar o motor de reconhecimento: {ex.Message}";
        }
    }

    public string Key => "modern";

    public bool IsAvailable => _detector is not null;

    public string? UnavailableReason { get; }

    /// <summary>
    /// RF-121 — Este motor devolve posição por palavra e por linha, então a sobreposição é
    /// permitida com ele (RF-351).
    /// </summary>
    public bool ProvidesWordPositions => true;

    /// <summary>
    /// RF-151 — Os idiomas que este motor sabe reconhecer. A interseção com a tabela de
    /// idiomas é feita pelo catálogo.
    /// </summary>
    public IReadOnlyList<string> Languages { get; }

    /// <summary>
    /// RF-140 — Quando a opção de orientação vertical está ativa, as linhas verticais são
    /// reordenadas por coluna: coordenada horizontal DECRESCENTE e, dentro da mesma coluna,
    /// coordenada vertical crescente. As linhas horizontais mantêm sua posição original na
    /// lista. 🔒
    /// </summary>
    public bool VerticalOrientation { get; set; }

    /// <summary>P-30 — Teto de linhas reconhecidas por imagem.</summary>
    public int MaxLines { get; init; } = P.ModernOcrMaxLines;

    public OcrResult Recognize(ImageBuffer image, string languageCode)
    {
        if (!IsAvailable) return OcrResult.FromError(UnavailableReason ?? "Motor indisponível.");
        if (image.IsEmpty) return OcrResult.Empty;

        var recognizer = RecognizerFor(languageCode);
        if (recognizer is null)
        {
            return OcrResult.FromError(
                $"Não há modelo de reconhecimento para o idioma '{languageCode}'.");
        }

        var detections = _detector!.Detect(image);

        // Uma imagem sem texto produz resultado VAZIO, não erro (critério de aceite da
        // Etapa 5 e caso de erro do cap. 14).
        if (detections.Count == 0) return OcrResult.Empty;

        var ordered = SortForReading(detections);

        // P-30 — o excedente é perdido silenciosamente (PARTE VIII).
        if (ordered.Count > MaxLines) ordered = ordered.Take(MaxLines).ToList();

        // UMA LINHA POR CHAMADA, por medição.
        //
        // O reconhecimento em lote existe em `TextRecognizer.RecognizeBatch` e está testado,
        // mas o motor NÃO o usa: medido na mesma imagem, pelos dois caminhos, ele saiu 4,9%
        // MAIS LENTO (66,3 ms contra 69,6 ms em 9 linhas reais; -1,1% em 40 linhas
        // sintéticas), com zero diferença de texto.
        //
        // A razão é a largura do tensor: num lote ela é a da linha MAIS LARGA, e as demais
        // viajam preenchidas com zeros até lá. Esse desperdício de cálculo consome o que se
        // economiza no custo fixo por chamada. A hipótese de que o custo fixo dominava
        // estava errada, e `tools/Gort.OcrProbe` refaz a medição a qualquer momento.
        var lines = new List<(string Text, Rect Box)>(ordered.Count);
        foreach (var detection in ordered)
        {
            var crop = ImageOps.Crop(image, detection.Box);
            var recognized = recognizer.Recognize(crop);
            if (string.IsNullOrWhiteSpace(recognized.Text)) continue;
            lines.Add((recognized.Text, detection.Box));
        }

        if (lines.Count == 0) return OcrResult.Empty;

        // RF-141 — o detector devolve LINHAS; cada uma vira uma única "palavra" com a caixa
        // da própria linha.
        return OcrResultBuilder.FromLines(lines);
    }

    /// <summary>Idiomas cujo modelo de reconhecimento existe de fato no disco.</summary>
    private IEnumerable<string> ResolvableLanguages()
    {
        foreach (var language in Languages)
        {
            if (ResolveRecognitionPaths(language) is not null) yield return language;
        }
    }

    private (string Model, string? Dictionary)? ResolveRecognitionPaths(string language)
    {
        string modelName;
        string? dictionaryName = null;

        var declared = _models?.For(language);
        if (declared is not null)
        {
            modelName = declared.Model;
            dictionaryName = declared.Dictionary;
        }
        else if (_models is null)
        {
            modelName = ModelLocator.RecognitionModel;   // reserva, sem catálogo
        }
        else
        {
            return null;
        }

        string? modelPath = ModelLocator.Find(modelName, _modelDirectory);
        if (modelPath is null) return null;

        string? dictionaryPath = dictionaryName is null
            ? null
            : ModelLocator.Find(dictionaryName, _modelDirectory);

        return (modelPath, dictionaryPath);
    }

    /// <summary>
    /// Reconhecedor do idioma, carregado sob demanda e mantido em cache. Um idioma sem
    /// modelo devolve nulo, e o chamador transforma isso na mensagem de RF-145.
    /// </summary>
    private TextRecognizer? RecognizerFor(string languageCode)
    {
        lock (_gate)
        {
            if (_recognizers.TryGetValue(languageCode, out var cached)) return cached;

            var paths = ResolveRecognitionPaths(languageCode);
            if (paths is null) return null;

            var recognizer = new TextRecognizer(paths.Value.Model, _order, _sessionOptions,
                                                paths.Value.Dictionary);
            _recognizers[languageCode] = recognizer;
            return recognizer;
        }
    }

    /// <summary>
    /// Ordem de leitura das linhas detectadas.
    ///
    /// O caso comum é horizontal: de cima para baixo, e da esquerda para a direita dentro
    /// da mesma faixa. RF-140 acrescenta o tratamento das linhas verticais.
    /// </summary>
    private List<TextDetector.Detection> SortForReading(List<TextDetector.Detection> detections)
    {
        if (!VerticalOrientation)
        {
            return detections
                .OrderBy(d => d.Box.Top)
                .ThenBy(d => d.Box.Left)
                .ToList();
        }

        // RF-140 — identifica as verticais por P-32 e as reordena por coluna, da direita
        // para a esquerda; as horizontais ficam onde estavam.
        var result = new List<TextDetector.Detection>(detections);

        var verticalIndices = new List<int>();
        for (int i = 0; i < result.Count; i++)
        {
            var box = result[i].Box;
            if (box.Height > box.Width * P.ModernOcrVerticalRatio) verticalIndices.Add(i);
        }

        var reordered = verticalIndices
            .Select(i => result[i])
            .OrderByDescending(d => d.Box.Left)
            .ThenBy(d => d.Box.Top)
            .ToList();

        for (int i = 0; i < verticalIndices.Count; i++) result[verticalIndices[i]] = reordered[i];
        return result;
    }

    public void Dispose()
    {
        _detector?.Dispose();
        lock (_gate)
        {
            foreach (var r in _recognizers.Values) r.Dispose();
            _recognizers.Clear();
        }
    }
}
