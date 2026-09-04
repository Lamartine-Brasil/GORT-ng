using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Rendering;

/// <summary>Alinhamento vertical do texto na janela em modo camada (RF-341).</summary>
public enum VerticalAlignment { Top, Bottom }

/// <summary>
/// 19.3 — Geometria do modo CAMADA.
///
/// Uma janela transparente e sem bordas que o usuário posiciona onde quiser. Enquanto
/// traduz, fica invisível exceto pelo texto, com contorno duplo para legibilidade, e deixa
/// os cliques passarem através dela.
/// </summary>
public static class LayerLayout
{
    /// <summary>
    /// RF-338 — O texto é desenhado dentro de um retângulo com margem de P-86 em cima e à
    /// esquerda, e o MESMO VALOR descontado da largura e da altura.
    ///
    /// A margem é assimétrica de propósito: descontar P-86 uma única vez de cada dimensão
    /// deixa a margem direita e a inferior menores que a esquerda e a superior. É como o
    /// requisito está escrito.
    /// </summary>
    public static RectD TextRect(double windowWidth, double windowHeight)
        => new(P.LayerTextMargin, P.LayerTextMargin,
               Math.Max(0, windowWidth - P.LayerTextMargin),
               Math.Max(0, windowHeight - P.LayerTextMargin));

    /// <summary>
    /// RF-337 — Retângulo pintado atrás do texto, medido pela EXTENSÃO REAL do texto e
    /// expandido em P-82 à esquerda, P-83 acima, P-84 na largura e P-85 na altura. 🔒
    ///
    /// As quatro expansões são valores distintos e assimétricos; não são "uma margem".
    /// </summary>
    public static RectD BackgroundRect(RectD textExtent)
        => new(textExtent.X - P.LayerBackgroundExpandLeft,
               textExtent.Y - P.LayerBackgroundExpandTop,
               textExtent.Width + P.LayerBackgroundExpandWidth,
               textExtent.Height + P.LayerBackgroundExpandHeight);

    /// <summary>
    /// RF-341 — Deslocamento vertical do bloco de texto conforme o alinhamento.
    /// O alinhamento inferior é o que serve a legendas: o texto cresce para cima e a última
    /// linha fica sempre no mesmo lugar.
    /// </summary>
    public static double VerticalOffset(RectD area, double textHeight, VerticalAlignment alignment)
        => alignment == VerticalAlignment.Bottom
            ? Math.Max(0, area.Height - textHeight)
            : 0;

    /// <summary>
    /// RF-333 / RF-334 — Alfa do fundo da janela.
    ///
    /// Parada, ela é semitransparente (P-79) e recebe cliques, com uma borda de destaque
    /// para o usuário conseguir achá-la e movê-la. Traduzindo, o fundo fica TOTALMENTE
    /// transparente e os cliques passam através dela — do contrário ela ficaria entre o
    /// usuário e o jogo.
    ///
    /// RF-335 — A transparência forçada mantém o estado de tradução mesmo depois de parar.
    /// </summary>
    public static byte BackgroundAlpha(bool translating, bool forcedTransparency)
        => translating || forcedTransparency ? (byte)0 : P.LayerIdleBackgroundAlpha;

    /// <summary>
    /// RF-334 / RF-335 — Se a janela deixa os cliques passarem. Acompanha o alfa: uma janela
    /// invisível que ainda captura cliques é pior que uma janela visível.
    /// </summary>
    public static bool ClickThrough(bool translating, bool forcedTransparency)
        => translating || forcedTransparency;

    /// <summary>
    /// RF-343 — Verdadeiro quando a janela de tradução INTERSECTA alguma área de OCR, caso
    /// em que ela estaria sendo capturada e traduzindo a si mesma.
    ///
    /// Só vale nos modos escuro e camada, com captura de tela e tradução em tempo real: no
    /// modo sobreposição a janela é excluída da captura, e com captura de janela anexada a
    /// fonte não é a tela.
    /// </summary>
    public static bool WouldCaptureItself(Rect windowRect, IEnumerable<Rect> ocrAreas)
    {
        foreach (var area in ocrAreas)
        {
            if (area.IntersectsWith(windowRect)) return true;
        }
        return false;
    }
}

/// <summary>
/// RF-342 — Mensagem de aviso temporária, prefixada ao texto e com prazo de validade;
/// depois do prazo ela desaparece sozinha.
///
/// É separada do texto traduzido porque não pode entrar no cache nem na memória de
/// exibição: ela é do programa, não do conteúdo.
/// </summary>
public sealed class TemporaryNotice
{
    private readonly Func<DateTime> _now;
    private string? _message;
    private DateTime _expiry;

    public TemporaryNotice(Func<DateTime>? now = null) => _now = now ?? (() => DateTime.UtcNow);

    /// <summary>Publica um aviso com prazo. Um aviso novo substitui o anterior.</summary>
    public void Show(string message, TimeSpan duration)
    {
        _message = message;
        _expiry = _now() + duration;
    }

    public void Clear() => _message = null;

    public bool IsActive => _message is not null && _now() < _expiry;

    /// <summary>Prefixa o aviso ao texto, quando ele está no prazo.</summary>
    public string Apply(string text)
    {
        if (!IsActive)
        {
            _message = null;
            return text;
        }
        return string.IsNullOrEmpty(text) ? _message! : $"{_message}\n\n{text}";
    }
}
