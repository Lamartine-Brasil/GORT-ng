using System.Text;

namespace Gort.Core.Caching;

/// <summary>Um par texto-original / texto-traduzido.</summary>
public readonly record struct TranslationPair(string Source, string Target);

/// <summary>
/// RF-209 / RF-243 — Formato compartilhado pelos arquivos de pares: a memória de resultados
/// anteriores, o banco de dados local e os arquivos da coletânea usam o MESMO formato.
///
///   linha "/s"
///   texto de origem (uma ou mais linhas)
///   linha "/t"
///   texto traduzido (uma ou mais linhas)
///   linha "/e"
///   linha em branco
///
/// Ao carregar, o texto de origem tem os espaços à direita removidos (RF-209).
/// Linhas inválidas são ignoradas em vez de interromper a leitura (caso de erro do cap. 17).
/// </summary>
public static class PairFile
{
    public const string SourceMarker = "/s";
    public const string TargetMarker = "/t";
    public const string EndMarker = "/e";

    public static List<TranslationPair> Load(string path)
    {
        var pairs = new List<TranslationPair>();
        if (!File.Exists(path)) return pairs;

        try
        {
            Parse(File.ReadAllLines(path), pairs);
        }
        catch
        {
            // P8 — um arquivo ilegível degrada para "sem pares", nunca para exceção.
        }
        return pairs;
    }

    public static List<TranslationPair> Parse(IReadOnlyList<string> lines)
    {
        var pairs = new List<TranslationPair>();
        Parse(lines, pairs);
        return pairs;
    }

    private static void Parse(IReadOnlyList<string> lines, List<TranslationPair> pairs)
    {
        int i = 0;
        while (i < lines.Count)
        {
            if (lines[i].TrimEnd() != SourceMarker) { i++; continue; }
            i++;

            var source = new StringBuilder();
            bool first = true;
            while (i < lines.Count && lines[i].TrimEnd() != TargetMarker)
            {
                if (!first) source.Append('\n');
                source.Append(lines[i]);
                first = false;
                i++;
            }
            if (i >= lines.Count) break;   // registro truncado: ignorado
            i++;   // consome "/t"

            var target = new StringBuilder();
            first = true;
            while (i < lines.Count && lines[i].TrimEnd() != EndMarker
                                   && lines[i].TrimEnd() != SourceMarker)
            {
                if (!first) target.Append('\n');
                target.Append(lines[i]);
                first = false;
                i++;
            }
            if (i < lines.Count && lines[i].TrimEnd() == EndMarker) i++;

            // RF-209 — o texto de origem tem espaços à direita removidos ao carregar.
            string src = source.ToString().TrimEnd();
            if (src.Length == 0) continue;
            pairs.Add(new TranslationPair(src, target.ToString()));
        }
    }

    public static string Format(TranslationPair pair)
    {
        var sb = new StringBuilder();
        Format(sb, pair);
        return sb.ToString();
    }

    public static void Format(StringBuilder sb, TranslationPair pair)
    {
        sb.Append(SourceMarker).Append('\n');
        sb.Append(pair.Source).Append('\n');
        sb.Append(TargetMarker).Append('\n');
        sb.Append(pair.Target).Append('\n');
        sb.Append(EndMarker).Append('\n');
        sb.Append('\n');
    }

    public static void Append(string path, IEnumerable<TranslationPair> pairs)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        foreach (var p in pairs) Format(sb, p);
        if (sb.Length == 0) return;
        File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public static void Write(string path, IEnumerable<TranslationPair> pairs)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        foreach (var p in pairs) Format(sb, p);
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }
}
