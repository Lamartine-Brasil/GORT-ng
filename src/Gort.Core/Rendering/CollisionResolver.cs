using Gort.Core.Model;

namespace Gort.Core.Rendering;

/// <summary>
/// RF-355 a RF-358 — Resolução de colisões entre blocos da sobreposição. 🔒
///
/// Sem ela, dois blocos vizinhos com traduções longas escrevem um por cima do outro e
/// nenhum dos dois fica legível — é o defeito mais visível do modo sobreposição depois do
/// agrupamento errado.
/// </summary>
public static class CollisionResolver
{
    /// <summary>Um bloco para efeito de colisão: só o retângulo e se é título.</summary>
    public sealed class Item
    {
        public required Rect Rect { get; set; }

        /// <summary>
        /// RF-357 — Títulos não cedem. Nomes de personagem são curtos e precisam ficar
        /// legíveis.
        /// </summary>
        public required bool IsTitle { get; init; }
    }

    /// <summary>
    /// RF-355 — Enquanto houver dois blocos cujos retângulos se sobrepõem, o par com MAIOR
    /// área de interseção é separado. A separação é testada nos dois eixos e escolhe-se a
    /// que perde menos área total.
    ///
    /// RF-358 — O número máximo de iterações é o quadrado da quantidade de blocos
    /// multiplicado por 4, para garantir término.
    /// </summary>
    public static void Resolve(IReadOnlyList<Item> blocks)
    {
        if (blocks.Count < 2) return;

        int maxIterations = blocks.Count * blocks.Count * 4;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            var pair = LargestOverlap(blocks);
            if (pair is null) return;

            var (a, b) = pair.Value;
            Separate(blocks[a], blocks[b]);
        }
    }

    /// <summary>O par com maior área de interseção, ou null quando não há sobreposição.</summary>
    private static (int A, int B)? LargestOverlap(IReadOnlyList<Item> blocks)
    {
        long best = 0;
        (int, int)? found = null;

        for (int i = 0; i < blocks.Count; i++)
        {
            for (int j = i + 1; j < blocks.Count; j++)
            {
                long area = blocks[i].Rect.Intersect(blocks[j].Rect).Area;
                if (area > best)
                {
                    best = area;
                    found = (i, j);
                }
            }
        }
        return found;
    }

    /// <summary>
    /// Separa um par, testando os dois eixos e ficando com o que perde menos área total.
    /// </summary>
    private static void Separate(Item a, Item b)
    {
        var (ax, bx) = SplitHorizontally(a, b);
        var (ay, by) = SplitVertically(a, b);

        long lossX = (a.Rect.Area - ax.Area) + (b.Rect.Area - bx.Area);
        long lossY = (a.Rect.Area - ay.Area) + (b.Rect.Area - by.Area);

        if (lossX <= lossY)
        {
            a.Rect = ax;
            b.Rect = bx;
        }
        else
        {
            a.Rect = ay;
            b.Rect = by;
        }
    }

    private static (Rect A, Rect B) SplitHorizontally(Item a, Item b)
    {
        // Quem começa mais à esquerda fica com a parte esquerda.
        bool aFirst = a.Rect.Left <= b.Rect.Left;
        var first = aFirst ? a : b;
        var second = aFirst ? b : a;

        int boundary = Boundary(
            Math.Max(first.Rect.Left, second.Rect.Left),
            Math.Min(first.Rect.Right, second.Rect.Right),
            first, second,
            titleEdgeWhenFirstIsTitle: first.Rect.Right,
            titleEdgeWhenSecondIsTitle: second.Rect.Left);

        var firstRect = Rect.FromBounds(first.Rect.Left, first.Rect.Top,
                                        Math.Max(first.Rect.Left, boundary), first.Rect.Bottom);
        var secondRect = Rect.FromBounds(Math.Min(second.Rect.Right, boundary), second.Rect.Top,
                                         second.Rect.Right, second.Rect.Bottom);

        return aFirst ? (firstRect, secondRect) : (secondRect, firstRect);
    }

    private static (Rect A, Rect B) SplitVertically(Item a, Item b)
    {
        bool aFirst = a.Rect.Top <= b.Rect.Top;
        var first = aFirst ? a : b;
        var second = aFirst ? b : a;

        int boundary = Boundary(
            Math.Max(first.Rect.Top, second.Rect.Top),
            Math.Min(first.Rect.Bottom, second.Rect.Bottom),
            first, second,
            titleEdgeWhenFirstIsTitle: first.Rect.Bottom,
            titleEdgeWhenSecondIsTitle: second.Rect.Top);

        var firstRect = Rect.FromBounds(first.Rect.Left, first.Rect.Top,
                                        first.Rect.Right, Math.Max(first.Rect.Top, boundary));
        var secondRect = Rect.FromBounds(second.Rect.Left, Math.Min(second.Rect.Bottom, boundary),
                                         second.Rect.Right, second.Rect.Bottom);

        return aFirst ? (firstRect, secondRect) : (secondRect, firstRect);
    }

    /// <summary>
    /// RF-356 / RF-357 — Onde cortar.
    ///
    /// Quando NENHUM dos dois é título, a fronteira é PROPORCIONAL ÀS ÁREAS: começa no
    /// início da sobreposição e avança pela fração `área_do_primeiro ÷ (área_do_primeiro +
    /// área_do_segundo)` do comprimento da sobreposição. 🔒 O bloco maior fica com a maior
    /// parte do espaço disputado.
    ///
    /// Quando UM é título, ele preserva o retângulo INTEIRO e o outro cede: a fronteira vai
    /// para a borda do título. 🔒
    /// </summary>
    private static int Boundary(int overlapStart, int overlapEnd, Item first, Item second,
                                int titleEdgeWhenFirstIsTitle, int titleEdgeWhenSecondIsTitle)
    {
        // RF-357 — o título preserva tudo; o outro cede.
        if (first.IsTitle && !second.IsTitle) return titleEdgeWhenFirstIsTitle;
        if (second.IsTitle && !first.IsTitle) return titleEdgeWhenSecondIsTitle;

        // RF-356 — proporcional às áreas.
        long areaFirst = first.Rect.Area;
        long areaSecond = second.Rect.Area;
        long total = areaFirst + areaSecond;

        double fraction = total <= 0 ? 0.5 : (double)areaFirst / total;
        int length = overlapEnd - overlapStart;

        return overlapStart + (int)Math.Round(length * fraction);
    }
}
