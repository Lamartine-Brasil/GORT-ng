using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Platform.Monitors;

/// <summary>
/// C18 — Um monitor: seus limites em coordenadas globais da área de trabalho e sua escala.
///
/// RF-100 — Coordenadas negativas são suportadas: um monitor à esquerda ou acima do
/// principal tem origem negativa.
/// </summary>
public sealed record MonitorInfo(Rect Bounds, double Scale, bool IsPrimary, string Name = "")
{
    /// <summary>P-141 — Resolução de referência do cálculo de escala.</summary>
    public double Dpi => Scale * P.ReferenceDpi;

    public override string ToString()
        => $"{(IsPrimary ? "*" : " ")}{Name} {Bounds} escala {Scale:0.##}";
}

/// <summary>
/// C18 — Enumeração de monitores. Nunca uma escala global lida uma vez na abertura:
/// RF-075 exige a escala do monitor que CONTÉM cada moldura, obtida no momento de
/// converter aquela moldura em retângulo.
/// </summary>
public interface IMonitorProvider
{
    /// <summary>Monitores presentes, na ordem em que o sistema os informa.</summary>
    IReadOnlyList<MonitorInfo> Monitors { get; }

    /// <summary>
    /// Reconsulta o sistema. PARTE VIII — a resolução pode mudar, um monitor pode ser
    /// removido e a disposição pode ser alterada com o programa aberto.
    /// </summary>
    void Refresh();
}

/// <summary>Operações de geometria sobre o conjunto de monitores.</summary>
public static class MonitorGeometry
{
    /// <summary>
    /// RF-344 — União de todos os monitores: é o retângulo que a janela de sobreposição
    /// cobre. PARTE VIII — pode ter origem negativa, e o deslocamento entre a origem da
    /// união e a origem do monitor principal precisa ser guardado.
    /// </summary>
    public static Rect VirtualDesktop(IReadOnlyList<MonitorInfo> monitors)
        => Rect.UnionAll(monitors.Select(m => m.Bounds));

    /// <summary>
    /// RF-075 / RF-076 — O monitor de uma moldura é aquele que contém o seu CANTO SUPERIOR
    /// ESQUERDO. Quando a moldura é arrastada de um monitor para outro de escala diferente,
    /// o fator é recalculado por esta mesma regra.
    ///
    /// Motivo: com um monitor a 100% e outro a 150%, um fator único erra em um dos dois e a
    /// região capturada sai deslocada alguns pixels; na sobreposição isso aparece como a
    /// tradução desalinhada em relação ao texto original.
    /// </summary>
    public static MonitorInfo? MonitorOf(IReadOnlyList<MonitorInfo> monitors, Rect frame)
    {
        foreach (var m in monitors)
        {
            if (m.Bounds.Contains(frame.Left, frame.Top)) return m;
        }

        // O canto está fora de todos os monitores: cai para o de maior interseção, e depois
        // para o primário — degradação silenciosa em vez de exceção (P8).
        MonitorInfo? best = null;
        long bestArea = 0;
        foreach (var m in monitors)
        {
            long area = m.Bounds.Intersect(frame).Area;
            if (area > bestArea) { bestArea = area; best = m; }
        }
        return best ?? monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
    }

    /// <summary>
    /// RF-075 — Escala do monitor que contém a moldura. Devolve 1,0 quando não há monitores,
    /// para que o cálculo continue coerente em vez de falhar.
    /// </summary>
    public static double ScaleOf(IReadOnlyList<MonitorInfo> monitors, Rect frame)
        => MonitorOf(monitors, frame)?.Scale ?? 1.0;

    /// <summary>
    /// RF-086 — Uma área é inválida quando fica TOTAL ou parcialmente fora da área de
    /// trabalho. O programa avisa o usuário e aponta quais áreas ficaram inválidas; NUNCA
    /// as reposiciona por conta própria, porque não tem como saber onde o conteúdo do jogo
    /// foi parar, e mover a área produziria uma região errada silenciosamente.
    /// </summary>
    public static bool IsFullyVisible(IReadOnlyList<MonitorInfo> monitors, Rect area)
    {
        if (area.IsEmpty) return false;

        // Cobertura por união de retângulos: soma das interseções com monitores que não se
        // sobrepõem entre si. Monitores reais não se sobrepõem.
        long covered = 0;
        foreach (var m in monitors) covered += m.Bounds.Intersect(area).Area;
        return covered >= area.Area;
    }

    /// <summary>RF-086 / RF-087 — Índices das áreas que deixaram de caber na área de trabalho.</summary>
    public static List<int> InvalidAreas(IReadOnlyList<MonitorInfo> monitors,
                                         IReadOnlyList<Rect> areas)
    {
        var invalid = new List<int>();
        for (int i = 0; i < areas.Count; i++)
        {
            if (!IsFullyVisible(monitors, areas[i])) invalid.Add(i);
        }
        return invalid;
    }
}
