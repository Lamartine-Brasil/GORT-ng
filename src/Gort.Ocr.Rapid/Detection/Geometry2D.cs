namespace Gort.Ocr.Rapid.Detection;

/// <summary>Ponto em ponto flutuante, no espaço do mapa de probabilidade.</summary>
public readonly record struct PointD(double X, double Y)
{
    public static PointD operator +(PointD a, PointD b) => new(a.X + b.X, a.Y + b.Y);
    public static PointD operator -(PointD a, PointD b) => new(a.X - b.X, a.Y - b.Y);
    public static PointD operator *(PointD a, double k) => new(a.X * k, a.Y * k);

    public double Length => Math.Sqrt(X * X + Y * Y);

    public override string ToString() => $"({X:0.##},{Y:0.##})";
}

/// <summary>
/// Retângulo rotacionado, como o devolve o cálculo de área mínima: centro, tamanho e
/// ângulo. O pós-processamento do detector trabalha com ele até a conversão final para
/// caixa alinhada aos eixos (RF-142).
/// </summary>
public readonly record struct RotatedRect(PointD Center, double Width, double Height, double Angle)
{
    public double MinSide => Math.Min(Width, Height);

    /// <summary>Os quatro vértices, em ordem.</summary>
    public PointD[] Corners()
    {
        double c = Math.Cos(Angle), s = Math.Sin(Angle);
        double hw = Width / 2, hh = Height / 2;

        var offsets = new[]
        {
            new PointD(-hw, -hh), new PointD(hw, -hh),
            new PointD(hw, hh), new PointD(-hw, hh),
        };

        var corners = new PointD[4];
        for (int i = 0; i < 4; i++)
        {
            corners[i] = new PointD(
                Center.X + offsets[i].X * c - offsets[i].Y * s,
                Center.Y + offsets[i].X * s + offsets[i].Y * c);
        }
        return corners;
    }

    /// <summary>
    /// Expande o retângulo em <paramref name="distance"/> para cada lado.
    ///
    /// É a operação de "desprender" (unclip) do pós-processamento do DBNet. A formulação
    /// canônica infla o POLÍGONO e depois toma de novo o retângulo de área mínima; como o
    /// polígono de entrada já é um retângulo, inflá-lo e reduzi-lo ao retângulo de área
    /// mínima dá exatamente o mesmo resultado que somar 2·distância a cada dimensão — sem
    /// precisar de uma biblioteca de recorte de polígonos.
    /// </summary>
    public RotatedRect Expand(double distance)
        => this with { Width = Width + 2 * distance, Height = Height + 2 * distance };
}

/// <summary>
/// Geometria de que o pós-processamento do detector precisa: casco convexo, retângulo de
/// área mínima e preenchimento de polígono.
/// </summary>
public static class Geometry2D
{
    /// <summary>
    /// Casco convexo pela varredura monótona de Andrew. Devolve os vértices em sentido
    /// anti-horário, sem pontos colineares.
    /// </summary>
    public static List<PointD> ConvexHull(IReadOnlyList<PointD> points)
    {
        if (points.Count <= 2) return points.ToList();

        var sorted = points.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();

        static double Cross(PointD o, PointD a, PointD b)
            => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

        var lower = new List<PointD>();
        foreach (var p in sorted)
        {
            while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0) lower.RemoveAt(lower.Count - 1);
            lower.Add(p);
        }

