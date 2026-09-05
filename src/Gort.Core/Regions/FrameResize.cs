using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Regions;

/// <summary>
/// RF-056 — Onde o ponteiro está sobre a moldura, e portanto o que um arraste ali faz.
///
/// A ordem importa: os cantos são testados ANTES dos lados, senão um ponto no canto
/// superior esquerdo casaria primeiro com "esquerda" e o arraste redimensionaria só um eixo.
/// </summary>
public enum FrameHandle
{
    None,
    Move,
    Left, Right, Top, Bottom,
    TopLeft, TopRight, BottomLeft, BottomRight,
}

/// <summary>
/// RF-056 a RF-058 — A geometria de mover e redimensionar uma moldura.
///
/// Fica aqui, e não na janela, porque é aritmética de retângulos: a janela só traduz
/// ponteiro em chamada. É também o que torna as regras verificáveis sem tela — o mínimo de
/// P-12 e o reposicionamento de RF-058 são fáceis de errar em um sinal.
/// </summary>
public static class FrameResize
{
    /// <summary>
    /// RF-056 — Que parte da moldura está sob o ponto, em coordenadas RELATIVAS à moldura.
    ///
    /// A zona sensível é P-11, escalada por DPI (`FrameMetrics.ResizeHotZone`).
    /// </summary>
    public static FrameHandle HandleAt(Rect frame, int x, int y, int hotZone, int titleBar)
    {
        if (x < 0 || y < 0 || x >= frame.Width || y >= frame.Height) return FrameHandle.None;

        // A zona não pode passar da metade: numa moldura no tamanho mínimo de P-12 as duas
        // bordas opostas se sobreporiam, e um lado sempre venceria o outro.
        int zone = Math.Max(1, Math.Min(hotZone, Math.Min(frame.Width, frame.Height) / 2));

        bool left = x < zone;
        bool right = x >= frame.Width - zone;
        bool top = y < zone;
        bool bottom = y >= frame.Height - zone;

        // Cantos primeiro (ver o comentário do enumerado).
        if (top && left) return FrameHandle.TopLeft;
        if (top && right) return FrameHandle.TopRight;
        if (bottom && left) return FrameHandle.BottomLeft;
        if (bottom && right) return FrameHandle.BottomRight;

        if (left) return FrameHandle.Left;
        if (right) return FrameHandle.Right;
        if (bottom) return FrameHandle.Bottom;

        // RF-056 — a barra de título MOVE; o resto do topo redimensiona.
        if (y < titleBar) return FrameHandle.Move;
        if (top) return FrameHandle.Top;

        return FrameHandle.Move;
    }

    /// <summary>
    /// RF-056 / RF-057 — Aplica um deslocamento ao arraste em curso.
    ///
    /// RF-057 — a moldura nunca fica menor que P-12 em nenhuma dimensão. O limite é imposto
    /// na BORDA QUE SE MOVE: arrastar a esquerda para além do mínimo trava a esquerda, e não
    /// empurra a direita — empurrar o lado parado faria a moldura fugir do cursor.
    /// </summary>
    public static Rect Apply(Rect start, FrameHandle handle, int dx, int dy)
    {
        int left = start.X;
        int top = start.Y;
        int right = start.X + start.Width;
        int bottom = start.Y + start.Height;

        switch (handle)
        {
            case FrameHandle.Move:
                return start with { X = left + dx, Y = top + dy };

            case FrameHandle.Left: left += dx; break;
            case FrameHandle.Right: right += dx; break;
            case FrameHandle.Top: top += dy; break;
            case FrameHandle.Bottom: bottom += dy; break;

            case FrameHandle.TopLeft: left += dx; top += dy; break;
            case FrameHandle.TopRight: right += dx; top += dy; break;
            case FrameHandle.BottomLeft: left += dx; bottom += dy; break;
            case FrameHandle.BottomRight: right += dx; bottom += dy; break;

            default: return start;
        }

        if (right - left < P.FrameMinWidth)
        {
            if (MovesLeftEdge(handle)) left = right - P.FrameMinWidth;
            else right = left + P.FrameMinWidth;
        }

        if (bottom - top < P.FrameMinHeight)
        {
            if (MovesTopEdge(handle)) top = bottom - P.FrameMinHeight;
            else bottom = top + P.FrameMinHeight;
        }

        return Rect.FromBounds(left, top, right, bottom);
    }

    private static bool MovesLeftEdge(FrameHandle h)
        => h is FrameHandle.Left or FrameHandle.TopLeft or FrameHandle.BottomLeft;

    private static bool MovesTopEdge(FrameHandle h)
        => h is FrameHandle.Top or FrameHandle.TopLeft or FrameHandle.TopRight;

    /// <summary>
    /// RF-058 — Ao SOLTAR o arraste, a moldura volta para dentro da área de trabalho virtual
    /// se saiu pela ESQUERDA ou pelo TOPO.
    ///
    /// Só esses dois lados, e isso é deliberado: sair pela direita ou por baixo deixa a
    /// barra de título visível, e o usuário consegue trazer a moldura de volta. Sair pela
    /// esquerda ou pelo topo leva a barra de título junto, e com ela some o único ponto por
    /// onde a moldura pode ser agarrada.
    /// </summary>
    public static Rect BringBack(Rect frame, Rect desktop)
    {
        int x = Math.Max(frame.X, desktop.X);
        int y = Math.Max(frame.Y, desktop.Y);
        return frame with { X = x, Y = y };
    }
}
