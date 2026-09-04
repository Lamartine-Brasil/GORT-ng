namespace Gort.Core.Model;

/// <summary>
/// Formato de pixel de uma imagem em memória. O pipeline aceita 1, 3 e 4 canais
/// (7.1) e converte para o formato exigido por cada motor de OCR (RF-117).
/// </summary>
public enum PixelFormat
{
    /// <summary>1 canal — cinza.</summary>
    Gray8 = 1,
    /// <summary>3 canais — BGR.</summary>
    Bgr24 = 3,
    /// <summary>4 canais — BGRA.</summary>
    Bgra32 = 4,
}

/// <summary>
/// Imagem em memória, linha a linha, SEM preenchimento de fim de linha (7.1).
/// O passo entre linhas é sempre Largura × Canais.
/// </summary>
public sealed class ImageBuffer
{
    public ImageBuffer(int width, int height, PixelFormat format, byte[] pixels)
    {
        if (width < 0 || height < 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Dimensões negativas.");
        int expected = width * height * (int)format;
        if (pixels.Length < expected)
            throw new ArgumentException(
                $"Buffer insuficiente: {pixels.Length} bytes para {width}x{height}x{(int)format} " +
                $"({expected} esperados).", nameof(pixels));

        Width = width;
        Height = height;
        Format = format;
        Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public PixelFormat Format { get; }
    public int Channels => (int)Format;
    public int Stride => Width * Channels;
    public byte[] Pixels { get; }
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Bytes ocupados pelos pixels — usado pelo indicador de memória (RF-559).</summary>
    public long ByteCount => (long)Stride * Height;

    public static ImageBuffer Allocate(int width, int height, PixelFormat format)
        => new(width, height, format, new byte[Math.Max(0, width * height * (int)format)]);

    public int OffsetOf(int x, int y) => y * Stride + x * Channels;

    /// <summary>
    /// Lê um pixel como BGRA. Imagens de 1 canal replicam o valor nos três canais de cor
    /// (RF-117); imagens sem alfa devolvem alfa 255.
    /// </summary>
    public (byte B, byte G, byte R, byte A) GetPixel(int x, int y)
    {
        int o = OffsetOf(x, y);
        return Format switch
        {
            PixelFormat.Gray8 => (Pixels[o], Pixels[o], Pixels[o], (byte)255),
            PixelFormat.Bgr24 => (Pixels[o], Pixels[o + 1], Pixels[o + 2], (byte)255),
            _ => (Pixels[o], Pixels[o + 1], Pixels[o + 2], Pixels[o + 3]),
        };
    }
}

/// <summary>
/// 7.1 — Imagem de região: o que a captura entrega ao pré-processamento e ao OCR.
///
/// RF-098/RF-099 — A imagem ORIGINAL só é solicitada quando o modo é sobreposição E a cor
/// automática está ativa; ela é liberada assim que a análise de cor termina, e a imagem
/// tratada assim que o OCR termina. Cada região pode ocupar dezenas de megabytes com
/// ampliação.
/// </summary>
public sealed class RegionImage : IDisposable
{
    /// <summary>Qual área de OCR originou esta imagem (base 0).</summary>
    public required int Index { get; init; }

    /// <summary>Imagem tratada, pronta para o OCR (em geral binarizada e ampliada).</summary>
    public required ImageBuffer Processed { get; set; }

    /// <summary>
    /// Imagem original não tratada, nas dimensões da imagem ampliada.
    /// Ausente quando não foi solicitada.
    /// </summary>
    public ImageBuffer? Original { get; set; }

    /// <summary>Retângulo da área de OCR em coordenadas absolutas de tela.</summary>
    public required Rect ScreenRect { get; init; }

    /// <summary>
    /// Posição de origem do cliente capturado — relevante no modo janela anexada (6.2, RF-353).
    /// </summary>
    public (int X, int Y) ClientOrigin { get; init; }

    /// <summary>Verdadeiro quando esta imagem veio de uma área instantânea (7.5).</summary>
    public bool IsSnapshot { get; init; }

    public long ByteCount => Processed.ByteCount + (Original?.ByteCount ?? 0);

    /// <summary>RF-099 — Libera a imagem original assim que a análise de cor termina.</summary>
    public void ReleaseOriginal() => Original = null;

    public void Dispose()
    {
        Original = null;
        Processed = ImageBuffer.Allocate(0, 0, PixelFormat.Gray8);
    }
}

/// <summary>7.5 — Resultado de uma região, após OCR, agrupamento e tradução.</summary>
public sealed class RegionResult
{
    /// <summary>Área de OCR de origem.</summary>
    public required int Index { get; init; }

    /// <summary>Verdadeiro quando veio de uma área instantânea.</summary>
    public bool IsSnapshot { get; init; }

    /// <summary>Retângulo da área de OCR em coordenadas de tela, para o desenho (RF-352).</summary>
    public Rect ScreenRect { get; init; }

    /// <summary>Posição do cliente da janela capturada (RF-353).</summary>
    public (int X, int Y) ClientOrigin { get; init; }

    /// <summary>Todas as linhas reconhecidas.</summary>
    public required IReadOnlyList<Line> Lines { get; init; }

    /// <summary>Resultado do agrupamento (cap. 15).</summary>
    public required IReadOnlyList<TranslationBlock> Blocks { get; init; }

    /// <summary>RF-156 — União das caixas de todas as linhas.</summary>
    public Rect ResultBox { get; init; }

    /// <summary>A resposta inteira do tradutor, antes de dividir pelo token separador.</summary>
    public string? RawTranslatedText { get; set; }

    /// <summary>Verdadeiro se a análise de cor rodou (cap. 20).</summary>
    public bool UsesAutoColor { get; set; }

    /// <summary>Um par (fonte, fundo) por bloco, na mesma ordem dos blocos.</summary>
    public IReadOnlyList<AutoColorResult?> AutoColors { get; set; } = Array.Empty<AutoColorResult?>();
}
