namespace Gort.Ocr.Rapid.Detection;

/// <summary>
/// Parâmetros do pós-processamento do detector.
///
/// ATENÇÃO — estes NÃO são valores 🔒 da especificação. São os padrões de referência do
/// próprio modelo, e a PARTE XII não se aplica a eles: podem ser ajustados com evidência,
/// como qualquer parâmetro de biblioteca. Os valores 🔒 do programa estão todos em
/// <c>Gort.Core.Calibration.P</c> e não se misturam com estes.
/// </summary>
public sealed class DbOptions
{
    /// <summary>Corte do mapa de probabilidade para formar a máscara binária.</summary>
    public double Threshold { get; init; } = 0.3;

    /// <summary>Pontuação mínima de uma região para ser aceita como texto.</summary>
    public double BoxThreshold { get; init; } = 0.5;

    /// <summary>Fator de expansão da caixa detectada (unclip).</summary>
    public double UnclipRatio { get; init; } = 1.6;

    /// <summary>Lado menor mínimo de uma região, em pixels do mapa.</summary>
    public int MinSize { get; init; } = 3;

    /// <summary>Teto de regiões consideradas, para limitar o custo em imagens ruidosas.</summary>
    public int MaxCandidates { get; init; } = 1000;

    /// <summary>Dilatação 2×2 da máscara antes de extrair as regiões.</summary>
    public bool UseDilation { get; init; } = true;
}

/// <summary>
/// Pós-processamento do detector DBNet: transforma o mapa de probabilidade em caixas de
/// linha de texto.
///
/// O caminho é o canônico do modelo: binarizar, dilatar, achar as componentes conectadas,
/// tomar o retângulo de área mínima de cada uma, pontuá-la contra o mapa, expandi-la, e
/// devolver as que sobrarem.
/// </summary>
public static class DbPostProcessor
{
    /// <summary>Uma região detectada, ainda no espaço do mapa de probabilidade.</summary>
    public readonly record struct DetectedBox(PointD[] Corners, double Score);

    public static List<DetectedBox> ExtractBoxes(
        ReadOnlySpan<float> probability, int width, int height, DbOptions options)
    {
        var mask = Binarize(probability, width, height, options.Threshold);
        if (options.UseDilation) mask = Dilate(mask, width, height);

        var boxes = new List<DetectedBox>();
        var visited = new bool[width * height];
        var stack = new Stack<int>();
        var component = new List<PointD>();

        for (int start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || visited[start]) continue;
            if (boxes.Count >= options.MaxCandidates) break;

            // Componente conectada por vizinhança de 8, que é a mesma conectividade que o
            // traçado de contornos do modelo de referência usa para o primeiro plano.
            component.Clear();
            stack.Push(start);
            visited[start] = true;

            while (stack.Count > 0)
            {
                int index = stack.Pop();
                int y = index / width, x = index % width;
                component.Add(new PointD(x, y));

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;

                        int n = ny * width + nx;
                        if (!mask[n] || visited[n]) continue;
                        visited[n] = true;
                        stack.Push(n);
                    }
                }
            }

            var box = Geometry2D.MinAreaRect(component);
            if (box.MinSide < options.MinSize) continue;

            // A pontuação é medida contra o mapa de PROBABILIDADE, não contra a máscara:
            // uma região que mal passou do corte tem média baixa e é descartada aqui.
            var corners = box.Corners();
            double score = Geometry2D.MeanInsidePolygon(probability, width, height, corners);
            if (score < options.BoxThreshold) continue;

            // Expansão: as caixas do DBNet saem encolhidas por construção, porque o modelo é
            // treinado para prever o núcleo do texto e não a sua extensão.
            double area = Geometry2D.PolygonArea(corners);
            double perimeter = Geometry2D.PolygonPerimeter(corners);
            if (perimeter <= 0) continue;

            double distance = area * options.UnclipRatio / perimeter;
            var expanded = box.Expand(distance);
            if (expanded.MinSide < options.MinSize + 2) continue;

            boxes.Add(new DetectedBox(expanded.Corners(), score));
        }

        return boxes;
    }

    private static bool[] Binarize(ReadOnlySpan<float> map, int width, int height, double threshold)
    {
        var mask = new bool[width * height];
        for (int i = 0; i < mask.Length; i++) mask[i] = map[i] > threshold;
        return mask;
    }

    /// <summary>
    /// Dilatação com elemento 2 × 2. Junta traços vizinhos que a binarização separou, o que
    /// evita que uma linha de texto vire várias caixas.
    /// </summary>
    private static bool[] Dilate(bool[] mask, int width, int height)
    {
        var output = new bool[mask.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!mask[y * width + x]) continue;
                for (int dy = 0; dy <= 1; dy++)
                {
                    for (int dx = 0; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < width && ny < height) output[ny * width + nx] = true;
                    }
                }
            }
        }
        return output;
    }
}
