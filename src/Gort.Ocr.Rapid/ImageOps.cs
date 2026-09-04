using Gort.Core.Model;

namespace Gort.Ocr.Rapid;

/// <summary>
/// Ordem dos canais de cor esperada pelo modelo. Os modelos de referência são treinados
/// numa ordem específica, e trocá-la degrada o reconhecimento sem produzir erro nenhum —
/// por isso é explícita e verificável, e não uma suposição enterrada no código.
/// </summary>
public enum ChannelOrder { Rgb, Bgr }

/// <summary>
/// Operações de imagem de que os modelos precisam: redimensionamento para dimensões
/// arbitrárias e recorte. São separadas do pré-processamento do capítulo 13, que é do
/// PROGRAMA; estas pertencem ao adaptador do motor.
/// </summary>
public static class ImageOps
{
    /// <summary>Redimensionamento bilinear para dimensões arbitrárias.</summary>
    public static ImageBuffer ResizeTo(ImageBuffer source, int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (source.Width == width && source.Height == height) return source;

        var output = ImageBuffer.Allocate(width, height, source.Format);
        int ch = source.Channels;

        double sx = (double)source.Width / width;
        double sy = (double)source.Height / height;

        for (int y = 0; y < height; y++)
        {
            double fy = (y + 0.5) * sy - 0.5;
            int y0 = (int)Math.Floor(fy);
            double wy = fy - y0;
            int y1 = Math.Clamp(y0 + 1, 0, source.Height - 1);
            y0 = Math.Clamp(y0, 0, source.Height - 1);

            for (int x = 0; x < width; x++)
            {
                double fx = (x + 0.5) * sx - 0.5;
                int x0 = (int)Math.Floor(fx);
                double wx = fx - x0;
                int x1 = Math.Clamp(x0 + 1, 0, source.Width - 1);
                x0 = Math.Clamp(x0, 0, source.Width - 1);

                int o00 = y0 * source.Stride + x0 * ch;
                int o01 = y0 * source.Stride + x1 * ch;
                int o10 = y1 * source.Stride + x0 * ch;
                int o11 = y1 * source.Stride + x1 * ch;
                int od = y * output.Stride + x * ch;

                for (int c = 0; c < ch; c++)
                {
                    double top = source.Pixels[o00 + c] * (1 - wx) + source.Pixels[o01 + c] * wx;
                    double bottom = source.Pixels[o10 + c] * (1 - wx) + source.Pixels[o11 + c] * wx;
                    output.Pixels[od + c] = (byte)Math.Clamp(
                        Math.Round(top * (1 - wy) + bottom * wy), 0, 255);
                }
            }
        }
        return output;
    }

    /// <summary>Recorta um retângulo, saturado aos limites da imagem.</summary>
    public static ImageBuffer Crop(ImageBuffer source, Rect rect)
    {
        var r = rect.Intersect(new Rect(0, 0, source.Width, source.Height));
        if (r.IsEmpty) return ImageBuffer.Allocate(1, 1, source.Format);

        var output = ImageBuffer.Allocate(r.Width, r.Height, source.Format);
        int rowBytes = r.Width * source.Channels;

        for (int y = 0; y < r.Height; y++)
        {
            Array.Copy(source.Pixels, (r.Top + y) * source.Stride + r.Left * source.Channels,
                       output.Pixels, y * output.Stride, rowBytes);
        }
        return output;
    }

    /// <summary>
    /// Converte para o tensor de entrada dos modelos: layout CHW, normalizado por
    /// (valor ÷ 255 − média) ÷ desvio.
    ///
    /// Os modelos de referência usam média e desvio 0,5 nos três canais, o que reduz a
    /// normalização a (valor ÷ 127,5 − 1).
    /// </summary>
    public static float[] ToTensor(ImageBuffer image, ChannelOrder order,
                                   int targetWidth = 0, int targetHeight = 0,
                                   float mean = 0.5f, float std = 0.5f)
    {
        int w = targetWidth > 0 ? targetWidth : image.Width;
        int h = targetHeight > 0 ? targetHeight : image.Height;

        // Um tensor maior que a imagem é preenchido com zeros à direita, que é como o
        // reconhecedor recebe linhas mais estreitas que a largura do lote.
        var tensor = new float[3 * w * h];
        int plane = w * h;

        for (int y = 0; y < Math.Min(h, image.Height); y++)
        {
            for (int x = 0; x < Math.Min(w, image.Width); x++)
            {
                var (b, g, r, _) = image.GetPixel(x, y);
                byte c0 = order == ChannelOrder.Rgb ? r : b;
                byte c2 = order == ChannelOrder.Rgb ? b : r;

                int i = y * w + x;
                tensor[i] = (c0 / 255f - mean) / std;
                tensor[plane + i] = (g / 255f - mean) / std;
                tensor[2 * plane + i] = (c2 / 255f - mean) / std;
            }
        }
        return tensor;
    }
}
