using Gort.Core.Model;

namespace Gort.Core.Tests;

/// <summary>
/// Constrói linhas sintéticas com caixas de palavra plausíveis, para exercitar o
/// agrupamento sem depender de um motor de OCR.
///
/// A largura de cada palavra é max(fonte, fonte × 0,6 × nº de caracteres), de modo que
/// min(largura, altura) de cada caixa seja exatamente a fonte pedida — é isso que
/// RF-164 mede.
/// </summary>
public static class LineBuilder
{
    public const double CharWidthRatio = 0.6;
    public const double SpaceRatio = 0.5;

    /// <summary>Linha horizontal começando em (x, y), com a fonte dada.</summary>
    public static Line Horizontal(string text, int x, int y, int font)
    {
        var words = new List<Word>();
        int cursor = x;
        foreach (var token in Tokenize(text))
        {
            int w = Math.Max(font, (int)Math.Round(font * CharWidthRatio * token.Length));
            words.Add(new Word { Text = token, Box = new Rect(cursor, y, w, font) });
            cursor += w + (int)Math.Round(font * SpaceRatio);
        }
        return new Line(words);
    }

    /// <summary>
    /// Coluna vertical começando em (x, y) e descendo. A caixa resultante é alta e
    /// estreita, o que faz RF-155 classificá-la como vertical.
    /// </summary>
    public static Line Vertical(string text, int x, int y, int font)
    {
        var words = new List<Word>();
        int cursor = y;
        foreach (var token in Tokenize(text))
        {
            int h = Math.Max(font, (int)Math.Round(font * CharWidthRatio * token.Length));
            words.Add(new Word { Text = token, Box = new Rect(x, cursor, font, h) });
            cursor += h + (int)Math.Round(font * SpaceRatio);
        }
        return new Line(words);
    }

    /// <summary>Linha com uma única palavra e caixa explícita — para casos de borda.</summary>
    public static Line Explicit(string text, Rect box)
        => new(new[] { new Word { Text = text, Box = box } });

    private static string[] Tokenize(string text)
        => text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
