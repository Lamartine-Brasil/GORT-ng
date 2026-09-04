namespace Gort.Core.Caching;

/// <summary>RF-218 — Modos de busca da coletânea de tradução.</summary>
public enum CollectionLookupMode
{
    /// <summary>O par só se aplica se o texto for idêntico.</summary>
    Exact,
    /// <summary>
    /// Os arquivos ativos são consultados pelo mesmo mecanismo do banco de dados local,
    /// que permite correspondência parcial.
    /// </summary>
    Database,
}

/// <summary>
/// RF-215 a RF-221 — Coletânea de tradução do usuário: um conjunto de arquivos de pares que
/// o usuário ativa por caixas de seleção, consultado ANTES da memória de resultados e antes
/// de qualquer chamada de rede (6.6).
/// </summary>
public sealed class TranslationCollection
{
    private readonly Dictionary<string, string> _exact = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _exactIgnoreCase = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TranslationPair> _all = new();

    /// <summary>RF-218 — Modo de busca ativo.</summary>
    public CollectionLookupMode Mode { get; set; } = CollectionLookupMode.Database;

    /// <summary>RF-220 — O modo de banco de dados tem opção de ignorar maiúsculas/minúsculas.</summary>
    public bool IgnoreCase { get; set; } = true;

    /// <summary>
    /// RF-219 — O modo de banco de dados só é usado quando o idioma de OCR ativo separa ou
    /// não palavras de forma conhecida pelo mecanismo — na prática, inglês ou japonês.
    /// Em outros idiomas, cai para correspondência exata. 🔒
    /// Esta propriedade é resolvida pelo chamador a partir das PROPRIEDADES do idioma
    /// (RF-311), nunca comparando identificadores (RF-567).
    /// </summary>
    public bool DatabaseModeAvailable { get; set; } = true;

    public int Count => _all.Count;
    public IReadOnlyList<TranslationPair> Pairs => _all;

    /// <summary>
    /// RF-216 — Carrega os arquivos ativos. Apenas arquivos que EXISTEM no disco são
    /// mantidos na lista ao carregar; a lista efetiva é devolvida ao chamador para que ele
    /// a persista de volta nas opções avançadas.
    /// </summary>
    public IReadOnlyList<string> Load(IEnumerable<string> activeFiles)
    {
        _exact.Clear();
        _exactIgnoreCase.Clear();
        _all.Clear();

        var kept = new List<string>();
        foreach (var file in activeFiles)
        {
            if (!File.Exists(file)) continue;   // RF-216 — some da lista no próximo carregamento
            kept.Add(file);
            foreach (var p in PairFile.Load(file))
            {
                _all.Add(p);
                _exact[p.Source] = p.Target;
                _exactIgnoreCase[p.Source] = p.Target;
            }
        }
        return kept;
    }

    /// <summary>
    /// RF-215 / RF-218 / RF-219 — Consulta. No modo de banco de dados indisponível, cai
    /// para correspondência exata.
    /// </summary>
    public string? Lookup(string source)
    {
        if (_all.Count == 0) return null;
        string key = source.TrimEnd();
        if (key.Length == 0) return null;

        bool database = Mode == CollectionLookupMode.Database && DatabaseModeAvailable;

        if (!database)
        {
            // RF-218 modo 1 — o par só se aplica se o texto for idêntico.
            return _exact.TryGetValue(key, out var t) ? t : null;
        }

        return LocalDatabase.Match(_all, key, IgnoreCase, partialMultiline: true);
    }

    /// <summary>
    /// RF-217 — Cada arquivo pode conter uma seção de informação, exibida ao usuário quando
    /// ele seleciona o arquivo na lista. Convenção: linhas iniciadas por "#" no topo do
    /// arquivo, antes do primeiro registro.
    /// </summary>
    public static string ReadInfo(string file)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            foreach (var line in File.ReadLines(file))
            {
                if (line.TrimEnd() == PairFile.SourceMarker) break;
                if (line.StartsWith('#')) sb.AppendLine(line.TrimStart('#').Trim());
            }
            return sb.ToString().Trim();
        }
        catch
        {
            return "";
        }
    }
}

/// <summary>
/// RF-241 a RF-243 — Banco de dados local: consulta um dicionário de pares carregado de
/// arquivo, sem rede. Também é o mecanismo usado pela coletânea em "modo de banco de
/// dados" (RF-218).
/// </summary>
public sealed class LocalDatabase
{
    private readonly List<TranslationPair> _pairs = new();
    private readonly Dictionary<string, string> _exact = new(StringComparer.Ordinal);

    /// <summary>RF-242 — Ignorar maiúsculas/minúsculas.</summary>
    public bool IgnoreCase { get; set; }

    /// <summary>RF-242 — Correspondência parcial em múltiplas linhas.</summary>
    public bool PartialMultiline { get; set; }

    public int Count => _pairs.Count;

    /// <summary>RF-243 — O formato do arquivo é o mesmo da memória de resultados.</summary>
    public void Load(string path)
    {
        _pairs.Clear();
        _exact.Clear();
        foreach (var p in PairFile.Load(path))
        {
            _pairs.Add(p);
            _exact[p.Source] = p.Target;
        }
    }

    /// <summary>
    /// RF-241 — Consulta primeiro o dicionário em memória; se não encontrar, delega à busca
    /// que suporta correspondência parcial. Resultado igual ao marcador de "sem resultado"
    /// vira vazio.
    /// </summary>
    public string Lookup(string source)
    {
        string key = source.TrimEnd();
        if (key.Length == 0) return "";

        if (!IgnoreCase && _exact.TryGetValue(key, out var direct))
            return Normalize(direct);

        return Normalize(Match(_pairs, key, IgnoreCase, PartialMultiline) ?? "");
    }

    private static string Normalize(string value)
        => value == Structuring.TextPostProcessor.NoResultMarker ? "" : value;

    /// <summary>
    /// Mecanismo de busca compartilhado. Correspondência exata primeiro; depois, quando
    /// habilitada, correspondência parcial: o par vale se seu texto de origem estiver
    /// contido no texto consultado. Entre vários candidatos parciais, vence o de origem
    /// mais LONGA — a correspondência mais específica.
    /// </summary>
    internal static string? Match(IReadOnlyList<TranslationPair> pairs, string key,
                                  bool ignoreCase, bool partialMultiline)
    {
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        foreach (var p in pairs)
        {
            if (string.Equals(p.Source, key, comparison)) return p.Target;
        }

        if (!partialMultiline) return null;

        string? best = null;
        int bestLength = -1;
        foreach (var p in pairs)
        {
            if (p.Source.Length == 0) continue;
            if (key.Contains(p.Source, comparison) && p.Source.Length > bestLength)
            {
                best = p.Target;
                bestLength = p.Source.Length;
            }
        }
        return best;
    }
}
