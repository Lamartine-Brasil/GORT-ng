using System.Diagnostics;
using System.Runtime.Versioning;
using Gort.Platform.Capabilities;
using Gort.Platform.Capture;
using Gort.Platform.Monitors;

namespace Gort.Platform.MacOS;

/// <summary>
/// Serviços de plataforma do macOS.
///
/// RF-569 — O macOS exige permissão de GRAVAÇÃO DE TELA para C1, C2 e C3, e permissão de
/// ACESSIBILIDADE para C10 e C12. O programa detecta a ausência, explica em texto claro
/// qual permissão falta e para quê, e oferece abrir a tela de configuração correspondente.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacPlatformServices : IPlatformServices
{
    private readonly MacCaptureBackend _backend = new();

    public MacPlatformServices()
    {
        Monitors = new MacMonitorProvider();
        Capture = new ScreenCapture(_backend, Monitors);
        Capabilities = Detect(Monitors);

        // RF-569 — sem a permissão de Acessibilidade, o gancho não é sequer tentado: a
        // capacidade já foi reportada indisponível e o usuário opera pelo controle remoto.
        Keyboard = Capabilities.Has(Capability.GlobalHotkeys)
            ? new MacKeyboardHook()
            : new Gort.Platform.Input.InactiveKeyboardHook(
                Capabilities[Capability.GlobalHotkeys].Explanation);
    }

    public string PlatformName => "macOS";
    public CapabilityReport Capabilities { get; private set; }
    public ScreenCapture Capture { get; }
    public IMonitorProvider Monitors { get; }
    public Gort.Platform.Input.IGlobalKeyboardHook Keyboard { get; }

    /// <summary>RF-576 — Tudo apurado uma única vez, na inicialização.</summary>
    private static CapabilityReport Detect(IMonitorProvider monitors)
    {
        var list = new List<CapabilityStatus>();

        bool screenRecording = HasScreenRecordingPermission();

        // RF-569 — sem permissão de gravação de tela, NENHUMA tradução é possível; o
        // programa deve dizer isso e não iniciar.
        foreach (var c in new[]
                 {
                     Capability.ScreenRegionCapture,
                     Capability.WindowCapture,
                     Capability.WindowPicker,
                 })
        {
            list.Add(screenRecording
                ? (c == Capability.ScreenRegionCapture
                    ? CapabilityStatus.Ok(c)
                    : CapabilityStatus.Missing(c, UnavailabilityKind.NotSupported,
                        "A captura de uma janela específica ainda não está implementada no " +
                        "macOS. Use a captura de tela."))
                : CapabilityStatus.Missing(c, UnavailabilityKind.PermissionRequired,
                    "O macOS exige permissão de Gravação de Tela para que o programa possa " +
                    "ler os pixels da tela. Sem ela nenhuma tradução é possível.",
                    "Ajustes do Sistema › Privacidade e Segurança › Gravação de Tela"));
        }

        // C10 e C12 dependem de Acessibilidade. RF-569 — sem ela, os atalhos globais ficam
        // indisponíveis e o usuário deve usar o controle remoto; o programa informa isso
        // uma vez.
        bool accessibility = HasAccessibilityPermission();
        foreach (var c in new[] { Capability.GlobalHotkeys, Capability.ForegroundWindowInfo })
        {
            list.Add(accessibility
                ? CapabilityStatus.Ok(c)
                : CapabilityStatus.Missing(c, UnavailabilityKind.PermissionRequired,
                    "O macOS exige permissão de Acessibilidade para receber teclas enquanto " +
                    "outro programa está em primeiro plano. Sem ela, use o controle remoto.",
                    "Ajustes do Sistema › Privacidade e Segurança › Acessibilidade"));
        }

        list.Add(monitors.Monitors.Count > 0
            ? CapabilityStatus.Ok(Capability.MonitorEnumeration)
            : CapabilityStatus.Missing(Capability.MonitorEnumeration,
                UnavailabilityKind.InitializationFailed,
                "Não foi possível enumerar os monitores."));

        // Capacidades que o macOS oferece e que serão ligadas pelas etapas seguintes.
        list.Add(CapabilityStatus.Ok(Capability.WindowFrameBounds));
        list.Add(CapabilityStatus.Ok(Capability.AlwaysOnTop));
        list.Add(CapabilityStatus.Ok(Capability.PerPixelTransparency));
        list.Add(CapabilityStatus.Ok(Capability.ClickThrough));
        list.Add(CapabilityStatus.Ok(Capability.TrayIcon));
        list.Add(CapabilityStatus.Ok(Capability.Clipboard));
        list.Add(CapabilityStatus.Ok(Capability.SpeechSynthesis));
        list.Add(CapabilityStatus.Ok(Capability.VectorTextOutline));
        list.Add(CapabilityStatus.Ok(Capability.TextMeasurement));
        list.Add(CapabilityStatus.Ok(Capability.AuxiliaryProcessChannel));

        // RF-569 — "C8 pode não existir. Degradação aceitável: a sobreposição aparece em
        // capturas de tela; o programa deve documentar isso e RF-347 vira inócuo."
        list.Add(CapabilityStatus.Missing(Capability.ExcludeFromCapture,
            UnavailabilityKind.NotSupported,
            "O macOS não permite excluir uma janela das capturas de tela feitas por outros " +
            "programas. A sobreposição aparecerá em prints e gravações."));

        // RF-569 — "C11 pode não ser detectável. Degradação aceitável: RF-347 é omitido."
        list.Add(CapabilityStatus.Missing(Capability.ScreenshotKeyDetection,
            UnavailabilityKind.NotSupported,
            "Não é possível detectar o atalho de captura de tela do sistema no macOS. " +
            "Como a sobreposição já aparece nas capturas, o recurso é dispensável aqui."));

        // RF-571 — C9 não existe uniformemente. Degradação aceitável: substituir por uma
        // espera de um intervalo de quadro estimado, ou aceitar a cintilação inicial.
        list.Add(CapabilityStatus.Missing(Capability.CompositorSync,
            UnavailabilityKind.NotSupported,
            "Sem sincronização explícita com o compositor; o primeiro quadro da " +
            "sobreposição pode piscar."));

        // RF-575 — C20 varia por plataforma. O motor do sistema (Vision) será ligado na
        // Etapa 14; até lá ele não aparece na lista, em vez de falhar ao ser usado.
        list.Add(CapabilityStatus.Missing(Capability.SystemTextRecognition,
            UnavailabilityKind.NotSupported,
            "O reconhecimento de texto do sistema ainda não está ligado nesta plataforma."));

        return new CapabilityReport(list);
    }

    /// <summary>
    /// RF-569 — Verificação SEM solicitar: é o que permite detectar na inicialização em vez
    /// de fazer o sistema pedir permissão sozinho ao abrir o programa.
    ///
    /// A verificação oficial do sistema dá FALSO NEGATIVO quando o programa roda sob um
    /// "processo responsável" que tem a permissão — é o caso de qualquer processo lançado
    /// por um terminal ou por um ambiente de desenvolvimento já autorizado. Confiar só nela
    /// faria o programa se recusar a abrir numa instalação que captura perfeitamente, e
    /// RF-569 manda não iniciar apenas quando a tradução é de fato impossível.
    ///
    /// Por isso, quando a verificação oficial diz que não, faz-se uma SONDAGEM FUNCIONAL:
    /// tenta-se capturar um pixel. Se o sistema devolve uma imagem, a captura funciona.
    ///
    /// O caso restante — permissão negada, em que o sistema devolve o papel de parede sem
    /// as janelas — não é distinguível por esta via; ele é coberto por RF-570, que manda
    /// sugerir a causa ao usuário quando a captura só devolve quadros inúteis, em vez de
    /// exibir tradução vazia repetidamente.
    /// </summary>
    private static bool HasScreenRecordingPermission()
    {
        try
        {
            if (CoreGraphics.CGPreflightScreenCaptureAccess()) return true;
        }
        catch
        {
            return false;
        }

        return CanCaptureAnything();
    }

    /// <summary>Sondagem funcional: um único pixel do canto da tela.</summary>
    private static bool CanCaptureAnything()
    {
        nint image = nint.Zero;
        try
        {
            image = CoreGraphics.CGWindowListCreateImage(
                new CoreGraphics.CGRect(0, 0, 1, 1),
                CoreGraphics.ListOptionOnScreenOnly | CoreGraphics.ListExcludeDesktopElements,
                CoreGraphics.NullWindowID,
                CoreGraphics.ImageNominalResolution);

            return image != nint.Zero && CoreGraphics.CGImageGetWidth(image) > 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (image != nint.Zero) CoreGraphics.CGImageRelease(image);
        }
    }

    /// <summary>
    /// RF-569 — A permissão de Acessibilidade, verificada SEM ser solicitada, para que a
    /// ausência seja detectada na inicialização (RF-576).
    /// </summary>
    private static bool HasAccessibilityPermission()
    {
        try
        {
            return MacInput.AXIsProcessTrusted();
        }
        catch
        {
            return false;
        }
    }

    public CapabilityStatus RequestPermission(Capability capability)
    {
        if (Capabilities[capability].Kind != UnavailabilityKind.PermissionRequired)
            return Capabilities[capability];

        if (capability is Capability.ScreenRegionCapture
                       or Capability.WindowCapture
                       or Capability.WindowPicker)
        {
            try
            {
                CoreGraphics.CGRequestScreenCaptureAccess();
            }
            catch
            {
                // P8 — a falha ao solicitar não derruba nada; a situação continua a mesma.
            }
            Capabilities = Detect(Monitors);
        }

        return Capabilities[capability];
    }

    public bool OpenPermissionSettings(Capability capability)
    {
        string? pane = capability switch
        {
            Capability.ScreenRegionCapture or Capability.WindowCapture or Capability.WindowPicker
                => "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture",
            Capability.GlobalHotkeys or Capability.ForegroundWindowInfo
                => "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility",
            _ => null,
        };
        if (pane is null) return false;

        try
        {
            Process.Start(new ProcessStartInfo("open", pane) { UseShellExecute = false });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        Keyboard.Dispose();
        _backend.Dispose();
    }
}
