using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Gort.Core.Model;
using Gort.Platform.Capture;

namespace Gort.Platform.MacOS;

/// <summary>
/// C1 — Captura de região da tela no macOS, via CoreGraphics.
///
/// RF-100 — Funciona com múltiplos monitores, incluindo coordenadas negativas: o espaço de
/// coordenadas global do CoreGraphics tem origem no canto superior esquerdo do monitor
/// principal e Y crescendo para baixo, que é exatamente a convenção de <see cref="Rect"/>.
///
/// RF-569 — Exige permissão de gravação de tela. A ausência é detectada na inicialização
/// (RF-576), não aqui.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacCaptureBackend : ICaptureBackend
{
    private readonly List<uint> _ownWindows = new();

    /// <summary>
    /// RF-089 — A janela escolhida pelo usuário. Zero significa "nenhuma", e nesse estado a
    /// fonte de janela anexada não é utilizável.
    /// </summary>
    private uint _attachedWindow;

    /// <summary>
    /// RF-092 — Origem do conteúdo capturado, dos limites ESTENDIDOS do quadro da janela.
    ///
    /// Sombras e bordas invisíveis deslocariam todo o alinhamento da sobreposição, e é por
    /// isso que o requisito insiste no quadro estendido em vez do retângulo simples.
    /// </summary>
    private (int X, int Y) _attachedOrigin;

    public bool Supports(CaptureSource source) => source switch
    {
        CaptureSource.Screen => true,

        // A janela ativa é lida como uma região da tela, ajustada à origem do cliente
        // daquela janela; quem faz esse ajuste é o chamador, com C12.
        CaptureSource.ActiveWindow => true,

        // C2 — captura de uma janela específica, mesmo coberta. O CoreGraphics faz isso
        // com `ListOptionIncludingWindow`: a imagem sai do conteúdo DAQUELA janela, e não da
        // região da tela onde ela está, então o que estiver por cima não entra.
        //
        // Sem janela anexada a fonte não é utilizável — ela não tem de onde ler.
        CaptureSource.AttachedWindow => _attachedWindow != 0,

        _ => false,
    };

    /// <summary>
    /// C2 / RF-092 — Captura o conteúdo da janela anexada, mesmo coberta.
    ///
    /// O retângulo pedido está em coordenadas de TELA, e a imagem da janela começa na origem
    /// dela: a conversão é subtrair a origem do quadro. Sem isso, uma janela que não está no
    /// canto da tela daria um recorte deslocado — e o deslocamento cresceria conforme o
    /// usuário movesse a janela, o que é o sintoma mais confuso possível.
    ///
    /// RF-097 — quando a janela deixou de existir, o sistema não produz imagem e o índice é
    /// pulado; é o laço que decide encerrar-se, a partir de `IWindowEnumerator.Exists`.
    /// </summary>
    private CapturedRegion? CaptureAttached(int index, Rect rect)
    {
        if (_attachedWindow == 0) return null;

        var inWindow = new CoreGraphics.CGRect(
            rect.X - _attachedOrigin.X, rect.Y - _attachedOrigin.Y,
            rect.Width, rect.Height);

        nint image = CoreGraphics.CGWindowListCreateImage(
            inWindow,
            CoreGraphics.ListOptionIncludingWindow,
            _attachedWindow,
            CoreGraphics.ImageNominalResolution | CoreGraphics.ImageBoundsIgnoreFraming);

        if (image == nint.Zero) return null;

        try
        {
            var buffer = ToImageBuffer(image, rect.Width, rect.Height);
            if (buffer is null) return null;

            return new CapturedRegion
            {
                Index = index,
                Image = buffer,
                ScreenRect = rect,

                // RF-092 / RF-353 — a origem do cliente é o que a sobreposição usa para
                // pôr a tradução no lugar certo da tela.
                ClientOrigin = _attachedOrigin,
            };
        }
        finally
        {
            CoreGraphics.CGImageRelease(image);
        }
    }

    /// <summary>
    /// C1 — "com a janela do próprio programa EXCLUÍDA do resultado".
    ///
    /// No macOS isso é feito capturando apenas o que está ABAIXO da janela do programa na
    /// ordem de empilhamento. Sem isso, o modo camada capturaria a si mesmo e traduziria a
    /// própria tradução (o problema que RF-343 avisa nos outros modos).
    /// </summary>
    public void ExcludeOwnWindow(nint windowHandle)
    {
        uint id = (uint)windowHandle;
        if (id != 0 && !_ownWindows.Contains(id)) _ownWindows.Add(id);
    }

    /// <summary>
    /// RF-089 — Anexa a captura a uma janela. Zero desanexa.
    ///
    /// RF-092 — a origem vem dos limites do quadro da janela, informados pelo sistema junto
    /// com a lista: é ela que alinha as coordenadas da área de OCR ao conteúdo capturado.
    /// </summary>
    public void AttachToWindow(ulong windowId, int originX, int originY)
    {
        _attachedWindow = windowId > uint.MaxValue ? 0 : (uint)windowId;
        _attachedOrigin = (originX, originY);
    }

    internal uint AttachedWindow => _attachedWindow;

    public CapturedRegion? Capture(int index, Rect rect, CaptureSource source)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return null;

        if (source == CaptureSource.AttachedWindow) return CaptureAttached(index, rect);

        uint listOption;
        uint windowId;

        if (_ownWindows.Count > 0)
        {
            // Tudo que está abaixo da janela mais ao fundo do programa.
            listOption = CoreGraphics.ListOptionOnScreenBelowWindow
                         | CoreGraphics.ListExcludeDesktopElements;
            windowId = _ownWindows[0];
        }
        else
        {
            listOption = CoreGraphics.ListOptionOnScreenOnly
                         | CoreGraphics.ListExcludeDesktopElements;
            windowId = CoreGraphics.NullWindowID;
        }

        var bounds = new CoreGraphics.CGRect(rect.X, rect.Y, rect.Width, rect.Height);
        nint image = CoreGraphics.CGWindowListCreateImage(
            bounds, listOption, windowId, CoreGraphics.ImageNominalResolution);

        // Retângulo fora de qualquer monitor: o sistema não produz imagem. O índice é
        // pulado silenciosamente e o ciclo continua (PARTE VIII).
        if (image == nint.Zero) return null;

        try
        {
            var buffer = ToImageBuffer(image, rect.Width, rect.Height);
            if (buffer is null) return null;

            return new CapturedRegion
            {
                Index = index,
                Image = buffer,
                ScreenRect = rect,
                ClientOrigin = (0, 0),
            };
        }
        finally
        {
            CoreGraphics.CGImageRelease(image);
        }
    }

    /// <summary>
    /// Desenha a imagem num contexto de bitmap das dimensões EXATAS pedidas.
    ///
    /// Normalizar aqui é deliberado: seja qual for a resolução que o sistema devolva, a
    /// imagem entregue ao pipeline tem sempre as dimensões do retângulo pedido, que é o que
    /// o contrato de coordenadas de 6.3 exige.
    /// </summary>
    private static ImageBuffer? ToImageBuffer(nint image, int width, int height)
    {
        var pixels = new byte[(long)width * height * 4];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        nint space = nint.Zero;
        nint context = nint.Zero;

        try
        {
            space = CoreGraphics.CGColorSpaceCreateDeviceRGB();
            if (space == nint.Zero) return null;

            context = CoreGraphics.CGBitmapContextCreate(
                handle.AddrOfPinnedObject(),
                (nuint)width, (nuint)height,
                bitsPerComponent: 8,
                bytesPerRow: (nuint)(width * 4),
                space,
                CoreGraphics.BitmapInfoBgra32);

            if (context == nint.Zero) return null;

            CoreGraphics.CGContextDrawImage(
                context, new CoreGraphics.CGRect(0, 0, width, height), image);

            return new ImageBuffer(width, height, PixelFormat.Bgra32, pixels);
        }
        finally
        {
            // RF-555 — todo recurso gráfico nativo é liberado deterministicamente,
            // inclusive em caminhos de exceção.
            if (context != nint.Zero) CoreGraphics.CGContextRelease(context);
            if (space != nint.Zero) CoreGraphics.CGColorSpaceRelease(space);
            handle.Free();
        }
    }

    public void Dispose() => _ownWindows.Clear();
}

