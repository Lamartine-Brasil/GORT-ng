using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Imaging;

/// <summary>
/// Cap. 13 — Pré-processamento de imagem: transforma a imagem capturada em uma imagem que
/// maximize a taxa de acerto do OCR.
///
/// Ordem das operações (pseudocódigo de 13, RF-101, RF-112):
///   exclusões → filtro/binarização → erosão → ampliação.
/// A ordem "erodir ANTES de ampliar" é explícita em RF-112 e não pode ser trocada: erodir
/// depois afinaria o traço já interpolado pela ampliação, destruindo detalhe fino em vez
/// de separar glifos.
/// </summary>
public static class Preprocessor
{
    /// <summary>
    /// RF-109 — Valor do pixel que PASSA no filtro (é texto). A pré-visualização usa preto
    /// para quem passa e branco para quem não passa (RF-082); o OCR recebe a mesma
    /// convenção, que é a que os motores esperam (texto escuro sobre fundo claro).
    /// </summary>
    public const byte TextValue = 0;

    /// <summary>
    /// RF-109 — Valor do pixel que NÃO passa no filtro. É também o "valor de fundo do
    /// filtro ativo" a que RF-102 se refere ao preencher as áreas de exclusão.
    /// </summary>
    public const byte BackgroundValue = 255;

    /// <summary>
    /// Aplica o pré-processamento completo a uma região.
    /// </summary>
    /// <param name="source">Imagem capturada da região.</param>
    /// <param name="exclusions">
    /// RF-101 — Retângulos de exclusão, já em coordenadas da imagem da região.
    /// </param>
    /// <param name="settings">Filtro, erosão e ampliação.</param>
    public static ImageBuffer Process(ImageBuffer source, IReadOnlyList<Rect> exclusions,
                                      FilterSettings settings)
    {
        if (source.IsEmpty) return source;

        // RF-101 — as porções cobertas por áreas decrementais saem antes de qualquer outro
        // tratamento. RF-103 — a geometria não muda: a região excluída continua ocupando
        // seu lugar, porque as caixas do OCR precisam continuar mapeando para as
        // coordenadas de tela originais.
        var clipped = ClipExclusions(source, exclusions);

        ImageBuffer working;
        if (settings.Mode == FilterMode.None)
        {
            // RF-110 / RF-118 — sem filtro, a imagem colorida vai ao OCR sem binarização.
            // RF-102 — nesse caso a exclusão é preenchida com a cor dominante da borda da
            // própria região removida, e não com o valor de fundo de um filtro que não existe.
            working = source;
            foreach (var e in clipped) FillWithDominantBorderColor(working, e);
        }
        else
        {
            working = Binarize(source, settings, clipped);
        }

        // RF-111 / RF-112 — erosão sobre a imagem já binarizada e ANTES da ampliação.
        if (settings.Erosion && settings.Mode != FilterMode.None)
            working = Erode(working);

        // RF-113 — ampliação, o ajuste de maior impacto com fontes pequenas.
        return Scale(working, settings.Scale);
    }

