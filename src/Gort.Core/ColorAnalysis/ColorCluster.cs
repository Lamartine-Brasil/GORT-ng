using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.ColorAnalysis;

/// <summary>
/// RF-398 — Agrupamento de cores. 🔒
///
/// As cores são quantizadas descartando os P-158 bits menos significativos de cada
/// componente (32 níveis por canal), e o VALOR FINAL de cada agrupamento é a MEDIANA por
/// componente das cores que caíram nele.
///
/// A mediana — e não a média — é o que impede que um único pixel de borda desloque a cor
/// escolhida.
/// </summary>
public sealed class ColorCluster
{
    private readonly List<byte> _r = new();
    private readonly List<byte> _g = new();
    private readonly List<byte> _b = new();

    public ColorCluster(int key) => Key = key;

    /// <summary>Chave quantizada do agrupamento.</summary>
    public int Key { get; }

    /// <summary>Quantidade de amostras acumuladas.</summary>
    public int Population { get; private set; }

    /// <summary>Quantas sondas de borda caíram neste agrupamento (RF-400).</summary>
    public int Probes { get; set; }

    /// <summary>Quantas dessas sondas eram CANTOS (RF-400).</summary>
    public int Corners { get; set; }

    /// <summary>Em quantas palavras distintas este agrupamento apareceu (RF-407, RF-401).</summary>
    public int SupportingWords { get; set; }

    /// <summary>Soma de contraste acumulada, para o desempate de RF-408.</summary>
    public double ContrastSum { get; set; }

    public double AverageContrast => Population == 0 ? 0 : ContrastSum / Population;

    public void Add(byte r, byte g, byte b, double contrast = 0)
    {
        _r.Add(r); _g.Add(g); _b.Add(b);
        Population++;
        ContrastSum += contrast;
    }

    public void AddWeighted(Rgba color, int weight)
    {
        for (int i = 0; i < weight; i++) Add(color.R, color.G, color.B);
    }

    /// <summary>RF-398 — O valor final é a mediana POR COMPONENTE.</summary>
    public Rgba Value => new(Median(_r), Median(_g), Median(_b));

    private static byte Median(List<byte> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.ToArray();
        Array.Sort(sorted);
        int n = sorted.Length;
        return n % 2 == 1
            ? sorted[n / 2]
            : (byte)((sorted[n / 2 - 1] + sorted[n / 2]) / 2);
    }

    /// <summary>RF-398 / P-158 — Quantiza descartando os 3 bits menos significativos.</summary>
    public static int Quantize(byte r, byte g, byte b)
    {
        int bits = P.ColorQuantizationBitsDropped;
        return ((r >> bits) << 16) | ((g >> bits) << 8) | (b >> bits);
    }

    public static int Quantize(Rgba c) => Quantize(c.R, c.G, c.B);
}

/// <summary>Conjunto de agrupamentos indexado pela chave quantizada.</summary>
public sealed class ClusterSet
{
    private readonly Dictionary<int, ColorCluster> _clusters = new();

    public IReadOnlyCollection<ColorCluster> Clusters => _clusters.Values;
    public int Count => _clusters.Count;

    public ColorCluster For(byte r, byte g, byte b)
    {
        int key = ColorCluster.Quantize(r, g, b);
        if (!_clusters.TryGetValue(key, out var c))
        {
            c = new ColorCluster(key);
            _clusters[key] = c;
        }
        return c;
    }

    public ColorCluster For(Rgba color) => For(color.R, color.G, color.B);

    /// <summary>
    /// RF-403 — Agrupamento mais frequente. Em caso de empate na frequência, desempata-se
    /// pela MENOR chave quantizada, para que a escolha seja estável entre quadros com o
    /// mesmo conteúdo.
    /// </summary>
    public ColorCluster? MostPopulous()
        => _clusters.Values
            .OrderByDescending(c => c.Population)
            .ThenBy(c => c.Key)
            .FirstOrDefault();
}
