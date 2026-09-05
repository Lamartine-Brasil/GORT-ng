namespace Gort.Platform.Input;

/// <summary>
/// Posição do cursor em coordenadas globais da área de trabalho.
///
/// Usada pela área de OCR que segue o mouse (RF-454), que reposiciona a área para que o seu
/// centro fique sob o cursor.
/// </summary>
public interface ICursorPosition
{
    /// <summary>Devolve falso quando a plataforma não expõe a posição do cursor.</summary>
    bool TryGet(out int x, out int y);
}

/// <summary>
/// C15 — Síntese de voz.
///
/// RF-573 / RF-480 — Pode não existir. Nesse caso a opção fica DESABILITADA com uma
/// explicação, e nunca gera erro.
/// </summary>
public interface ITextToSpeech : IDisposable
{
    bool IsAvailable { get; }
    string? UnavailableReason { get; }

    /// <summary>RF-477 — Se ainda há fala tocando.</summary>
    bool IsSpeaking { get; }

    /// <summary>Lê o texto. Interrompe o que estiver tocando quando pedido.</summary>
    void Speak(string text, bool interrupt);

    void Stop();
}

/// <summary>Implementação inerte, para plataformas sem síntese de voz.</summary>
public sealed class NoTextToSpeech : ITextToSpeech
{
    public NoTextToSpeech(string reason) => UnavailableReason = reason;

    public bool IsAvailable => false;
    public string? UnavailableReason { get; }
    public bool IsSpeaking => false;

    public void Speak(string text, bool interrupt) { }
    public void Stop() { }
    public void Dispose() { }
}

/// <summary>Cursor indisponível.</summary>
public sealed class NoCursorPosition : ICursorPosition
{
    public bool TryGet(out int x, out int y)
    {
        x = 0;
        y = 0;
        return false;
    }
}
