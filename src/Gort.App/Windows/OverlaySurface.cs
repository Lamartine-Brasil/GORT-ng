using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Gort.Core.Calibration;
using Gort.Core.Model;
using Gort.Core.Rendering;

using GortRect = Gort.Core.Model.Rect;
using GortRgba = Gort.Core.Model.Rgba;

// `FontStyle` existe nos dois mundos: o do programa, que entra na chave do cache de
// medição (RF-374), e o do Avalonia, que descreve itálico. O apelido separa os dois.
using GortFontStyle = Gort.Core.Rendering.FontStyle;

namespace Gort.App.Windows;

/// <summary>Um bloco pronto para desenhar sobre o texto original.</summary>
public sealed class OverlayBlock
{
    public required string Text { get; init; }

    /// <summary>Retângulo de visualização, em coordenadas da janela de sobreposição.</summary>
    public required GortRect ViewRect { get; set; }

    public required bool IsTitle { get; init; }
    public required Orientation Orientation { get; init; }

    /// <summary>RF-360 — Tamanho mediano das linhas do bloco, em pixels da imagem.</summary>
    public double OwnMedianSize { get; init; }

    /// <summary>RF-413 — Cores da análise automática, quando ela rodou.</summary>
    public AutoColorResult? AutoColor { get; init; }

    // Preenchidos pelo layout, e gravados no retrato de depuração (RF-493).
    public double FinalFontSize { get; set; }
    public GortRect ContentRect { get; set; }
    public IReadOnlyList<string> Lines { get; set; } = Array.Empty<string>();
    public bool Clipped { get; set; }
}

/// <summary>
/// 19.4 — Superfície de desenho do MODO SOBREPOSIÇÃO.
///
/// Não há janela visível: a tradução de cada bloco é desenhada diretamente sobre o bloco
/// original, com o tamanho de fonte proporcional ao original e — se ativado — a cor do texto
/// e do fundo extraídas da própria imagem.
/// </summary>
public sealed class OverlaySurface : Control
{
    private readonly AvaloniaTextMeasurer _measurer = new();
    private readonly TextMeasurementCache _cache;

    public OverlaySurface() => _cache = new TextMeasurementCache(_measurer);

    public IReadOnlyList<OverlayBlock> Blocks { get; private set; } = Array.Empty<OverlayBlock>();
    public bool Translating { get; set; }

    public string FontFamilyName { get; set; } = "";
    public GortFontStyle FontStyle { get; set; } = GortFontStyle.Normal;

    public GortRgba TextColor { get; set; } = new(255, 255, 255);
    public GortRgba Stroke1Color { get; set; } = new(192, 192, 192);
    public GortRgba Stroke2Color { get; set; } = new(0, 0, 0);
    public GortRgba BackgroundColor { get; set; } = new(0, 0, 0, 170);

    /// <summary>RF-336 / RF-392 — Contorno de fonte na sobreposição.</summary>
    public bool FontStroke { get; set; }

    /// <summary>RF-377 — Pintar o fundo de cada bloco.</summary>
    public bool UseBackground { get; set; } = true;

    /// <summary>RF-378 — Usar a transparência do fundo, ou pintá-lo opaco.</summary>
    public bool UseBackgroundTransparency { get; set; }

    /// <summary>RF-360 — Tamanho automático de fonte.</summary>
    public bool AutoFontSize { get; set; }

    public double MinFontSize { get; set; } = P.AutoFontSizeMinDefault;
    public double MaxFontSize { get; set; } = P.AutoFontSizeMaxDefault;
    public double FixedFontSize { get; set; } = P.DefaultFontSize;

    /// <summary>RF-375 — Modo vertical só com "preservar a direção do original" ativa.</summary>
    public bool PreserveOrientation { get; set; }

    /// <summary>Ampliação e resolução, para converter pixels de imagem em pontos (RF-360).</summary>
    public double Scale { get; set; } = P.DefaultScale;
    public double VerticalDpi { get; set; } = P.ReferenceDpi;

    /// <summary>RF-491 — Modo de depuração "mostrar áreas de palavra".</summary>
    public bool ShowWordAreas { get; set; }

