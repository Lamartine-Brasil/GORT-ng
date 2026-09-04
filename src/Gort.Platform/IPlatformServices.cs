using Gort.Platform.Capabilities;
using Gort.Platform.Capture;
using Gort.Platform.Monitors;

namespace Gort.Platform;

/// <summary>
/// RF-577 — A camada de abstração explícita para C1 a C12, com uma implementação por
/// sistema, de modo que os módulos de OCR, tradução, agrupamento e layout sejam idênticos
/// em todas as plataformas.
///
/// Nada acima desta interface conhece o sistema operacional.
/// </summary>
public interface IPlatformServices : IDisposable
{
    /// <summary>Nome do sistema, para diagnóstico e mensagens.</summary>
    string PlatformName { get; }

    /// <summary>
    /// RF-576 — Situação de todas as capacidades, apurada NA INICIALIZAÇÃO. A interface
    /// oculta ou desabilita controles a partir daqui; nada é descoberto no meio de uma
    /// tradução.
    /// </summary>
    CapabilityReport Capabilities { get; }

    /// <summary>C1 / C2 — Captura. Cap. 12.</summary>
    ScreenCapture Capture { get; }

    /// <summary>C18 — Monitores e suas escalas.</summary>
    IMonitorProvider Monitors { get; }

    /// <summary>C5, C7 e C8 — Efeitos de janela alternáveis em tempo de execução.</summary>
    IWindowEffects WindowEffects { get; }

    /// <summary>
    /// C10 — Atalho global de teclado. Quando indisponível, o gancho é inerte e explica o
    /// motivo; o usuário opera pelo controle remoto (RF-569).
    /// </summary>
    Input.IGlobalKeyboardHook Keyboard { get; }

    /// <summary>
    /// RF-569 — Solicita ao sistema uma permissão que falta, o que faz o sistema exibir o
    /// pedido ao usuário. Devolve a nova situação da capacidade.
    ///
    /// Só faz sentido para <see cref="UnavailabilityKind.PermissionRequired"/>; para os
    /// demais casos devolve a situação inalterada.
    /// </summary>
    CapabilityStatus RequestPermission(Capability capability);

    /// <summary>
    /// RF-569 — Abre a tela de configuração do sistema correspondente a uma permissão que
    /// falta. Devolve falso quando não há tela a oferecer nesta plataforma.
    /// </summary>
    bool OpenPermissionSettings(Capability capability);
}

/// <summary>
/// Escolhe a implementação do sistema atual e apura as capacidades uma única vez.
///
/// RF-575 / RF-576 — O programa lista apenas o que está efetivamente disponível e nunca
/// apresenta uma função que falhará ao ser usada.
/// </summary>
public static class PlatformServices
{
    /// <summary>
    /// Cria os serviços de plataforma do sistema atual.
    ///
    /// Um sistema sem implementação não é um erro fatal: devolve-se uma implementação
    /// inerte que reporta tudo indisponível com explicação, e a interface reage a isso
    /// (P7, P8, RF-576).
    /// </summary>
    public static IPlatformServices Create()
    {
        if (OperatingSystem.IsMacOS()) return new MacOS.MacPlatformServices();
        if (OperatingSystem.IsWindows()) return new Windows.WindowsPlatformServices();
        if (OperatingSystem.IsLinux()) return new Linux.LinuxPlatformServices();
        return new UnsupportedPlatformServices();
    }
}

/// <summary>
/// Implementação inerte para sistemas sem suporte. Existe para que a ausência de plataforma
/// seja um estado observável e explicável na interface, não uma exceção na inicialização.
/// </summary>
internal sealed class UnsupportedPlatformServices : IPlatformServices
{
    public string PlatformName => "sistema não suportado";

    public CapabilityReport Capabilities { get; } = new(
        Enum.GetValues<Capability>().Select(c => CapabilityStatus.Missing(
            c, UnavailabilityKind.NotSupported,
            "Este sistema operacional ainda não tem implementação da camada de plataforma.")));

    public ScreenCapture Capture { get; } = new(new NullCaptureBackend());

    public IMonitorProvider Monitors { get; } = new NullMonitorProvider();

    public Input.IGlobalKeyboardHook Keyboard { get; } = new Input.InactiveKeyboardHook(
        "Este sistema operacional ainda não tem implementação da camada de plataforma.");

    public IWindowEffects WindowEffects { get; } = new NoWindowEffects();

    public CapabilityStatus RequestPermission(Capability capability) => Capabilities[capability];

    public bool OpenPermissionSettings(Capability capability) => false;

    public void Dispose() { }
}

internal sealed class NullCaptureBackend : ICaptureBackend
{
    public bool Supports(CaptureSource source) => false;
    public CapturedRegion? Capture(int index, Gort.Core.Model.Rect rect, CaptureSource source) => null;
    public void ExcludeOwnWindow(nint windowHandle) { }
    public void Dispose() { }
}

internal sealed class NullMonitorProvider : IMonitorProvider
{
    public IReadOnlyList<MonitorInfo> Monitors => Array.Empty<MonitorInfo>();
    public void Refresh() { }
}
