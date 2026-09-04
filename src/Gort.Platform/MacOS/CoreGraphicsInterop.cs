using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Gort.Platform.MacOS;

/// <summary>
/// Ligação com o CoreGraphics do macOS. É o único lugar do programa que fala com o sistema
/// nesta plataforma — RF-577 exige que tudo acima da abstração seja idêntico em todos os
/// sistemas.
/// </summary>
[SupportedOSPlatform("macos")]
internal static partial class CoreGraphics
{
    private const string Lib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    // CGFloat é double em 64 bits.
    [StructLayout(LayoutKind.Sequential)]
    internal struct CGPoint
    {
        public double X, Y;
        public CGPoint(double x, double y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CGSize
    {
        public double Width, Height;
        public CGSize(double w, double h) { Width = w; Height = h; }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CGRect
    {
        public CGPoint Origin;
        public CGSize Size;
        public CGRect(double x, double y, double w, double h)
        {
            Origin = new CGPoint(x, y);
            Size = new CGSize(w, h);
        }
    }

    // ── Opções de CGWindowListCreateImage ────────────────────────────────────

    internal const uint ListOptionOnScreenOnly = 1u << 0;
    internal const uint ListOptionOnScreenBelowWindow = 1u << 2;
    internal const uint ListExcludeDesktopElements = 1u << 4;

    internal const uint ImageDefault = 0;
    internal const uint ImageBoundsIgnoreFraming = 1u << 0;

    /// <summary>
    /// Uma imagem com 1 pixel por PONTO.
    ///
    /// A escolha é obrigatória, não uma preferência de qualidade: o contrato de coordenadas
    /// de 6.3 diz que voltar do espaço da imagem para o da tela é "dividir pelo fator de
    /// ampliação e somar a origem da área". Isso só vale se a imagem capturada for 1:1 com
    /// as coordenadas de tela. Capturar em resolução nativa numa tela Retina dobraria a
    /// escala silenciosamente e desalinharia toda a sobreposição.
    /// </summary>
    internal const uint ImageNominalResolution = 1u << 4;

    internal const uint NullWindowID = 0;

    // ── Bitmap ───────────────────────────────────────────────────────────────

    private const uint AlphaPremultipliedFirst = 2;
    private const uint ByteOrder32Little = 2u << 12;

    /// <summary>BGRA em memória, que é exatamente <c>PixelFormat.Bgra32</c>.</summary>
    internal const uint BitmapInfoBgra32 = AlphaPremultipliedFirst | ByteOrder32Little;

    // ── Monitores (C18) ──────────────────────────────────────────────────────

    [LibraryImport(Lib)]
    internal static partial uint CGMainDisplayID();

    [LibraryImport(Lib)]
    internal static partial int CGGetActiveDisplayList(uint maxDisplays,
                                                       [Out] uint[]? activeDisplays,
                                                       out uint displayCount);

    [LibraryImport(Lib)]
    internal static partial CGRect CGDisplayBounds(uint display);

    [LibraryImport(Lib)]
    internal static partial nint CGDisplayCopyDisplayMode(uint display);

    [LibraryImport(Lib)]
    internal static partial nuint CGDisplayModeGetWidth(nint mode);

    [LibraryImport(Lib)]
    internal static partial nuint CGDisplayModeGetPixelWidth(nint mode);

    [LibraryImport(Lib)]
    internal static partial void CGDisplayModeRelease(nint mode);

    // ── Captura (C1 / C2) ────────────────────────────────────────────────────

    [LibraryImport(Lib)]
    internal static partial nint CGWindowListCreateImage(CGRect screenBounds,
                                                         uint listOption,
                                                         uint windowId,
                                                         uint imageOption);

    [LibraryImport(Lib)]
    internal static partial nuint CGImageGetWidth(nint image);

    [LibraryImport(Lib)]
    internal static partial nuint CGImageGetHeight(nint image);

    [LibraryImport(Lib)]
    internal static partial void CGImageRelease(nint image);

    // ── Contexto de bitmap ───────────────────────────────────────────────────

    [LibraryImport(Lib)]
    internal static partial nint CGColorSpaceCreateDeviceRGB();

    [LibraryImport(Lib)]
    internal static partial void CGColorSpaceRelease(nint space);

    [LibraryImport(Lib)]
    internal static partial nint CGBitmapContextCreate(nint data, nuint width, nuint height,
                                                       nuint bitsPerComponent, nuint bytesPerRow,
                                                       nint space, uint bitmapInfo);

    [LibraryImport(Lib)]
    internal static partial void CGContextDrawImage(nint context, CGRect rect, nint image);

    [LibraryImport(Lib)]
    internal static partial void CGContextRelease(nint context);

    // ── Permissões (RF-569) ──────────────────────────────────────────────────

    /// <summary>
    /// RF-569 — Verifica a permissão de gravação de tela SEM pedi-la. É o que permite
    /// detectar a ausência na inicialização (RF-576) em vez de descobri-la no meio de uma
    /// tradução.
    /// </summary>
    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CGPreflightScreenCaptureAccess();

    /// <summary>
    /// RF-569 — Solicita a permissão, o que faz o sistema exibir o pedido ao usuário.
    /// </summary>
    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CGRequestScreenCaptureAccess();
}
