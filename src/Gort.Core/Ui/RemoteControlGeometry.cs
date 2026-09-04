using Gort.Core.Model;

namespace Gort.Core.Ui;

/// <summary>Por onde uma janela está sendo redimensionada.</summary>
[Flags]
public enum ResizeEdge
{
    None = 0,
    Left = 1,
    Right = 2,
    Top = 4,
    Bottom = 8,

    TopLeft = Top | Left,
    TopRight = Top | Right,
    BottomLeft = Bottom | Left,
    BottomRight = Bottom | Right,
}

/// <summary>
/// RF-518 / RF-519 — Geometria do controle remoto.
///
/// A janela é redimensionável pelas bordas MANTENDO A PROPORÇÃO ORIGINAL: ao redimensionar
/// por uma borda, a outra dimensão é derivada da proporção; ao redimensionar por um canto,
/// usa-se o MAIOR fator dos dois eixos.
///
/// A proporção fixa é o que permite RF-519: os controles internos são escalados
/// proporcionalmente, e um redimensionamento livre os distorceria.
/// </summary>
public static class RemoteControlGeometry
{
    /// <summary>Menor tamanho aceito, para que os botões continuem alcançáveis.</summary>
    public const int MinimumWidth = 80;

    /// <summary>
    /// Calcula o novo retângulo preservando a proporção de <paramref name="original"/>.
    ///
    /// Os deslocamentos são do ponteiro desde o início do arraste. As bordas esquerda e
    /// superior movem a ORIGEM da janela além do tamanho, porque a borda oposta tem de
    /// ficar parada — do contrário a janela escaparia debaixo do cursor.
    /// </summary>
    public static Rect Resize(Rect original, ResizeEdge edge, int deltaX, int deltaY)
    {
        if (edge == ResizeEdge.None || original.Width <= 0 || original.Height <= 0)
            return original;

        double aspect = (double)original.Height / original.Width;

        bool horizontal = edge.HasFlag(ResizeEdge.Left) || edge.HasFlag(ResizeEdge.Right);
        bool vertical = edge.HasFlag(ResizeEdge.Top) || edge.HasFlag(ResizeEdge.Bottom);

        // Largura que cada eixo pediria isoladamente.
        double widthFromX = original.Width
            + (edge.HasFlag(ResizeEdge.Right) ? deltaX : edge.HasFlag(ResizeEdge.Left) ? -deltaX : 0);
        double widthFromY = (original.Height
            + (edge.HasFlag(ResizeEdge.Bottom) ? deltaY : edge.HasFlag(ResizeEdge.Top) ? -deltaY : 0))
            / aspect;

        double width;
        if (horizontal && vertical)
        {
            // Canto: o MAIOR fator dos dois eixos manda. Assim o canto arrastado nunca fica
            // para trás do cursor em nenhuma das direções.
            width = Math.Max(widthFromX, widthFromY);
        }
        else if (horizontal)
        {
            width = widthFromX;
        }
        else
        {
            width = widthFromY;
        }

        int newWidth = Math.Max(MinimumWidth, (int)Math.Round(width));
        int newHeight = Math.Max(1, (int)Math.Round(newWidth * aspect));

        // As bordas esquerda e superior deslocam a origem para manter a borda oposta parada.
        int x = edge.HasFlag(ResizeEdge.Left) ? original.Right - newWidth : original.X;
        int y = edge.HasFlag(ResizeEdge.Top) ? original.Bottom - newHeight : original.Y;

        return new Rect(x, y, newWidth, newHeight);
    }

    /// <summary>
    /// RF-519 — Fator de escala dos controles internos em relação ao tamanho de referência.
    /// </summary>
    public static double ContentScale(int currentWidth, int referenceWidth)
        => referenceWidth <= 0 ? 1.0 : Math.Max(0.1, (double)currentWidth / referenceWidth);

    /// <summary>
    /// Qual borda está sob o ponteiro, dada a zona sensível. Devolve <see cref="ResizeEdge.None"/>
    /// no miolo, onde o gesto é ARRASTAR a janela (RF-518: movível por qualquer ponto).
    /// </summary>
    public static ResizeEdge EdgeAt(int x, int y, int width, int height, int hotZone)
    {
        var edge = ResizeEdge.None;
        if (x <= hotZone) edge |= ResizeEdge.Left;
        else if (x >= width - hotZone) edge |= ResizeEdge.Right;

        if (y <= hotZone) edge |= ResizeEdge.Top;
        else if (y >= height - hotZone) edge |= ResizeEdge.Bottom;

        return edge;
    }
}
