using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Gort.Platform.MacOS;

/// <summary>
/// C7 no macOS, pela mensagem <c>setIgnoresMouseEvents:</c> da janela nativa.
///
/// A ligação é por mensagem em vez de por uma API C dedicada porque o AppKit só existe em
/// Objective-C; é a única forma de alcançar a propriedade.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed partial class MacWindowEffects : IWindowEffects
{
    private const string ObjC = "/usr/lib/libobjc.dylib";

    [LibraryImport(ObjC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint sel_registerName(string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendBool(nint receiver, nint selector,
                                        [MarshalAs(UnmanagedType.I1)] bool value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint SendGet(nint receiver, nint selector);

    /// <summary>
    /// C7 — <c>ignoresMouseEvents</c>. Com ele ligado, o clique atravessa a janela e chega
    /// ao jogo por baixo, que é o que RF-334 exige durante a tradução.
    /// </summary>
    public bool SetClickThrough(nint windowHandle, bool value)
    {
        if (windowHandle == nint.Zero) return false;

        try
        {
            nint window = ResolveWindow(windowHandle);
            if (window == nint.Zero) return false;

            SendBool(window, sel_registerName("setIgnoresMouseEvents:"), value);
            return true;
        }
        catch
        {
            // P8 — a falha de um efeito não pode derrubar a janela.
            return false;
        }
    }

    /// <summary>
    /// C8 — Não existe no macOS: não há como marcar uma janela para sair das capturas
    /// feitas por OUTROS programas.
    ///
    /// RF-569 declara a degradação aceitável: a sobreposição aparece em capturas de tela, o
    /// programa documenta isso, e RF-347 — que a tornaria capturável por alguns segundos —
    /// vira inócuo.
    /// </summary>
    public bool SetExcludedFromCapture(nint windowHandle, bool value) => false;

    /// <summary>
    /// O identificador entregue pela camada de interface pode ser a janela ou a vista dela;
    /// no segundo caso, pergunta-se à vista qual é a sua janela.
    /// </summary>
    private static nint ResolveWindow(nint handle)
    {
        nint windowSelector = sel_registerName("window");
        nint maybeWindow = SendGet(handle, windowSelector);

        // Uma NSWindow responde a `window` com nulo ou consigo mesma; uma NSView responde
        // com a janela que a contém.
        return maybeWindow != nint.Zero ? maybeWindow : handle;
    }
}
