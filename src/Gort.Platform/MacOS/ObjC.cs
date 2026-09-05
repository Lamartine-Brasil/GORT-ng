using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Gort.Platform.MacOS;

/// <summary>
/// Envio de mensagens ao Objective-C.
///
/// Existe porque várias capacidades do macOS — C7 e C20 entre elas — só têm interface em
/// Objective-C: não há API em C para alcançá-las. Fica confinado aqui, atrás da abstração de
/// RF-577, e nada acima da camada de plataforma sabe que ele existe.
/// </summary>
[SupportedOSPlatform("macos")]
internal static partial class ObjC
{
    private const string Lib = "/usr/lib/libobjc.dylib";
    private const string Foundation =
        "/System/Library/Frameworks/Foundation.framework/Foundation";

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint objc_getClass(string name);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint sel_registerName(string name);

    // O envio de mensagem tem uma assinatura por forma de chamada: o marshalling precisa
    // saber o tamanho e o tipo exatos de cada argumento e do retorno.

    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    internal static extern nint Send(nint receiver, nint selector);

    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    internal static extern nint Send(nint receiver, nint selector, nint a);

    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    internal static extern nint Send(nint receiver, nint selector, nint a, nint b);

    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    internal static extern nint Send(nint receiver, nint selector, nint a, nint b, nint c);

    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    internal static extern void SendVoid(nint receiver, nint selector, nint a);

    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    internal static extern void SendVoidLong(nint receiver, nint selector, long a);

    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SendBoolResult(nint receiver, nint selector, nint a, nint b);

    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    internal static extern nint SendULong(nint receiver, nint selector, ulong a);

    /// <summary>
    /// Um retângulo devolvido por mensagem. Em ARM64 uma estrutura deste tamanho volta por
    /// referência indireta, e o marshalling do runtime cuida disso.
    /// </summary>
    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    internal static extern CoreGraphics.CGRect SendRect(nint receiver, nint selector);

    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    internal static extern nuint SendCount(nint receiver, nint selector);

    // ── Objetos comuns ───────────────────────────────────────────────────────

    internal static nint New(string className)
    {
        nint cls = objc_getClass(className);
        if (cls == nint.Zero) return nint.Zero;

        nint instance = Send(cls, sel_registerName("alloc"));
        return instance == nint.Zero ? nint.Zero : Send(instance, sel_registerName("init"));
    }

    internal static void Release(nint obj)
    {
        if (obj != nint.Zero) Send(obj, sel_registerName("release"));
    }

    [LibraryImport(Foundation, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint NSSelectorFromString(string name);

    /// <summary>Cria uma NSString a partir de uma cadeia gerenciada.</summary>
    internal static nint NSString(string value)
    {
        nint cls = objc_getClass("NSString");
        if (cls == nint.Zero) return nint.Zero;

        nint utf8 = Marshal.StringToHGlobalAnsi(value);
        try
        {
            // 4 = NSUTF8StringEncoding
            return SendStringWithEncoding(cls, sel_registerName("stringWithUTF8String:"), utf8);
        }
        finally
        {
            Marshal.FreeHGlobal(utf8);
        }
    }

    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    private static extern nint SendStringWithEncoding(nint receiver, nint selector, nint utf8);

    /// <summary>Lê uma NSString como cadeia gerenciada, decodificando UTF-8 (RF-143).</summary>
    internal static string ReadString(nint nsString)
    {
        if (nsString == nint.Zero) return "";

        nint utf8 = Send(nsString, sel_registerName("UTF8String"));
        if (utf8 == nint.Zero) return "";

        return Marshal.PtrToStringUTF8(utf8) ?? "";
    }

    /// <summary>Cria um NSArray com os objetos dados.</summary>
    internal static nint NSArray(params nint[] items)
    {
        nint cls = objc_getClass("NSArray");
        if (cls == nint.Zero) return nint.Zero;

        if (items.Length == 0) return Send(cls, sel_registerName("array"));

        var handle = GCHandle.Alloc(items, GCHandleType.Pinned);
        try
        {
            return SendArray(cls, sel_registerName("arrayWithObjects:count:"),
                             handle.AddrOfPinnedObject(), (nuint)items.Length);
        }
        finally
        {
            handle.Free();
        }
    }

    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    private static extern nint SendArray(nint receiver, nint selector, nint objects, nuint count);

    internal static nuint ArrayCount(nint array)
        => array == nint.Zero ? 0 : SendCount(array, sel_registerName("count"));

    internal static nint ArrayAt(nint array, nuint index)
        => array == nint.Zero ? nint.Zero
            : SendULong(array, sel_registerName("objectAtIndex:"), index);
}