    /// <summary>RF-494 — Tempos e contagens do desenho, para o retrato de análise.</summary>
    public double LastLayoutMs { get; private set; }
    public double LastDrawMs { get; private set; }
    public int LastCacheHits { get; private set; }
    public int LastCacheMisses { get; private set; }

    public void SetBlocks(IReadOnlyList<OverlayBlock> blocks)
    {
        Blocks = blocks;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (Blocks.Count == 0) return;

        var watch = System.Diagnostics.Stopwatch.StartNew();

        // RF-355 — colisões resolvidas antes de qualquer medição: mover um bloco depois de
        // escolher o tamanho da fonte invalidaria a escolha.
        var items = Blocks
            .Select(b => new CollisionResolver.Item { Rect = b.ViewRect, IsTitle = b.IsTitle })
            .ToList();
        CollisionResolver.Resolve(items);
        for (int i = 0; i < Blocks.Count; i++) Blocks[i].ViewRect = items[i].Rect;

        // RF-360 passo 2 — o "tamanho do corpo" é a mediana dos blocos NÃO TÍTULO.
        double bodyMedian = Median(Blocks.Where(b => !b.IsTitle).Select(b => b.OwnMedianSize));

        // O bloco líder é o mais acima e à esquerda.
        var lead = Blocks
            .OrderBy(b => b.ViewRect.Top)
            .ThenBy(b => b.ViewRect.Left)
            .FirstOrDefault();

        foreach (var block in Blocks) LayOut(block, bodyMedian, ReferenceEquals(block, lead));

        watch.Stop();
        LastLayoutMs = watch.Elapsed.TotalMilliseconds;

        var drawWatch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var block in Blocks) Draw(context, block);
        drawWatch.Stop();

        LastDrawMs = drawWatch.Elapsed.TotalMilliseconds;
        LastCacheHits = _cache.Hits;
        LastCacheMisses = _cache.Misses;

