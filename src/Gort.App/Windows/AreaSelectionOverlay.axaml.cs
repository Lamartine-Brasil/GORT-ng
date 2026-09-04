using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Gort.Core.Regions;

// `Rect` existe nos dois mundos: o do Avalonia, em pontos de layout, e o do programa, em
// pixels de tela. O apelido explícito evita que a distinção dependa da ordem dos `using`.
using GortRect = Gort.Core.Model.Rect;
using Rgba = Gort.Core.Model.Rgba;

namespace Gort.App.Windows;

/// <summary>
/// RF-047 a RF-053 — Camada de seleção de área.
///
/// Cobre TODA a área de trabalho virtual, semitransparente, e deixa o usuário desenhar um
/// retângulo arrastando com o botão esquerdo.
///
/// RF-053 — Enquanto ela está aberta, todos os atalhos globais do programa ficam inertes:
/// o usuário está usando o teclado e o mouse para marcar a área, e um atalho disparado no
/// meio disso seria imprevisível.
/// </summary>
public partial class AreaSelectionOverlay : Window
{
    private Point _origin;
    private bool _dragging;

    private readonly TaskCompletionSource<GortRect?> _completion = new();

    /// <summary>Deslocamento da janela em relação à origem da área de trabalho virtual.</summary>
    private readonly GortRect _desktop;

    public AreaSelectionOverlay() : this(new GortRect(0, 0, 800, 600), Rgba.Black, Rgba.White) { }

    public AreaSelectionOverlay(GortRect virtualDesktop, Rgba highlight, Rgba background)
    {
        InitializeComponent();

        _desktop = virtualDesktop;

        Position = new PixelPoint(virtualDesktop.X, virtualDesktop.Y);
        Width = virtualDesktop.Width;
        Height = virtualDesktop.Height;

        // RF-048 — o interior do retângulo em construção é pintado com a cor de destaque
        // configurada, e a borda em verde escuro de 2 px.
        Selection.Fill = new SolidColorBrush(
            Color.FromArgb(90, highlight.R, highlight.G, highlight.B));
        Selection.Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0x64, 0x00));

        // RF-050 / P-10 — a opacidade vem do canal alfa da cor de fundo escolhida, saturada
        // num mínimo. Sem o piso, uma cor totalmente transparente deixaria a camada
        // invisível e o usuário não saberia que está no modo de seleção.
        Opacity = Math.Clamp(
            FrameGeometry.SelectionOverlayOpacity(background.A) * 6.0, 0.08, 0.85);

        Surface.Background = new SolidColorBrush(
            Color.FromArgb(255, background.R, background.G, background.B));

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        KeyDown += OnKeyDown;
    }

    /// <summary>
    /// Abre a camada e devolve o retângulo desenhado em coordenadas ABSOLUTAS de tela, ou
    /// nulo quando o usuário cancelou ou o arraste foi descartado como clique acidental.
    /// </summary>
    public Task<GortRect?> SelectAsync(Window? owner = null)
    {
        if (owner is not null) Show(owner);
        else Show();

        Activate();
        return _completion.Task;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;

        // RF-051 — o botão direito cancela sem criar área.
        if (properties.IsRightButtonPressed)
        {
            Finish(null);
            return;
        }

        if (!properties.IsLeftButtonPressed) return;

        _origin = e.GetPosition(this);
        _dragging = true;

        Selection.IsVisible = true;
        Hint.IsVisible = false;
        Update(_origin);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        // RF-048 — o retângulo em construção é mostrado EM TEMPO REAL.
        if (_dragging) Update(e.GetPosition(this));
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;

        var drawn = RectFrom(_origin, e.GetPosition(this));

        // RF-052 / P-145 — um retângulo de até 4 px em qualquer dimensão é tratado como
        // clique acidental e descartado sem criar área.
        if (FrameGeometry.IsAccidentalClick(drawn))
        {
            Finish(null);
            return;
        }

        // De coordenadas da janela para coordenadas absolutas de tela.
        Finish(drawn.Offset(_desktop.X, _desktop.Y));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Finish(null);
    }

    private void Update(Point current)
    {
        var r = RectFrom(_origin, current);
        Canvas.SetLeft(Selection, r.X);
        Canvas.SetTop(Selection, r.Y);
        Selection.Width = r.Width;
        Selection.Height = r.Height;
    }

    /// <summary>Retângulo normalizado: o arraste pode ir em qualquer direção.</summary>
    private static GortRect RectFrom(Point a, Point b)
        => GortRect.FromBounds(
            (int)Math.Round(Math.Min(a.X, b.X)), (int)Math.Round(Math.Min(a.Y, b.Y)),
            (int)Math.Round(Math.Max(a.X, b.X)), (int)Math.Round(Math.Max(a.Y, b.Y)));

    private void Finish(GortRect? result)
    {
        _completion.TrySetResult(result);
        Close();
    }
}
