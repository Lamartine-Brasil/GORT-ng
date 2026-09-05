using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Gort.Core.Calibration;
using Gort.Core.Model;
using Gort.Platform.Capabilities;
using Gort.Platform.Capture;
using Gort.Platform.Monitors;

namespace Gort.Platform.Windows;

/// <summary>C1 — Captura de região da tela no Windows, via GDI.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsCaptureBackend : ICaptureBackend
{
    public bool Supports(CaptureSource source) => source switch
    {
        CaptureSource.Screen => true,
        CaptureSource.ActiveWindow => true,
        // C2 — a captura de janela coberta usa a API de captura gráfica do sistema; será
        // ligada na Etapa 16, que é onde a especificação a coloca.
        CaptureSource.AttachedWindow => false,
        _ => false,
    };

    /// <summary>
    /// C1 / C8 — No Windows, excluir a própria janela do resultado é uma propriedade da
    /// janela: WDA_EXCLUDEFROMCAPTURE faz o compositor omiti-la de qualquer captura.
    /// </summary>
    public void ExcludeOwnWindow(nint windowHandle)
    {
        if (windowHandle != nint.Zero)
            Win32.SetWindowDisplayAffinity(windowHandle, Win32.WDA_EXCLUDEFROMCAPTURE);
    }

    public CapturedRegion? Capture(int index, Rect rect, CaptureSource source)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return null;

        nint sourceWindow = source == CaptureSource.ActiveWindow
            ? Win32.GetForegroundWindow()
            : nint.Zero;

        nint screenDc = Win32.GetDC(sourceWindow);
        if (screenDc == nint.Zero) return null;

        nint memDc = nint.Zero, bitmap = nint.Zero, previous = nint.Zero;
        try
        {
            memDc = Win32.CreateCompatibleDC(screenDc);
            if (memDc == nint.Zero) return null;

            // Altura NEGATIVA pede um DIB de cima para baixo, que é a ordem de linhas que
            // ImageBuffer espera.
            var header = new Win32.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
                biWidth = rect.Width,
                biHeight = -rect.Height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = Win32.BI_RGB,
            };

            bitmap = Win32.CreateDIBSection(screenDc, ref header, Win32.DIB_RGB_COLORS,
                                            out nint bits, nint.Zero, 0);
            if (bitmap == nint.Zero || bits == nint.Zero) return null;

            previous = Win32.SelectObject(memDc, bitmap);

            // RF-100 — coordenadas negativas são passadas tal e qual: o espaço de
            // coordenadas do BitBlt sobre o DC da área de trabalho já é o global.
            if (!Win32.BitBlt(memDc, 0, 0, rect.Width, rect.Height,
                              screenDc, rect.X, rect.Y, Win32.SRCCOPY | Win32.CAPTUREBLT))
            {
                return null;
            }

            var pixels = new byte[(long)rect.Width * rect.Height * 4];
            Marshal.Copy(bits, pixels, 0, pixels.Length);

            return new CapturedRegion
            {
                Index = index,
                Image = new ImageBuffer(rect.Width, rect.Height, PixelFormat.Bgra32, pixels),
                ScreenRect = rect,
                ClientOrigin = (0, 0),
            };
        }
        finally
        {
            // RF-555 — liberação determinística, inclusive por exceção.
            if (previous != nint.Zero) Win32.SelectObject(memDc, previous);
            if (bitmap != nint.Zero) Win32.DeleteObject(bitmap);
            if (memDc != nint.Zero) Win32.DeleteDC(memDc);
            Win32.ReleaseDC(sourceWindow, screenDc);
        }
    }

    public void Dispose() { }
}

/// <summary>C18 — Monitores e escalas no Windows.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsMonitorProvider : IMonitorProvider
{
    private List<MonitorInfo> _monitors = new();

    public WindowsMonitorProvider() => Refresh();

    public IReadOnlyList<MonitorInfo> Monitors => _monitors;

    public void Refresh()
    {
        var list = new List<MonitorInfo>();
        try
        {
            Win32.EnumDisplayMonitors(nint.Zero, nint.Zero,
                (nint monitor, nint dc, ref Win32.RECT rect, nint data) =>
                {
                    var info = new Win32.MONITORINFOEXW
                    {
                        cbSize = Marshal.SizeOf<Win32.MONITORINFOEXW>(),
                    };
                    if (!Win32.GetMonitorInfoW(monitor, ref info)) return true;

                    double scale = 1.0;
                    try
                    {
                        if (Win32.GetDpiForMonitor(monitor, Win32.MDT_EFFECTIVE_DPI,
                                                   out uint dpiX, out _) == 0 && dpiX > 0)
                        {
                            scale = dpiX / P.ReferenceDpi;
                        }
                    }
                    catch
                    {
                        // Sistemas anteriores ao suporte por monitor ficam em 1,0.
                    }

                    var b = info.rcMonitor;
                    list.Add(new MonitorInfo(
                        new Rect(b.Left, b.Top, b.Width, b.Height),
                        scale,
                        (info.dwFlags & Win32.MONITORINFOF_PRIMARY) != 0,
                        info.szDevice ?? ""));
                    return true;
                }, nint.Zero);
        }
        catch
        {
            // P8
        }
        _monitors = list;
    }
}

