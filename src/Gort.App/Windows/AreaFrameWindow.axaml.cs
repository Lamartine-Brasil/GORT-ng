using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Gort.Core.Calibration;
using Gort.Core.Regions;
using GortRect = Gort.Core.Model.Rect;

namespace Gort.App.Windows;

/// <summary>
/// RF-054 a RF-060, RF-063 — A moldura de uma área.
///
/// Uma janela sem borda de sistema, sempre no topo, fora da barra de tarefas, composta de
/// uma barra de título e uma borda dupla DESENHADA. Nada disso é decoração: a borda dupla é
/// o que torna a moldura visível sobre qualquer fundo, claro ou escuro, e a barra de título
/// é o único ponto por onde ela pode ser agarrada sem redimensionar.
///
/// O interior é vazado — o usuário precisa ver o que está sendo capturado —, mas não
/// totalmente transparente: um fundo de alfa 1 mantém a janela clicável, porque um pixel
/// completamente transparente deixa o clique passar para a janela de baixo.
/// </summary>
public partial class AreaFrameWindow : Window
{
    private readonly CaptureFrame _frame;
    private readonly Func<GortRect> _desktop;

    private FrameHandle _handle = FrameHandle.None;
    private PixelPoint _dragOrigin;
    private GortRect _dragStart;
    private DateTime _lastNotice = DateTime.MinValue;

    public AreaFrameWindow() : this(new CaptureFrame(new GortRect(0, 0, 300, 200)), 1,
                                    () => new GortRect(0, 0, 1920, 1080)) { }

    public AreaFrameWindow(CaptureFrame frame, int index, Func<GortRect> desktop)
    {
        InitializeComponent();

        _frame = frame;
        _desktop = desktop;
        _index = index;

        ApplyGeometry();
        Style();

        Surface.PointerMoved += OnPointerMoved;
        Surface.PointerPressed += OnPointerPressed;
        Surface.PointerReleased += OnPointerReleased;
    }

    public CaptureFrame Frame => _frame;

    /// <summary>RF-055 — O índice mostrado na barra de título; muda ao reindexar (RF-064).</summary>
    public int Index
    {
        get => _index;
        set { _index = value; RefreshTitle(); }
    }
    private int _index;

    /// <summary>
    /// RF-059 — Notificação para recalcular as áreas, no máximo uma a cada P-13.
    ///
    /// RF-060 — quem assina decide se está em condição de recalcular; a moldura só avisa.
    /// </summary>
    public Action? AreasChanged { get; set; }

    /// <summary>
    /// Nome do tipo da área, para a barra de título (RF-055). Vem da tabela de localização.
    ///
    /// Atribuir REDESENHA o título: as duas propriedades da barra costumam ser preenchidas
    /// depois do construtor, e sem isto o título ficaria com o valor que tinha na
    /// construção — foi o que aconteceu na primeira versão, e só a imagem denunciou.
    /// </summary>
    public string KindName
    {
        get => _kindName;
        set { _kindName = value; RefreshTitle(); }
    }
    private string _kindName = "";

    // ─────────────────────────────────────────────────────────────────────────
    // Aparência
    // ─────────────────────────────────────────────────────────────────────────

    private Gort.Core.Regions.FrameGeometry.FrameMetrics Metrics
        => FrameGeometry.MetricsFor(ScaleOfThisMonitor());

    private double ScaleOfThisMonitor() => RenderScaling <= 0 ? 1 : RenderScaling;

    /// <summary>
    /// RF-063 — A área de exclusão tem aparência DISTINTA: borda vermelha e opacidade
    /// reduzida a 70%. É o que evita o erro mais caro do gerenciamento de áreas — confundir
    /// o que soma com o que subtrai e ficar procurando por que o texto sumiu.
    /// </summary>
    private void Style()
    {
        bool exclusion = _frame.Kind == AreaKind.Exclusion;
        var m = Metrics;

        var accent = exclusion
            ? new SolidColorBrush(Color.FromRgb(200, 40, 40))
            : new SolidColorBrush(Color.FromRgb(30, 120, 30));

        OuterBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(20, 20, 20));
        OuterBorder.BorderThickness = new Thickness(m.OuterBorder);

        InnerBorder.BorderBrush = accent;
        InnerBorder.BorderThickness = new Thickness(m.Border);

        TitleBar.Background = accent;
        TitleBar.Height = m.TitleBar;

