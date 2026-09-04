using Tomlyn;
using Tomlyn.Model;

namespace Gort.Core.Configuration;

/// <summary>
/// RF-023 — Formato dos arquivos de dados do usuário.
///
/// A especificação deixa a escolha concreta de serialização a quem constrói, desde que
/// satisfaça quatro exigências. A escolha aqui é TOML, que as satisfaz todas:
///   - texto legível e editável por uma pessoa;
///   - valores de múltiplas linhas (cadeias entre três aspas);
///   - comentários (linhas iniciadas por "#");
///   - um número de versão de esquema na raiz.
///
/// RF-564 — Não há leitura de formato legado em lugar algum: o programa não tem
/// compatibilidade com nenhum produto anterior.
///
/// RF-038 — Chaves DESCONHECIDAS de uma versão mais nova são preservadas intactas na
/// regravação, porque o usuário pode alternar entre uma versão nova e uma antiga do
/// programa e não pode perder configuração por isso. É o que a tabela interna garante:
/// escrevemos por cima das chaves que conhecemos e deixamos as demais onde estavam.
/// </summary>
public sealed class TomlStore
{
    public const string SchemaVersionKey = "schema_version";

    private TomlTable _table;

    public TomlStore(TomlTable? table = null) => _table = table ?? new TomlTable();

    /// <summary>RF-023 / RF-038 — Versão do esquema gravada na raiz.</summary>
    public int SchemaVersion
    {
        get => GetInt(SchemaVersionKey, 0);
        set => Set(SchemaVersionKey, value);
    }

    public TomlTable Table => _table;

    /// <summary>
    /// RF-024 — O leitor é TOLERANTE: linhas desconhecidas são ignoradas; a ausência de uma
    /// chave mantém o valor padrão; qualquer exceção durante a leitura restaura TODOS os
    /// padrões e continua.
    /// </summary>
    /// <param name="recovered">
    /// Verdadeiro quando o arquivo estava ausente, vazio ou corrompido e o resultado é um
    /// armazenamento vazio — o chamador usa isso para avisar o usuário uma única vez (RF-028).
    /// </param>
    public static TomlStore Load(string path, out bool recovered)
    {
        recovered = false;
        try
        {
            if (!File.Exists(path))
            {
                recovered = true;
                return new TomlStore();
            }
            string text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text))
            {
                recovered = true;
                return new TomlStore();
            }
            return new TomlStore(Toml.ToModel(text));
        }
        catch
        {
            // Qualquer exceção restaura todos os padrões e continua (RF-024).
            recovered = true;
            return new TomlStore();
        }
    }

    public static TomlStore Load(string path) => Load(path, out _);

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, Toml.FromModel(_table));
    }

    public string ToText() => Toml.FromModel(_table);

    public static TomlStore FromText(string text) => new(Toml.ToModel(text));

    // ─────────────────────────────────────────────────────────────────────────
    // Leitura tolerante — a ausência de uma chave devolve o padrão (RF-024, RF-025)
    // ─────────────────────────────────────────────────────────────────────────

    public bool Has(string key) => _table.ContainsKey(key);

    public string GetString(string key, string fallback)
        => _table.TryGetValue(key, out var v) && v is string s ? s : fallback;

    public bool GetBool(string key, bool fallback)
        => _table.TryGetValue(key, out var v) && v is bool b ? b : fallback;

    public int GetInt(string key, int fallback)
    {
        if (!_table.TryGetValue(key, out var v)) return fallback;
        try { return Convert.ToInt32(v); } catch { return fallback; }
    }

    public double GetDouble(string key, double fallback)
    {
        if (!_table.TryGetValue(key, out var v)) return fallback;
        try { return Convert.ToDouble(v); } catch { return fallback; }
    }

    public IReadOnlyList<string> GetStringList(string key)
        => _table.TryGetValue(key, out var v) && v is TomlArray a
            ? a.OfType<string>().ToList()
            : Array.Empty<string>();

    public IReadOnlyList<TomlTable> GetTables(string key)
    {
        if (!_table.TryGetValue(key, out var v)) return Array.Empty<TomlTable>();
        return v switch
        {
            TomlTableArray arr => arr.ToList(),
            TomlArray a => a.OfType<TomlTable>().ToList(),
            _ => Array.Empty<TomlTable>(),
        };
    }

    public TomlStore GetSection(string key)
        => _table.TryGetValue(key, out var v) && v is TomlTable t
            ? new TomlStore(t)
            : new TomlStore();

    // ─────────────────────────────────────────────────────────────────────────
    // Escrita — só as chaves conhecidas são tocadas (RF-038)
    // ─────────────────────────────────────────────────────────────────────────

    public void Set(string key, string value) => _table[key] = value;
    public void Set(string key, bool value) => _table[key] = value;
    public void Set(string key, int value) => _table[key] = (long)value;
    public void Set(string key, double value) => _table[key] = value;

    public void Set(string key, IEnumerable<string> values)
    {
        var arr = new TomlArray();
        foreach (var v in values) arr.Add(v);
        _table[key] = arr;
    }

    public void SetTables(string key, IEnumerable<TomlTable> tables)
    {
        var arr = new TomlTableArray();
        foreach (var t in tables) arr.Add(t);
        _table[key] = arr;
    }

    public TomlStore Section(string key)
    {
        if (_table.TryGetValue(key, out var v) && v is TomlTable t) return new TomlStore(t);
        var created = new TomlTable();
        _table[key] = created;
        return new TomlStore(created);
    }
}