        // RF-374 — o cache é descartado ao fim do desenho.
        _cache.Clear();
    }

    private void LayOut(OverlayBlock block, double bodyMedian, bool isLead)
    {
        // RF-359 — o retângulo de conteúdo desconta o espaço do contorno.
        block.ContentRect = OverlayGeometry.ContentRect(block.ViewRect, FontStroke);

        var content = RectD.From(block.ContentRect);
        var orientation = EffectiveOrientation(block);

        var font = new FontSpec(FontFamilyName, FixedFontSize, FontStyle)
            .WithoutBoldWhenNoStroke(FontStroke);   // RF-392

        double size;
        if (AutoFontSize)
        {
            double preferred = OverlayTextLayout.PreferredFontSize(
                block.OwnMedianSize, bodyMedian, block.IsTitle, isLead, Scale, VerticalDpi);
            preferred = OverlayTextLayout.Clamp(preferred, MinFontSize, MaxFontSize);

            size = OverlayTextLayout.FindFontSize(
                _cache, block.Text, font, orientation, content,
                MinFontSize, preferred, FontStroke);
        }
        else
        {
            size = FixedFontSize;
        }

        block.FinalFontSize = size;
        font = font with { Size = size };

        double available = orientation == Orientation.Vertical ? content.Height : content.Width;
        block.Lines = LineBreaker.Break(_cache, block.Text, font, orientation, available);

        // Caso de erro do cap. 19: texto que não cabe nem no tamanho mínimo é desenhado
        // assim mesmo e MARCADO como recortado no registro de depuração.
        block.Clipped = !OverlayTextLayout.Fits(_cache, block.Lines, font, orientation,
                                                content, FontStroke);
    }

    /// <summary>
    /// RF-375 — O modo vertical é usado apenas quando "preservar a direção do original" está
    /// ativa E o bloco foi classificado como vertical.
    /// </summary>
    private Orientation EffectiveOrientation(OverlayBlock block)
        => PreserveOrientation && block.Orientation == Orientation.Vertical
            ? Orientation.Vertical
            : Orientation.Horizontal;

    private void Draw(DrawingContext context, OverlayBlock block)
    {
        if (block.ContentRect.IsEmpty) return;

        var view = new Avalonia.Rect(block.ViewRect.X, block.ViewRect.Y,
                                     block.ViewRect.Width, block.ViewRect.Height);

        // RF-491 — em depuração, as caixas de origem aparecem no lugar do fundo normal.
        if (ShowWordAreas)
        {
            var debug = P.DebugWordRectColor;
            context.FillRectangle(
                new SolidColorBrush(Color.FromArgb(debug.A, debug.R, debug.G, debug.B)), view);
        }
        else if (UseBackground && Translating)
        {
            // RF-377 — o fundo cobre o retângulo de VISUALIZAÇÃO inteiro.
            // RF-414 — com cor automática, os componentes vêm da análise e o alfa da
            // configuração do usuário.
            var background = block.AutoColor?.Background ?? BackgroundColor;

            // RF-378 — sem "usar transparência do fundo", o fundo é OPACO, preservando
            // apenas os componentes de cor. 🔒
            byte alpha = UseBackgroundTransparency ? background.A : (byte)255;
            if (alpha > 0)
            {
                context.FillRectangle(
                    new SolidColorBrush(Color.FromArgb(alpha, background.R, background.G, background.B)),
                    view);
            }
        }

        if (block.Lines.Count == 0) return;

        var orientation = EffectiveOrientation(block);
        var font = new FontSpec(FontFamilyName, block.FinalFontSize, FontStyle)
            .WithoutBoldWhenNoStroke(FontStroke);

        double advance = TextMetrics.LineAdvance(_cache, font);
        var content = RectD.From(block.ContentRect);

        // RF-413 — a cor de fonte automática só é usada quando a análise produziu uma;
        // caso contrário valem as cores configuradas (RF-415).
        var textColor = block.AutoColor?.Font ?? TextColor;

        // RF-393 — quando a cor de fonte é automática ou foi corrigida por contraste, as
        // cores de contorno são DERIVADAS dela.
        var (stroke1, stroke2) = block.AutoColor is not null
            ? ColorMath.DeriveStrokeColors(textColor)
            : (Stroke1Color, Stroke2Color);

        for (int i = 0; i < block.Lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(block.Lines[i])) continue;

            var band = OverlayTextLayout.LineBand(content, i, advance, orientation);
            DrawLine(context, block.Lines[i], font, new Point(band.X, band.Y),
                     textColor, stroke1, stroke2);
        }
    }

    /// <summary>RF-336 — Contorno duplo: externo, interno, preenchimento.</summary>
    private void DrawLine(DrawingContext context, string text, FontSpec font, Point origin,
                          GortRgba textColor, GortRgba stroke1, GortRgba stroke2)
    {
        var formatted = AvaloniaTextMeasurer.Build(text, font);

        if (!FontStroke || !LayerTextSurface.VectorTextAvailable)
        {
            formatted.SetForegroundBrush(new SolidColorBrush(ToColor(textColor)));
            context.DrawText(formatted, origin);
            return;
        }

        var geometry = formatted.BuildGeometry(origin);
        if (geometry is null)
        {
            formatted.SetForegroundBrush(new SolidColorBrush(ToColor(textColor)));
            context.DrawText(formatted, origin);
            return;
        }

        var outer = new Pen(new SolidColorBrush(ToColor(stroke2)), P.OuterStrokeWidth)
        {
            LineJoin = PenLineJoin.Round,
            LineCap = PenLineCap.Round,
        };
        var inner = new Pen(new SolidColorBrush(ToColor(stroke1)), P.InnerStrokeWidth)
        {
            LineJoin = PenLineJoin.Round,
            LineCap = PenLineCap.Round,
        };

        context.DrawGeometry(null, outer, geometry);
        context.DrawGeometry(null, inner, geometry);
        context.DrawGeometry(new SolidColorBrush(ToColor(textColor)), null, geometry);
    }

    private static double Median(IEnumerable<double> values)
    {
        var list = values.Where(v => v > 0).OrderBy(v => v).ToList();
        if (list.Count == 0) return 0;
        return list.Count % 2 == 1
            ? list[list.Count / 2]
            : (list[list.Count / 2 - 1] + list[list.Count / 2]) / 2;
    }

    private static Color ToColor(GortRgba c) => Color.FromArgb(c.A, c.R, c.G, c.B);
}
