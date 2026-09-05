using Gort.Core.Calibration;
using Gort.Core.Configuration;
using Tomlyn.Model;

namespace Gort.Core.Ocr;

/// <summary>
/// RF-124 a RF-127 — Contagem de uso do motor de OCR de nuvem.
///
/// A contagem é POR CREDENCIAL e POR MÊS CIVIL, zerando quando o mês ou o ano mudam. O
/// programa impõe o próprio limite (P-29) abaixo da cota gratuita real do serviço, porque
/// ultrapassá-la gera cobrança — e a contagem local pode divergir da do serviço.
/// </summary>
public sealed class CloudOcrQuota
{
    public const int CurrentSchemaVersion = 1;

    private readonly Func<DateTime> _now;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public CloudOcrQuota(Func<DateTime>? now = null) => _now = now ?? (() => DateTime.Now);

    private sealed class Entry
    {
        public int Used;
        public int Year;
        public int Month;
    }

    /// <summary>P-29 — Limite mensal imposto pelo programa. Exposto na interface.</summary>
    public int Limit { get; set; } = P.CloudOcrMonthlyLimit;

    /// <summary>
    /// RF-124 — Chamadas usadas pela credencial no mês corrente. A leitura já aplica a
    /// virada de mês: consultar depois do dia 1 devolve zero sem precisar de nenhum passo
    /// de manutenção.
    /// </summary>
    public int UsedBy(string credential)
    {
        var entry = Current(credential);
        return entry.Used;
    }

    /// <summary>RF-127 — Exibição "usadas / limite".</summary>
    public string Format(string credential) => $"{UsedBy(credential)} / {Limit}";

    /// <summary>
    /// RF-125 — Verdadeiro quando a contagem atingiu o limite e o motor deve RECUSAR novas
    /// chamadas.
    /// </summary>
    public bool IsExhausted(string credential) => UsedBy(credential) >= Limit;

    /// <summary>Registra uma chamada. Devolve falso quando a cota já acabou.</summary>
    public bool TryConsume(string credential)
    {
        var entry = Current(credential);
        if (entry.Used >= Limit) return false;

        entry.Used++;
        return true;
    }

    /// <summary>
    /// RF-124 — Entrada da credencial, com a contagem zerada quando o mês ou o ano mudaram.
    /// </summary>
    private Entry Current(string credential)
    {
        var now = _now();

        if (!_entries.TryGetValue(credential, out var entry))
        {
            entry = new Entry { Year = now.Year, Month = now.Month };
            _entries[credential] = entry;
            return entry;
        }

        if (entry.Year != now.Year || entry.Month != now.Month)
        {
            entry.Used = 0;
            entry.Year = now.Year;
            entry.Month = now.Month;
        }
        return entry;
    }

    /// <summary>
    /// RF-127 — A contagem é persistida por credencial, junto com a data da última
    /// renovação.
    /// </summary>
    public void Save(string path)
    {
        var store = new TomlStore();
        store.SchemaVersion = CurrentSchemaVersion;
        store.Set("limit", Limit);

        store.SetTables("credential", _entries.Select(kv => new TomlTable
        {
            ["id"] = kv.Key,
            ["used"] = (long)kv.Value.Used,
            ["year"] = (long)kv.Value.Year,
            ["month"] = (long)kv.Value.Month,
        }));

        store.Save(path);
    }

    public static CloudOcrQuota Load(string path, Func<DateTime>? now = null)
    {
        var quota = new CloudOcrQuota(now);

        try
        {
            var store = TomlStore.Load(path, out bool recovered);
            if (recovered) return quota;

            quota.Limit = store.GetInt("limit", P.CloudOcrMonthlyLimit);

            foreach (var record in store.GetTables("credential"))
            {
                if (record.TryGetValue("id", out var id) && id is string key)
                {
                    quota._entries[key] = new Entry
                    {
                        Used = record.TryGetValue("used", out var u) ? Convert.ToInt32(u) : 0,
                        Year = record.TryGetValue("year", out var y) ? Convert.ToInt32(y) : 0,
                        Month = record.TryGetValue("month", out var m) ? Convert.ToInt32(m) : 0,
                    };
                }
            }
        }
        catch
        {
            // RF-024 — um arquivo ilegível volta à contagem vazia, em vez de impedir o uso.
            return new CloudOcrQuota(now);
        }

        return quota;
    }
}
