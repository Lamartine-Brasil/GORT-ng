using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Structuring;

/// <summary>
/// RF-163 — Adjacência espacial: o coração do agrupamento. 🔒
///
/// Duas linhas são espacialmente adjacentes quando TODAS estas condições valem:
///   1. têm a mesma orientação;
///   2. o tamanho de fonte estimado de ambas é maior que zero e a razão entre o maior e
///      o menor não excede P-34;
///   3. o intervalo entre elas no eixo de ESCOAMENTO (vertical para linhas horizontais,
///      horizontal para linhas verticais) não excede o tamanho médio de fonte
///      multiplicado por P-35;
///   4. no eixo TRANSVERSAL, ou a sobreposição relativa é de pelo menos P-36, ou a
///      diferença entre os inícios não excede o tamanho médio de fonte multiplicado
///      por P-37.
///
/// Errar aqui é o defeito mais visível do produto (Parte XII.4): diálogos se fragmentam
/// em blocos soltos, ou o nome do personagem gruda no texto da fala.
/// </summary>
public static class Adjacency
{
    /// <summary>
    /// Intervalo entre dois segmentos em um eixo: max(0, max(inícios) − min(fins)).
    /// Zero quando os segmentos se tocam ou se sobrepõem.
    /// </summary>
    public static double AxisGap(double aStart, double aEnd, double bStart, double bEnd)
        => Math.Max(0, Math.Max(aStart, bStart) - Math.Min(aEnd, bEnd));

    /// <summary>
    /// Sobreposição relativa entre dois segmentos:
    ///   max(0, min(fins) − max(inícios)) ÷ max(1, min(comprimentos)).
    /// O max(1, ...) do denominador é da especificação e protege contra segmentos
    /// degenerados; não trocar por uma guarda diferente.
    /// </summary>
    public static double Overlap(double aStart, double aEnd, double bStart, double bEnd)
    {
        double inter = Math.Max(0, Math.Min(aEnd, bEnd) - Math.Max(aStart, bStart));
        double denom = Math.Max(1, Math.Min(aEnd - aStart, bEnd - bStart));
        return inter / denom;
    }

    /// <summary>RF-163 — Aplica as quatro condições às duas linhas.</summary>
    public static bool AreAdjacent(Line a, Line b)
        => AreAdjacent(a, FontSizeEstimator.Estimate(a), b, FontSizeEstimator.Estimate(b));

    /// <summary>
    /// Sobrecarga que recebe os tamanhos de fonte já calculados — o agrupamento os calcula
    /// uma única vez por linha, porque a adjacência é consultada O(n²) vezes.
    /// </summary>
    public static bool AreAdjacent(Line a, double fontA, Line b, double fontB)
    {
        // 1. mesma orientação
        if (a.Orientation != b.Orientation) return false;

        // 2. tamanhos positivos e razão dentro de P-34
        if (fontA <= 0 || fontB <= 0) return false;
        double ratio = Math.Max(fontA, fontB) / Math.Min(fontA, fontB);
        if (ratio > P.AdjacencyMaxFontRatio) return false;

        double avg = (fontA + fontB) / 2.0;
        Rect ra = a.Box, rb = b.Box;

        double flowGap, crossOverlap, crossStartDiff;
        if (a.Orientation == Orientation.Horizontal)
        {
            // Escoamento vertical; transversal horizontal.
            flowGap = AxisGap(ra.Top, ra.Bottom, rb.Top, rb.Bottom);
            crossOverlap = Overlap(ra.Left, ra.Right, rb.Left, rb.Right);
            crossStartDiff = Math.Abs(ra.Left - rb.Left);
        }
        else
        {
            // Vertical: eixos trocados. Escoamento horizontal; transversal vertical.
            flowGap = AxisGap(ra.Left, ra.Right, rb.Left, rb.Right);
            crossOverlap = Overlap(ra.Top, ra.Bottom, rb.Top, rb.Bottom);
            crossStartDiff = Math.Abs(ra.Top - rb.Top);
        }

        // 4. eixo transversal
        bool crossOk = crossOverlap >= P.AdjacencyMinCrossOverlap
                       || crossStartDiff <= avg * P.AdjacencyStartAlignFactor;
        if (!crossOk) return false;

        // 3. eixo de escoamento
        return flowGap <= avg * P.AdjacencyFlowGapFactor;
    }
}
