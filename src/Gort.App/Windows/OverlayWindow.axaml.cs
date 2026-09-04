using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Gort.Core.Calibration;
using Gort.Core.Rendering;
using Gort.Platform;

using GortRect = Gort.Core.Model.Rect;

namespace Gort.App.Windows;

/// <summary>
/// 19.4 — Janela de MODO SOBREPOSIÇÃO.
///
/// RF-344 — Sem bordas, com transparência por pixel, cobrindo a união de todos os monitores.
/// RF-345 — Sempre no topo INCONDICIONALMENTE, e fora da barra de tarefas: ela vive sobre o
/// jogo e não é uma janela que o usuário gerencia.
/// </summary>
public partial class OverlayWindow : Window, ITranslationWindow
{
    private readonly IWindowEffects _effects;

    /// <summary>
    /// RF-381 — O desenho é protegido por um bloqueio de REENTRÂNCIA, liberado em qualquer
    /// caminho de saída, inclusive por exceção.
    /// </summary>
    private int _drawing;

    /// <summary>RF-350 — Retângulo acumulado enquanto a tradução roda. Zerado ao parar.</summary>
    private GortRect? _accumulated;

    /// <summary>
    /// RF-347 — Enquanto a janela está capturável por causa do atalho de captura do sistema,
    /// as atualizações de desenho ficam SUSPENSAS.
    /// </summary>
    private bool _drawingSuspended;

    /// <summary>RF-385 — Contador de tarefa, para cancelar o retorno de um ciclo pontual.</summary>
    private int _task;

    public OverlayWindow() : this(new NoWindowEffects()) { }

    public OverlayWindow(IWindowEffects effects)
    {
        _effects = effects;
        InitializeComponent();

        Opened += (_, _) => ApplyCaptureExclusion();
    }

    /// <summary>Blocos do ciclo atual, já em coordenadas da janela.</summary>
    public IReadOnlyList<OverlayBlock> Blocks => Surface.Blocks;

    public OverlaySurface Canvas => Surface;

    /// <summary>
    /// RF-348 — Com a captura de janela anexada ativa, a sobreposição PODE ser capturável:
    /// a fonte de imagem não é a tela e não há risco de realimentação.
    /// </summary>
    public bool AttachedWindowCapture { get; set; }

    /// <summary>
    /// RF-344 / RF-349 / RF-350 — Reposiciona a janela para cobrir as áreas, com a folga de
    /// P-92 e o acúmulo que impede o encolhimento no meio da tradução.
    /// </summary>
    public void FitTo(IEnumerable<GortRect> areas)
    {
        var target = OverlayGeometry.WindowRect(areas);
        if (target.IsEmpty) return;

        _accumulated = OverlayGeometry.Accumulate(_accumulated, target);
        var rect = _accumulated.Value;

        Position = new PixelPoint(rect.X, rect.Y);
        Width = rect.Width;
        Height = rect.Height;
    }

    public void Show(string translated, string recognized) { /* a sobreposição desenha blocos */ }

    /// <summary>Entrega os blocos do ciclo e redesenha.</summary>
    public void SetBlocks(IReadOnlyList<OverlayBlock> blocks)
    {
        if (_drawingSuspended) return;

        // RF-382 — se a janela ainda não tem identificador de sistema e o pedido vem de
        // outra thread, o desenho é ABANDONADO, não adiado. 🔒
        //
        // Motivo, na letra do requisito: ler o identificador a partir de outra thread cria a
        // janela naquela thread, que não processa mensagens, e todo despacho subsequente
        // trava para sempre.
        if (!Dispatcher.UIThread.CheckAccess() && TryGetPlatformHandle() is null) return;

        // RF-381 — bloqueio de reentrância, liberado inclusive por exceção.
        if (Interlocked.CompareExchange(ref _drawing, 1, 0) != 0) return;

        try
        {
            Surface.SetBlocks(blocks);
        }
        finally
        {
            Interlocked.Exchange(ref _drawing, 0);
        }
    }

