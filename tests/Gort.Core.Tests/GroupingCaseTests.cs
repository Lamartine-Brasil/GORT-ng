using Gort.Core.Model;
using Gort.Core.Structuring;
using Tomlyn;
using Tomlyn.Model;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>
/// Etapa 6 da PARTE X: "Esta etapa deve ter uma bateria de testes de unidade com casos
/// reais gravados em arquivo — é a parte mais fácil de quebrar sem perceber."
///
/// Cada arquivo em tests/cases/grouping descreve as linhas de entrada e os blocos
/// esperados. Acrescentar um caso é acrescentar um arquivo; nenhum código muda.
/// </summary>
public class GroupingCaseTests
{
    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        var dir = Path.Combine(TestPaths.CasesDirectory, "grouping");
        foreach (var f in Directory.EnumerateFiles(dir, "*.toml").OrderBy(f => f))
            data.Add(Path.GetFileName(f));
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Caso_gravado_produz_os_blocos_esperados(string fileName)
    {
        var c = GroupingCase.Load(Path.Combine(TestPaths.CasesDirectory, "grouping", fileName));

        var blocks = BlockGrouper.Group(c.Lines, c.MergeLines, c.RemoveSpaces);

        Assert.Equal(c.ExpectedBlocks.Count, blocks.Count);

        for (int i = 0; i < blocks.Count; i++)
        {
            var expected = c.ExpectedBlocks[i];
            var actual = blocks[i];

            // As linhas do bloco, identificadas pelo índice de ENTRADA, na ordem de leitura.
            var actualIndices = actual.Lines.Select(l => c.IndexOf(l)).ToArray();
            Assert.Equal(expected.LineIndices, actualIndices);
            Assert.Equal(expected.IsTitle, actual.IsTitle);

            // RF-179 — a caixa do bloco é a união das caixas das suas linhas, e as três
            // caixas nascem iguais a ela.
            var union = Rect.UnionAll(actual.Lines.Select(l => l.Box));
            Assert.Equal(union, actual.SourceBox);
            Assert.Equal(union, actual.ViewBox);
            Assert.Equal(union, actual.ContentBox);
        }

        // Nenhuma linha de entrada pode se perder nem se duplicar no agrupamento.
        var todas = blocks.SelectMany(b => b.Lines).Select(c.IndexOf).OrderBy(i => i).ToArray();
        Assert.Equal(Enumerable.Range(0, c.Lines.Count).ToArray(), todas);
    }

    /// <summary>
    /// Critério de aceite do capítulo 15: "Ligar e desligar a fusão de linhas altera apenas
    /// o agrupamento, nunca o texto reconhecido."
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void RF_157_a_fusao_altera_apenas_o_agrupamento_nunca_o_texto(string fileName)
    {
        var c = GroupingCase.Load(Path.Combine(TestPaths.CasesDirectory, "grouping", fileName));

        var comFusao = BlockGrouper.Group(c.Lines, mergeLines: true, c.RemoveSpaces);
        var semFusao = BlockGrouper.Group(c.Lines, mergeLines: false, c.RemoveSpaces);

        // Sem fusão, cada linha é um bloco (RF-157).
        Assert.Equal(c.Lines.Count, semFusao.Count);

        // O conjunto de texto reconhecido é idêntico nos dois modos.
        static string Todo(IEnumerable<TranslationBlock> bs)
            => string.Concat(bs.SelectMany(b => b.Lines).OrderBy(l => l.Box.Top)
                                                        .ThenBy(l => l.Box.Left)
                                                        .Select(l => l.Text));
        Assert.Equal(Todo(semFusao), Todo(comFusao));
    }
}

/// <summary>Um caso de agrupamento gravado em arquivo.</summary>
public sealed class GroupingCase
{
    public required string Name { get; init; }
    public required bool MergeLines { get; init; }
    public required bool RemoveSpaces { get; init; }
    public required IReadOnlyList<Line> Lines { get; init; }
    public required IReadOnlyList<ExpectedBlock> ExpectedBlocks { get; init; }

    public int IndexOf(Line line)
    {
        for (int i = 0; i < Lines.Count; i++)
        {
            if (ReferenceEquals(Lines[i], line)) return i;
        }
        return -1;
    }

    public sealed record ExpectedBlock(int[] LineIndices, bool IsTitle);

    public static GroupingCase Load(string path)
    {
        var t = Toml.ToModel(File.ReadAllText(path));

        var lines = new List<Line>();
        foreach (var item in Array(t, "line"))
        {
            string text = (string)item["text"];
            int x = Int(item, "x"), y = Int(item, "y"), font = Int(item, "font");
            string orientation = item.TryGetValue("orientation", out var o) ? (string)o : "horizontal";
            lines.Add(orientation.Equals("vertical", StringComparison.OrdinalIgnoreCase)
                ? LineBuilder.Vertical(text, x, y, font)
                : LineBuilder.Horizontal(text, x, y, font));
        }

        var blocks = new List<ExpectedBlock>();
        foreach (var item in Array(t, "block"))
        {
            var idx = ((TomlArray)item["lines"]).Select(v => Convert.ToInt32(v)).ToArray();
            bool title = item.TryGetValue("title", out var ti) && ti is bool b && b;
            blocks.Add(new ExpectedBlock(idx, title));
        }

        return new GroupingCase
        {
            Name = t.TryGetValue("name", out var n) ? (string)n : Path.GetFileName(path),
            MergeLines = !t.TryGetValue("merge_lines", out var m) || (bool)m,
            RemoveSpaces = t.TryGetValue("remove_spaces", out var r) && (bool)r,
            Lines = lines,
            ExpectedBlocks = blocks,
        };
    }

    private static IEnumerable<TomlTable> Array(TomlTable t, string key)
        => t.TryGetValue(key, out var v) && v is TomlTableArray a
            ? a
            : Enumerable.Empty<TomlTable>();

    private static int Int(TomlTable t, string key) => Convert.ToInt32(t[key]);
}
