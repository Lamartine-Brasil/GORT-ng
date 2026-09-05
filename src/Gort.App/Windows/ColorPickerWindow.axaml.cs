using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Gort.Core.Calibration;
using Gort.Core.Imaging;
using Gort.Core.Localization;
using Gort.Core.Model;

namespace Gort.App.Windows;

/// <summary>
/// RF-535 / RF-536 — Conta-gotas e pré-visualização binarizada, na mesma janela.
///
/// As duas coisas são o mesmo trabalho visto de dois lados: o conta-gotas diz QUAL cor o
/// texto tem, e a pré-visualização mostra o que o filtro faz com essa escolha. Separá-las em
/// duas janelas obrigaria a alternar entre elas a cada ajuste.
///
/// A janela é ÚNICA (RF-535) e, enquanto está aberta, as molduras deixam de ser "sempre no
/// topo" — senão elas ficariam por cima justamente da imagem que se está examinando.
/// </summary>
public partial class ColorPickerWindow : Window
{
    private readonly Localizer _loc;
    private readonly Func<ImageBuffer?> _capture;

    private ImageBuffer? _original;
    private ImageBuffer? _shown;
    private WriteableBitmap? _bitmap;
    private bool _loading;

    /// <summary>O grupo de cor que a janela edita — o mesmo objeto do perfil.</summary>
    private readonly ColorGroup _group;
    private readonly FilterSettings _settings;

    public ColorPickerWindow() : this(new Localizer(), new ColorGroup(), new FilterSettings(),
                                      () => null) { }

    public ColorPickerWindow(Localizer loc, ColorGroup group, FilterSettings settings,
                             Func<ImageBuffer?> capture)
    {
        InitializeComponent();

        _loc = loc;
        _group = group;
        _settings = settings;
        _capture = capture;

        ApplyTexts();
        WireEvents();
        LoadFromSettings();
        Process();
    }

    /// <summary>Chamado quando o usuário aceita os valores, para o perfil recolhê-los.</summary>
    public Action? Changed { get; set; }

    private void ApplyTexts()
    {
        Title = _loc["picker.title"];
        TitleText.Text = _loc["picker.title"];
        PickerLabel.Text = _loc["picker.color"];
        ZoomLabel.Text = _loc["picker.zoom"];
        ModeLabel.Text = _loc["picker.mode"];
        RgbHelp.Text = _loc["picker.rgb_help"];
        HsvHelp.Text = _loc["picker.hsv_help"];
        SLabel.Text = "S";
        VLabel.Text = "V";
        ThresholdLabel.Text = _loc["picker.threshold"];
        ErosionCheck.Content = _loc["picker.erosion"];
        TransformButton.Content = _loc["picker.transform"];
        RevertButton.Content = _loc["picker.revert"];
        ProcessButton.Content = _loc["picker.process"];

        ModeBox.ItemsSource = new[]
        {
            _loc["filter.none"], _loc["filter.rgb"], _loc["filter.hsv"], _loc["filter.threshold"],
        };
    }

