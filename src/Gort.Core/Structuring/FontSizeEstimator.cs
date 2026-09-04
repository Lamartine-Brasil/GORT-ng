using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Structuring;

/// <summary>
/// RF-164 — Tamanho de fonte estimado de uma linha. 🔒
///
/// É a MEDIANA de min(largura, altura) sobre todas as caixas de palavra com largura e
/// altura positivas. Se não houver nenhuma, o valor é P-38. Para número par de amostras,
/// a mediana é a média das duas centrais, com piso em 1.
///
/// Motivo (da especificação): a mediana resiste a caixas espúrias de pontuação e ruído;
/// usar min da caixa aproxima a altura x da fonte independentemente da orientação.
///
/// Não trocar por média: a média é justamente o que a mediana existe para evitar aqui.
/// </summary>
public static class FontSizeEstimator
{
    public static double Estimate(Line line) => Estimate(line.Words);

    public static double Estimate(IReadOnlyList<Word> words)
    {
        var samples = new List<int>(words.Count);
        foreach (var w in words)
        {
            if (w.Box.Width > 0 && w.Box.Height > 0)
                samples.Add(Math.Min(w.Box.Width, w.Box.Height));
        }

        if (samples.Count == 0) return P.FontSizeFallback;   // P-38

        samples.Sort();
        int n = samples.Count;
        if (n % 2 == 1) return samples[n / 2];

        // Número par de amostras: média das duas centrais, com piso em 1.
        return Math.Max(1, (samples[n / 2 - 1] + samples[n / 2]) / 2.0);
    }

    /// <summary>
    /// Mediana dos tamanhos de fonte de um conjunto de linhas, usada pelo teste de
    /// anexação (RF-176, passo 2) e pelo tamanho automático de fonte (RF-360).
    /// Devolve 0 quando não há linhas — o chamador exige valor positivo.
    /// </summary>
    public static double Median(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count == 0) return 0;
        list.Sort();
        int n = list.Count;
        return n % 2 == 1 ? list[n / 2] : (list[n / 2 - 1] + list[n / 2]) / 2.0;
    }
}
