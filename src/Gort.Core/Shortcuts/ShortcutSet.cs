using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Shortcuts;

/// <summary>
/// Cap. 22 — O conjunto de atalhos configurados e a decisão de qual deles um conjunto de
/// teclas pressionadas dispara.
///
/// RF-439 — Combinações DUPLICADAS são permitidas. Se a mesma combinação estiver
/// configurada para duas ações, vence a primeira encontrada na ordem de verificação e a
/// segunda nunca dispara — SEM recusar a configuração e SEM avisar o usuário.
///
/// A ordem de verificação é: a ordem de declaração de <see cref="ShortcutAction"/>, e dentro
/// de cada ação, o <see cref="ShortcutConfig.Index"/> crescente. É estável e é esta a
/// documentação que RF-439 exige.
/// </summary>
public sealed class ShortcutSet
{
    private readonly List<ShortcutConfig> _shortcuts = new();

    public IReadOnlyList<ShortcutConfig> All => _shortcuts;

    /// <summary>RF-444 — As sete ações com atalho dedicado e seus padrões.</summary>
    public static IReadOnlyList<(ShortcutAction Action, string[] Keys)> Defaults { get; } = new[]
    {
        (ShortcutAction.ToggleRealtimeTranslation, new[] { KeyNames.Control, KeyNames.Shift, "Z" }),
        (ShortcutAction.TranslateOnce,             new[] { KeyNames.Control, KeyNames.Shift, "C" }),
        (ShortcutAction.SnapshotArea,              new[] { KeyNames.Control, KeyNames.Shift, "A" }),
        (ShortcutAction.QuickArea,                 new[] { KeyNames.Control, KeyNames.Shift, "X" }),
        (ShortcutAction.OpenDictionaryEditor,      new[] { KeyNames.Control, KeyNames.Shift, "S" }),
        (ShortcutAction.ToggleTranslationWindow,   new[] { KeyNames.Control, KeyNames.Shift, "D" }),
        (ShortcutAction.ToggleMouseFollowArea,     new[] { KeyNames.Control, KeyNames.Shift, "F" }),
    };

    /// <summary>Cria o conjunto com os padrões de RF-444.</summary>
    public static ShortcutSet WithDefaults()
    {
        var set = new ShortcutSet();
        foreach (var (action, keys) in Defaults) set.Set(action, keys);
        return set;
    }

    /// <summary>
    /// Define ou substitui o atalho de uma ação.
    /// RF-442 — no máximo três teclas; o excedente é descartado em vez de recusado (P7).
    /// RF-446 — um atalho vazio é válido e nunca dispara.
    /// </summary>
    public ShortcutConfig Set(ShortcutAction action, IEnumerable<string> keys,
                              int index = 0, string? data = null)
    {
        var normalized = keys
            .Select(KeyNames.Normalize)
            .Where(k => k.Length > 0)
            .Distinct()                      // RF-513 — teclas repetidas são ignoradas
            .Take(P.MaxShortcutKeys)         // RF-442
            .ToList();

        var existing = _shortcuts.FirstOrDefault(s => s.Action == action && s.Index == index);
        if (existing is not null) _shortcuts.Remove(existing);

        var config = new ShortcutConfig { Action = action, Index = index, Data = data };
        config.Keys.AddRange(normalized);

        _shortcuts.Add(config);
        Sort();
        return config;
    }

    public ShortcutConfig? Find(ShortcutAction action, int index = 0)
        => _shortcuts.FirstOrDefault(s => s.Action == action && s.Index == index);

    public void Remove(ShortcutAction action, int index = 0)
    {
        var existing = Find(action, index);
        if (existing is not null) _shortcuts.Remove(existing);
    }

    /// <summary>RF-445 — "Restaurar padrão" de uma ação.</summary>
    public void RestoreDefault(ShortcutAction action)
    {
        var fallback = Defaults.FirstOrDefault(d => d.Action == action);
        if (fallback.Keys is not null) Set(action, fallback.Keys);
    }

    /// <summary>RF-445 — "Limpar": o atalho fica vazio, que é válido e nunca dispara.</summary>
    public void Clear(ShortcutAction action, int index = 0)
        => Set(action, Array.Empty<string>(), index);

    /// <summary>
    /// RF-438 / RF-439 — Encontra a ação disparada pelo conjunto de teclas pressionadas.
    ///
    /// Uma combinação é reconhecida quando o conjunto pressionado tem EXATAMENTE o mesmo
    /// tamanho e os mesmos elementos, independentemente da ordem. Havendo duplicatas, vence
    /// a primeira na ordem de verificação, em silêncio.
    /// </summary>
    public ShortcutConfig? Match(IReadOnlyCollection<string> pressed)
    {
        if (pressed.Count == 0) return null;

        var normalized = pressed.Select(KeyNames.Normalize).ToHashSet(StringComparer.Ordinal);

        foreach (var shortcut in _shortcuts)
        {
            if (shortcut.Matches(normalized)) return shortcut;
        }
        return null;
    }

    /// <summary>
    /// A ordem de verificação de RF-439: pela ordem de declaração da ação e, dentro dela,
    /// pelo índice. `OrderBy` é estável, então atalhos idênticos preservam a ordem de
    /// inserção — o resultado é previsível, que é o que o requisito pede.
    /// </summary>
    private void Sort()
    {
        var sorted = _shortcuts
            .OrderBy(s => (int)s.Action)
            .ThenBy(s => s.Index)
            .ToList();
        _shortcuts.Clear();
        _shortcuts.AddRange(sorted);
    }
}
