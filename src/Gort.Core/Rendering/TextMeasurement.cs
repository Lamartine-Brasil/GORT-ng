using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Rendering;

/// <summary>Estilo de fonte que entra na chave do cache de medição (RF-374).</summary>
[Flags]
public enum FontStyle
{
    Normal = 0,
    Bold = 1,
    Italic = 2,
}

/// <summary>Uma fonte concreta, como ela será usada no desenho.</summary>
public readonly record struct FontSpec(string Family, double Size, FontStyle Style)
{
    /// <summary>
    /// RF-392 — Quando o contorno de fonte está DESATIVADO no modo sobreposição, o negrito
    /// é removido. 🔒
    ///
    /// Motivo: sem contorno, o negrito engrossa demais e reduz a legibilidade sobre fundos
    /// claros — o traço mais largo encosta em si mesmo e o glifo perde definição.
    /// </summary>
    public FontSpec WithoutBoldWhenNoStroke(bool fontStroke)
        => fontStroke ? this : this with { Style = Style & ~FontStyle.Bold };
}

/// <summary>Extensão de um texto desenhado.</summary>
public readonly record struct TextExtent(double Width, double Height);

/// <summary>
/// C16 / C17 — Medição de texto, por trás da abstração de RF-577.
///
/// RF-572 — A mesma implementação de desenho de texto tem de valer em todas as plataformas,
/// para que o layout CALCULADO corresponda ao DESENHADO. Medir com um motor e desenhar com
/// outro produziria texto que cabe na conta e estoura na tela.
/// </summary>
public interface ITextMeasurer
{
    /// <summary>Limites do CAMINHO VETORIAL do texto.</summary>
    TextExtent MeasurePath(string text, FontSpec font);

    /// <summary>Largura medida pelo MOTOR DE TEXTO, que difere da do caminho.</summary>
    double MeasureEngineWidth(string text, FontSpec font);

    /// <summary>Altura da fonte, base do avanço entre linhas (RF-365).</summary>
    double FontHeight(FontSpec font);
}

/// <summary>
/// RF-374 — Cache de medição e de quebra de texto, válido durante UM desenho. 🔒
///
/// A busca binária de tamanho de fonte repete as mesmas medições dezenas de vezes; o cache
/// é o que torna a sobreposição viável (RF-550 o lista entre as otimizações obrigatórias).
///
/// A chave é composta de texto, família e estilo da fonte, tamanho em unidades de desenho,
/// orientação, sinalizadores de formato e alinhamento — tudo que muda o resultado. O cache
/// é DESCARTADO ao fim do desenho: guardá-lo entre quadros arriscaria devolver medidas de
/// uma configuração que já mudou.
/// </summary>
public sealed class TextMeasurementCache : ITextMeasurer
{
    private readonly ITextMeasurer _inner;
    private readonly Dictionary<Key, TextExtent> _paths = new();
    private readonly Dictionary<Key, double> _engineWidths = new();
    private readonly Dictionary<FontSpec, double> _heights = new();

    public TextMeasurementCache(ITextMeasurer inner) => _inner = inner;

    /// <summary>RF-494 — Contagens de acerto e erro, gravadas no retrato de depuração.</summary>
    public int Hits { get; private set; }
    public int Misses { get; private set; }

    private readonly record struct Key(string Text, string Family, FontStyle Style, long Size);

    private static Key KeyOf(string text, FontSpec font)
        // O tamanho entra arredondado em centésimos: dois tamanhos indistinguíveis no
        // desenho não devem gerar duas entradas.
        => new(text, font.Family, font.Style, (long)Math.Round(font.Size * 100));

    public TextExtent MeasurePath(string text, FontSpec font)
    {
        var key = KeyOf(text, font);
        if (_paths.TryGetValue(key, out var cached)) { Hits++; return cached; }

        Misses++;
        var extent = _inner.MeasurePath(text, font);
        _paths[key] = extent;
        return extent;
    }

    public double MeasureEngineWidth(string text, FontSpec font)
    {
        var key = KeyOf(text, font);
        if (_engineWidths.TryGetValue(key, out double cached)) { Hits++; return cached; }

        Misses++;
        double width = _inner.MeasureEngineWidth(text, font);
        _engineWidths[key] = width;
        return width;
    }

    public double FontHeight(FontSpec font)
    {
        if (_heights.TryGetValue(font, out double cached)) { Hits++; return cached; }

        Misses++;
        double height = _inner.FontHeight(font);
        _heights[font] = height;
        return height;
    }

    /// <summary>RF-374 — O cache é descartado ao fim do desenho.</summary>
    public void Clear()
    {
        _paths.Clear();
        _engineWidths.Clear();
        _heights.Clear();
        Hits = 0;
        Misses = 0;
    }
}

/// <summary>Operações de medição que dependem da orientação do bloco.</summary>
public static class TextMetrics
{
    /// <summary>
    /// RF-373 — Comprimento do texto na direção de escoamento.
    ///
    /// Para blocos HORIZONTAIS, o MAIOR entre a largura do caminho vetorial e a largura
    /// medida pelo motor de texto; para blocos VERTICAIS, a altura do caminho vetorial. 🔒
    ///
    /// O maior dos dois não é excesso de zelo: as duas medidas discordam — o caminho ignora
    /// o avanço final do último glifo, e o motor ignora o transbordo lateral de itálicos e
    /// de acentos — e usar a menor faria o texto estourar a borda.
    /// </summary>
    public static double Length(ITextMeasurer measurer, string text, FontSpec font,
                                Orientation orientation)
    {
        var path = measurer.MeasurePath(text, font);

        if (orientation == Orientation.Vertical) return path.Height;

        return Math.Max(path.Width, measurer.MeasureEngineWidth(text, font));
    }

    /// <summary>RF-365 — O avanço entre linhas é a altura da fonte multiplicada por P-98.</summary>
    public static double LineAdvance(ITextMeasurer measurer, FontSpec font)
        => measurer.FontHeight(font) * P.LineAdvanceFactor;
}
