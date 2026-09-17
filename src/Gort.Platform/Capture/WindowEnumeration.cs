namespace Gort.Platform.Capture;

/// <summary>
/// C3 / RF-089 — Uma janela que o usuário pode escolher para a captura anexada.
///
/// O identificador é OPACO de propósito: cada sistema o representa de um jeito — número de
/// janela no macOS, handle no Windows — e nada acima da abstração deve interpretá-lo.
/// </summary>
public sealed record CapturableWindow(
    ulong Id,
    string Title,
    string Application,
    Gort.Core.Model.Rect Bounds)
{
    /// <summary>
    /// O que o seletor mostra. O nome do aplicativo vem primeiro porque é por ele que o
    /// usuário procura: ele sabe que abriu o jogo, não como a janela se chama.
    /// </summary>
    public string DisplayName => Title.Length == 0
        ? Application
        : $"{Application} — {Title}";
}

/// <summary>
/// C3 — Enumerar as janelas capturáveis.
///
/// Separado de <see cref="ICaptureBackend"/> porque é capacidade distinta: um sistema pode
/// saber capturar uma janela específica (C2) sem oferecer um jeito de listá-las, e a
/// degradação de 6.4 trata os dois casos de forma diferente.
/// </summary>
public interface IWindowEnumerator
{
    /// <summary>Verdadeiro quando este sistema sabe listar janelas.</summary>
    bool IsAvailable { get; }

    /// <summary>Motivo da indisponibilidade, para ser exibido uma única vez.</summary>
    string? UnavailableReason { get; }

    /// <summary>
    /// As janelas capturáveis, na ordem em que o sistema as informa — que é, nos sistemas
    /// que conhecemos, da frente para trás.
    ///
    /// Janelas sem título e as do próprio programa não aparecem: as primeiras o usuário não
    /// reconheceria, e a segunda traduziria a si mesma (RF-343).
    /// </summary>
    IReadOnlyList<CapturableWindow> List();

    /// <summary>
    /// RF-090 / RF-097 — Se a janela ainda existe. Chamado antes de cada captura: quando ela
    /// deixa de existir, a captura para e o modo é desativado.
    /// </summary>
    bool Exists(ulong id);
}

/// <summary>Onde C3 não existe. A interface pergunta; esta responde que não sabe.</summary>
public sealed class NoWindowEnumerator : IWindowEnumerator
{
    public NoWindowEnumerator(string reason) => UnavailableReason = reason;

    public bool IsAvailable => false;
    public string? UnavailableReason { get; }

    public IReadOnlyList<CapturableWindow> List() => Array.Empty<CapturableWindow>();
    public bool Exists(ulong id) => false;
}
