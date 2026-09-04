using Gort.Core.Model;

namespace Gort.Core.Shortcuts;

/// <summary>
/// Cap. 22 — Recebe eventos de teclado do sistema inteiro e devolve comandos.
///
/// RF-436 — A interceptação é GLOBAL: funciona mesmo quando nenhuma janela do programa tem
/// foco. É isso que permite operar sem sair do jogo.
///
/// Esta classe é a parte independente de plataforma: quem entrega os eventos é a abstração
/// C10 (RF-577).
/// </summary>
public sealed class ShortcutDispatcher
{
    private readonly HashSet<string> _pressed = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public ShortcutDispatcher(ShortcutSet shortcuts) => Shortcuts = shortcuts;

    public ShortcutSet Shortcuts { get; set; }

    /// <summary>
    /// RF-443 — Os atalhos ficam INERTES enquanto: a camada de seleção de área está aberta;
    /// algum campo de captura de atalho está com foco; ou a janela de opções avançadas está
    /// aberta.
    ///
    /// Nos três casos o usuário está usando o teclado para outra coisa, e um atalho
    /// disparado no meio disso seria imprevisível.
    /// </summary>
    public bool Suspended { get; set; }

    /// <summary>Teclas atualmente pressionadas, para diagnóstico e para a interface.</summary>
    public IReadOnlyCollection<string> Pressed
    {
        get { lock (_gate) return _pressed.ToList(); }
    }

    /// <summary>
    /// Processa uma tecla PRESSIONADA. Devolve o atalho disparado, ou null.
    ///
    /// RF-440 — Uma tecla já presente no conjunto é IGNORADA até ser solta, para que a
    /// repetição automática do teclado não dispare a ação várias vezes. Sem isso, segurar
    /// o atalho de tradução iniciaria e pararia o laço dezenas de vezes por segundo.
    /// </summary>
    public ShortcutConfig? KeyDown(string key)
    {
        string normalized = KeyNames.Normalize(key);
        if (normalized.Length == 0) return null;

        lock (_gate)
        {
            // RF-440 — repetição automática não dispara de novo.
            if (!_pressed.Add(normalized)) return null;

            if (Suspended) return null;

            return Shortcuts.Match(_pressed);
        }
    }

    /// <summary>
    /// Processa uma tecla SOLTA.
    ///
    /// RF-441 — Soltar QUALQUER tecla limpa o conjunto inteiro de pressionadas. É mais
    /// simples que remover só a que saiu, e evita que um evento de soltura perdido — comum
    /// quando o foco muda de janela no meio da combinação — deixe o conjunto sujo para
    /// sempre.
    /// </summary>
    public void KeyUp(string key)
    {
        lock (_gate) _pressed.Clear();
    }

    /// <summary>
    /// RF-504 — O botão aplicar limpa o estado de teclas pressionadas. Também é o que se
    /// faz ao suspender e retomar, para que uma tecla presa não fique no conjunto.
    /// </summary>
    public void Reset()
    {
        lock (_gate) _pressed.Clear();
    }
}
