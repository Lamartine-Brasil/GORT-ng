using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Rendering;

/// <summary>
/// RF-360 a RF-368 — Escolha do tamanho de fonte e posicionamento das linhas na
/// sobreposição. 🔒
///
/// É o grupo que a PARTE XII.4 lista como "layout da sobreposição": errar aqui produz texto
/// recortado, blocos escrevendo um por cima do outro, ou fonte grande demais e minúscula.
/// </summary>
public static class OverlayTextLayout
{
    /// <summary>
    /// RF-368 — Faixa em que uma linha é desenhada. 🔒
    ///
    /// Para blocos HORIZONTAIS: a largura inteira do conteúdo, altura igual ao avanço,
    /// deslocada verticalmente pelo índice multiplicado pelo avanço (com PISO).
    ///
    /// Para blocos VERTICAIS: a altura inteira do conteúdo, largura igual ao avanço,
    /// posicionada a partir da DIREITA recuando o índice mais um multiplicado pelo avanço
    /// (com TETO) — é a ordem de leitura em colunas, da direita para a esquerda.
    /// </summary>
    public static RectD LineBand(RectD content, int index, double advance, Orientation orientation)
    {
        if (orientation == Orientation.Vertical)
        {
            double right = content.Right - Math.Ceiling(index * advance);
            double left = content.Right - Math.Ceiling((index + 1) * advance);
            return RectD.FromBounds(left, content.Top, right, content.Bottom);
        }

        double top = content.Top + Math.Floor(index * advance);
        return new RectD(content.X, top, content.Width, advance);
    }

    /// <summary>
    /// RF-364 — Teste de "cabe". 🔒
    ///
    /// Feito POSICIONANDO CADA LINHA exatamente onde ela será desenhada e verificando se os
    /// limites do desenho ultrapassam o retângulo de conteúdo — NÃO pela soma de alturas de
    /// linha.
    ///
    /// Motivo, na letra do requisito: somar alturas ignora o espaço que a última linha ocupa
    /// dentro da sua faixa, e o texto acaba invadindo o bloco vizinho.
    /// </summary>
    public static bool Fits(ITextMeasurer measurer, IReadOnlyList<string> lines, FontSpec font,
                            Orientation orientation, RectD content, bool fontStroke)
    {
        double advance = TextMetrics.LineAdvance(measurer, font);
        if (advance <= 0) return false;

        // RF-366 — com contorno, os limites medidos são expandidos em P-99 antes da
        // comparação: o traço do contorno sai para fora do glifo.
        double slack = fontStroke ? P.StrokeMeasurementSlack : 0;

        for (int i = 0; i < lines.Count; i++)
        {
            // RF-367 — linhas compostas apenas de espaços são ignoradas: elas não desenham
            // nada, e contá-las encolheria a fonte sem motivo.
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            var band = LineBand(content, i, advance, orientation);

            // A faixa da linha já saiu do retângulo de conteúdo.
            if (orientation == Orientation.Vertical)
            {
                if (band.Left - slack < content.Left) return false;
            }
            else
            {
                if (band.Bottom + slack > content.Bottom) return false;
            }

            double length = TextMetrics.Length(measurer, lines[i], font, orientation);
            double availableLength = orientation == Orientation.Vertical
                ? content.Height
                : content.Width;

            if (length + slack > availableLength) return false;
        }

        return true;
    }