/// <summary>Serviços de plataforma do Windows.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsPlatformServices : IPlatformServices
{
    private readonly WindowsCaptureBackend _backend = new();

    public WindowsPlatformServices()
    {
        Monitors = new WindowsMonitorProvider();
        Capture = new ScreenCapture(_backend, Monitors);
        Capabilities = Detect(Monitors);
    }

    public string PlatformName => "Windows";
    public CapabilityReport Capabilities { get; }
    public ScreenCapture Capture { get; }
    public IMonitorProvider Monitors { get; }

    /// <summary>
    /// C10 — Será ligado com a implementação específica desta plataforma; até lá, o gancho
    /// é inerte e explica o motivo, e o usuário opera pelo controle remoto (RF-569).
    /// </summary>
    public IWindowEffects WindowEffects { get; } = new NoWindowEffects();

    public Gort.Platform.Input.ICursorPosition Cursor { get; } =
        new Gort.Platform.Input.NoCursorPosition();

    /// <summary>RF-573 — A síntese de voz pode não existir; a opção fica desabilitada.</summary>
    public Gort.Platform.Input.ITextToSpeech Speech { get; } =
        new Gort.Platform.Input.NoTextToSpeech(
            "A síntese de voz ainda não está ligada nesta plataforma.");

    public Gort.Platform.Input.IGlobalKeyboardHook Keyboard { get; } =
        new Gort.Platform.Input.InactiveKeyboardHook(
            "O interceptador global de teclado ainda não está ligado nesta plataforma.");

    private static CapabilityReport Detect(IMonitorProvider monitors)
    {
        var list = new List<CapabilityStatus>
        {
            // O Windows não exige consentimento para C1, C10 e C12.
            CapabilityStatus.Ok(Capability.ScreenRegionCapture),
            CapabilityStatus.Ok(Capability.WindowFrameBounds),
            CapabilityStatus.Ok(Capability.AlwaysOnTop),
            CapabilityStatus.Ok(Capability.PerPixelTransparency),
            CapabilityStatus.Ok(Capability.ClickThrough),
            CapabilityStatus.Ok(Capability.ExcludeFromCapture),
            CapabilityStatus.Ok(Capability.CompositorSync),
            CapabilityStatus.Ok(Capability.GlobalHotkeys),
            CapabilityStatus.Ok(Capability.ScreenshotKeyDetection),
            CapabilityStatus.Ok(Capability.ForegroundWindowInfo),
            CapabilityStatus.Ok(Capability.TrayIcon),
            CapabilityStatus.Ok(Capability.Clipboard),
            CapabilityStatus.Ok(Capability.SpeechSynthesis),
            CapabilityStatus.Ok(Capability.VectorTextOutline),
            CapabilityStatus.Ok(Capability.TextMeasurement),
            CapabilityStatus.Ok(Capability.AuxiliaryProcessChannel),

            // Ligadas na Etapa 16, que é onde a especificação as coloca.
            CapabilityStatus.Missing(Capability.WindowCapture, UnavailabilityKind.NotSupported,
                "A captura de janela anexada será ligada na etapa correspondente."),
            CapabilityStatus.Missing(Capability.WindowPicker, UnavailabilityKind.NotSupported,
                "O seletor de janelas será ligado junto com a captura anexada."),

            // RF-575 — ligado na Etapa 14.
            CapabilityStatus.Missing(Capability.SystemTextRecognition,
                UnavailabilityKind.NotSupported,
                "O reconhecimento de texto do sistema ainda não está ligado."),

            monitors.Monitors.Count > 0
                ? CapabilityStatus.Ok(Capability.MonitorEnumeration)
                : CapabilityStatus.Missing(Capability.MonitorEnumeration,
                    UnavailabilityKind.InitializationFailed,
                    "Não foi possível enumerar os monitores."),
        };
        return new CapabilityReport(list);
    }

    public CapabilityStatus RequestPermission(Capability capability) => Capabilities[capability];

    public bool OpenPermissionSettings(Capability capability) => false;

    public void Dispose() => _backend.Dispose();
}