    private static List<Rect> ClipExclusions(ImageBuffer image, IReadOnlyList<Rect> exclusions)
    {
        var bounds = new Rect(0, 0, image.Width, image.Height);
        var result = new List<Rect>();
        foreach (var e in exclusions)
        {
            var r = e.Intersect(bounds);
            if (!r.IsEmpty) result.Add(r);
        }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Filtro e binarização
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RF-104 a RF-109 — Produz a imagem binária de 1 canal: <see cref="TextValue"/> para os
    /// pixels que passam no filtro e <see cref="BackgroundValue"/> para os que não passam.
    ///
    /// RF-102 — Os pixels dentro de uma área de exclusão recebem diretamente o valor de
    /// fundo, que é exatamente "a mesma cor que um pixel reprovado no filtro receberia".
    /// Fazê-lo aqui, e não pintando a imagem de origem, é o que garante o objetivo do
    /// requisito: um retângulo preto sobre fundo claro criaria uma aresta de contraste
    /// máximo que vários motores leem como traço, e a exclusão passaria a INVENTAR
    /// caracteres em vez de eliminar ruído. Pintar a origem com uma cor arbitrária correria
    /// o risco de essa cor PASSAR no filtro ativo, que é a mesma falha ao contrário.
    /// </summary>
    public static ImageBuffer Binarize(ImageBuffer source, FilterSettings settings,
                                       IReadOnlyList<Rect>? exclusions = null)
    {
        var output = ImageBuffer.Allocate(source.Width, source.Height, PixelFormat.Gray8);
        var groups = settings.Groups;

        for (int y = 0; y < source.Height; y++)
        {
            int outRow = y * output.Stride;
            for (int x = 0; x < source.Width; x++)
            {
                var (b, g, r, _) = source.GetPixel(x, y);
                bool passes = settings.Mode switch
                {
                    FilterMode.Rgb => MatchesRgb(r, g, b, groups),
                    FilterMode.Hsv => MatchesHsv(r, g, b, groups),
                    FilterMode.Threshold => Luminance(r, g, b) < settings.Threshold,
                    _ => false,
                };
                output.Pixels[outRow + x] = passes ? TextValue : BackgroundValue;
            }
        }

        if (exclusions is not null)
        {
            foreach (var e in exclusions) Fill(output, e, BackgroundValue);
        }
        return output;
    }

    /// <summary>
    /// RF-105 — No modo RGB, um pixel é texto quando seus três componentes são EXATAMENTE
    /// iguais aos valores do grupo. O pixel passa se satisfizer qualquer grupo ativo.
    /// </summary>
    private static bool MatchesRgb(byte r, byte g, byte b, List<ColorGroup> groups)
    {
        foreach (var grp in groups)
        {
            if (grp.R == r && grp.G == g && grp.B == b) return true;
        }
        return false;
    }

    /// <summary>
    /// RF-106 — No modo HSV, um pixel é texto quando sua saturação está entre o início e o
    /// fim da faixa de saturação E seu brilho está entre o início e o fim da faixa de
    /// brilho do grupo, ambos em 0–100.
    /// </summary>
    private static bool MatchesHsv(byte r, byte g, byte b, List<ColorGroup> groups)
    {
        if (groups.Count == 0) return false;
        var (_, s, v) = ColorMath.ToHsvFilter(r, g, b);
        foreach (var grp in groups)
        {
            if (s >= grp.S1 && s <= grp.S2 && v >= grp.V1 && v <= grp.V2) return true;
        }
        return false;
    }

    /// <summary>RF-108 / P-146 — Matriz de luminância 0,30 / 0,59 / 0,11.</summary>
    public static double Luminance(byte r, byte g, byte b)
        => P.GrayR * r + P.GrayG * g + P.GrayB * b;

    private static void Fill(ImageBuffer image, Rect rect, byte value)
    {
        var r = rect.Intersect(new Rect(0, 0, image.Width, image.Height));
        for (int y = r.Top; y < r.Bottom; y++)
        {
            int row = y * image.Stride;
            for (int x = r.Left; x < r.Right; x++)
            {
                for (int c = 0; c < image.Channels; c++) image.Pixels[row + x * image.Channels + c] = value;
            }
        }
    }

    /// <summary>
    /// RF-102 — Quando nenhum filtro está ativo, a exclusão é preenchida com a COR DOMINANTE
    /// DA BORDA da própria região removida, para que ela desapareça no fundo em vez de criar
    /// uma aresta de contraste.
    /// </summary>
    private static void FillWithDominantBorderColor(ImageBuffer image, Rect rect)
    {
        var bounds = new Rect(0, 0, image.Width, image.Height);
        var r = rect.Intersect(bounds);
        if (r.IsEmpty) return;

        // Amostra o anel de 1 px imediatamente ao redor do retângulo, recortado pela imagem.
        var counts = new Dictionary<int, int>();
        void Sample(int x, int y)
        {
            if (!bounds.Contains(x, y) || r.Contains(x, y)) return;
            var (b, g, gr, a) = image.GetPixel(x, y);
            int key = (a << 24) | (gr << 16) | (g << 8) | b;
            counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
        }

        for (int x = r.Left - 1; x <= r.Right; x++)
        {
            Sample(x, r.Top - 1);
            Sample(x, r.Bottom);
        }
        for (int y = r.Top - 1; y <= r.Bottom; y++)
        {
            Sample(r.Left - 1, y);
            Sample(r.Right, y);
        }

        // Sem borda amostrável (região colada às quatro margens): nada a fazer sem inventar
        // uma cor de alto contraste, que é justamente o que RF-102 proíbe.
        if (counts.Count == 0) return;

        int dominant = counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
        byte db = (byte)(dominant & 0xFF);
        byte dg = (byte)((dominant >> 8) & 0xFF);
        byte dr = (byte)((dominant >> 16) & 0xFF);
        byte da = (byte)((dominant >> 24) & 0xFF);

        for (int y = r.Top; y < r.Bottom; y++)
        {
            int row = y * image.Stride;
            for (int x = r.Left; x < r.Right; x++)
            {
                int o = row + x * image.Channels;
                switch (image.Format)
                {
                    case PixelFormat.Gray8:
                        image.Pixels[o] = (byte)Luminance(dr, dg, db);
                        break;
                    case PixelFormat.Bgr24:
                        image.Pixels[o] = db; image.Pixels[o + 1] = dg; image.Pixels[o + 2] = dr;
                        break;
                    default:
                        image.Pixels[o] = db; image.Pixels[o + 1] = dg;
                        image.Pixels[o + 2] = dr; image.Pixels[o + 3] = da;
                        break;
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Erosão
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RF-111 / RF-112 — Erosão com elemento estruturante QUADRADO DE 3 × 3, UMA ÚNICA
    /// ITERAÇÃO, sobre a imagem já binarizada.
    ///
    /// Afina os traços do texto removendo uma camada de pixels da borda de cada glifo.
    /// Serve para fontes muito grossas, em que letras vizinhas se tocam e o OCR as lê como
    /// um caractere só, e para ruído de compressão, que aparece como pontos isolados.
    ///
    /// Como o texto é o valor MENOR (0) e o fundo o maior (255), erodir o texto é uma
    /// DILATAÇÃO do fundo: um pixel só continua sendo texto se todos os seus oito vizinhos
    /// também forem. Fora da imagem conta como fundo.
    ///
    /// [INFERIDO na especificação] — a forma e o número de iterações são uma escolha
    /// conservadora: é o menor elemento que produz efeito visível. Se 3 × 3 se mostrar
    /// agressivo demais para fontes pequenas, o ajuste correto é expor o tamanho ao
    /// usuário, NÃO trocar a ordem das operações.
    /// </summary>
    public static ImageBuffer Erode(ImageBuffer binary)
    {
        var output = ImageBuffer.Allocate(binary.Width, binary.Height, PixelFormat.Gray8);
        int w = binary.Width, h = binary.Height;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                byte value = BackgroundValue;
                if (binary.Pixels[y * binary.Stride + x] == TextValue)
                {
                    bool allText = true;
                    for (int dy = -1; dy <= 1 && allText; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h
                                || binary.Pixels[ny * binary.Stride + nx] != TextValue)
                            {
                                allText = false;
                                break;
                            }
                        }
                    }
                    if (allText) value = TextValue;
                }
                output.Pixels[y * output.Stride + x] = value;
            }
        }
        return output;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ampliação
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RF-113 — Ampliação por um fator configurável (P-22), aplicada por último.
    ///
    /// [INFERIDO] — a especificação diz "redimensionar(binária, fator = ampliação)" sem
    /// nomear o método de interpolação. Usa-se interpolação bilinear: sobre uma imagem
    /// binarizada ela suaviza a escada dos glifos, que é o que melhora a taxa de acerto do
    /// OCR com fontes pequenas — o objetivo declarado do requisito. Vizinho mais próximo
    /// preservaria a binariedade estrita mas devolveria exatamente a escada que a
    /// ampliação existe para atenuar.
    /// </summary>
    public static ImageBuffer Scale(ImageBuffer source, double factor)
    {
        if (source.IsEmpty) return source;
        if (Math.Abs(factor - 1.0) < 1e-9) return source;

        int w = Math.Max(1, (int)Math.Round(source.Width * factor));
        int h = Math.Max(1, (int)Math.Round(source.Height * factor));
        var output = ImageBuffer.Allocate(w, h, source.Format);
        int ch = source.Channels;

        double sx = (double)source.Width / w;
        double sy = (double)source.Height / h;

        for (int y = 0; y < h; y++)
        {
            double fy = (y + 0.5) * sy - 0.5;
            int y0 = (int)Math.Floor(fy);
            double wy = fy - y0;
            int y1 = Math.Clamp(y0 + 1, 0, source.Height - 1);
            y0 = Math.Clamp(y0, 0, source.Height - 1);

            for (int x = 0; x < w; x++)
            {
                double fx = (x + 0.5) * sx - 0.5;
                int x0 = (int)Math.Floor(fx);
                double wx = fx - x0;
                int x1 = Math.Clamp(x0 + 1, 0, source.Width - 1);
                x0 = Math.Clamp(x0, 0, source.Width - 1);

                int o00 = y0 * source.Stride + x0 * ch;
                int o01 = y0 * source.Stride + x1 * ch;
                int o10 = y1 * source.Stride + x0 * ch;
                int o11 = y1 * source.Stride + x1 * ch;
                int od = y * output.Stride + x * ch;

                for (int c = 0; c < ch; c++)
                {
                    double top = source.Pixels[o00 + c] * (1 - wx) + source.Pixels[o01 + c] * wx;
                    double bottom = source.Pixels[o10 + c] * (1 - wx) + source.Pixels[o11 + c] * wx;
                    output.Pixels[od + c] = (byte)Math.Clamp(
                        Math.Round(top * (1 - wy) + bottom * wy), 0, 255);
                }
            }
        }
        return output;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Conversão de coordenadas
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RF-116 — As coordenadas devolvidas pelo OCR estão no espaço da imagem AMPLIADA.
    /// Toda conversão de volta divide pelo fator de ampliação, usando PISO para os cantos
    /// superior/esquerdo e TETO para os inferior/direito.
    /// </summary>
    public static Rect ToSourceSpace(Rect scaled, double factor)
    {
        if (factor <= 0) return scaled;
        return Rect.FromBounds(
            (int)Math.Floor(scaled.Left / factor),
            (int)Math.Floor(scaled.Top / factor),
            (int)Math.Ceiling(scaled.Right / factor),
            (int)Math.Ceiling(scaled.Bottom / factor));
    }

    /// <summary>
    /// RF-082 — Pré-visualização binarizada: preto para os pixels que PASSAM no filtro,
    /// branco para os que não passam. Aplica exatamente o mesmo critério que o
    /// pré-processamento usaria (RF-081), para que o usuário veja o que o OCR vai receber.
    /// </summary>
    public static ImageBuffer Preview(ImageBuffer source, FilterSettings settings)
        => settings.Mode == FilterMode.None
            ? source
            : Binarize(source, settings);
}