    public void Clear() => Surface.SetBlocks(Array.Empty<OverlayBlock>());

    public void Repaint() => Surface.InvalidateVisual();

    public void SetRunning(bool running)
    {
        Surface.Translating = running;

        // RF-350 — ao parar, o acúmulo é zerado.
        if (!running) _accumulated = null;

        // RF-334 análogo: durante a tradução a janela não pode interceptar cliques. Aqui
        // ela nunca pode: não há nada nela para clicar.
        var handle = TryGetPlatformHandle();
        if (handle is not null) _effects.SetClickThrough(handle.Handle, true);

        Surface.InvalidateVisual();
    }

    /// <summary>RF-345 — Sempre no topo INCONDICIONALMENTE; a opção do usuário não se aplica.</summary>
    public void SetAlwaysOnTop(bool value) => Topmost = true;

    /// <summary>
    /// RF-383 — Preparação para uma nova tradução: limpar os dados, zerar o retângulo
    /// acumulado, liberar os bloqueios e desenhar uma vez.
    ///
    /// A sincronização com o compositor que o requisito pede (para o primeiro quadro não
    /// piscar) depende de C9, que não existe uniformemente; RF-571 declara aceitável
    /// omiti-la e conviver com a cintilação inicial.
    /// </summary>
    public void PrepareForTranslation()
    {
        _accumulated = null;
        _drawingSuspended = false;
        Interlocked.Exchange(ref _drawing, 0);

        Surface.SetBlocks(Array.Empty<OverlayBlock>());
        Surface.InvalidateVisual();
    }

    /// <summary>
    /// RF-346 / RF-348 — A janela é excluída de capturas e gravações, exceto quando a fonte
    /// de imagem é uma janela anexada.
    ///
    /// RF-569 — No macOS a capacidade não existe: a sobreposição aparece em prints, e o
    /// programa documenta isso.
    /// </summary>
    private void ApplyCaptureExclusion()
    {
        var handle = TryGetPlatformHandle();
        if (handle is null) return;

        _effects.SetExcludedFromCapture(handle.Handle, !AttachedWindowCapture);
        _effects.SetClickThrough(handle.Handle, true);
    }

    /// <summary>
    /// RF-347 — Quando o usuário aciona o atalho de captura de tela do sistema, a janela
    /// torna-se capturável IMEDIATAMENTE e volta a ser excluída após P-91. Durante esse
    /// intervalo, as atualizações de desenho ficam suspensas. 🔒
    ///
    /// RF-569 — Onde C11 não é detectável, isto nunca é chamado, e RF-347 fica inócuo.
    /// </summary>
    public void AllowCaptureBriefly()
    {
        var handle = TryGetPlatformHandle();
        if (handle is null) return;

        _effects.SetExcludedFromCapture(handle.Handle, false);
        _drawingSuspended = true;

        int task = Interlocked.Increment(ref _task);

        DispatcherTimer.RunOnce(() =>
        {
            // RF-385 — cancelado se outra tarefa começou nesse intervalo.
            if (task != Volatile.Read(ref _task)) return;

            _drawingSuspended = false;
            ApplyCaptureExclusion();
        }, P.ScreenshotCapturableWindow);
    }

    /// <summary>
    /// RF-384 / RF-385 — Após um ciclo pontual, a sobreposição permanece visível pelo tempo
    /// configurado e depois volta ao estado normal. O retorno é CANCELADO se uma nova
    /// tradução começar nesse intervalo, comparando um contador de tarefa.
    /// </summary>
    public void HoldAfterOneShot(int seconds, Action onReturn)
    {
        int task = Interlocked.Increment(ref _task);

        if (seconds <= 0)
        {
            onReturn();
            return;
        }

        DispatcherTimer.RunOnce(() =>
        {
            if (task != Volatile.Read(ref _task)) return;
            onReturn();
        }, TimeSpan.FromSeconds(seconds));
    }

    /// <summary>RF-385 — Uma nova tradução invalida qualquer retorno pendente.</summary>
    public void CancelPendingReturn() => Interlocked.Increment(ref _task);
}
