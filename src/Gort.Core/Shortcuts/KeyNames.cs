namespace Gort.Core.Shortcuts;

/// <summary>
/// Nomes de tecla independentes de plataforma, que é a forma como os atalhos são
/// persistidos (RF-026: identificador textual, nunca posição numérica).
///
/// RF-437 — As variantes ESQUERDA e DIREITA dos modificadores são normalizadas para um
/// único código, de modo que Shift esquerdo e direito sejam equivalentes. É por isso que
/// não existem "LeftShift" e "RightShift" aqui: eles nem chegam a ser representáveis.
/// </summary>
public static class KeyNames
{
    public const string Control = "Ctrl";
    public const string Shift = "Shift";
    public const string Alt = "Alt";

    /// <summary>Tecla de comando no macOS, Windows no PC, Super no Linux.</summary>
    public const string Meta = "Meta";

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // RF-437 — normalização das variantes esquerda/direita.
        ["LeftCtrl"] = Control, ["RightCtrl"] = Control,
        ["LCtrl"] = Control, ["RCtrl"] = Control,
        ["ControlLeft"] = Control, ["ControlRight"] = Control,
        ["Control"] = Control, ["Ctl"] = Control,

        ["LeftShift"] = Shift, ["RightShift"] = Shift,
        ["LShift"] = Shift, ["RShift"] = Shift,
        ["ShiftLeft"] = Shift, ["ShiftRight"] = Shift,

        ["LeftAlt"] = Alt, ["RightAlt"] = Alt,
        ["LAlt"] = Alt, ["RAlt"] = Alt,
        ["AltLeft"] = Alt, ["AltRight"] = Alt,
        ["Option"] = Alt, ["AltGr"] = Alt,

        ["LeftMeta"] = Meta, ["RightMeta"] = Meta,
        ["LWin"] = Meta, ["RWin"] = Meta,
        ["LeftWindows"] = Meta, ["RightWindows"] = Meta,
        ["Command"] = Meta, ["Cmd"] = Meta, ["Super"] = Meta, ["Win"] = Meta,
    };

    /// <summary>
    /// RF-437 — Normaliza um nome de tecla. Nomes desconhecidos são apenas padronizados em
    /// caixa, para que um teclado com uma tecla que este programa não conhece ainda funcione.
    /// </summary>
    public static string Normalize(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "";
        key = key.Trim();

        if (Aliases.TryGetValue(key, out var normalized)) return normalized;

        // Teclas de um caractere viram maiúsculas: "z" e "Z" são a mesma tecla.
        return key.Length == 1 ? key.ToUpperInvariant() : key;
    }

    public static bool IsModifier(string key)
        => key is Control or Shift or Alt or Meta;
}
