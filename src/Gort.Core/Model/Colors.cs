using Gort.Core.Calibration;

namespace Gort.Core.Model;

/// <summary>Cor de 8 bits por canal com alfa.</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)
{
    public static readonly Rgba Black = new(0, 0, 0);
    public static readonly Rgba White = new(255, 255, 255);
    public static readonly Rgba Transparent = new(0, 0, 0, 0);

    public Rgba WithAlpha(byte a) => this with { A = a };

    public int ToArgb() => (A << 24) | (R << 16) | (G << 8) | B;

    public static Rgba FromArgb(int argb) => new(
        (byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF),
        (byte)((argb >> 24) & 0xFF));

    public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}

/// <summary>
/// Conversões de cor exigidas pela especificação. São duas escalas diferentes e
/// deliberadamente separadas:
///
///  - <see cref="ToHsvFilter"/> implementa RF-107, usada pelo FILTRO de pré-processamento,
///    onde saturação e brilho são expressos em 0–100.
///  - <see cref="ToHsb"/> é a escala 0–1 usada por RF-393 para derivar as cores de contorno
///    a partir da cor de fonte.
///
/// Não unificar as duas: a de RF-107 passa por 0–255 antes de virar 0–100, e o
/// arredondamento intermediário faz parte do comportamento calibrado do filtro.
/// </summary>
public static class ColorMath
{
    /// <summary>
    /// RF-107 — Conversão para HSV pelo máximo e mínimo dos componentes:
    ///   brilho = máximo;
    ///   saturação = (máximo − mínimo) × 255 ÷ máximo, com saturação 0 quando o máximo é 0;
    ///   matiz calculada nos setores de 60 graus, normalizada para 0–360.
    /// Devolve S e V já convertidos de 0–255 para 0–100, como exige a comparação (RF-106).
    /// </summary>
    public static (int H, int S, int V) ToHsvFilter(byte r, byte g, byte b)
    {
        int max = Math.Max(r, Math.Max(g, b));
        int min = Math.Min(r, Math.Min(g, b));
        int delta = max - min;

        int v255 = max;
        int s255 = max == 0 ? 0 : (delta * 255) / max;

        int h;
        if (delta == 0)
        {
            h = 0;
        }
        else if (max == r)
        {
            h = 60 * (g - b) / delta;
        }
        else if (max == g)
        {
            h = 60 * (b - r) / delta + 120;
        }
        else
        {
            h = 60 * (r - g) / delta + 240;
        }
        if (h < 0) h += 360;
        if (h >= 360) h -= 360;

        // Para exibição e comparação, saturação e brilho vão de 0–255 para 0–100.
        int s = s255 * 100 / 255;
        int vv = v255 * 100 / 255;
        return (h, s, vv);
    }

    /// <summary>
    /// Escala 0–1 de matiz/saturação/brilho usada por RF-393 (derivação das cores de
    /// contorno). Matiz em graus 0–360; saturação e brilho em 0–1.
    /// </summary>
    public static (double H, double S, double B) ToHsb(Rgba c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double h = 0;
        if (delta > 0)
        {
            if (max == r) h = 60 * (((g - b) / delta) % 6);
            else if (max == g) h = 60 * (((b - r) / delta) + 2);
            else h = 60 * (((r - g) / delta) + 4);
        }
        if (h < 0) h += 360;
        double s = max <= 0 ? 0 : delta / max;
        return (h, s, max);
    }

    /// <summary>Inverso de <see cref="ToHsb"/>. Saturação e brilho são saturados em 0–1.</summary>
    public static Rgba FromHsb(double h, double s, double brightness, byte alpha = 255)
    {
        s = Math.Clamp(s, 0, 1);
        brightness = Math.Clamp(brightness, 0, 1);
        h = ((h % 360) + 360) % 360;

        double c = brightness * s;
        double x = c * (1 - Math.Abs((h / 60.0 % 2) - 1));
        double m = brightness - c;

        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return new Rgba(
            (byte)Math.Round(Math.Clamp((r + m) * 255, 0, 255)),
            (byte)Math.Round(Math.Clamp((g + m) * 255, 0, 255)),
            (byte)Math.Round(Math.Clamp((b + m) * 255, 0, 255)),
            alpha);
    }

    /// <summary>
    /// RF-411 — Luminância relativa: 0,2126·R' + 0,7152·G' + 0,0722·B' (P-160), com cada
    /// componente linearizado por c/12,92 se c ≤ 0,04045 e por ((c+0,055)/1,055)^2,4
    /// caso contrário (P-161).
    /// </summary>
    public static double RelativeLuminance(Rgba c)
        => P.LumR * Linearize(c.R) + P.LumG * Linearize(c.G) + P.LumB * Linearize(c.B);

    private static double Linearize(byte component)
    {
        double c = component / 255.0;
        return c <= P.SrgbCutoff
            ? c / P.SrgbSlope
            : Math.Pow((c + P.SrgbOffset) / P.SrgbScale, P.SrgbGamma);
    }

    /// <summary>
    /// RF-411 — Razão de contraste: (luminância_maior + 0,05) ÷ (luminância_menor + 0,05).
    /// </summary>
    public static double ContrastRatio(Rgba a, Rgba b)
    {
        double la = RelativeLuminance(a);
        double lb = RelativeLuminance(b);
        double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
        return (hi + P.ContrastConstant) / (lo + P.ContrastConstant);
    }

    /// <summary>
    /// RF-409 / RF-410 — Entre preto e branco, devolve o que der maior contraste contra
    /// a cor de fundo dada.
    /// </summary>
    public static Rgba BestBlackOrWhite(Rgba background)
        => ContrastRatio(Rgba.White, background) >= ContrastRatio(Rgba.Black, background)
            ? Rgba.White
            : Rgba.Black;

    /// <summary>
    /// RF-393 — Deriva as cores de contorno a partir da cor de fonte, usada quando a cor de
    /// fonte é automática ou foi corrigida por contraste. 🔒
    ///
    ///  - brilho ≥ 0,5 (fonte clara): contorno 1 = mesma matiz, saturação −0,05, brilho −0,1;
    ///                                contorno 2 = preto;
    ///  - brilho &lt; 0,5 (fonte escura): contorno 1 = mesma matiz, saturação +0,05, brilho +0,1;
    ///                                contorno 2 = branco.
    /// </summary>
    public static (Rgba Stroke1, Rgba Stroke2) DeriveStrokeColors(Rgba fontColor)
    {
        var (h, s, b) = ToHsb(fontColor);
        if (b >= 0.5)
        {
            return (FromHsb(h, s - 0.05, b - 0.1, fontColor.A), Rgba.Black);
        }
        return (FromHsb(h, s + 0.05, b + 0.1, fontColor.A), Rgba.White);
    }
}

/// <summary>
/// 6.8 — Resultado da análise automática de cor de um bloco: cor de fonte, cor de fundo e
/// indicadores de qualidade.
/// </summary>
public sealed record AutoColorResult(
    Rgba Font,
    Rgba Background,
    int SupportingWords,
    double Contrast,
    bool UsedFallback,
    bool ContrastCorrected);
