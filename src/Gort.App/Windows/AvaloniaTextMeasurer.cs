using Avalonia;
using Avalonia.Media;
using Gort.Core.Rendering;

// `FontStyle` existe nos dois mundos; o apelido separa o do programa do do Avalonia.
using GortFontStyle = Gort.Core.Rendering.FontStyle;

namespace Gort.App.Windows;

/// <summary>
/// C16 / C17 — Medição de texto sobre o motor de desenho do Avalonia.
///
/// RF-572 — É a MESMA implementação que desenha, e é isso que garante que o layout
/// calculado corresponda ao desenhado. Medir com um motor e desenhar com outro produziria
/// texto que cabe na conta e estoura na tela.
/// </summary>
public sealed class AvaloniaTextMeasurer : ITextMeasurer
{
    public TextExtent MeasurePath(string text, FontSpec font)
    {
        var formatted = Build(text, font);
        var geometry = formatted.BuildGeometry(new Point(0, 0));

        // Sem caminho vetorial — texto só de espaços, ou desenho vetorial indisponível
        // (RF-007) — a extensão do motor é a melhor informação que existe.
        if (geometry is null) return new TextExtent(formatted.Width, formatted.Height);

        var bounds = geometry.Bounds;
        return new TextExtent(bounds.Width, bounds.Height);
    }

    public double MeasureEngineWidth(string text, FontSpec font)
        => Build(text, font).WidthIncludingTrailingWhitespace;

    public double FontHeight(FontSpec font)
    {
        // A altura é medida sobre uma cadeia com ascendente e descendente, para que ela não
        // varie conforme o conteúdo da linha.
        return Build("Ápg", font).Height;
    }

    public static FormattedText Build(string text, FontSpec font)
    {
        var typeface = new Typeface(
            string.IsNullOrWhiteSpace(font.Family) ? FontFamily.Default : new FontFamily(font.Family),
            font.Style.HasFlag(GortFontStyle.Italic) ? Avalonia.Media.FontStyle.Italic
                                                 : Avalonia.Media.FontStyle.Normal,
            font.Style.HasFlag(GortFontStyle.Bold) ? FontWeight.Bold : FontWeight.Normal);

        return new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            Math.Max(1, font.Size),
            Brushes.White);
    }
}
