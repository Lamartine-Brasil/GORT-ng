namespace Gort.Platform;

/// <summary>
/// C5, C7 e C8 — Efeitos de janela que o programa precisa alternar EM TEMPO DE EXECUÇÃO.
///
/// São separados da criação da janela porque mudam durante o uso: a janela em modo camada
/// vira atravessável ao iniciar a tradução e volta a receber cliques ao parar (RF-333,
/// RF-334), e a sobreposição sai e volta às capturas de tela conforme RF-347.
/// </summary>
public interface IWindowEffects
{
    /// <summary>
    /// C7 — Faz os eventos de mouse ATRAVESSAREM a janela e chegarem à de baixo.
    /// Devolve falso quando a plataforma não oferece a capacidade.
    /// </summary>
    bool SetClickThrough(nint windowHandle, bool value);

    /// <summary>
    /// C8 — Marca a janela para não aparecer em capturas nem gravações feitas por outros
    /// programas (P4, RF-346). Devolve falso onde a capacidade não existe — no macOS ela
    /// não existe, e RF-569 declara essa degradação aceitável.
    /// </summary>
    bool SetExcludedFromCapture(nint windowHandle, bool value);
}

/// <summary>Implementação inerte, para plataformas sem esses efeitos.</summary>
public sealed class NoWindowEffects : IWindowEffects
{
    public bool SetClickThrough(nint windowHandle, bool value) => false;
    public bool SetExcludedFromCapture(nint windowHandle, bool value) => false;
}
