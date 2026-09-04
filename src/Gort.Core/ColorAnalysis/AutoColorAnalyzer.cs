using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.ColorAnalysis;

/// <summary>Opções que governam o uso do resultado da análise (RF-412 a RF-414).</summary>
public sealed class AutoColorOptions
{
    /// <summary>RF-413 — Caixa mestre; as duas abaixo ficam sob ela.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>RF-413 — Cor de fonte automática.</summary>
    public bool FontColor { get; set; } = true;
    /// <summary>RF-413 — Cor de fundo automática.</summary>
    public bool BackgroundColor { get; set; } = true;

    /// <summary>RF-412 — O fundo do texto está ativado.</summary>
    public bool TextBackgroundEnabled { get; set; } = true;
    /// <summary>RF-412 / RF-414 — Alfa efetivo do fundo, vindo da cor configurada pelo usuário.</summary>
    public byte BackgroundAlpha { get; set; } = P.DefaultBackgroundColor.A;

    /// <summary>
    /// RF-412 — A correção final de legibilidade só se aplica quando a cor automática está
    /// em uso, o fundo do texto está ativado, e o alfa efetivo do fundo é maior que zero.
    /// Motivo: sem fundo pintado, não faz sentido corrigir contra uma cor que não será
    /// desenhada.
    /// </summary>
    public bool ApplyContrastCorrection
        => Enabled && TextBackgroundEnabled && BackgroundAlpha > 0;
}

/// <summary>
/// Cap. 20 — Análise automática de cor. 🔒
///
/// Descobre, a partir da imagem ORIGINAL, qual a cor do texto e qual a cor do fundo de cada
/// bloco, para que a tradução sobreposta pareça parte do software original.
///
/// Errar aqui faz a sobreposição escolher cor de fonte igual à do fundo, e o texto some
/// (Parte XII.4).
///
/// RF-394 — Só roda quando o modo é sobreposição E a opção de cor automática está ativa.
/// Só nesse caso a imagem original é capturada (RF-098) e ela é liberada assim que a
/// análise termina (RF-099).
/// </summary>
public static class AutoColorAnalyzer
{
    /// <summary>
    /// Analisa um bloco. Devolve null quando a análise falha; nesse caso o desenho usa as
    /// cores configuradas pelo usuário (RF-404, RF-415).
    /// </summary>
    /// <param name="original">
    /// RF-395 — Imagem ORIGINAL, sem filtro nem binarização, nas dimensões da imagem
    /// capturada.
    /// </param>
    /// <param name="blockRectScaled">Retângulo do bloco no espaço da imagem AMPLIADA.</param>
    /// <param name="wordRectsScaled">Retângulos das palavras do bloco, no mesmo espaço.</param>
    /// <param name="scaledWidth">Largura da imagem ampliada, para converter os retângulos.</param>
    /// <param name="scaledHeight">Altura da imagem ampliada.</param>
    public static AutoColorResult? Analyze(
        ImageBuffer original,
        Rect blockRectScaled,
        IReadOnlyList<Rect> wordRectsScaled,
        int scaledWidth,
        int scaledHeight,
        AutoColorOptions? options = null)
    {
        options ??= new AutoColorOptions();

        // Caso de erro do cap. 20: imagem ausente ou com dimensões incoerentes → a análise
        // não roda e as cores configuradas são usadas.
        if (original.IsEmpty || scaledWidth <= 0 || scaledHeight <= 0) return null;

        var imageBounds = new Rect(0, 0, original.Width, original.Height);

        // RF-395 — converte os retângulos do espaço ampliado para o da imagem original,
        // por escala em cada eixo, com piso nos cantos superior/esquerdo e teto nos
        // inferior/direito, saturados aos limites da imagem.
        double sx = (double)original.Width / scaledWidth;
        double sy = (double)original.Height / scaledHeight;

        Rect blockRect = ToOriginal(blockRectScaled, sx, sy).Intersect(imageBounds);
        if (blockRect.IsEmpty) return null;   // interseção vazia → falha

        var words = new List<Rect>(wordRectsScaled.Count);
        foreach (var w in wordRectsScaled)
        {
            var r = ToOriginal(w, sx, sy).Intersect(imageBounds);
            if (!r.IsEmpty) words.Add(r);
        }

        // ── Cor de fundo: três estratégias em cascata ──────────────────────────
        var perWordBorder = new Rgba?[words.Count];      // RF-399/RF-400
        var perWordRing = new Rgba?[words.Count];        // RF-402

        Rgba? background = StrategyWordBorders(original, words, perWordBorder);   // Estratégia 1
        background ??= StrategyRings(original, blockRect, words, perWordRing);    // Estratégia 2
        background ??= StrategyBlockDominant(original, blockRect);                // Estratégia 3

        // RF-404 — se as três falharem, a análise devolve falha.
        if (background is null) return null;
        Rgba bg = background.Value;

        // ── Cor da fonte ───────────────────────────────────────────────────────
        var font = DetermineFontColor(original, words, perWordBorder, perWordRing, bg,
                                      out int supportingWords, out bool usedFallback);

        double contrast = ColorMath.ContrastRatio(font, bg);
        bool corrected = false;

        // RF-410 / RF-412 — verificação final de legibilidade.
        if (options.ApplyContrastCorrection && contrast < P.MinContrastRatio)
        {
            font = ColorMath.BestBlackOrWhite(bg);
            contrast = ColorMath.ContrastRatio(font, bg);
            corrected = true;
        }

        // RF-414 — quando a cor de fundo automática é usada, o canal ALFA vem da cor de
        // fundo configurada pelo usuário e os componentes de cor vêm da análise.
        bg = bg.WithAlpha(options.BackgroundAlpha);

        return new AutoColorResult(font, bg, supportingWords, contrast, usedFallback, corrected);
    }

