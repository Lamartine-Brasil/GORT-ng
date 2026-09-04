using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Structuring;

/// <summary>
/// Cap. 15.2 — Agrupamento de linhas em blocos de tradução. 🔒
///
/// NÃO HÁ HEURÍSTICA ALTERNATIVA (Parte IV.6): este algoritmo é o único. Não existe modo
/// legado, nem correção de caixa de linha por tipo de glifo, nem calibragem de reserva.
/// Quando o agrupamento erra, o ajuste se faz nos parâmetros P-33 a P-45, não trocando de
/// algoritmo.
///
/// RF-159 a RF-162 — componentes conectados por adjacência espacial (união-busca),
/// ordenação dentro do componente e entre componentes, e varredura em ordem aplicando as
/// regras de item de lista, título e continuação.
/// </summary>
public static class BlockGrouper
{
    /// <summary>
    /// Agrupa as linhas de uma região em blocos de tradução.
    /// </summary>
    /// <param name="lines">Linhas reconhecidas, na ordem devolvida pelo OCR.</param>
    /// <param name="mergeLines">
    /// RF-157/RF-158 — Sinalizador de fusão de linhas. Quando desligado, cada linha vira um
    /// bloco independente.
    /// </param>
    /// <param name="removeSpaces">RF-173 — Muda o limite de "linha curta".</param>
    /// <param name="oneLinePerTranslation">
    /// RF-157 — Modo de depuração "uma linha por tradução": também força um bloco por linha.
    /// </param>
    public static List<TranslationBlock> Group(
        IReadOnlyList<Line> lines,
        bool mergeLines,
        bool removeSpaces,
        bool oneLinePerTranslation = false)
    {
        // RF-157 — fusão desligada, ou depuração "uma linha por tradução".
        if (!mergeLines || oneLinePerTranslation)
        {
            var simple = lines.Select(l => new TranslationBlock(l)).ToList();
            foreach (var b in simple) b.RecalculateBoxes();   // RF-179
            return simple;
        }

        if (lines.Count == 0) return new List<TranslationBlock>();

        // O tamanho de fonte é consultado O(n²) vezes pela adjacência: calcular uma vez.
        var fontSizes = new double[lines.Count];
        for (int i = 0; i < lines.Count; i++)
            fontSizes[i] = FontSizeEstimator.Estimate(lines[i]);

        var components = BuildComponents(lines, fontSizes);   // RF-159
        SortWithinComponents(components);                      // RF-160
        SortComponents(components);                            // RF-161

        var blocks = new List<TranslationBlock>();
        foreach (var component in components)
            GroupComponent(component, removeSpaces, blocks);   // RF-162

        // RF-179 — a caixa de cada bloco é a união das caixas das suas linhas.
        foreach (var b in blocks) b.RecalculateBoxes();
        return blocks;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RF-159 — Componentes conectados por adjacência espacial (união-busca)
    // ─────────────────────────────────────────────────────────────────────────

    private static List<List<Line>> BuildComponents(IReadOnlyList<Line> lines, double[] fontSizes)
    {
        var dsu = new DisjointSet(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            for (int j = i + 1; j < lines.Count; j++)
            {
                if (Adjacency.AreAdjacent(lines[i], fontSizes[i], lines[j], fontSizes[j]))
                    dsu.Union(i, j);
            }
        }

        // Preserva a ordem de descoberta dos componentes, para que a ordenação estável de
        // RF-161 tenha um desempate previsível.
        var byRoot = new Dictionary<int, List<Line>>();
        var order = new List<int>();
        for (int i = 0; i < lines.Count; i++)
        {
            int root = dsu.Find(i);
            if (!byRoot.TryGetValue(root, out var list))
            {
                list = new List<Line>();
                byRoot[root] = list;
                order.Add(root);
            }
            list.Add(lines[i]);
        }
        return order.Select(r => byRoot[r]).ToList();
    }

    /// <summary>
    /// RF-160 — Dentro de cada componente: se o componente é vertical, por coordenada
    /// direita DECRESCENTE e depois topo crescente (leitura em colunas da direita para a
    /// esquerda); se é horizontal, por topo crescente e depois esquerda crescente. 🔒
    /// </summary>
    private static void SortWithinComponents(List<List<Line>> components)
    {
        for (int i = 0; i < components.Count; i++)
        {
            var c = components[i];
            components[i] = ComponentOrientation(c) == Orientation.Vertical
                ? c.OrderByDescending(l => l.Box.Right).ThenBy(l => l.Box.Top).ToList()
                : c.OrderBy(l => l.Box.Top).ThenBy(l => l.Box.Left).ToList();
        }
    }

    /// <summary>
    /// RF-161 — Os componentes são ordenados entre si por topo crescente; em caso de
    /// empate, componentes verticais por direita decrescente e horizontais por esquerda
    /// crescente. A ordenação é estável, então empates remanescentes preservam a ordem de
    /// descoberta.
    /// </summary>
    private static void SortComponents(List<List<Line>> components)
    {
        var sorted = components
            .OrderBy(c => c.Min(l => l.Box.Top))
            .ThenBy(c => ComponentOrientation(c) == Orientation.Vertical
                ? -c.Max(l => l.Box.Right)
                : c.Min(l => l.Box.Left))
            .ToList();

        components.Clear();
        components.AddRange(sorted);
    }

    /// <summary>
    /// A adjacência exige mesma orientação (RF-163, condição 1), então todas as linhas de
    /// um componente compartilham a orientação da primeira.
    /// </summary>
    private static Orientation ComponentOrientation(List<Line> component)
        => component.Count == 0 ? Orientation.Horizontal : component[0].Orientation;

    // ─────────────────────────────────────────────────────────────────────────
    // RF-162 — Varredura do componente
    //
    // Segue literalmente o pseudocódigo de 15.2.
    // ─────────────────────────────────────────────────────────────────────────

    private static void GroupComponent(
        List<Line> component, bool removeSpaces, List<TranslationBlock> blocks)
    {
        bool listContext = LineClassifier.IsListContext(component);   // RF-165
        TranslationBlock? current = null;
        Line? previous = null;

        for (int i = 0; i < component.Count; i++)
        {
            Line line = component[i];
            Line? next = i + 1 < component.Count ? component[i + 1] : null;

            bool isItem = LineClassifier.IsListItem(line, listContext);

            // RF-174 — itens de lista têm precedência sobre títulos.
            bool isTitle = !isItem
                && (LineClassifier.IsExplicitTitle(line.Text)
                    || (i == 0 && LineClassifier.IsContextTitle(line, next, removeSpaces)));

            if (isItem)
            {
                // RF-170 — vira bloco próprio e quebra o bloco em construção.
                blocks.Add(new TranslationBlock(line));
                current = null;
                previous = line;
                continue;
            }

            if (isTitle)
            {
                // RF-174 — vira bloco próprio marcado como título e quebra o bloco atual.
                var titleBlock = new TranslationBlock(line) { IsTitle = true };
                blocks.Add(titleBlock);
                current = null;
                previous = line;
                continue;
            }

            // RF-175 — a linha continua o bloco atual apenas se todas valerem.
            if (current is null
                || previous is null
                || LineClassifier.EndsSentence(previous)
                || !CanAppend(current, previous, line))
            {
                current = new TranslationBlock(line);
                blocks.Add(current);
            }
            else
            {
                current.Append(line);
            }

            previous = line;

            // RF-178 — quando uma linha termina frase, o bloco é encerrado após recebê-la.
            if (LineClassifier.EndsSentence(line)) current = null;
        }
    }

    /// <summary>
    /// RF-176 — Teste de anexação por tamanho de fonte:
    ///   1. a linha candidata deve ser espacialmente adjacente à linha anterior;
    ///   2. o tamanho da candidata e a MEDIANA dos tamanhos das linhas já no bloco devem
    ///      existir e ser positivos;
    ///   3. a razão entre a candidata e essa mediana não pode exceder P-44; 🔒
    ///   4. a razão entre o maior e o menor tamanho considerando todo o bloco MAIS a
    ///      candidata não pode exceder P-44. 🔒
    /// </summary>
    private static bool CanAppend(TranslationBlock block, Line previous, Line candidate)
    {
        // 1.
        if (!Adjacency.AreAdjacent(previous, candidate)) return false;

        // 2.
        double candidateSize = FontSizeEstimator.Estimate(candidate);
        if (candidateSize <= 0) return false;

        var blockSizes = block.Lines.Select(FontSizeEstimator.Estimate).ToList();
        double median = FontSizeEstimator.Median(blockSizes);
        if (median <= 0) return false;

        // 3.
        double ratioToMedian = Math.Max(candidateSize, median) / Math.Min(candidateSize, median);
        if (ratioToMedian > P.AppendMaxFontRatio) return false;

        // 4.
        double min = candidateSize, max = candidateSize;
        foreach (double s in blockSizes)
        {
            if (s < min) min = s;
            if (s > max) max = s;
        }
        if (min <= 0) return false;
        return max / min <= P.AppendMaxFontRatio;
    }

    /// <summary>União-busca com compressão de caminho e união por posto.</summary>
    private sealed class DisjointSet
    {
        private readonly int[] _parent;
        private readonly int[] _rank;

        public DisjointSet(int n)
        {
            _parent = new int[n];
            _rank = new int[n];
            for (int i = 0; i < n; i++) _parent[i] = i;
        }

        public int Find(int x)
        {
            while (_parent[x] != x)
            {
                _parent[x] = _parent[_parent[x]];
                x = _parent[x];
            }
            return x;
        }

        public void Union(int a, int b)
        {
            int ra = Find(a), rb = Find(b);
            if (ra == rb) return;
            if (_rank[ra] < _rank[rb]) (ra, rb) = (rb, ra);
            _parent[rb] = ra;
            if (_rank[ra] == _rank[rb]) _rank[ra]++;
        }
    }
}
