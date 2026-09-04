using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Imaging;

/// <summary>
/// RF-104 — Os três modos de filtro de cor são MUTUAMENTE EXCLUSIVOS: marcar um desmarca
/// os outros dois. <see cref="None"/> corresponde a nenhum filtro ativo (RF-110).
/// </summary>
public enum FilterMode
{
    /// <summary>RF-110 / RF-118 — Sem filtro: a imagem vai ao OCR sem binarização.</summary>
    None,
    /// <summary>RF-105 — Extração por RGB exato.</summary>
    Rgb,
    /// <summary>RF-106 — Extração por faixas HSV.</summary>
    Hsv,
    /// <summary>RF-108 — Limiar simples.</summary>
    Threshold,
}

/// <summary>Configuração do pré-processamento de uma região (cap. 13).</summary>
public sealed class FilterSettings
{
    /// <summary>RF-104 — Modo ativo; os três são mutuamente exclusivos.</summary>
    public FilterMode Mode { get; set; } = FilterMode.None;

    /// <summary>
    /// Grupos de cor ativos para esta região. RF-105/RF-106 — o pixel passa se satisfizer
    /// QUALQUER grupo ativo.
    /// </summary>
    public List<ColorGroup> Groups { get; set; } = new();

    /// <summary>RF-108 — Valor de corte do modo limiar (P-21).</summary>
    public int Threshold { get; set; } = P.DefaultThreshold;

    /// <summary>RF-111 — Erosão opcional, aplicada sobre a imagem já binarizada.</summary>
    public bool Erosion { get; set; }

    /// <summary>RF-113 — Fator de ampliação aplicado antes do OCR (P-22).</summary>
    public double Scale { get; set; } = P.DefaultScale;

    /// <summary>
    /// RF-042 / RF-114 — Satura os valores nos limites em vez de rejeitá-los, e substitui
    /// pelo padrão um fator de ampliação lido acima de P-24.
    /// </summary>
    public void Normalize()
    {
        Threshold = Math.Clamp(Threshold, 0, 255);
        if (Scale > P.ScaleMax) Scale = P.DefaultScale;    // RF-114
        if (Scale < P.ScaleMin) Scale = P.ScaleMin;
        foreach (var g in Groups) g.Normalize();
    }

    /// <summary>
    /// RF-119 — Assistente de configuração rápida: a partir de "texto claro" ou "texto
    /// escuro", configura automaticamente os grupos de cor HSV. 🔒
    /// Para texto escuro, dois grupos (P-26 e P-27); para texto claro, um (P-28).
    /// </summary>
    public static List<ColorGroup> WizardGroups(bool darkText)
    {
        static ColorGroup From((int S1, int S2, int V1, int V2) r)
            => new() { S1 = r.S1, S2 = r.S2, V1 = r.V1, V2 = r.V2 };

        return darkText
            ? new List<ColorGroup> { From(P.HsvDarkTextRange1), From(P.HsvDarkTextRange2) }
            : new List<ColorGroup> { From(P.HsvLightTextRange) };
    }
}