    private void WireEvents()
    {
        Canvas.PointerMoved += (_, e) => ReadPixelUnder(e);
        Canvas.PointerPressed += (_, e) => { ReadPixelUnder(e); AdoptColorUnderCursor(); };

        ZoomSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name != nameof(Slider.Value)) return;
            ZoomValue.Text = $"{(int)ZoomSlider.Value}×";
            Redraw();
        };

        ModeBox.SelectionChanged += (_, _) => OnModeChanged();

        // RF-536 — deslizante e campo numérico SINCRONIZADOS, e alterar o limiar
        // REPROCESSA automaticamente: o valor certo se acha arrastando e olhando, e um
        // botão no meio do caminho tornaria isso um exercício de cliques.
        ThresholdSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name != nameof(Slider.Value) || _loading) return;
            _loading = true;
            ThresholdBox.Value = (decimal)ThresholdSlider.Value;
            _loading = false;
            OnThresholdChanged();
        };
        ThresholdBox.ValueChanged += (_, _) =>
        {
            if (_loading) return;
            _loading = true;
            ThresholdSlider.Value = (double)(ThresholdBox.Value ?? 0);
            _loading = false;
            OnThresholdChanged();
        };

        foreach (var box in new[] { GroupR, GroupG, GroupB, GroupS1, GroupS2, GroupV1, GroupV2 })
            box.ValueChanged += (_, _) => OnGroupChanged();

        ErosionCheck.IsCheckedChanged += (_, _) =>
        {
            if (_loading) return;
            _settings.Erosion = ErosionCheck.IsChecked == true;
            Changed?.Invoke();
            if (_shown is not null && !ReferenceEquals(_shown, _original)) Transform();
        };

        TransformButton.Click += (_, _) => Transform();
        RevertButton.Click += (_, _) => Revert();
        ProcessButton.Click += (_, _) => Process();
    }

    private void LoadFromSettings()
    {
        _loading = true;

        ModeBox.SelectedIndex = (int)_settings.Mode;
        ThresholdSlider.Value = _settings.Threshold;
        ThresholdBox.Value = _settings.Threshold;
        ErosionCheck.IsChecked = _settings.Erosion;

        GroupR.Value = _group.R; GroupG.Value = _group.G; GroupB.Value = _group.B;
        GroupS1.Value = _group.S1; GroupS2.Value = _group.S2;
        GroupV1.Value = _group.V1; GroupV2.Value = _group.V2;

        ZoomSlider.Value = 1;
        ZoomValue.Text = "1×";

        _loading = false;
        RefreshPanels();
    }

    /// <summary>RF-536 — Um painel de parâmetros POR MODO; os outros somem.</summary>
    private void RefreshPanels()
    {
        var mode = (FilterMode)Math.Max(0, ModeBox.SelectedIndex);
        RgbPanel.IsVisible = mode == FilterMode.Rgb;
        HsvPanel.IsVisible = mode == FilterMode.Hsv;
        ThresholdPanel.IsVisible = mode == FilterMode.Threshold;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RF-535 — O conta-gotas
    // ─────────────────────────────────────────────────────────────────────────

    private int _lastX = -1, _lastY = -1;

    /// <summary>
    /// RF-535 — Lê o pixel sob o cursor e mostra R, G, B, H, S, V e a amostra.
    ///
    /// A leitura é feita na imagem ORIGINAL, não na ampliada nem na binarizada: o que
    /// interessa é a cor que o texto tem na tela, e é ela que vai para o grupo de cor.
    /// </summary>
    private void ReadPixelUnder(PointerEventArgs e)
    {
        if (_original is null) return;

        var point = e.GetPosition(Canvas);
        int zoom = Math.Max(1, (int)ZoomSlider.Value);
        int x = (int)(point.X / zoom);
        int y = (int)(point.Y / zoom);

        if (x < 0 || y < 0 || x >= _original.Width || y >= _original.Height) return;
        if (x == _lastX && y == _lastY) return;

        _lastX = x; _lastY = y;

        var color = PixelAt(_original, x, y);
        var (h, sat, val) = ColorMath.ToHsvFilter(color.R, color.G, color.B);

        RValue.Text = color.R.ToString();
        GValue.Text = color.G.ToString();
        BValue.Text = color.B.ToString();
        HValue.Text = h.ToString();
        SValue.Text = sat.ToString();
        VValue.Text = val.ToString();

        ColorSample.Background = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.FromRgb(color.R, color.G, color.B));
    }

    /// <summary>
    /// Clicar ADOTA a cor: os três componentes do grupo passam a ser os do pixel clicado.
    ///
    /// É o gesto que o conta-gotas existe para oferecer — ler o valor e depois digitá-lo à
    /// mão em três campos seria transformar uma escolha visual em transcrição.
    /// </summary>
    private void AdoptColorUnderCursor()
    {
        if (_original is null || _lastX < 0) return;

        var color = PixelAt(_original, _lastX, _lastY);

        _loading = true;
        GroupR.Value = color.R;
        GroupG.Value = color.G;
        GroupB.Value = color.B;
        _loading = false;

        OnGroupChanged();
    }

    private static Rgba PixelAt(ImageBuffer image, int x, int y)
    {
        int stride = image.Width * (int)image.Format;
        int offset = y * stride + x * (int)image.Format;

        return image.Format == Gort.Core.Model.PixelFormat.Gray8
            ? new Rgba(image.Pixels[offset], image.Pixels[offset], image.Pixels[offset])
            : new Rgba(image.Pixels[offset + 2], image.Pixels[offset + 1], image.Pixels[offset]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RF-536 — A pré-visualização
    // ─────────────────────────────────────────────────────────────────────────

    private void OnModeChanged()
    {
        RefreshPanels();
        if (_loading) return;

        _settings.Mode = (FilterMode)Math.Max(0, ModeBox.SelectedIndex);
        Changed?.Invoke();

        // Trocar de modo muda o critério inteiro; se já havia uma binarização na tela, ela
        // é refeita para não ficar mostrando o resultado do modo anterior.
        if (_shown is not null && !ReferenceEquals(_shown, _original)) Transform();
    }

    private void OnThresholdChanged()
    {
        _settings.Threshold = (int)ThresholdSlider.Value;
        Changed?.Invoke();

        // RF-536 — alterar o limiar REPROCESSA automaticamente.
        Transform();
    }

    private void OnGroupChanged()
    {
        if (_loading) return;

        _group.R = (int)(GroupR.Value ?? 0);
        _group.G = (int)(GroupG.Value ?? 0);
        _group.B = (int)(GroupB.Value ?? 0);
        _group.S1 = (int)(GroupS1.Value ?? 0);
        _group.S2 = (int)(GroupS2.Value ?? 0);
        _group.V1 = (int)(GroupV1.Value ?? 0);
        _group.V2 = (int)(GroupV2.Value ?? 0);

        // RF-042 / RF-043 — faixas invertidas são trocadas, componentes saturados.
        _group.Normalize();

        _loading = true;
        GroupS1.Value = _group.S1; GroupS2.Value = _group.S2;
        GroupV1.Value = _group.V1; GroupV2.Value = _group.V2;
        _loading = false;

        Changed?.Invoke();
        if (_shown is not null && !ReferenceEquals(_shown, _original)) Transform();
    }

    /// <summary>RF-535 — "Processar": recaptura a região e volta à imagem original.</summary>
    private void Process()
    {
        var captured = _capture();
        if (captured is null)
        {
            StatusText.Text = _loc["picker.no_area"];
            return;
        }

        _original = captured;
        _shown = captured;
        StatusText.Text = _loc.Format("picker.size", captured.Width, captured.Height);
        Redraw();
    }

    /// <summary>
    /// RF-536 — "Transformar": mostra a binarização, com o MESMO critério que o
    /// pré-processamento usaria (RF-081, RF-082). É a razão de a janela existir: ver o que o
    /// OCR vai receber, e não o que a tela mostra.
    /// </summary>
    private void Transform()
    {
        if (_original is null) return;

        var settings = new FilterSettings
        {
            Mode = _settings.Mode,
            Threshold = _settings.Threshold,
            Erosion = _settings.Erosion,
            Groups = new List<ColorGroup> { _group.Clone() },
        };

        _shown = Preprocessor.Preview(_original, settings);
        Redraw();
    }

    /// <summary>RF-536 — "Reverter": volta à imagem capturada, sem recapturar.</summary>
    private void Revert()
    {
        _shown = _original;
        Redraw();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Desenho
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RF-535 — A imagem é AMPLIÁVEL, por repetição de pixel e não por interpolação: quem
    /// está escolhendo a cor de um pixel precisa ver aquele pixel, e não uma média dele com
    /// os vizinhos.
    /// </summary>
    private void Redraw()
    {
        if (_shown is null) { Canvas.Source = null; return; }

        int zoom = Math.Max(1, (int)ZoomSlider.Value);
        int width = _shown.Width * zoom;
        int height = _shown.Height * zoom;

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height), new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888, AlphaFormat.Opaque);

        using (var buffer = bitmap.Lock())
        {
            unsafe
            {
                byte* target = (byte*)buffer.Address;
                for (int y = 0; y < height; y++)
                {
                    byte* row = target + y * buffer.RowBytes;
                    int sourceY = y / zoom;
                    for (int x = 0; x < width; x++)
                    {
                        var color = PixelAt(_shown, x / zoom, sourceY);
                        row[x * 4 + 0] = color.B;
                        row[x * 4 + 1] = color.G;
                        row[x * 4 + 2] = color.R;
                        row[x * 4 + 3] = 255;
                    }
                }
            }
        }

        _bitmap?.Dispose();
        _bitmap = bitmap;
        Canvas.Source = bitmap;
        Canvas.Width = width;
        Canvas.Height = height;
    }

    protected override void OnClosed(EventArgs e)
    {
        _bitmap?.Dispose();
        base.OnClosed(e);
    }
}
