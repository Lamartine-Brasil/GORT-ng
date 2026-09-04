namespace Gort.Core.Model;

/// <summary>Orientação de uma linha ou bloco (glossário, RF-155).</summary>
public enum Orientation
{
    Horizontal,
    Vertical,
}

/// <summary>
/// Retângulo inteiro em coordenadas de pixel, meio-aberto: contém x em [Left, Right)
/// e y em [Top, Bottom). É o retângulo usado em todo o pipeline — imagem, tela e desenho.
/// </summary>
public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public long Area => (long)Math.Max(0, Width) * Math.Max(0, Height);
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static readonly Rect Empty = new(0, 0, 0, 0);

    public static Rect FromBounds(int left, int top, int right, int bottom)
        => new(left, top, right - left, bottom - top);

    /// <summary>
    /// RF-153 — Cria a caixa de uma palavra expandindo para fora: piso das coordenadas de
    /// origem, teto das coordenadas de origem somadas às dimensões. Largura e altura
    /// negativas são tratadas como zero.
    /// Isso evita perder pixels de borda do glifo (7.2).
    /// </summary>
    public static Rect FromWordBox(double x, double y, double width, double height)
    {
        if (width < 0) width = 0;
        if (height < 0) height = 0;
        int left = (int)Math.Floor(x);
        int top = (int)Math.Floor(y);
        int right = (int)Math.Ceiling(x + width);
        int bottom = (int)Math.Ceiling(y + height);
        return FromBounds(left, top, Math.Max(left, right), Math.Max(top, bottom));
    }

    /// <summary>
    /// RF-142 — Caixa delimitadora de um quadrilátero: mínimo e máximo dos quatro pontos em
    /// cada eixo, nunca por diferença direta entre dois pontos. 🔒
    /// Motivo: evita larguras e alturas negativas em texto rotacionado.
    /// </summary>
    public static Rect FromQuad(ReadOnlySpan<(double X, double Y)> points)
    {
        if (points.Length == 0) return Empty;
        double minX = points[0].X, maxX = points[0].X;
        double minY = points[0].Y, maxY = points[0].Y;
        for (int i = 1; i < points.Length; i++)
        {
            if (points[i].X < minX) minX = points[i].X;
            if (points[i].X > maxX) maxX = points[i].X;
            if (points[i].Y < minY) minY = points[i].Y;
            if (points[i].Y > maxY) maxY = points[i].Y;
        }
        return FromBounds(
            (int)Math.Floor(minX), (int)Math.Floor(minY),
            (int)Math.Ceiling(maxX), (int)Math.Ceiling(maxY));
    }

    /// <summary>União com outro retângulo. Um retângulo vazio é neutro.</summary>
    public Rect Union(Rect other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;
        return FromBounds(
            Math.Min(Left, other.Left), Math.Min(Top, other.Top),
            Math.Max(Right, other.Right), Math.Max(Bottom, other.Bottom));
    }

    /// <summary>Interseção; devolve <see cref="Empty"/> quando não há sobreposição.</summary>
    public Rect Intersect(Rect other)
    {
        int left = Math.Max(Left, other.Left);
        int top = Math.Max(Top, other.Top);
        int right = Math.Min(Right, other.Right);
        int bottom = Math.Min(Bottom, other.Bottom);
        if (right <= left || bottom <= top) return Empty;
        return FromBounds(left, top, right, bottom);
    }

    public bool IntersectsWith(Rect other)
        => Left < other.Right && other.Left < Right && Top < other.Bottom && other.Top < Bottom;

    public bool Contains(Rect other)
        => other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;

    public bool Contains(int px, int py)
        => px >= Left && px < Right && py >= Top && py < Bottom;

    /// <summary>Infla em todas as direções (valor negativo reduz).</summary>
    public Rect Inflate(int amount) => Inflate(amount, amount);

    public Rect Inflate(int dx, int dy)
        => FromBounds(Left - dx, Top - dy, Right + dx, Bottom + dy);

    public Rect Offset(int dx, int dy) => new(X + dx, Y + dy, Width, Height);

    /// <summary>União de uma sequência; sequência vazia devolve <see cref="Empty"/>.</summary>
    public static Rect UnionAll(IEnumerable<Rect> rects)
    {
        var result = Empty;
        foreach (var r in rects) result = result.Union(r);
        return result;
    }

    public override string ToString() => $"({X},{Y} {Width}x{Height})";
}

/// <summary>Retângulo em ponto flutuante, usado apenas no layout de desenho (cap. 19).</summary>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static RectD FromBounds(double left, double top, double right, double bottom)
        => new(left, top, right - left, bottom - top);

    public RectD Deflate(double amount)
        => FromBounds(Left + amount, Top + amount, Right - amount, Bottom - amount);

    public Rect ToRect() => Rect.FromBounds(
        (int)Math.Floor(Left), (int)Math.Floor(Top),
        (int)Math.Ceiling(Right), (int)Math.Ceiling(Bottom));

    public static RectD From(Rect r) => new(r.X, r.Y, r.Width, r.Height);
}
