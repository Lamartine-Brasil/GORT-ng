using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Gort.Platform.Windows;

/// <summary>Ligação com o Win32. Único ponto do programa que fala com o sistema no Windows.</summary>
[SupportedOSPlatform("windows")]
internal static partial class Win32
{
    // ── Estruturas ───────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MONITORINFOEXW
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    internal const uint MONITORINFOF_PRIMARY = 1;
    internal const uint BI_RGB = 0;
    internal const uint DIB_RGB_COLORS = 0;

    /// <summary>SRCCOPY | CAPTUREBLT — o CAPTUREBLT inclui janelas em camadas.</summary>
    internal const uint SRCCOPY = 0x00CC0020;
    internal const uint CAPTUREBLT = 0x40000000;

    /// <summary>
    /// C8 — WDA_EXCLUDEFROMCAPTURE. É também o que satisfaz a exigência de C1 de que a
    /// janela do próprio programa saia do resultado da captura.
    /// </summary>
    internal const uint WDA_NONE = 0;
    internal const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

    internal delegate bool MonitorEnumProc(nint monitor, nint dc, ref RECT rect, nint data);

    // ── user32 ───────────────────────────────────────────────────────────────

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint GetDC(nint hWnd);

    [LibraryImport("user32.dll")]
    internal static partial int ReleaseDC(nint hWnd, nint hDC);

    [DllImport("user32.dll")]
    internal static extern bool EnumDisplayMonitors(nint hdc, nint clip,
                                                    MonitorEnumProc callback, nint data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool GetMonitorInfoW(nint monitor, ref MONITORINFOEXW info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowDisplayAffinity(nint hWnd, uint affinity);

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    // ── gdi32 ────────────────────────────────────────────────────────────────

    [LibraryImport("gdi32.dll")]
    internal static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    internal static partial nint SelectObject(nint hdc, nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool BitBlt(nint dest, int x, int y, int w, int h,
                                        nint src, int srcX, int srcY, uint rop);

    [DllImport("gdi32.dll")]
    internal static extern nint CreateDIBSection(nint hdc, ref BITMAPINFOHEADER info,
                                                  uint usage, out nint bits,
                                                  nint section, uint offset);

    // ── shcore (escala por monitor) ──────────────────────────────────────────

    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(nint monitor, int dpiType,
                                                 out uint dpiX, out uint dpiY);

    internal const int MDT_EFFECTIVE_DPI = 0;
}
