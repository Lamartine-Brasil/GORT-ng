using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Rendering;

/// <summary>
/// RF-369 a RF-372 — Quebra de linha do texto traduzido. 🔒
///
/// A quebra é POR CARACTERE, não por palavra (PARTE XI, item 15). Isso é deliberado: o
/// idioma de destino pode não ter separador de palavra, e o objetivo é preencher o
/// retângulo do bloco original, não produzir tipografia.
/// </summary>
public static class LineBreaker
{
    /// <summary>
    /// Quebra o texto para caber em <paramref name="available"/> na direção de escoamento.
    ///
    /// RF-372 — As quebras de linha EXPLÍCITAS do texto traduzido são respeitadas ANTES da
    /// quebra automática: o tradutor devolveu aquela estrutura por algum motivo.
    /// </summary>
    public static List<string> Break(ITextMeasurer measurer, string text, FontSpec font,
                                     Orientation orientation, double available)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text)) return result;

        // RF-369 — a dimensão disponível é reduzida por uma folga de P-100 vezes o tamanho
        // da fonte. Sem ela, a última palavra estoura a borda.
        double usable = available - P.LineBreakSlackFactor * font.Size;
        if (usable <= 0) usable = Math.Max(1, available);

        foreach (var explicitLine in SplitExplicit(text))
        {
            BreakOne(measurer, explicitLine, font, orientation, usable, result);
        }
        return result;
    }

    /// <summary>RF-372 — Separa nas quebras explícitas, em qualquer formato.</summary>
    private static IEnumerable<string> SplitExplicit(string text)
        => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static void BreakOne(ITextMeasurer measurer, string line, FontSpec font,
                                 Orientation orientation, double usable, List<string> result)
    {
        if (line.Length == 0)
        {
            result.Add("");
            return;
        }

        string remaining = line;

        while (remaining.Length > 0)
        {
            if (TextMetrics.Length(measurer, remaining, font, orientation) <= usable)
            {
                result.Add(remaining);
                return;
            }

            int fit = LongestPrefixThatFits(measurer, remaining, font, orientation, usable);

            // RF-370 — se nem um caractere couber, coloca-se um mesmo assim, para garantir
            // progresso. Sem isso o laço seria infinito num retângulo estreito demais.
            if (fit < 1) fit = 1;

            result.Add(remaining[..fit]);

            // RF-371 — depois de quebrar, os espaços iniciais do restante são removidos.
            remaining = remaining[fit..].TrimStart(' ');
        }
    }

    /// <summary>
    /// RF-369 — Maior prefixo que cabe, por BUSCA BINÁRIA. 🔒
    ///
    /// Motivo: medir de 1 em 1 caractere torna a busca do tamanho de fonte inviavelmente
    /// lenta — ela repete a quebra a cada iteração da bissecção. A monotonicidade do
    /// comprimento garante que a busca binária dá o mesmo resultado.
    /// </summary>
    internal static int LongestPrefixThatFits(ITextMeasurer measurer, string text, FontSpec font,
                                              Orientation orientation, double usable)
    {
        int low = 0;
        int high = text.Length;

        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            double length = TextMetrics.Length(measurer, text[..middle], font, orientation);

            if (length <= usable) low = middle;
            else high = middle - 1;
        }
        return low;
    }
}
