using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Gort.Core.Ui;

using GortRect = Gort.Core.Model.Rect;

namespace Gort.App.Windows;

/// <summary>
/// V.2 / RF-517 a RF-522 — Controle remoto.
///
/// Janela pequena, sem bordas de sistema, sempre acessível, com os botões de ação mais
/// usados. É por ela que o programa é operado quando os atalhos globais não estão
/// disponíveis — o que RF-569 estabelece como a degradação aceitável no macOS sem permissão
/// de Acessibilidade.
/// </summary>
public partial class RemoteControlWindow : Window
{
    /// <summary>Zona sensível de borda para redimensionamento.</summary>
    private const int HotZone = 8;

    /// <summary>Largura de referência da qual a escala dos controles é derivada (RF-519).</summary>
    private readonly int _referenceWidth;

    private ResizeEdge _edge = ResizeEdge.None;
    private PixelPoint _dragOrigin;
    private GortRect _originalBounds;

    public RemoteControlWindow()
    {
        InitializeComponent();
        _referenceWidth = (int)Width;

        AreaButton.Click += (_, _) => DefineArea?.Invoke();
        SnapshotButton.Click += (_, _) => Snapshot?.Invoke();
        StartButton.Click += (_, _) => Start?.Invoke();
        StopButton.Click += (_, _) => Stop?.Invoke();
        SettingsButton.Click += (_, _) => OpenSettings?.Invoke();

        // RF-521 — o botão de minimizar OCULTA; ele nunca encerra o programa.
        MinimizeButton.Click += (_, _) => Hide();

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    public Action? DefineArea { get; set; }
    public Action? Snapshot { get; set; }
    public Action? Start { get; set; }
    public Action? Stop { get; set; }
    public Action? OpenSettings { get; set; }

    /// <summary>
    /// RF-517 — Os botões de iniciar e parar ocupam o MESMO LUGAR, alternando visibilidade.
    /// </summary>
    public void SetRunning(bool running)
    {
        StartButton.IsVisible = !running;
        StopButton.IsVisible = running;
    }

    /// <summary>RF-520 — "Sempre no topo", configurável nas opções avançadas.</summary>
    public void SetAlwaysOnTop(bool value) => Topmost = value;

    /// <summary>RF-521 — Fechar a janela apenas a minimiza, nunca encerra o programa.</summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    /// <summary>Suspende RF-521 quando o programa está de fato encerrando (RF-016).</summary>
    public bool AllowClose { get; set; }

    // ── RF-518: mover e redimensionar ────────────────────────────────────────

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var p = e.GetPosition(this);
        _edge = RemoteControlGeometry.EdgeAt((int)p.X, (int)p.Y,
                                             (int)Bounds.Width, (int)Bounds.Height, HotZone);

        if (_edge == ResizeEdge.None)
        {
            // RF-518 — movível por arraste em QUALQUER ponto.
            BeginMoveDrag(e);
            return;
        }

        _dragOrigin = this.PointToScreen(p);
        _originalBounds = new GortRect(Position.X, Position.Y,
                                       (int)Bounds.Width, (int)Bounds.Height);
        e.Pointer.Capture(this);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var p = e.GetPosition(this);

        if (_edge == ResizeEdge.None)
        {
            // O cursor indica a direção antes do gesto começar.
            var hover = RemoteControlGeometry.EdgeAt((int)p.X, (int)p.Y,
                                                     (int)Bounds.Width, (int)Bounds.Height, HotZone);
            Cursor = new Cursor(CursorFor(hover));
            return;
        }

        var screen = this.PointToScreen(p);
        int dx = screen.X - _dragOrigin.X;
        int dy = screen.Y - _dragOrigin.Y;

        // RF-518 — a proporção original é mantida; RF-519 — o conteúdo escala junto, o que
        // só faz sentido porque a proporção é fixa.
        var result = RemoteControlGeometry.Resize(_originalBounds, _edge, dx, dy);

        Position = new PixelPoint(result.X, result.Y);
        Width = result.Width;
        Height = result.Height;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _edge = ResizeEdge.None;
        e.Pointer.Capture(null);
    }

    private static StandardCursorType CursorFor(ResizeEdge edge) => edge switch
    {
        ResizeEdge.Left or ResizeEdge.Right => StandardCursorType.SizeWestEast,
        ResizeEdge.Top or ResizeEdge.Bottom => StandardCursorType.SizeNorthSouth,
        ResizeEdge.TopLeft or ResizeEdge.BottomRight => StandardCursorType.TopLeftCorner,
        ResizeEdge.TopRight or ResizeEdge.BottomLeft => StandardCursorType.TopRightCorner,
        _ => StandardCursorType.Arrow,
    };
}
