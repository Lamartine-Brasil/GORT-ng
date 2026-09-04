using Gort.Core.Calibration;

namespace Gort.Core.Caching;

/// <summary>
/// RF-206 a RF-214 — Memória de resultados anteriores: evita traduzir de novo o que já foi
/// traduzido, dentro da sessão e entre sessões.
///
/// RF-206 — Separada POR SERVIÇO de tradução: o mesmo texto traduzido por serviços
/// diferentes gera entradas diferentes.
/// RF-214 — Os serviços que NÃO usam memória são o banco de dados local e o tradutor local
/// por processo auxiliar; já são consultas locais instantâneas.
/// </summary>
public sealed class ResultMemory
{
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _entries = new(StringComparer.Ordinal);
    private readonly List<TranslationPair> _pending = new();

    /// <summary>RF-212 — Enquanto uma gravação está em andamento, a memória se comporta como VAZIA.</summary>
    private bool _writing;

    public ResultMemory(string serviceKey, string filePath)
    {
        ServiceKey = serviceKey;
        FilePath = filePath;
    }

    public string ServiceKey { get; }
    public string FilePath { get; }

    public int Count
    {
        get { lock (_gate) return _entries.Count; }
    }

    /// <summary>Bytes aproximados ocupados — usado pelo detalhamento de memória (RF-559).</summary>
    public long ApproximateByteCount
    {
        get
        {
            lock (_gate)
                return _entries.Sum(kv => (long)(kv.Key.Length + kv.Value.Length) * sizeof(char));
        }
    }

    /// <summary>RF-208 — Recarregada na inicialização, um arquivo por serviço.</summary>
    public void Load()
    {
        lock (_gate)
        {
            _entries.Clear();
            foreach (var p in PairFile.Load(FilePath)) _entries[p.Source] = p.Target;
        }
    }

    /// <summary>
    /// RF-207 — Consultada ANTES de qualquer chamada de rede.
    /// RF-212 — Devolve nada enquanto uma gravação está em curso.
    /// </summary>
    public string? Lookup(string source)
    {
        lock (_gate)
        {
            if (_writing) return null;
            return _entries.TryGetValue(source.TrimEnd(), out var t) ? t : null;
        }
    }

    /// <summary>
    /// RF-235 — Cada tradução obtida por rede é gravada na memória imediatamente.
    /// RF-210 — Ao atingir P-48 entradas, TODAS as entradas do serviço são descartadas e o
    /// arquivo é esvaziado. 🔒 É política deliberada: simples e barata, sem LRU
    /// (Parte XI, item 17).
    /// </summary>
    public void Store(string source, string target)
    {
        lock (_gate)
        {
            if (_writing) return;

            string key = source.TrimEnd();
            if (key.Length == 0) return;

            if (_entries.Count >= P.ResultMemoryMaxEntries)
            {
                _entries.Clear();
                _pending.Clear();
                TryEmptyFile();
                return;
            }

            if (_entries.ContainsKey(key)) return;
            _entries[key] = target;
            _pending.Add(new TranslationPair(key, target));
        }
    }

    /// <summary>
    /// RF-211 — A gravação em disco é assíncrona, acumulando as novas entradas e gravando
    /// em modo ANEXAR quando o laço termina (passo 21 do fluxo principal).
    /// RF-212 — Enquanto grava, leituras e escritas ficam suspensas, para não corromper a
    /// lista que está sendo serializada.
    /// </summary>
    public Task FlushAsync()
    {
        List<TranslationPair> batch;
        lock (_gate)
        {
            if (_pending.Count == 0) return Task.CompletedTask;
            batch = new List<TranslationPair>(_pending);
            _pending.Clear();
            _writing = true;
        }

        return Task.Run(() =>
        {
            try
            {
                PairFile.Append(FilePath, batch);
            }
            catch
            {
                // P8 — falha de disco não pode derrubar o laço.
            }
            finally
            {
                lock (_gate) _writing = false;
            }
        });
    }

    /// <summary>RF-213 / RF-499 — Limpa toda a memória, apagando também o arquivo.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _pending.Clear();
            TryEmptyFile();
        }
    }

    /// <summary>RF-499 — O comando de limpar fica desabilitado enquanto uma gravação está em curso.</summary>
    public bool IsWriting
    {
        get { lock (_gate) return _writing; }
    }

    private void TryEmptyFile()
    {
        try
        {
            if (File.Exists(FilePath)) File.WriteAllText(FilePath, "");
        }
        catch
        {
            // P8
        }
    }
}
