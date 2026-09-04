namespace Gort.Platform.Input;

/// <summary>Uma tecla que subiu ou desceu, já com o nome independente de plataforma.</summary>
public sealed record KeyEvent(string Key, bool IsDown, bool IsAutoRepeat = false);

/// <summary>
/// C10 — Atalho global de teclado: receber eventos de teclado enquanto OUTRO programa tem o
/// foco, SEM consumir os eventos.
///
/// O "sem consumir" é parte do contrato: o jogo continua recebendo tudo o que o usuário
/// digita. Um gancho que engolisse as teclas quebraria o jogo que está sendo traduzido.
///
/// RF-011 — Quem chama precisa saber que o sistema REMOVE um gancho de baixo nível que
/// fique preso por mais de ~300 ms, e que isso mata todos os atalhos até reiniciar. Por
/// isso o tratamento de cada evento tem de devolver depressa, e uma parada pedida de dentro
/// dele usa o prazo curto P-04.
/// </summary>
public interface IGlobalKeyboardHook : IDisposable
{
    /// <summary>
    /// RF-576 — Se o gancho pôde ser instalado. Quando falso, <see cref="UnavailableReason"/>
    /// explica por quê e a interface oferece o controle remoto como alternativa (RF-569).
    /// </summary>
    bool IsActive { get; }

    string? UnavailableReason { get; }

    /// <summary>
    /// Disparado a cada tecla. É chamado de uma thread do sistema; o tratamento tem de ser
    /// curto (ver acima).
    /// </summary>
    event Action<KeyEvent>? KeyChanged;

    /// <summary>Instala o gancho. Devolve falso quando não é possível.</summary>
    bool Start();

    /// <summary>Remove o gancho (RF-016 — ao encerrar).</summary>
    void Stop();
}

/// <summary>
/// Gancho inerte, para plataformas ou sessões em que C10 não existe.
///
/// RF-569 — "Sem permissão de acessibilidade, os atalhos globais ficam indisponíveis e o
/// usuário deve usar o controle remoto; o programa deve informar isso uma vez."
/// </summary>
public sealed class InactiveKeyboardHook : IGlobalKeyboardHook
{
    public InactiveKeyboardHook(string reason) => UnavailableReason = reason;

    public bool IsActive => false;
    public string? UnavailableReason { get; }
    public event Action<KeyEvent>? KeyChanged { add { } remove { } }

    public bool Start() => false;
    public void Stop() { }
    public void Dispose() { }
}