        Opacity = exclusion ? 0.7 : 1.0;
    }

    /// <summary>Reposiciona os três desenhos quando o tamanho muda.</summary>
    private void ApplyGeometry()
    {
        var rect = _frame.FrameRect;

        Position = new PixelPoint(rect.X, rect.Y);
        Width = rect.Width;
        Height = rect.Height;

        var m = Metrics;

        OuterBorder.Width = rect.Width;
        OuterBorder.Height = rect.Height;

        Canvas.SetLeft(InnerBorder, m.OuterBorder);
        Canvas.SetTop(InnerBorder, m.OuterBorder);
        InnerBorder.Width = Math.Max(0, rect.Width - 2 * m.OuterBorder);
        InnerBorder.Height = Math.Max(0, rect.Height - 2 * m.OuterBorder);

        Canvas.SetLeft(TitleBar, m.OuterBorder + m.Border);
        Canvas.SetTop(TitleBar, m.OuterBorder + m.Border);
        TitleBar.Width = Math.Max(0, rect.Width - 2 * (m.OuterBorder + m.Border));

        RefreshTitle();
    }

    /// <summary>
    /// RF-055 — Tipo, índice, tamanho em pixels e posição, atualizados EM TEMPO REAL durante
    /// o arraste. É a única forma de ajustar uma área a um retângulo conhecido sem
    /// tentativa e erro.
    /// </summary>
    private void RefreshTitle()
    {
        var r = _frame.FrameRect;
        TitleText.Text = $"{KindName} {Index}   {r.Width}×{r.Height}   ({r.X}, {r.Y})";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RF-056 — Mover e redimensionar
    // ─────────────────────────────────────────────────────────────────────────

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(Surface);

        if (_handle == FrameHandle.None)
        {
            // RF-056 — o cursor indica a direção antes do arraste começar.
            Cursor = CursorFor(HandleUnder(point));
            return;
        }

        var screen = ToScreen(e);
        var moved = FrameResize.Apply(
            _dragStart, _handle,
            screen.X - _dragOrigin.X, screen.Y - _dragOrigin.Y);

        _frame.FrameRect = moved;
        ApplyGeometry();
        NotifyThrottled();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Surface).Properties.IsLeftButtonPressed) return;

        _handle = HandleUnder(e.GetPosition(Surface));
        if (_handle == FrameHandle.None) return;

        _dragOrigin = ToScreen(e);
        _dragStart = _frame.FrameRect;
        e.Pointer.Capture(Surface);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_handle == FrameHandle.None) return;

        _handle = FrameHandle.None;
        e.Pointer.Capture(null);

        // RF-058 — ao SOLTAR, a moldura volta para dentro se saiu pela esquerda ou pelo topo.
        _frame.FrameRect = FrameResize.BringBack(_frame.FrameRect, _desktop());
        ApplyGeometry();

        // O fim do arraste sempre notifica, mesmo que P-13 ainda não tenha passado: é o
        // estado final, e deixá-lo esperando o próximo tique deixaria a captura defasada.
        _lastNotice = DateTime.MinValue;
        NotifyThrottled();
    }

    private FrameHandle HandleUnder(Point point)
    {
        var m = Metrics;
        return FrameResize.HandleAt(
            _frame.FrameRect, (int)point.X, (int)point.Y,
            m.ResizeHotZone, m.OuterBorder + m.Border + m.TitleBar);
    }

    private PixelPoint ToScreen(PointerEventArgs e)
    {
        var local = e.GetPosition(Surface);
        return new PixelPoint(Position.X + (int)local.X, Position.Y + (int)local.Y);
    }

    /// <summary>RF-059 — No máximo uma notificação a cada P-13.</summary>
    private void NotifyThrottled()
    {
        var now = DateTime.UtcNow;
        if (now - _lastNotice < P.FrameDragRecalcInterval) return;

        _lastNotice = now;
        AreasChanged?.Invoke();
    }

    private static Cursor CursorFor(FrameHandle handle) => new(handle switch
    {
        FrameHandle.Left or FrameHandle.Right => StandardCursorType.SizeWestEast,
        FrameHandle.Top or FrameHandle.Bottom => StandardCursorType.SizeNorthSouth,
        FrameHandle.TopLeft or FrameHandle.BottomRight => StandardCursorType.TopLeftCorner,
        FrameHandle.TopRight or FrameHandle.BottomLeft => StandardCursorType.TopRightCorner,
        FrameHandle.Move => StandardCursorType.SizeAll,
        _ => StandardCursorType.Arrow,
    });
}