        var upper = new List<PointD>();
        for (int i = sorted.Count - 1; i >= 0; i--)
        {
            var p = sorted[i];
            while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0) upper.RemoveAt(upper.Count - 1);
            upper.Add(p);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    /// <summary>
    /// Retângulo de área mínima que contém os pontos, por calibradores rotativos sobre o
    /// casco convexo: o retângulo mínimo tem sempre um lado colinear com uma aresta do
    /// casco, então basta testar cada aresta.
    /// </summary>
    public static RotatedRect MinAreaRect(IReadOnlyList<PointD> points)
    {
        var hull = ConvexHull(points);
        if (hull.Count == 0) return default;
        if (hull.Count == 1) return new RotatedRect(hull[0], 0, 0, 0);

        double bestArea = double.MaxValue;
        RotatedRect best = default;

        for (int i = 0; i < hull.Count; i++)
        {
            var a = hull[i];
            var b = hull[(i + 1) % hull.Count];

            var edge = b - a;
            double length = edge.Length;
            if (length < 1e-9) continue;

            // Base ortonormal alinhada com a aresta.
            var ux = new PointD(edge.X / length, edge.Y / length);
            var uy = new PointD(-ux.Y, ux.X);

            double minU = double.MaxValue, maxU = double.MinValue;
            double minV = double.MaxValue, maxV = double.MinValue;

            foreach (var p in hull)
            {
                double u = p.X * ux.X + p.Y * ux.Y;
                double v = p.X * uy.X + p.Y * uy.Y;
                if (u < minU) minU = u;
                if (u > maxU) maxU = u;
                if (v < minV) minV = v;
                if (v > maxV) maxV = v;
            }

            double w = maxU - minU, h = maxV - minV;
            double area = w * h;
            if (area >= bestArea) continue;

            bestArea = area;
            double cu = (minU + maxU) / 2, cv = (minV + maxV) / 2;
            var center = new PointD(cu * ux.X + cv * uy.X, cu * ux.Y + cv * uy.Y);
            best = new RotatedRect(center, w, h, Math.Atan2(ux.Y, ux.X));
        }

        return best;
    }

    /// <summary>Área de um polígono pela fórmula do laço (valor absoluto).</summary>
    public static double PolygonArea(IReadOnlyList<PointD> polygon)
    {
        double sum = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(sum) / 2;
    }

    /// <summary>Perímetro de um polígono fechado.</summary>
    public static double PolygonPerimeter(IReadOnlyList<PointD> polygon)
    {
        double sum = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            sum += (polygon[(i + 1) % polygon.Count] - polygon[i]).Length;
        }
        return sum;
    }

    /// <summary>
    /// Média dos valores do mapa que caem DENTRO do polígono, por varredura de linhas.
    ///
    /// É a pontuação que decide se uma região detectada é texto ou ruído. Medir só a caixa
    /// envolvente inflaria a média com o fundo ao redor de texto inclinado, e regiões
    /// legítimas seriam descartadas.
    /// </summary>
    public static double MeanInsidePolygon(ReadOnlySpan<float> map, int width, int height,
                                           IReadOnlyList<PointD> polygon)
    {
        if (polygon.Count < 3) return 0;

        double minYd = polygon.Min(p => p.Y), maxYd = polygon.Max(p => p.Y);
        int minY = Math.Max(0, (int)Math.Floor(minYd));
        int maxY = Math.Min(height - 1, (int)Math.Ceiling(maxYd));
        if (minY > maxY) return 0;

        double sum = 0;
        long count = 0;
        var crossings = new List<double>(polygon.Count);

        for (int y = minY; y <= maxY; y++)
        {
            crossings.Clear();
            double scan = y + 0.5;

            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                if (a.Y == b.Y) continue;

                double lo = Math.Min(a.Y, b.Y), hi = Math.Max(a.Y, b.Y);
                if (scan < lo || scan >= hi) continue;

                crossings.Add(a.X + (scan - a.Y) * (b.X - a.X) / (b.Y - a.Y));
            }

            if (crossings.Count < 2) continue;
            crossings.Sort();

            for (int i = 0; i + 1 < crossings.Count; i += 2)
            {
                int x0 = Math.Max(0, (int)Math.Ceiling(crossings[i] - 0.5));
                int x1 = Math.Min(width - 1, (int)Math.Floor(crossings[i + 1] - 0.5));
                for (int x = x0; x <= x1; x++)
                {
                    sum += map[y * width + x];
                    count++;
                }
            }
        }

        return count == 0 ? 0 : sum / count;
    }
}
