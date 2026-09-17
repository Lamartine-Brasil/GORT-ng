using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Regions;

/// <summary>Um quadro guardado, com o instante em que foi recebido.</summary>
public sealed record AttachedFrame(ImageBuffer Image, DateTime Received);

/// <summary>
/// RF-093 a RF-096 — O buffer de quadros recentes do modo janela anexada.
///
/// A captura de janela anexada é um FLUXO: o sistema entrega quadros quando quer, não quando
/// o laço pede. Sem buffer, cada ciclo esperaria o próximo quadro chegar — e o laço de
/// tradução é síncrono de ponta a ponta (RF-009), então essa espera seria latência pura.
///
/// Com buffer, o pedido é atendido IMEDIATAMENTE com o último quadro válido. As três
/// calibragens que governam isso são 🔒:
///
///  - P-17 (5) — quantos quadros ficam em reserva;
///  - P-18 (10) — a cada quantos quadros recebidos um é guardado sem pedido, para manter o
///    buffer aquecido;
///  - P-19 (100 ms) — a idade máxima de um quadro ainda reaproveitável. Mais velho que isso
///    e a tela já mudou; o laço espera um novo (RF-095).
/// </summary>
public sealed class AttachedFrameBuffer
{
    private readonly object _gate = new();
    private readonly Queue<AttachedFrame> _frames = new();
    private readonly Func<DateTime> _now;

    private int _received;

    public AttachedFrameBuffer(Func<DateTime>? now = null)
        => _now = now ?? (() => DateTime.UtcNow);

    /// <summary>P-17 — Quantos quadros cabem na reserva.</summary>
    public int Capacity { get; init; } = P.AttachedFrameBufferSize;

    /// <summary>P-18 — A cada quantos quadros recebidos um é guardado sem pedido.</summary>
    public int IdlePeriod { get; init; } = P.AttachedIdleCapturePeriodFrames;

    /// <summary>P-19 — Idade máxima de um quadro ainda utilizável.</summary>
    public TimeSpan MaxAge { get; init; } = P.AttachedFrameMaxAge;

    public int Count
    {
        get { lock (_gate) return _frames.Count; }
    }

    /// <summary>Quantos quadros o fluxo entregou desde o início.</summary>
    public int Received
    {
        get { lock (_gate) return _received; }
    }

    /// <summary>
    /// RF-094 — Um quadro chegou do fluxo.
    ///
    /// Devolve verdadeiro quando ele foi GUARDADO. Não é todo quadro que se guarda: a
    /// primeira entrega enche o buffer, e depois só uma a cada P-18 — guardar todos gastaria
    /// memória com quadros que ninguém vai ler, e a janela pode estar entregando 60 por
    /// segundo.
    ///
    /// <paramref name="wanted"/> força a guarda: é o caso em que o laço PEDIU e o buffer
    /// estava vazio ou velho.
    /// </summary>
    public bool Offer(ImageBuffer image, bool wanted = false)
    {
        if (image.IsEmpty) return false;

        lock (_gate)
        {
            _received++;

            bool keep = wanted
                        || _frames.Count == 0
                        || _received % IdlePeriod == 0;

            if (!keep) return false;

            _frames.Enqueue(new AttachedFrame(image, _now()));

            // P-17 — o mais antigo sai quando a reserva enche.
            while (_frames.Count > Capacity) _frames.Dequeue();

            return true;
        }
    }

    /// <summary>
    /// RF-093 / RF-095 — O quadro mais recente, se ainda for utilizável.
    ///
    /// Devolve nulo quando não há nenhum ou quando o mais novo já passou de P-19 — nesse
    /// caso RF-095 manda o laço aguardar um novo em vez de desenhar uma tela velha.
    /// </summary>
    public ImageBuffer? Latest()
    {
        lock (_gate)
        {
            if (_frames.Count == 0) return null;

            var newest = _frames.Last();
            return _now() - newest.Received <= MaxAge ? newest.Image : null;
        }
    }

    /// <summary>
    /// RF-096 — Aguarda um quadro utilizável, tentando de novo a cada P-20.
    ///
    /// Devolve nulo quando a parada foi pedida ou o prazo acabou. O prazo existe porque
    /// RF-097 manda o laço encerrar-se quando a janela deixou de existir: sem prazo, uma
    /// janela fechada deixaria o laço girando aqui para sempre.
    /// </summary>
    public ImageBuffer? Wait(Func<bool> stopRequested, TimeSpan timeout)
    {
        var deadline = _now() + timeout;

        while (true)
        {
            if (stopRequested()) return null;

            var frame = Latest();
            if (frame is not null) return frame;

            if (_now() >= deadline) return null;

            Thread.Sleep(P.AttachedCaptureRetryMs);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _frames.Clear();
            _received = 0;
        }
    }
}
