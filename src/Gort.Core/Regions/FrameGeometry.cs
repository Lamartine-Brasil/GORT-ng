using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Regions;

/// <summary>
/// RF-073 a RF-077 — Conversão de MOLDURA (a janelinha que o usuário arrasta) para
/// RETÂNGULO DE CAPTURA (o que efetivamente vai para o OCR).
///
/// É o ponto em que a especificação avisa que a falha "passa despercebida na maioria das
/// instalações": com um monitor a 100% e outro a 150%, um fator de escala único erra em um
/// dos dois e a região capturada sai deslocada alguns pixels. Na sobreposição isso aparece
/// como a tradução desalinhada em relação ao texto original.
/// </summary>
public static class FrameGeometry
{
    /// <summary>
    /// RF-074 — As espessuras usadas na conversão são os valores BASE P-14, P-15 e P-16
    /// escalados pelo fator de DPI do monitor.
    /// </summary>
    public readonly record struct FrameMetrics(int Border, int OuterBorder, int TitleBar)
    {
        /// <summary>
        /// RF-056 / P-11 — Zona sensível de borda: a soma das três espessuras, que é
        /// exatamente como P-11 está definido (31 = 3 + 8 + 20).
        /// </summary>
        public int ResizeHotZone => Border + OuterBorder + TitleBar;
    }

    /// <summary>
    /// RF-074 — Escala os valores base pelo fator do monitor.
    ///
    /// O arredondamento é para CIMA, e o erro é assimétrico de propósito: a coluna de efeito
    /// de P-14 diz que DIMINUIR a espessura faz as "bordas entrarem na captura e virarem
    /// ruído", enquanto aumentá-la só faz a "área capturada ficar menor que a desenhada".
    /// Perder um pixel de conteúdo é barato; deixar a moldura entrar na imagem faz o OCR
    /// inventar caracteres na borda.
    ///
    /// O piso de 1 px impede que uma escala pequena zere um desconto.
    /// </summary>
    public static FrameMetrics MetricsFor(double scale)
    {
        static int Scaled(int baseValue, double scale)
            => Math.Max(1, (int)Math.Ceiling(baseValue * scale));

        return new FrameMetrics(
            Scaled(P.FrameBorderThickness, scale),
            Scaled(P.FrameOuterBorderThickness, scale),
            Scaled(P.FrameTitleBarHeight, scale));
    }

    /// <summary>
    /// RF-073 — Desconta a borda e a barra de título da moldura:
    ///   origem  = (x + borda, y + barra de título)
    ///   tamanho = (largura − 2 × borda, altura − barra de título − borda)
    /// com mínimo de 1 px em cada dimensão.
    ///
    /// O desconto usa P-14 (a borda) e P-16 (a barra de título); P-15 é a borda visual
    /// externa, que entra na zona sensível de RF-056 mas não neste cálculo.
    /// </summary>
    public static Rect ToCaptureRect(Rect frame, FrameMetrics m)
    {
        int x = frame.X + m.Border;
        int y = frame.Y + m.TitleBar;
        int width = Math.Max(1, frame.Width - 2 * m.Border);
        int height = Math.Max(1, frame.Height - m.TitleBar - m.Border);
        return new Rect(x, y, width, height);
    }

    /// <summary>
    /// RF-073 a RF-076 — Conversão completa, com a escala resolvida no MOMENTO da conversão
    /// a partir do monitor que contém a moldura. Nunca um fator global lido uma vez na
    /// abertura do programa.
    /// </summary>
    public static Rect ToCaptureRect(Rect frame, Func<Rect, double> scaleAt)
        => ToCaptureRect(frame, MetricsFor(scaleAt(frame)));

    /// <summary>
    /// Inverso de <see cref="ToCaptureRect"/>: dado o retângulo que se quer capturar,
    /// devolve a moldura que o produz. Usado ao restaurar áreas persistidas e ao criar uma
    /// moldura a partir de um retângulo desenhado na camada de seleção.
    /// </summary>
    public static Rect ToFrameRect(Rect capture, FrameMetrics m)
        => new(capture.X - m.Border,
               capture.Y - m.TitleBar,
               capture.Width + 2 * m.Border,
               capture.Height + m.TitleBar + m.Border);

    /// <summary>
    /// RF-077 / P-144 — A largura de cada retângulo entregue à captura é arredondada para
    /// CIMA até o próximo múltiplo de 4, por exigência de alinhamento de linha da imagem.
    /// Uma largura que já é múltipla de 4 permanece como está.
    /// </summary>
    public static int AlignWidth(int width)
    {
        if (width <= 0) return P.CaptureWidthAlignment;
        int remainder = width % P.CaptureWidthAlignment;
        return remainder == 0 ? width : width + (P.CaptureWidthAlignment - remainder);
    }

    /// <summary>RF-077 — Aplica o alinhamento de largura a um retângulo.</summary>
    public static Rect AlignWidth(Rect rect) => rect with { Width = AlignWidth(rect.Width) };

    /// <summary>
    /// RF-057 / P-12 — A moldura não pode ficar menor que o mínimo em nenhuma dimensão.
    /// </summary>
    public static Rect ClampToMinimumSize(Rect frame)
        => new(frame.X, frame.Y,
               Math.Max(P.FrameMinWidth, frame.Width),
               Math.Max(P.FrameMinHeight, frame.Height));

    /// <summary>
    /// RF-058 — Ao soltar o arraste, a moldura é reposicionada para dentro dos limites da
    /// área de trabalho virtual se tiver saído PELA ESQUERDA OU PELO TOPO.
    ///
    /// Só esses dois lados: sair pela direita ou por baixo é legítimo — o usuário pode
    /// querer uma área que termina fora da tela —, mas uma origem acima ou à esquerda da
    /// área de trabalho tornaria a moldura inalcançável para um novo arraste.
    /// </summary>
    public static Rect ClampIntoDesktop(Rect frame, Rect desktop)
    {
        int x = Math.Max(frame.X, desktop.Left);
        int y = Math.Max(frame.Y, desktop.Top);
        return frame with { X = x, Y = y };
    }

    /// <summary>
    /// RF-456 — Posição da moldura da área que segue o mouse, de modo que o CENTRO da área
    /// de captura fique sob o cursor:
    ///   x = cursor.x − borda − largura/2
    ///   y = cursor.y − barra_de_título − altura/2
    /// usando as mesmas espessuras de RF-073.
    /// </summary>
    public static Rect PositionUnderCursor(Rect frame, int cursorX, int cursorY, FrameMetrics m)
    {
        var capture = ToCaptureRect(frame, m);
        return frame with
        {
            X = cursorX - m.Border - capture.Width / 2,
            Y = cursorY - m.TitleBar - capture.Height / 2,
        };
    }

    /// <summary>
    /// RF-052 / P-145 — Um retângulo com largura ou altura de até 4 px é tratado como
    /// clique acidental e descartado sem criar área.
    /// </summary>
    public static bool IsAccidentalClick(Rect drawn)
        => drawn.Width <= P.MinSelectionRectSize || drawn.Height <= P.MinSelectionRectSize;

    /// <summary>
    /// RF-050 / P-10 — Opacidade da camada de seleção, derivada do canal alfa da cor de
    /// fundo escolhida e saturada num mínimo: max(alfa, 75) ÷ 255 × 0,15.
    /// </summary>
    public static double SelectionOverlayOpacity(byte backgroundAlpha)
        => P.SelectionOverlayOpacity(backgroundAlpha);
}
