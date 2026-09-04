namespace Gort.Core.Model;

/// <summary>
/// 7.6 — Grupo de cor: conjunto de faixas RGB ou HSV usado para decidir quais pixels
/// contam como texto. O usuário pode ter vários e escolher quais valem para cada área.
/// </summary>
public sealed class ColorGroup
{
    /// <summary>Cor exata a extrair no modo RGB. Faixa 0–255 cada.</summary>
    public int R { get; set; }
    public int G { get; set; }
    public int B { get; set; }

    /// <summary>Faixa de saturação no modo HSV. Faixa 0–100.</summary>
    public int S1 { get; set; }
    public int S2 { get; set; }

    /// <summary>Faixa de brilho no modo HSV. Faixa 0–100.</summary>
    public int V1 { get; set; }
    public int V2 { get; set; }

    public ColorGroup Clone() => new()
    {
        R = R, G = G, B = B, S1 = S1, S2 = S2, V1 = V1, V2 = V2,
    };

    /// <summary>
    /// RF-042 / RF-043 — Normaliza o grupo: componentes saturados nos limites, e faixas
    /// cujo início supera o fim têm os dois valores trocados. Aplicado ao carregar e ao
    /// aplicar; nunca rejeita valores (P7).
    /// </summary>
    public void Normalize()
    {
        R = Math.Clamp(R, 0, 255);
        G = Math.Clamp(G, 0, 255);
        B = Math.Clamp(B, 0, 255);
        S1 = Math.Clamp(S1, 0, 100);
        S2 = Math.Clamp(S2, 0, 100);
        V1 = Math.Clamp(V1, 0, 100);
        V2 = Math.Clamp(V2, 0, 100);
        if (S1 > S2) (S1, S2) = (S2, S1);
        if (V1 > V2) (V1, V2) = (V2, V1);
    }

    public override string ToString()
        => $"RGB({R},{G},{B}) S[{S1}-{S2}] V[{V1}-{V2}]";
}

/// <summary>Ações que podem ser disparadas por um atalho de teclado (RF-444, RF-447).</summary>
public enum ShortcutAction
{
    // RF-444 — atalhos com padrão dedicado.
    ToggleRealtimeTranslation,
    TranslateOnce,
    SnapshotArea,
    QuickArea,
    OpenDictionaryEditor,
    ToggleTranslationWindow,
    ToggleMouseFollowArea,

    // RF-447 — atalhos avançados.
    OpenProfile,
    ToggleForcedTransparency,
    SwitchTranslationService,
}

/// <summary>
/// 7.7 — Configuração de um atalho.
/// As variantes esquerda e direita dos modificadores são normalizadas para um único
/// código antes de chegar aqui (RF-437).
/// </summary>
public sealed class ShortcutConfig
{
    public required ShortcutAction Action { get; init; }

    /// <summary>Até três teclas (RF-442). Vazio é válido e nunca dispara (RF-446).</summary>
    public List<string> Keys { get; init; } = new();

    /// <summary>Usado quando há várias instâncias da mesma ação (ex.: os quatro "abrir perfil").</summary>
    public int Index { get; init; }

    /// <summary>Parâmetro da ação — por exemplo o nome do arquivo de perfil a abrir.</summary>
    public string? Data { get; set; }

    public bool IsEmpty => Keys.Count == 0;

    /// <summary>
    /// RF-438 — Uma combinação é reconhecida quando o conjunto de teclas pressionadas tem
    /// exatamente o mesmo tamanho e os mesmos elementos, independentemente da ordem.
    /// </summary>
    public bool Matches(IReadOnlyCollection<string> pressed)
    {
        if (IsEmpty || pressed.Count != Keys.Count) return false;
        foreach (var key in Keys)
        {
            if (!pressed.Contains(key)) return false;
        }
        return true;
    }

    public override string ToString()
        => Keys.Count == 0 ? "(vazio)" : string.Join("+", Keys);
}

/// <summary>7.8 — Entrada da memória de exibição (RF-222, RF-223).</summary>
public sealed record DisplayMemoryEntry(string Text, DateTime CreatedAt);
