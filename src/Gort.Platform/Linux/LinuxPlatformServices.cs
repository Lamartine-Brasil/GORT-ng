using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Gort.Core.Model;
using Gort.Platform.Capabilities;
using Gort.Platform.Capture;
using Gort.Platform.Monitors;

namespace Gort.Platform.Linux;

/// <summary>Ligação com X11 e Xinerama.</summary>
[SupportedOSPlatform("linux")]
internal static partial class X11
{
    private const string LibX11 = "libX11.so.6";
    private const string LibXinerama = "libXinerama.so.1";

    internal const int ZPixmap = 2;
    internal const ulong AllPlanes = ~0UL;

    [StructLayout(LayoutKind.Sequential)]
    internal struct XineramaScreenInfo
    {
        public int ScreenNumber;
        public short XOrg, YOrg, Width, Height;
    }

    [LibraryImport(LibX11, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint XOpenDisplay(string? name);

    [LibraryImport(LibX11)]
    internal static partial int XCloseDisplay(nint display);

    [LibraryImport(LibX11)]
    internal static partial nint XDefaultRootWindow(nint display);

    [LibraryImport(LibX11)]
    internal static partial nint XGetImage(nint display, nint drawable,
                                           int x, int y, uint width, uint height,
                                           ulong planeMask, int format);

    [LibraryImport(LibX11)]
    internal static partial int XDestroyImage(nint image);

    [LibraryImport(LibXinerama)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool XineramaIsActive(nint display);

    [LibraryImport(LibXinerama)]
    internal static partial nint XineramaQueryScreens(nint display, out int number);

    [LibraryImport(LibX11)]
    internal static partial int XFree(nint data);

    /// <summary>
    /// Campos de XImage de que precisamos. O deslocamento depende do layout da struct C,
    /// que é estável há décadas: width, height, xoffset, format, data, byte_order, ...
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct XImage
    {
        public int Width, Height;
        public int XOffset;
        public int Format;
        public nint Data;
        public int ByteOrder;
        public int BitmapUnit;
        public int BitmapBitOrder;
        public int BitmapPad;
        public int Depth;
        public int BytesPerLine;
        public int BitsPerPixel;
    }
}

/// <summary>
/// C1 — Captura de região da tela em sessões X11.
///
/// RF-568 — Em sessões que não permitem que a aplicação posicione a própria janela nem
/// force "sempre no topo" (notadamente Wayland), C1 e C10 exigem portais com consentimento
/// explícito do usuário. Aqui a sessão é detectada na inicialização e, sob Wayland, a
/// captura por X11 é reportada indisponível em vez de devolver quadros pretos.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxCaptureBackend : ICaptureBackend
{
    private nint _display;

    public LinuxCaptureBackend()
    {
        try
        {
            _display = X11.XOpenDisplay(null);
        }
        catch
        {
            _display = nint.Zero;
        }
    }

    public bool IsAvailable => _display != nint.Zero;

    public bool Supports(CaptureSource source)
        => IsAvailable && source is CaptureSource.Screen or CaptureSource.ActiveWindow;

    /// <summary>
    /// C1 — No X11 não há como marcar uma janela para sair de uma captura de terceiros. A
    /// exclusão da própria janela é obtida ocultando-a durante a captura, o que cabe a quem
    /// implementa as janelas; aqui não há o que fazer.
    /// </summary>
    public void ExcludeOwnWindow(nint windowHandle) { }

    public CapturedRegion? Capture(int index, Rect rect, CaptureSource source)
    {
        if (!IsAvailable || rect.Width <= 0 || rect.Height <= 0) return null;

        nint root = X11.XDefaultRootWindow(_display);
        nint image = X11.XGetImage(_display, root, rect.X, rect.Y,
                                   (uint)rect.Width, (uint)rect.Height,
                                   X11.AllPlanes, X11.ZPixmap);
        if (image == nint.Zero) return null;

        try
        {
            var header = Marshal.PtrToStructure<X11.XImage>(image);
            if (header.Data == nint.Zero || header.BitsPerPixel != 32) return null;

            var pixels = new byte[(long)rect.Width * rect.Height * 4];
            int stride = rect.Width * 4;

            // O XImage pode ter passo de linha maior que a largura útil; ImageBuffer exige
            // linhas sem preenchimento (7.1), então copiamos linha a linha.
            for (int y = 0; y < rect.Height; y++)
            {
                Marshal.Copy(header.Data + y * header.BytesPerLine,
                             pixels, y * stride, stride);
            }

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
            X11.XDestroyImage(image);
        }
    }

    public void Dispose()
    {
        if (_display != nint.Zero)
        {
            X11.XCloseDisplay(_display);
            _display = nint.Zero;
        }
    }
}

/// <summary>C18 — Monitores via Xinerama.</summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxMonitorProvider : IMonitorProvider
{
    private List<MonitorInfo> _monitors = new();

    public LinuxMonitorProvider() => Refresh();

    public IReadOnlyList<MonitorInfo> Monitors => _monitors;

    public void Refresh()
    {
        var list = new List<MonitorInfo>();
        nint display = nint.Zero;
        try
        {
            display = X11.XOpenDisplay(null);
            if (display == nint.Zero) { _monitors = list; return; }

            nint screens = X11.XineramaQueryScreens(display, out int count);
            if (screens == nint.Zero || count <= 0) { _monitors = list; return; }

            try
            {
                int size = Marshal.SizeOf<X11.XineramaScreenInfo>();
                for (int i = 0; i < count; i++)
                {
                    var s = Marshal.PtrToStructure<X11.XineramaScreenInfo>(screens + i * size);
                    list.Add(new MonitorInfo(
                        new Rect(s.XOrg, s.YOrg, s.Width, s.Height),
                        // O X11 não expõe uma escala por monitor de forma portátil; a
                        // convenção do ambiente costuma vir por variável de ambiente.
                        Scale: EnvironmentScale(),
                        IsPrimary: i == 0,
                        Name: $"xinerama-{s.ScreenNumber}"));
                }
            }
            finally
            {
                X11.XFree(screens);
            }
        }
        catch
        {
            // P8
        }
        finally
        {
            if (display != nint.Zero) X11.XCloseDisplay(display);
        }
        _monitors = list;
    }

    private static double EnvironmentScale()
    {
        string? v = Environment.GetEnvironmentVariable("GDK_SCALE");
        return double.TryParse(v, out double s) && s > 0 ? s : 1.0;
    }
}

/// <summary>Serviços de plataforma para Linux.</summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxPlatformServices : IPlatformServices
{
    private readonly LinuxCaptureBackend _backend = new();

    public LinuxPlatformServices()
    {
        Monitors = new LinuxMonitorProvider();
        Capture = new ScreenCapture(_backend, Monitors);
        Capabilities = Detect(_backend, Monitors);
    }

    public string PlatformName => IsWayland ? "Linux (Wayland)" : "Linux (X11)";
    public CapabilityReport Capabilities { get; }
    public ScreenCapture Capture { get; }
    public IMonitorProvider Monitors { get; }

    /// <summary>
    /// C10 — Será ligado com a implementação específica desta plataforma; até lá, o gancho
    /// é inerte e explica o motivo, e o usuário opera pelo controle remoto (RF-569).
    /// Sob Wayland, C10 exige um portal com consentimento explícito (RF-568).
    /// </summary>
    public Gort.Platform.Input.IGlobalKeyboardHook Keyboard { get; } =
        new Gort.Platform.Input.InactiveKeyboardHook(
            "O interceptador global de teclado ainda não está ligado nesta plataforma.");

    /// <summary>RF-568 — A sessão gráfica muda o que é possível.</summary>
    internal static bool IsWayland
        => string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
                         "wayland", StringComparison.OrdinalIgnoreCase)
           || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

    private static CapabilityReport Detect(LinuxCaptureBackend backend, IMonitorProvider monitors)
    {
        var list = new List<CapabilityStatus>();

        if (IsWayland)
        {
            // RF-568 — degradação aceitável, item a item.
            const string portal =
                "Nesta sessão Wayland a captura depende de um portal com consentimento " +
                "explícito do usuário, que ainda não está implementado.";

            list.Add(CapabilityStatus.Missing(Capability.ScreenRegionCapture,
                UnavailabilityKind.PermissionRequired, portal));
            list.Add(CapabilityStatus.Missing(Capability.GlobalHotkeys,
                UnavailabilityKind.PermissionRequired, portal));
            list.Add(CapabilityStatus.Missing(Capability.AlwaysOnTop,
                UnavailabilityKind.NotSupported,
                "O Wayland não garante que uma aplicação possa manter a própria janela " +
                "acima das outras: a janela de tradução pode ficar atrás do jogo. " +
                "A alternativa é o modo escuro em uma janela normal, posicionada à mão."));
            list.Add(CapabilityStatus.Missing(Capability.ForegroundWindowInfo,
                UnavailabilityKind.NotSupported,
                "O Wayland não expõe a janela em primeiro plano às aplicações."));
        }
        else
        {
            list.Add(backend.IsAvailable
                ? CapabilityStatus.Ok(Capability.ScreenRegionCapture)
                : CapabilityStatus.Missing(Capability.ScreenRegionCapture,
                    UnavailabilityKind.InitializationFailed,
                    "Não foi possível abrir a conexão com o servidor X."));
            list.Add(CapabilityStatus.Ok(Capability.GlobalHotkeys));
            list.Add(CapabilityStatus.Ok(Capability.AlwaysOnTop));
            list.Add(CapabilityStatus.Ok(Capability.ForegroundWindowInfo));
        }

        list.Add(CapabilityStatus.Ok(Capability.PerPixelTransparency));
        list.Add(CapabilityStatus.Ok(Capability.ClickThrough));
        list.Add(CapabilityStatus.Ok(Capability.WindowFrameBounds));
        list.Add(CapabilityStatus.Ok(Capability.Clipboard));
        list.Add(CapabilityStatus.Ok(Capability.VectorTextOutline));
        list.Add(CapabilityStatus.Ok(Capability.TextMeasurement));
        list.Add(CapabilityStatus.Ok(Capability.AuxiliaryProcessChannel));
        list.Add(CapabilityStatus.Ok(Capability.TrayIcon));

        // RF-569 análogo: não há como excluir a janela de capturas de terceiros.
        list.Add(CapabilityStatus.Missing(Capability.ExcludeFromCapture,
            UnavailabilityKind.NotSupported,
            "Não é possível excluir uma janela das capturas feitas por outros programas."));
        list.Add(CapabilityStatus.Missing(Capability.ScreenshotKeyDetection,
            UnavailabilityKind.NotSupported,
            "Não é possível detectar o atalho de captura de tela do ambiente."));

        // RF-571 — sem sincronização com o compositor.
        list.Add(CapabilityStatus.Missing(Capability.CompositorSync,
            UnavailabilityKind.NotSupported,
            "Sem sincronização explícita com o compositor; o primeiro quadro pode piscar."));

        list.Add(CapabilityStatus.Missing(Capability.WindowCapture,
            UnavailabilityKind.NotSupported,
            "A captura de janela anexada será ligada na etapa correspondente."));
        list.Add(CapabilityStatus.Missing(Capability.WindowPicker,
            UnavailabilityKind.NotSupported,
            "O seletor de janelas será ligado junto com a captura anexada."));

        // RF-573 — a síntese de voz pode não existir; a opção fica desabilitada com explicação.
        list.Add(CapabilityStatus.Missing(Capability.SpeechSynthesis,
            UnavailabilityKind.NotSupported,
            "Nenhum sintetizador de voz do sistema foi encontrado."));

        // RF-574 — o tradutor local depende de biblioteca proprietária de outro sistema; em
        // plataformas onde ela não existe, o serviço nem aparece na lista de tradutores.
        // RF-575 — C20 varia por plataforma.
        list.Add(CapabilityStatus.Missing(Capability.SystemTextRecognition,
            UnavailabilityKind.NotSupported,
            "Não há reconhecimento de texto oferecido pelo sistema nesta plataforma."));

        list.Add(monitors.Monitors.Count > 0
            ? CapabilityStatus.Ok(Capability.MonitorEnumeration)
            : CapabilityStatus.Missing(Capability.MonitorEnumeration,
                UnavailabilityKind.InitializationFailed,
                "Não foi possível enumerar os monitores."));

        return new CapabilityReport(list);
    }

    public CapabilityStatus RequestPermission(Capability capability) => Capabilities[capability];

    public bool OpenPermissionSettings(Capability capability) => false;

    public void Dispose() => _backend.Dispose();
}
