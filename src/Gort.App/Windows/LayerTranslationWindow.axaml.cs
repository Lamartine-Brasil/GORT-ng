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

    // ── V.5 — menu de contexto (RF-545, RF-546) ─────────────────────────────

    /// <summary>
    /// RF-546 — De onde o menu lê o estado de "remover espaços", e para onde ele o devolve.
    ///
    /// A opção não é da janela: ela muda o TRATAMENTO TEXTUAL do ciclo (RF-186). A janela
    /// não guarda uma cópia — ela pergunta e avisa —, senão o menu passaria a discordar da
    /// aba de texto assim que qualquer um dos dois mudasse.
    /// </summary>
    public Func<bool>? ReadRemoveSpaces { get; set; }
    public Action<bool>? RemoveSpacesChanged { get; set; }

    /// <summary>Textos do menu, vindos da tabela de localização (RF-481).</summary>
    public Func<string, string>? Text { get; set; }

    private MenuItem? _orderDefault, _orderCenter, _removeSpaces, _forcedTransparency;

    /// <summary>
    /// V.5 / RF-545 — Menu de contexto: ordenação, remover espaços, transparência forçada,
    /// fechar.
    ///
    /// RF-546 — as marcações refletem o estado ATUAL e as alterações valem imediatamente,
    /// sem exigir "aplicar". As duas exigências andam juntas: como o efeito é imediato, o
    /// estado tem de ser relido toda vez que o menu abre — outra parte do programa pode
    /// ter mudado a mesma opção desde a última abertura.
    /// </summary>
    private void BuildContextMenu()
    {
        _orderDefault = new MenuItem { ToggleType = MenuItemToggleType.Radio, GroupName = "ordem" };
        _orderCenter = new MenuItem { ToggleType = MenuItemToggleType.Radio, GroupName = "ordem" };
        _removeSpaces = new MenuItem { ToggleType = MenuItemToggleType.CheckBox };
        _forcedTransparency = new MenuItem { ToggleType = MenuItemToggleType.CheckBox };
        var close = new MenuItem();

        _orderDefault.Click += (_, _) => SetAlignment(TextAlignment.Left);
        _orderCenter.Click += (_, _) => SetAlignment(TextAlignment.Center);

        _removeSpaces.Click += (_, _) =>
            RemoveSpacesChanged?.Invoke(_removeSpaces.IsChecked);

        _forcedTransparency.Click += (_, _) =>
            ForcedTransparency = _forcedTransparency.IsChecked;

        close.Click += (_, _) => Hide();

        var menu = new ContextMenu
        {
            ItemsSource = new[]
            {
                _orderDefault, _orderCenter, _removeSpaces, _forcedTransparency, close,
            },
        };

        // RF-546 — o estado é relido a cada abertura, nunca guardado em cópia.
        menu.Opening += (_, _) => RefreshContextMenu();

        ContextMenu = menu;
        ApplyMenuText(close);
    }

    private void ApplyMenuText(MenuItem close)
    {
        string Localized(string key) => Text?.Invoke(key) ?? key;

        _orderDefault!.Header = Localized("layer.menu.order_default");
        _orderCenter!.Header = Localized("layer.menu.order_center");
        _removeSpaces!.Header = Localized("layer.menu.remove_spaces");
        _forcedTransparency!.Header = Localized("layer.menu.forced_transparency");
        close.Header = Localized("layer.menu.close");
    }

    /// <summary>RF-546 — As marcações refletem o estado atual.</summary>
    private void RefreshContextMenu()
    {
        if (_orderDefault is null) return;

        bool centered = Surface.TextHorizontalAlignment == TextAlignment.Center;
        _orderDefault.IsChecked = !centered;
        _orderCenter.IsChecked = centered;

        _removeSpaces!.IsChecked = ReadRemoveSpaces?.Invoke() ?? false;
        _forcedTransparency!.IsChecked = ForcedTransparency;
    }

    /// <summary>RF-545 — A alteração vale imediatamente: a janela repinta na hora.</summary>
    private void SetAlignment(TextAlignment alignment)
    {
        Surface.TextHorizontalAlignment = alignment;
        Surface.InvalidateVisual();
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
