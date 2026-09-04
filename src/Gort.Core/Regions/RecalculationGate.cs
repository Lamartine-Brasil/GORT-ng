using Gort.Core.Calibration;

namespace Gort.Core.Regions;

/// <summary>
/// RF-059 / RF-060 / RF-457 — Portão do recálculo das áreas de captura.
///
/// Durante o arraste ou o redimensionamento de uma moldura, o sistema é notificado para
/// recalcular as áreas, mas no máximo uma vez a cada P-13. Motivo: permitir ajuste fino COM
/// A TRADUÇÃO RODANDO sem inundar o pipeline — um recálculo por evento de mouse faria a
/// tradução engasgar.
///
/// RF-060 — A notificação só ocorre se o programa já terminou de inicializar e não está no
/// meio de um carregamento ou aplicação de configuração.
/// </summary>
public sealed class RecalculationGate
{
    private readonly Func<DateTime> _now;
    private readonly TimeSpan _interval;
    private DateTime _last = DateTime.MinValue;

    public RecalculationGate(TimeSpan? interval = null, Func<DateTime>? now = null)
    {
        _interval = interval ?? P.FrameDragRecalcInterval;   // P-13
        _now = now ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// RF-060 — Enquanto falso, nenhuma notificação passa. A interface o mantém falso
    /// durante a inicialização e durante um carregamento ou aplicação de configuração.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Portão para a área que segue o mouse, que usa um intervalo próprio (P-123) e é
    /// consultado apenas quando a posição EFETIVAMENTE mudou (RF-457).
    /// </summary>
    public static RecalculationGate ForMouseFollow(Func<DateTime>? now = null)
        => new(TimeSpan.FromMilliseconds(P.MouseFollowRecalcMinIntervalMs), now);

    /// <summary>
    /// Verdadeiro quando o recálculo deve acontecer agora. Cada resposta verdadeira consome
    /// a janela: a próxima só virá depois do intervalo.
    /// </summary>
    public bool ShouldRecalculate()
    {
        if (!Enabled) return false;

        var now = _now();
        if (now - _last < _interval) return false;

        _last = now;
        return true;
    }

    /// <summary>
    /// Reabre o portão imediatamente. Usado ao terminar um arraste, para que a posição
    /// final seja sempre aplicada, mesmo que o último movimento tenha caído dentro do
    /// intervalo.
    /// </summary>
    public void Reset() => _last = DateTime.MinValue;
}
