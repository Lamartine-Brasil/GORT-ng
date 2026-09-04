using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Rendering;

/// <summary>
/// 19.4 — Geometria da janela de sobreposição e dos blocos desenhados sobre o texto
/// original.
/// </summary>
public static class OverlayGeometry
{
    /// <summary>
    /// RF-349 — A janela cobre a união das áreas de OCR AMPLIADA em P-92 em cada dimensão. 🔒
    ///
    /// A folga existe porque o texto traduzido quase sempre ocupa mais espaço que o
    /// original: sem ela, um bloco expandido por RF-362 sairia pela borda da janela e seria
    /// recortado.
    /// </summary>
    public static Rect WindowRect(IEnumerable<Rect> areas)
    {
        var union = Rect.UnionAll(areas);
        if (union.IsEmpty) return Rect.Empty;

        int width = (int)Math.Ceiling(union.Width * P.OverlayRectSlackFactor);
        int height = (int)Math.Ceiling(union.Height * P.OverlayRectSlackFactor);

        // A folga é distribuída em torno do centro, para que o crescimento não puxe a
        // janela para um dos lados.
        int x = union.X - (width - union.Width) / 2;
        int y = union.Y - (height - union.Height) / 2;

        return new Rect(x, y, width, height);
    }

    /// <summary>
    /// RF-350 — O retângulo de exibição é ACUMULATIVO enquanto a tradução roda: se o novo
    /// cabe no anterior, mantém-se o anterior; senão, usa-se a união dos dois. 🔒
    ///
    /// Motivo: evita que a janela encolha e recorte texto que ainda está desenhado. Ao
    /// parar, o acúmulo é zerado — daí <paramref name="accumulated"/> ser nulo no primeiro
    /// desenho de cada tradução.
    /// </summary>
    public static Rect Accumulate(Rect? accumulated, Rect current)
    {
        if (accumulated is null || accumulated.Value.IsEmpty) return current;
        if (accumulated.Value.Contains(current)) return accumulated.Value;
        return accumulated.Value.Union(current);
    }

    /// <summary>
    /// RF-352 — Retângulo de um bloco em coordenadas de tela.
    ///
    ///   origem da área de OCR
    ///   − metade da largura e da altura da borda da moldura
    ///   + coordenadas do bloco divididas pelo fator de ampliação
    ///   − posição da janela de sobreposição
    ///
    /// Os cantos superior e esquerdo usam PISO; os inferior e direito, TETO — a mesma regra
    /// de RF-116, para que a caixa nunca encolha e perca pixel de borda do glifo.
    /// </summary>
    /// <param name="frameBorder">
    /// Espessura da borda da moldura, já escalada por DPI (RF-074). Entra pela METADE: a
    /// origem da área já desconta a borda inteira em RF-073, e aqui corrige-se o meio pixel
    /// que sobra do arredondamento daquele desconto.
    /// </param>
    public static Rect BlockRect(Rect blockInImage, Rect ocrArea, double scale,
                                 Rect overlayWindow, int frameBorder = 0)
    {
        if (scale <= 0) scale = 1;

        double halfBorder = frameBorder / 2.0;

        double left = ocrArea.Left - halfBorder + blockInImage.Left / scale - overlayWindow.Left;
        double top = ocrArea.Top - halfBorder + blockInImage.Top / scale - overlayWindow.Top;
        double right = ocrArea.Left - halfBorder + blockInImage.Right / scale - overlayWindow.Left;
        double bottom = ocrArea.Top - halfBorder + blockInImage.Bottom / scale - overlayWindow.Top;

        return Rect.FromBounds(
            (int)Math.Floor(left), (int)Math.Floor(top),
            (int)Math.Ceiling(right), (int)Math.Ceiling(bottom));
    }

    /// <summary>
    /// RF-353 — No modo de captura de janela anexada, a origem é limitada POR BAIXO à
    /// posição do cliente da janela capturada: o conteúdo não começa antes dela.
    /// </summary>
    public static Rect ClampToClient(Rect rect, (int X, int Y) clientOrigin)
        => Rect.FromBounds(
            Math.Max(rect.Left, clientOrigin.X),
            Math.Max(rect.Top, clientOrigin.Y),
            Math.Max(rect.Right, clientOrigin.X),
            Math.Max(rect.Bottom, clientOrigin.Y));

    /// <summary>
    /// RF-354 — O retângulo de cada bloco é recortado pelo retângulo da área de OCR; blocos
    /// que ficarem sem área são descartados.
    ///
    /// Devolve <see cref="Rect.Empty"/> para o bloco descartado.
    /// </summary>
    public static Rect ClipToArea(Rect blockRect, Rect areaRect) => blockRect.Intersect(areaRect);

    /// <summary>
    /// RF-359 — O retângulo de CONTEÚDO é o de visualização reduzido em P-93 quando o
    /// contorno de fonte está ativo, e não reduzido quando não está.
    ///
    /// A redução é o espaço que o contorno ocupa para fora do glifo; sem contorno, não há o
    /// que reservar.
    /// </summary>
    public static Rect ContentRect(Rect viewRect, bool fontStroke)
        => fontStroke
            ? viewRect.Inflate(-(int)P.ContentRectInsetWithStroke)
            : viewRect;
}
