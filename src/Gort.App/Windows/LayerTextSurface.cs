using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Gort.Core.Calibration;
using Gort.Core.Rendering;

using GortRgba = Gort.Core.Model.Rgba;

// `VerticalAlignment` também é uma propriedade de layout do próprio Control; o apelido
// deixa claro que aqui se trata do alinhamento do TEXTO, de RF-341.
using TextVerticalAlignment = Gort.Core.Rendering.VerticalAlignment;

namespace Gort.App.Windows;

/// <summary>
/// 19.3 — A superfície de desenho do modo camada.
///
/// RF-332 — A janela é desenhada INTEIRA a cada atualização: não há desenho incremental,
/// porque a transparência por pixel exige que o quadro seja composto de uma vez.
/// </summary>
public sealed class LayerTextSurface : Control
{
    public string Text { get; private set; } = "";
    public bool Translating { get; set; }
    public bool ForcedTransparency { get; set; }

    /// <summary>RF-337 — Pintar um retângulo atrás do texto.</summary>
    public bool UseTextBackground { get; set; } = true;

    /// <summary>RF-336 / RF-392 — Desenhar o contorno duplo.</summary>
    public bool UseStroke { get; set; } = true;

    public string FontFamilyName { get; set; } = "";
    public double FontSizePoints { get; set; } = P.DefaultFontSize;

    public GortRgba TextColor { get; set; } = new(255, 255, 255);
    public GortRgba Stroke1Color { get; set; } = new(192, 192, 192);
    public GortRgba Stroke2Color { get; set; } = new(0, 0, 0);
    public GortRgba BackgroundColor { get; set; } = new(0, 0, 0, 170);

    public TextAlignment TextHorizontalAlignment { get; set; } = TextAlignment.Left;
    public TextVerticalAlignment VerticalTextAlignment { get; set; } = TextVerticalAlignment.Top;

    /// <summary>
    /// RF-007 — Quando o desenho vetorial de texto não funciona, ele é desativado em TODO o
    /// programa e o texto passa a ser desenhado sem contorno.
    /// </summary>
    public static bool VectorTextAvailable { get; set; } = true;

    public void SetText(string text)
    {
        Text = text;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);

        // RF-333 / RF-334 — o fundo da JANELA: semitransparente parada, invisível traduzindo.
        byte windowAlpha = LayerLayout.BackgroundAlpha(Translating, ForcedTransparency);
        if (windowAlpha > 0)
        {
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(windowAlpha, 20, 20, 24)), bounds);

            // RF-333 — a borda de destaque existe para o usuário LOCALIZAR e mover a janela
            // quando ela está parada; traduzindo, ela sumiria junto com o resto.
            var highlight = new Pen(
                new SolidColorBrush(Color.FromRgb(P.IdleHighlightBorderColor.R,
                                                  P.IdleHighlightBorderColor.G,
                                                  P.IdleHighlightBorderColor.B)),
                P.IdleHighlightBorderThickness);
            context.DrawRectangle(null, highlight, bounds.Deflate(P.IdleHighlightBorderThickness / 2));
        }

        if (string.IsNullOrEmpty(Text)) return;

        // RF-338 — o texto vive num retângulo com margem de P-86.
        var area = LayerLayout.TextRect(Bounds.Width, Bounds.Height);

        var formatted = new FormattedText(
            Text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            string.IsNullOrWhiteSpace(FontFamilyName)
                ? Typeface.Default
                : new Typeface(new FontFamily(FontFamilyName)),
            FontSizePoints,
            new SolidColorBrush(ToColor(TextColor)))
        {
            MaxTextWidth = Math.Max(1, area.Width),
            TextAlignment = TextHorizontalAlignment,
        };

        double offsetY = LayerLayout.VerticalOffset(
            new Gort.Core.Model.RectD(area.X, area.Y, area.Width, area.Height),
            formatted.Height, VerticalTextAlignment);

        var origin = new Point(area.X, area.Y + offsetY);

        // RF-337 — o fundo do texto é pintado apenas quando a opção está ativa E a tradução
        // está rodando: parado, o fundo da janela já cumpre esse papel.
        if (UseTextBackground && Translating && BackgroundColor.A > 0)
        {
            var extent = new Gort.Core.Model.RectD(
                origin.X, origin.Y, formatted.WidthIncludingTrailingWhitespace, formatted.Height);
            var back = LayerLayout.BackgroundRect(extent);

            context.FillRectangle(
                new SolidColorBrush(ToColor(BackgroundColor)),
                new Rect(back.X, back.Y, back.Width, back.Height));
        }

        DrawText(context, formatted, origin);
    }

    /// <summary>
    /// RF-336 — O texto é desenhado como CAMINHO VETORIAL com contorno DUPLO: um contorno
    /// externo de espessura P-80 na cor de contorno 2, um contorno interno de espessura
    /// P-81 na cor de contorno 1, ambos com junção arredondada, e o preenchimento na cor do
    /// texto.
    ///
    /// A ordem importa: o externo primeiro, depois o interno, depois o preenchimento. Ela é
    /// o que produz a moldura em duas camadas que mantém o texto legível sobre qualquer
    /// fundo de jogo.
    ///
    /// RF-007 — Sem desenho vetorial, cai para texto simples sem contorno.
    /// </summary>
    private void DrawText(DrawingContext context, FormattedText formatted, Point origin)
    {
        if (!UseStroke || !VectorTextAvailable)
        {
            context.DrawText(formatted, origin);
            return;
        }

        var geometry = formatted.BuildGeometry(origin);
        if (geometry is null)
        {
            // A construção do caminho falhou para este texto: desenha-se simples, em vez de
            // não desenhar nada (P8).
            context.DrawText(formatted, origin);
            return;
        }

        var outer = new Pen(new SolidColorBrush(ToColor(Stroke2Color)), P.OuterStrokeWidth)
        {
            LineJoin = PenLineJoin.Round,
            LineCap = PenLineCap.Round,
        };
        var inner = new Pen(new SolidColorBrush(ToColor(Stroke1Color)), P.InnerStrokeWidth)
        {
            LineJoin = PenLineJoin.Round,
            LineCap = PenLineCap.Round,
        };

        context.DrawGeometry(null, outer, geometry);
        context.DrawGeometry(null, inner, geometry);
        context.DrawGeometry(new SolidColorBrush(ToColor(TextColor)), null, geometry);
    }

    private static Color ToColor(GortRgba c) => Color.FromArgb(c.A, c.R, c.G, c.B);
}