    /// <summary>
    /// RF-363 — Tamanho final da fonte, por BUSCA BINÁRIA entre o mínimo e o preferido. 🔒
    ///
    ///   1. primeiro testa-se DIRETAMENTE o tamanho preferido; se couber, usa-se ele — é o
    ///      atalho para o caso comum, e RF-550 o lista entre as otimizações obrigatórias;
    ///   2. senão, no máximo P-96 iterações de bissecção, parando quando a diferença entre
    ///      os limites for menor ou igual a P-97.
    /// </summary>
    public static double FindFontSize(ITextMeasurer measurer, string text, FontSpec template,
                                      Orientation orientation, RectD content,
                                      double minimum, double preferred, bool fontStroke)
    {
        double availableForBreak = orientation == Orientation.Vertical
            ? content.Height
            : content.Width;

        bool FitsAt(double size)
        {
            var font = template with { Size = size };
            var lines = LineBreaker.Break(measurer, text, font, orientation, availableForBreak);
            return Fits(measurer, lines, font, orientation, content, fontStroke);
        }

        // 1. Atalho para o caso comum: quase sempre o texto cabe no tamanho preferido, e
        //    pagar a bissecção inteira por isso custaria caro no laço de 300 ms.
        if (FitsAt(preferred)) return preferred;

        if (minimum >= preferred) return minimum;

        double low = minimum;
        double high = preferred;

        for (int i = 0; i < P.FontSizeSearchIterations; i++)
        {
            if (high - low <= P.FontSizeSearchEpsilon) break;

            double middle = (low + high) / 2;
            if (FitsAt(middle)) low = middle;
            else high = middle;
        }

        return low;
    }

    /// <summary>
    /// RF-360 — Tamanho PREFERIDO de um bloco, derivado do tamanho do texto original. 🔒
    ///
    ///   1. tamanho mediano das linhas do bloco — altura da caixa para blocos horizontais,
    ///      largura para verticais; se zero, o tamanho estimado por RF-164;
    ///   2. tamanho mediano das linhas de todos os blocos NÃO TÍTULO da mesma área e mesma
    ///      orientação: o "tamanho do corpo";
    ///   3. usa-se o tamanho PRÓPRIO quando o bloco é título, ou quando é o bloco mais
    ///      acima/à esquerda da área E seu tamanho é pelo menos P-94 vezes o do corpo;
    ///      caso contrário usa-se o tamanho do corpo;
    ///   4. converte-se de pixels de imagem para pontos.
    ///
    /// Motivo do passo 3, na letra do requisito: blocos pequenos dentro de um parágrafo não
    /// devem encolher em relação ao parágrafo; mas um cabeçalho genuinamente maior deve
    /// manter seu tamanho.
    /// </summary>
    public static double PreferredFontSize(double ownMedian, double bodyMedian,
                                           bool isTitle, bool isLeadBlock,
                                           double scale, double verticalDpi)
    {
        double chosen;

        if (isTitle)
        {
            chosen = ownMedian;
        }
        else if (isLeadBlock && bodyMedian > 0 && ownMedian >= bodyMedian * P.LeadBlockOwnSizeRatio)
        {
            chosen = ownMedian;
        }
        else
        {
            chosen = bodyMedian > 0 ? bodyMedian : ownMedian;
        }

        // 4. pixels de imagem → pontos: dividir pela ampliação, multiplicar por 72, dividir
        //    pela resolução vertical, e multiplicar por P-95.
        if (scale <= 0) scale = 1;
        if (verticalDpi <= 0) verticalDpi = P.ReferenceDpi;

        return chosen / scale * 72.0 / verticalDpi * P.DerivedFontSizeScale;
    }

    /// <summary>
    /// RF-361 — O tamanho preferido é saturado entre o mínimo e o máximo configurados.
    /// </summary>
    public static double Clamp(double preferred, double minimum, double maximum)
        => Math.Clamp(preferred, minimum, Math.Max(minimum, maximum));

    /// <summary>
    /// RF-360, passo 1 — Tamanho mediano das linhas de um bloco: altura da caixa para blocos
    /// horizontais, largura para verticais. Zero cai para o tamanho estimado por RF-164.
    /// </summary>
    public static double MedianLineSize(IEnumerable<Line> lines, Orientation orientation)
    {
        var sizes = new List<double>();

        foreach (var line in lines)
        {
            double size = orientation == Orientation.Vertical ? line.Box.Width : line.Box.Height;
            if (size <= 0) size = Structuring.FontSizeEstimator.Estimate(line);
            if (size > 0) sizes.Add(size);
        }

        return Structuring.FontSizeEstimator.Median(sizes);
    }
}
