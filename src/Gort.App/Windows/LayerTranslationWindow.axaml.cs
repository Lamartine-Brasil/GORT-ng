using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Gort.Core.Calibration;
using Gort.Core.Rendering;
using Gort.Core.Structuring;
using Gort.Core.Ui;
using Gort.Platform;

using GortRect = Gort.Core.Model.Rect;
using TextVerticalAlignment = Gort.Core.Rendering.VerticalAlignment;

namespace Gort.App.Windows;

/// <summary>
/// 19.3 — Janela de tradução em MODO CAMADA.
///
/// Uma janela transparente e sem bordas que o usuário posiciona onde quiser. Enquanto
/// traduz, fica invisível exceto pelo texto, com contorno duplo para legibilidade, e deixa
/// os cliques passarem através dela (RF-332 a RF-335).
/// </summary>
public partial class LayerTranslationWindow : Window, ITranslationWindow
{
    private readonly IWindowEffects _effects;
    private readonly TemporaryNotice _notice = new();

    private ResizeEdge _edge = ResizeEdge.None;
    private PixelPoint _dragOrigin;
    private GortRect _originalBounds;
    private bool _translating;

    public LayerTranslationWindow() : this(new NoWindowEffects()) { }

    public LayerTranslationWindow(IWindowEffects effects)
    {
        _effects = effects;
        InitializeComponent();

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;

        BuildContextMenu();
        ApplyTransparency();
    }

    /// <summary>RF-326 — Fechar apenas oculta; a janela volta pelo controle remoto.</summary>
    public bool HideInsteadOfClose { get; set; } = true;

    /// <summary>RF-497 — Exibir o texto reconhecido junto da tradução.</summary>
    public bool ShowRecognizedText { get; set; }

    /// <summary>
    /// RF-335 — Transparência forçada: a janela permanece transparente e atravessável mesmo
    /// depois que a tradução para.
    /// </summary>
    public bool ForcedTransparency
    {
        get => Surface.ForcedTransparency;
        set { Surface.ForcedTransparency = value; ApplyTransparency(); }
    }

    public void Show(string translated, string recognized)
    {
        string text = TextPostProcessor.ComposeDarkModeText(translated, recognized, ShowRecognizedText);

        // RF-342 — o aviso temporário é prefixado ao texto e some sozinho no prazo.
        Surface.SetText(TextPostProcessor.NormalizeNewlines(_notice.Apply(text)));
    }

    public void Clear() => Surface.SetText("");

    /// <summary>RF-196 / RF-197 — Repintar sem recalcular nada.</summary>
    public void Repaint() => Surface.InvalidateVisual();

    public void SetRunning(bool running)
    {
        _translating = running;
        Surface.Translating = running;
        ApplyTransparency();
        Surface.InvalidateVisual();
    }

    public void SetAlwaysOnTop(bool value) => Topmost = value;

    /// <summary>
    /// RF-343 — Aviso de que a janela está sobre uma área de OCR e seria traduzida a si
    /// mesma. Dura P-90.
    /// </summary>
    public void WarnAboutSelfCapture(string message)
        => _notice.Show(message, P.WindowOverlapWarningDuration);

    /// <summary>Configura fonte, cores e alinhamento a partir do perfil (RF-325).</summary>
    public void Configure(string fontFamily, double fontSize,
                          Gort.Core.Model.Rgba text, Gort.Core.Model.Rgba stroke1,
                          Gort.Core.Model.Rgba stroke2, Gort.Core.Model.Rgba background,
                          bool useStroke, bool useBackground,
                          TextAlignment horizontal, TextVerticalAlignment vertical)
    {
        Surface.FontFamilyName = fontFamily;
        Surface.FontSizePoints = fontSize;
        Surface.TextColor = text;
        Surface.Stroke1Color = stroke1;
        Surface.Stroke2Color = stroke2;
        Surface.BackgroundColor = background;
        Surface.UseStroke = useStroke;
        Surface.UseTextBackground = useBackground;
        Surface.TextHorizontalAlignment = horizontal;
        Surface.VerticalTextAlignment = vertical;
        Surface.InvalidateVisual();
    }

    /// <summary>
    /// RF-334 / RF-335 — Alinha a transparência e a passagem de cliques ao estado atual.
    ///
    /// Os dois andam juntos de propósito: uma janela invisível que ainda captura cliques é
    /// pior que uma janela visível — o usuário clicaria no jogo e nada aconteceria.
    /// </summary>
    private void ApplyTransparency()
    {
        bool clickThrough = LayerLayout.ClickThrough(_translating, Surface.ForcedTransparency);

        var handle = TryGetPlatformHandle();
        if (handle is not null) _effects.SetClickThrough(handle.Handle, clickThrough);

        Surface.InvalidateVisual();
    }

    /// <summary>
    /// V.5 / RF-545 — Menu de contexto: ordenação, remover espaços, transparência forçada,
    /// fechar. RF-546 — as marcações refletem o estado e valem imediatamente, sem "aplicar".
    /// </summary>
    private void BuildContextMenu()
    {
        var alignLeft = new MenuItem { Header = "Ordenação: padrão" };
        var alignCenter = new MenuItem { Header = "Ordenação: centralizado" };
        var forced = new MenuItem { Header = "Transparência forçada" };
        var close = new MenuItem { Header = "Fechar" };

        alignLeft.Click += (_, _) =>
        {
            Surface.TextHorizontalAlignment = TextAlignment.Left;
            Surface.InvalidateVisual();
        };
        alignCenter.Click += (_, _) =>
        {
            Surface.TextHorizontalAlignment = TextAlignment.Center;
            Surface.InvalidateVisual();
        };
        forced.Click += (_, _) => ForcedTransparency = !ForcedTransparency;
        close.Click += (_, _) => Hide();

        ContextMenu = new ContextMenu
        {
            ItemsSource = new[] { alignLeft, alignCenter, forced, close },
        };
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (HideInsteadOfClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    // ── RF-339: mover e redimensionar ────────────────────────────────────────

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var p = e.GetPosition(this);
        _edge = RemoteControlGeometry.EdgeAt((int)p.X, (int)p.Y,
                                             (int)Bounds.Width, (int)Bounds.Height,
                                             P.TranslationWindowResizeHotZone);

        if (_edge == ResizeEdge.None)
        {
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
        if (_edge == ResizeEdge.None) return;

        var screen = this.PointToScreen(e.GetPosition(this));
        int dx = screen.X - _dragOrigin.X;
        int dy = screen.Y - _dragOrigin.Y;

        // RF-339 — diferente do controle remoto, aqui NÃO há proporção fixa: a janela de
        // tradução tem de acompanhar a forma da caixa de diálogo do jogo.
        int width = _originalBounds.Width
            + (_edge.HasFlag(ResizeEdge.Right) ? dx : _edge.HasFlag(ResizeEdge.Left) ? -dx : 0);
        int height = _originalBounds.Height
            + (_edge.HasFlag(ResizeEdge.Bottom) ? dy : _edge.HasFlag(ResizeEdge.Top) ? -dy : 0);

        width = Math.Max(P.LayerMinWidth, width);
        height = Math.Max(P.LayerMinHeight, height);

        int x = _edge.HasFlag(ResizeEdge.Left) ? _originalBounds.Right - width : _originalBounds.X;
        int y = _edge.HasFlag(ResizeEdge.Top) ? _originalBounds.Bottom - height : _originalBounds.Y;

        Position = new PixelPoint(x, y);
        Width = width;
        Height = height;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _edge = ResizeEdge.None;
        e.Pointer.Capture(null);
    }
}
