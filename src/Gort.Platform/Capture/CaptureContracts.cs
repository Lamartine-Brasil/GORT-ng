using Gort.Core.Model;

namespace Gort.Platform.Capture;

/// <summary>
/// RF-088 — As três fontes de imagem suportadas.
/// Persistida por identificador textual quando entra em configuração (RF-026).
/// </summary>
public enum CaptureSource
{
    /// <summary>Lê o conteúdo atual da área de trabalho nas coordenadas dadas.</summary>
    Screen,

    /// <summary>
    /// Lê a partir da janela que está em primeiro plano, ajustando as coordenadas à origem
    /// do cliente dessa janela.
    /// </summary>
    ActiveWindow,

    /// <summary>
    /// Lê de uma janela específica escolhida pelo usuário, independentemente de ela estar
    /// coberta ou não.
    /// </summary>
    AttachedWindow,
}

/// <summary>
/// 6.2 — O que a captura recebe: uma lista de retângulos, a fonte, e se a imagem ORIGINAL
/// (sem tratamento) também é necessária.
/// </summary>
public sealed class CaptureRequest
{
    /// <summary>Retângulos em coordenadas absolutas de tela, já alinhados por RF-077.</summary>
    public required IReadOnlyList<Rect> Rects { get; init; }

    public CaptureSource Source { get; init; } = CaptureSource.Screen;

    /// <summary>
    /// RF-098 — Verdadeiro apenas quando o modo é sobreposição E a cor automática está
    /// ativa. Só nesse caso a captura devolve duas versões de cada região.
    /// </summary>
    public bool NeedsOriginal { get; init; }
}

/// <summary>
/// 6.2 / 7.1 — O que a captura devolve para UM retângulo.
///
/// Casos vazios: se um retângulo não produz imagem, aquele índice é simplesmente AUSENTE
/// da lista devolvida — não é um erro (RF-100, PARTE VIII).
/// </summary>
public sealed class CapturedRegion
{
    /// <summary>Índice do retângulo na requisição.</summary>
    public required int Index { get; init; }

    /// <summary>Imagem bruta da região, antes do pré-processamento.</summary>
    public required ImageBuffer Image { get; set; }

    /// <summary>
    /// RF-554 / RF-099 — Solta os pixels assim que a região não é mais necessária.
    ///
    /// Com ampliação, cada região ocupa dezenas de megabytes. Segurar todas até o fim do
    /// ciclo faz o pico de memória crescer com o NÚMERO DE ÁREAS, e é justamente com muitas
    /// áreas que o usuário está usando o programa no limite.
    /// </summary>
    public void Release() => Image = ImageBuffer.Allocate(0, 0, Image.Format);

    /// <summary>Retângulo efetivamente capturado, em coordenadas de tela.</summary>
    public required Rect ScreenRect { get; init; }

    /// <summary>
    /// Posição de origem do cliente capturado — relevante no modo janela anexada (RF-092,
    /// RF-353). Na captura de tela é (0, 0).
    /// </summary>
    public (int X, int Y) ClientOrigin { get; init; }
}

/// <summary>
/// C1 / C2 — Contrato da captura, por trás da abstração de RF-577.
///
/// Não decide QUANDO capturar e não interpreta o conteúdo (6.2).
/// </summary>
public interface ICaptureBackend : IDisposable
{
    /// <summary>Fontes que esta implementação consegue atender neste sistema.</summary>
    bool Supports(CaptureSource source);

    /// <summary>
    /// C1 — Captura um único retângulo em coordenadas globais, incluindo coordenadas
    /// negativas (RF-100), com a janela do próprio programa excluída do resultado.
    /// Devolve null quando o retângulo não produz imagem.
    /// </summary>
    CapturedRegion? Capture(int index, Rect rect, CaptureSource source);

    /// <summary>
    /// C1 — Exclui uma janela do próprio programa do resultado da captura de tela.
    /// Chamado quando as janelas do programa são criadas.
    /// </summary>
    void ExcludeOwnWindow(nint windowHandle);
}

/// <summary>
/// Cap. 12 — Captura de tela: transforma retângulos em imagens de pixels.
///
/// Esta classe é IDÊNTICA em todas as plataformas (RF-577); só o
/// <see cref="ICaptureBackend"/> muda.
/// </summary>
public sealed class ScreenCapture
{
    private readonly ICaptureBackend _backend;
    private readonly Monitors.IMonitorProvider? _monitors;

    public ScreenCapture(ICaptureBackend backend, Monitors.IMonitorProvider? monitors = null)
    {
        _backend = backend;
        _monitors = monitors;
    }

    public bool Supports(CaptureSource source) => _backend.Supports(source);

    public void ExcludeOwnWindow(nint handle) => _backend.ExcludeOwnWindow(handle);

    /// <summary>
    /// 6.2 — Captura todos os retângulos da requisição.
    ///
    /// Casos vazios: um retângulo que não produz imagem tem seu índice simplesmente ausente
    /// do resultado; não é erro e o ciclo continua com as demais regiões (PARTE VIII).
    /// </summary>
    public List<CapturedRegion> Capture(CaptureRequest request)
    {
        var result = new List<CapturedRegion>(request.Rects.Count);
        for (int i = 0; i < request.Rects.Count; i++)
        {
            var rect = request.Rects[i];

            // Caso de erro do cap. 11: área de largura ou altura 0 após ajustes é forçada
            // a 1 px, em vez de descartada aqui.
            if (rect.Width <= 0) rect = rect with { Width = 1 };
            if (rect.Height <= 0) rect = rect with { Height = 1 };

            // PARTE VIII, "Região fora da tela": a captura não produz imagem, o índice é
            // pulado e o ciclo continua com as demais regiões.
            //
            // A verificação é feita AQUI, e não em cada backend, porque a regra é da
            // especificação e não do sistema: alguns sistemas devolvem uma imagem vazia em
            // vez de recusar, e uma imagem vazia entraria no OCR como texto em branco.
            // Só vale para a captura de tela; nas outras fontes as coordenadas são
            // relativas à janela, não à área de trabalho.
            if (request.Source == CaptureSource.Screen && !IntersectsDesktop(rect))
            {
                continue;
            }

            CapturedRegion? region;
            try
            {
                region = _backend.Capture(i, rect, request.Source);
            }
            catch
            {
                // P8 / RF-561 — uma falha de captura degrada para "índice pulado", nunca
                // para uma exceção que encerre o laço.
                region = null;
            }

            if (region is not null) result.Add(region);
        }
        return result;
    }

    /// <summary>
    /// Verdadeiro quando o retângulo tem alguma parte sobre algum monitor. Sem provedor de
    /// monitores, assume-se que sim — nunca descartar por falta de informação (P7).
    /// </summary>
    private bool IntersectsDesktop(Rect rect)
    {
        var monitors = _monitors?.Monitors;
        if (monitors is null || monitors.Count == 0) return true;

        foreach (var m in monitors)
        {
            if (m.Bounds.IntersectsWith(rect)) return true;
        }
        return false;
    }
}