    /// <summary>
    /// RF-395 — Piso nos cantos superior/esquerdo, teto nos inferior/direito.
    /// </summary>
    private static Rect ToOriginal(Rect r, double sx, double sy)
        => Rect.FromBounds(
            (int)Math.Floor(r.Left * sx),
            (int)Math.Floor(r.Top * sy),
            (int)Math.Ceiling(r.Right * sx),
            (int)Math.Ceiling(r.Bottom * sy));

    // ─────────────────────────────────────────────────────────────────────────
    // RF-396 — Amostragem esparsa
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RF-396 — O passo de amostragem é o TETO DA RAIZ QUADRADA da área do retângulo
    /// dividida pelo número máximo de amostras, com mínimo 1.
    /// </summary>
    public static int SampleStep(Rect rect, int maxSamples)
    {
        if (maxSamples <= 0) return 1;
        double step = Math.Ceiling(Math.Sqrt((double)rect.Area / maxSamples));
        return (int)Math.Max(1, step);
    }

    /// <summary>
    /// Percorre o retângulo com o passo de RF-396, ignorando pixels com alfa abaixo de
    /// P-107 (RF-397) e os que o filtro de exclusão recusar.
    /// </summary>
    private static void Sample(ImageBuffer image, Rect rect, int maxSamples,
                               Action<byte, byte, byte> onPixel,
                               Func<int, int, bool>? accept = null)
    {
        var r = rect.Intersect(new Rect(0, 0, image.Width, image.Height));
        if (r.IsEmpty) return;

        int step = SampleStep(r, maxSamples);
        for (int y = r.Top; y < r.Bottom; y += step)
        {
            for (int x = r.Left; x < r.Right; x += step)
            {
                if (accept is not null && !accept(x, y)) continue;
                var (b, g, rr, a) = image.GetPixel(x, y);
                if (a < P.ColorMinAlpha) continue;   // RF-397
                onPixel(rr, g, b);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Estratégia 1 — bordas das palavras (RF-399 a RF-401)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RF-399 — Para cada retângulo de palavra, sondam-se OITO sub-retângulos: as quatro
    /// faixas de borda e os quatro cantos.
    ///
    /// Motivo (RF-400): os cantos de uma caixa de palavra são quase sempre fundo puro;
    /// exigir concordância entre cantos evita capturar a cor do glifo.
    /// </summary>
    private static Rgba? StrategyWordBorders(ImageBuffer image, List<Rect> words, Rgba?[] perWord)
    {
        if (words.Count == 0) return null;

        var localClusters = new ClusterSet();
        int wordsWithBackground = 0;

        for (int i = 0; i < words.Count; i++)
        {
            var local = WordBorderBackground(image, words[i]);
            perWord[i] = local;
            if (local is null) continue;

            wordsWithBackground++;
            var c = localClusters.For(local.Value);
            c.Add(local.Value.R, local.Value.G, local.Value.B);
            c.SupportingWords++;
        }

        if (wordsWithBackground == 0) return null;

        // RF-401 — escolhe-se o agrupamento com apoio de pelo menos o teto de P-111 vezes
        // o número de palavras; entre os elegíveis, o de maior apoio, depois maior soma de
        // sondas, depois maior população.
        int required = (int)Math.Ceiling(P.GlobalBackgroundMinSupport * words.Count);
        var chosen = localClusters.Clusters
            .Where(c => c.SupportingWords >= required)
            .OrderByDescending(c => c.SupportingWords)
            .ThenByDescending(c => c.Probes)
            .ThenByDescending(c => c.Population)
            .ThenBy(c => c.Key)
            .FirstOrDefault();

        return chosen?.Value;   // null ⇒ a estratégia falha e cai para o anel
    }

    /// <summary>RF-399 / RF-400 — Fundo local de uma palavra a partir das oito sondas.</summary>
    private static Rgba? WordBorderBackground(ImageBuffer image, Rect word)
    {
        if (word.IsEmpty) return null;

        // Espessura da faixa: teto de P-108 vezes o lado menor, saturada entre 1 e P-109.
        int minSide = Math.Min(word.Width, word.Height);
        int band = (int)Math.Ceiling(P.ProbeBandRatio * minSide);
        band = Math.Clamp(band, 1, P.ProbeBandMaxThickness);

        // Cantos: largura = min(largura da palavra, max(espessura, min(4, um terço da largura))).
        int cornerW = Math.Min(word.Width, Math.Max(band, Math.Min(4, word.Width / 3)));
        int cornerH = Math.Min(word.Height, Math.Max(band, Math.Min(4, word.Height / 3)));
        cornerW = Math.Max(1, cornerW);
        cornerH = Math.Max(1, cornerH);

        var bands = new[]
        {
            new Rect(word.X, word.Y, word.Width, band),                        // topo
            new Rect(word.X, word.Bottom - band, word.Width, band),            // base
            new Rect(word.X, word.Y, band, word.Height),                       // esquerda
            new Rect(word.Right - band, word.Y, band, word.Height),            // direita
        };
        var corners = new[]
        {
            new Rect(word.X, word.Y, cornerW, cornerH),
            new Rect(word.Right - cornerW, word.Y, cornerW, cornerH),
            new Rect(word.X, word.Bottom - cornerH, cornerW, cornerH),
            new Rect(word.Right - cornerW, word.Bottom - cornerH, cornerW, cornerH),
        };

        var probeClusters = new ClusterSet();

        void Probe(Rect rect, bool isCorner)
        {
            var set = new ClusterSet();
            int samples = 0;
            Sample(image, rect, P.ColorMaxSamplesWord, (r, g, b) =>
            {
                set.For(r, g, b).Add(r, g, b);
                samples++;
            });
            if (samples == 0) return;

            // "Em cada sonda, determina-se a cor dominante."
            var dominant = set.MostPopulous();
            if (dominant is null) return;

            var value = dominant.Value;
            var c = probeClusters.For(value);
            c.AddWeighted(value, 1);
            c.Probes++;
            if (isCorner) c.Corners++;
        }

        foreach (var b in bands) Probe(b, isCorner: false);
        foreach (var c in corners) Probe(c, isCorner: true);

        // RF-400 — elegibilidade: pelo menos P-110 sondas E (pelo menos 2 cantos OU pelo
        // menos 5 sondas no total). P-159.
        var chosen = probeClusters.Clusters
            .Where(c => c.Probes >= P.ProbeMinCount
                        && (c.Corners >= P.ProbeRequiredCorners || c.Probes >= P.ProbeRequiredTotal))
            .OrderByDescending(c => c.Corners)
            .ThenByDescending(c => c.Probes)
            .ThenByDescending(c => c.Population)
            .ThenBy(c => c.Key)
            .FirstOrDefault();

        return chosen?.Value;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Estratégia 2 — anéis ao redor das palavras (RF-402)
    // ─────────────────────────────────────────────────────────────────────────

    private static Rgba? StrategyRings(ImageBuffer image, Rect blockRect, List<Rect> words,
                                       Rgba?[] perWord)
    {
        if (words.Count == 0) return null;

        var clusters = new ClusterSet();
        var imageBounds = new Rect(0, 0, image.Width, image.Height);
        var blockInflated = blockRect.Inflate(P.RingPaddingMax);

        for (int i = 0; i < words.Count; i++)
        {
            var ring = RingOf(words[i], blockInflated, imageBounds);
            if (ring.IsEmpty) continue;

            var wordSet = new ClusterSet();
            int samples = 0;
            Sample(image, ring, P.ColorMaxSamplesWord, (r, g, b) =>
            {
                wordSet.For(r, g, b).Add(r, g, b);
                samples++;
            },
            // Amostram-se os pixels do anel EXCLUINDO qualquer pixel que caia dentro de
            // QUALQUER retângulo de palavra.
            accept: (x, y) =>
            {
                foreach (var w in words)
                {
                    if (w.Contains(x, y)) return false;
                }
                return true;
            });

            if (samples == 0) continue;
            var dominant = wordSet.MostPopulous();
            if (dominant is null) continue;

            perWord[i] = dominant.Value;
            var c = clusters.For(dominant.Value);
            c.AddWeighted(dominant.Value, 1);
            c.SupportingWords++;
        }

        // RF-402 — escolhe-se a de maior apoio POR PALAVRA, depois maior população.
        var chosen = clusters.Clusters
            .OrderByDescending(c => c.SupportingWords)
            .ThenByDescending(c => c.Population)
            .ThenBy(c => c.Key)
            .FirstOrDefault();

        return chosen?.Value;
    }

    /// <summary>
    /// RF-402 — Anel: o retângulo da palavra inflado por um preenchimento igual ao teto de
    /// P-112 vezes o lado menor, saturado entre P-113 e P-114; recortado pelo retângulo do
    /// bloco inflado em P-114; e recortado pela imagem.
    /// </summary>
    private static Rect RingOf(Rect word, Rect blockInflated, Rect imageBounds)
    {
        int minSide = Math.Min(word.Width, word.Height);
        int padding = (int)Math.Ceiling(P.RingPaddingRatio * minSide);
        padding = Math.Clamp(padding, P.RingPaddingMin, P.RingPaddingMax);

        return word.Inflate(padding).Intersect(blockInflated).Intersect(imageBounds);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Estratégia 3 — cor dominante do bloco (RF-403)
    // ─────────────────────────────────────────────────────────────────────────

    private static Rgba? StrategyBlockDominant(ImageBuffer image, Rect blockRect)
    {
        var set = new ClusterSet();
        int samples = 0;
        Sample(image, blockRect, P.ColorMaxSamplesBackground, (r, g, b) =>
        {
            set.For(r, g, b).Add(r, g, b);
            samples++;
        });
        if (samples == 0) return null;
        return set.MostPopulous()?.Value;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cor da fonte (RF-405 a RF-409)
    // ─────────────────────────────────────────────────────────────────────────

    private static Rgba DetermineFontColor(
        ImageBuffer image, List<Rect> words, Rgba?[] borderBg, Rgba?[] ringBg,
        Rgba globalBackground, out int supportingWords, out bool usedFallback)
    {
        var clusters = new ClusterSet();

        for (int i = 0; i < words.Count; i++)
        {
            // RF-405 — fundo local: o valor da estratégia 1 se existir; senão, o anel
            // daquela palavra; senão, o fundo global.
            Rgba local = borderBg[i] ?? ringBg[i] ?? globalBackground;

            var seenInThisWord = new HashSet<int>();
            Sample(image, words[i], P.ColorMaxSamplesWord, (r, g, b) =>
            {
                var candidate = new Rgba(r, g, b);

                // RF-406 — um pixel só é candidato se seu contraste contra o fundo LOCAL
                // for de pelo menos P-115.
                double contrast = ColorMath.ContrastRatio(candidate, local);
                if (contrast < P.MinContrastRatio) return;

                // RF-407 — agrupa por cor quantizada, acumulando população e contraste, e
                // registra em quantas palavras DISTINTAS o agrupamento apareceu.
                var c = clusters.For(r, g, b);
                c.Add(r, g, b, contrast);
                if (seenInThisWord.Add(c.Key)) c.SupportingWords++;
            });
        }

        // RF-408 — maior número de palavras de apoio, depois maior população, depois maior
        // contraste médio.
        // Motivo: a cor do texto aparece em TODAS as palavras; um reflexo ou uma borda
        // aparece em uma só.
        var chosen = clusters.Clusters
            .OrderByDescending(c => c.SupportingWords)
            .ThenByDescending(c => c.Population)
            .ThenByDescending(c => c.AverageContrast)
            .ThenBy(c => c.Key)
            .FirstOrDefault();

        if (chosen is null)
        {
            // RF-409 — nenhum candidato passou no contraste mínimo: usa-se preto ou branco,
            // o que der maior contraste, e marca-se "recorreu a alternativa".
            supportingWords = 0;
            usedFallback = true;
            return ColorMath.BestBlackOrWhite(globalBackground);
        }

        supportingWords = chosen.SupportingWords;
        usedFallback = false;
        return chosen.Value;
    }
}