/// <summary>C18 — Enumeração de monitores no macOS.</summary>
[SupportedOSPlatform("macos")]
internal sealed class MacMonitorProvider : Monitors.IMonitorProvider
{
    private List<Monitors.MonitorInfo> _monitors = new();

    public MacMonitorProvider() => Refresh();

    public IReadOnlyList<Monitors.MonitorInfo> Monitors => _monitors;

    public void Refresh()
    {
        var list = new List<Monitors.MonitorInfo>();
        try
        {
            if (CoreGraphics.CGGetActiveDisplayList(0, null, out uint count) != 0 || count == 0)
            {
                _monitors = list;
                return;
            }

            var ids = new uint[count];
            if (CoreGraphics.CGGetActiveDisplayList(count, ids, out count) != 0)
            {
                _monitors = list;
                return;
            }

            uint main = CoreGraphics.CGMainDisplayID();

            for (int i = 0; i < count; i++)
            {
                var b = CoreGraphics.CGDisplayBounds(ids[i]);
                var bounds = new Rect(
                    (int)Math.Round(b.Origin.X), (int)Math.Round(b.Origin.Y),
                    (int)Math.Round(b.Size.Width), (int)Math.Round(b.Size.Height));

                list.Add(new Monitors.MonitorInfo(bounds, ScaleOf(ids[i]),
                                                  IsPrimary: ids[i] == main,
                                                  Name: $"display-{ids[i]}"));
            }
        }
        catch
        {
            // P8 — sem enumeração, o programa segue com a lista vazia e a capacidade C18 é
            // reportada indisponível na inicialização.
        }
        _monitors = list;
    }

    /// <summary>
    /// A escala é a razão entre a largura em PIXELS e a largura em PONTOS do modo de vídeo
    /// atual: 2,0 numa tela Retina, 1,0 numa comum.
    /// </summary>
    private static double ScaleOf(uint display)
    {
        nint mode = CoreGraphics.CGDisplayCopyDisplayMode(display);
        if (mode == nint.Zero) return 1.0;
        try
        {
            double points = CoreGraphics.CGDisplayModeGetWidth(mode);
            double pixels = CoreGraphics.CGDisplayModeGetPixelWidth(mode);
            return points > 0 ? pixels / points : 1.0;
        }
        finally
        {
            CoreGraphics.CGDisplayModeRelease(mode);
        }
    }
}
