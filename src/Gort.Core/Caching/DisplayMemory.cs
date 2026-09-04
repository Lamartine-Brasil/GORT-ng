using System.Text;
using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Caching;

/// <summary>
/// RF-222 a RF-224 — Memória de exibição: mantém as últimas N traduções visíveis
/// simultaneamente por alguns segundos, empilhadas da mais recente para a mais antiga,
/// separadas por linha em branco dupla.
///
/// É INDEPENDENTE do cache de traduções: o cache é técnico, esta é de leitura
/// (Parte XI, item 7 — não existe histórico navegável).
/// </summary>
public sealed class DisplayMemory
{
    private readonly List<DisplayMemoryEntry> _entries = new();
    private readonly Func<DateTime> _now;

    public DisplayMemory(Func<DateTime>? now = null) => _now = now ?? (() => DateTime.UtcNow);

    /// <summary>Recurso ligado/desligado pelo usuário; desligado por padrão (IV.12).</summary>
    public bool Enabled { get; set; }

    /// <summary>RF-222 — Quantidade de traduções mantidas (P-49, faixa 1–10).</summary>
    public int Capacity { get; set; } = P.DisplayMemoryCountDefault;

    /// <summary>RF-223 — Tempo de vida de cada entrada em segundos (P-50, faixa até 200).</summary>
    public int LifetimeSeconds { get; set; } = P.DisplayMemoryLifetimeSecondsDefault;

    public int Count => _entries.Count;

    /// <summary>
    /// Aplica a memória de exibição ao texto final do ciclo e devolve o que deve ser
    /// desenhado.
    ///
    /// RF-224 — Quando o texto atual está VAZIO e a memória está ativa, o texto exibido é
    /// composto apenas pelas entradas ainda vivas. Motivo: mantém o diálogo anterior
    /// legível enquanto não há texto novo na tela.
    /// </summary>
    public string Apply(string currentText)
    {
        if (!Enabled) return currentText;

        Expire();

        if (!string.IsNullOrEmpty(currentText))
        {
            _entries.Add(new DisplayMemoryEntry(currentText, _now()));

            int capacity = Math.Clamp(Capacity, P.DisplayMemoryCountMin, P.DisplayMemoryCountMax);
            while (_entries.Count > capacity) _entries.RemoveAt(0);
        }

        // RF-222 — empilhadas da MAIS RECENTE para a mais antiga, separadas por linha em
        // branco dupla.
        var sb = new StringBuilder();
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (sb.Length > 0) sb.Append("\n\n\n");
            sb.Append(_entries[i].Text);
        }
        return sb.ToString();
    }

    /// <summary>
    /// RF-223 — A expiração é verificada do INÍCIO da lista para o fim e PARA no primeiro
    /// item ainda válido. Como as entradas são acrescentadas em ordem cronológica, a lista
    /// está ordenada e a varredura pode parar cedo.
    /// </summary>
    private void Expire()
    {
        var limit = TimeSpan.FromSeconds(Math.Max(0, LifetimeSeconds));
        var now = _now();
        while (_entries.Count > 0 && now - _entries[0].CreatedAt >= limit)
            _entries.RemoveAt(0);
    }

    public void Clear() => _entries.Clear();
}
