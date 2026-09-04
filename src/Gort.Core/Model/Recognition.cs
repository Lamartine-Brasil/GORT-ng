using Gort.Core.Calibration;

namespace Gort.Core.Model;

/// <summary>7.2 — Palavra: unidade mínima devolvida pelo OCR.</summary>
public sealed class Word
{
    public required string Text { get; init; }

    /// <summary>Caixa da palavra, no espaço da imagem TRATADA (portanto já ampliada).</summary>
    public required Rect Box { get; init; }

    public override string ToString() => $"\"{Text}\" {Box}";
}

/// <summary>
/// 7.3 — Linha: sequência de palavras que o OCR agrupou como uma linha.
/// </summary>
public sealed class Line
{
    private readonly List<Word> _words;

    public Line(IEnumerable<Word> words)
    {
        _words = words.ToList();
        Box = Rect.UnionAll(_words.Select(w => w.Box));   // RF-154
        Orientation = ClassifyOrientation(Box);           // RF-155
        Text = BuildText(_words);                         // RF-152
    }

    public IReadOnlyList<Word> Words => _words;

    /// <summary>RF-154 — Caixa da linha: união das caixas das suas palavras.</summary>
    public Rect Box { get; }

    /// <summary>RF-155 — Horizontal ou vertical, segundo P-33.</summary>
    public Orientation Orientation { get; }

    /// <summary>
    /// RF-152 — Concatenação das palavras separadas por um espaço, INCLUINDO um espaço no
    /// final. 🔒 O comportamento a jusante depende dessa forma exata: não normalizar,
    /// não aparar, não trocar o separador.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// RF-155 — Uma linha é vertical quando a altura da sua caixa supera a largura
    /// multiplicada por P-33 (1,5). 🔒
    /// </summary>
    public static Orientation ClassifyOrientation(Rect box)
        => box.Height > box.Width * P.LineVerticalRatio ? Orientation.Vertical : Orientation.Horizontal;

    private static string BuildText(List<Word> words)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var w in words)
        {
            sb.Append(w.Text);
            sb.Append(' ');   // inclusive após a última palavra — RF-152 🔒
        }
        return sb.ToString();
    }

    public override string ToString() => $"[{Orientation}] \"{Text}\" {Box}";
}

/// <summary>
/// 6.4 — Resultado estruturado devolvido por um motor de OCR.
/// Contém as palavras em ordem de leitura e quantas palavras cada linha tem.
/// </summary>
public sealed class OcrResult
{
    public static readonly OcrResult Empty = new()
    {
        Words = Array.Empty<Word>(),
        WordsPerLine = Array.Empty<int>(),
        IsEmpty = true,
    };

    public required IReadOnlyList<Word> Words { get; init; }

    /// <summary>Quantidade de palavras de cada linha, na ordem das linhas.</summary>
    public required IReadOnlyList<int> WordsPerLine { get; init; }

    public int LineCount => WordsPerLine.Count;

    /// <summary>Indicador de resultado vazio (6.4). Também vale para erro (RF-145).</summary>
    public bool IsEmpty { get; init; }

    /// <summary>
    /// RF-145 — Erros do motor produzem resultado marcado como vazio com a mensagem de erro
    /// no campo de texto principal; o ciclo continua.
    /// </summary>
    public string? ErrorMessage { get; init; }

    public static OcrResult FromError(string message) => new()
    {
        Words = Array.Empty<Word>(),
        WordsPerLine = Array.Empty<int>(),
        IsEmpty = true,
        ErrorMessage = message,
    };

    /// <summary>
    /// 15.1 — Reconstrói as linhas a partir do vetor de palavras e da contagem por linha.
    /// Contagens que ultrapassam o vetor de palavras são truncadas em vez de lançar
    /// (P8 — degradação silenciosa).
    /// </summary>
    public List<Line> BuildLines()
    {
        var lines = new List<Line>(WordsPerLine.Count);
        int offset = 0;
        foreach (int count in WordsPerLine)
        {
            if (offset >= Words.Count) break;
            int take = Math.Min(Math.Max(count, 0), Words.Count - offset);
            if (take == 0) { continue; }
            lines.Add(new Line(Words.Skip(offset).Take(take)));
            offset += take;
        }
        return lines;
    }
}

/// <summary>
/// 7.4 — Bloco de tradução: conjunto de uma ou mais linhas agrupadas pelo
/// pós-processamento, traduzido e desenhado como uma unidade.
/// </summary>
public sealed class TranslationBlock
{
    private readonly List<Line> _lines = new();

    public TranslationBlock(Line first)
    {
        _lines.Add(first);
        Orientation = first.Orientation;   // 7.4 — herdada da primeira linha
        RecalculateBoxes();
    }

    public IReadOnlyList<Line> Lines => _lines;

    /// <summary>Texto reconhecido do bloco: concatenação do texto das suas linhas.</summary>
    public string SourceText => string.Concat(_lines.Select(l => l.Text));

    /// <summary>Preenchido depois da tradução.</summary>
    public string? TranslatedText { get; set; }

    /// <summary>
    /// RF-174 — Verdadeiro se o bloco foi classificado como título; um título nunca
    /// absorve a linha seguinte e, na resolução de colisões, preserva seu retângulo (RF-357).
    /// </summary>
    public bool IsTitle { get; set; }

    /// <summary>Herdada da primeira linha.</summary>
    public Orientation Orientation { get; }

    /// <summary>RF-179 — União das caixas das linhas, em coordenadas da imagem.</summary>
    public Rect SourceBox { get; private set; }

    /// <summary>Retângulo na tela, depois de resolver colisões e expansões.</summary>
    public Rect ViewBox { get; set; }

    /// <summary>Caixa de visualização menos a margem de contorno: onde o texto realmente cabe.</summary>
    public Rect ContentBox { get; set; }

    public void Append(Line line)
    {
        _lines.Add(line);
        RecalculateBoxes();
    }

    /// <summary>
    /// RF-179 — A caixa do bloco é a união das caixas das suas linhas, e as caixas de
    /// origem, de visualização e de conteúdo são inicializadas com esse mesmo valor.
    /// </summary>
    public void RecalculateBoxes()
    {
        SourceBox = Rect.UnionAll(_lines.Select(l => l.Box));
        ViewBox = SourceBox;
        ContentBox = SourceBox;
    }

    public override string ToString()
        => $"{(IsTitle ? "TÍTULO " : "")}{Orientation} x{_lines.Count} \"{SourceText}\"";
}
