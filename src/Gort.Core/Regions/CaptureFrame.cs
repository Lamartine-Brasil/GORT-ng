using Gort.Core.Model;

namespace Gort.Core.Regions;

/// <summary>
/// Os tipos de área. As duas primeiras são as do glossário — incrementais e decrementais —;
/// as três seguintes são áreas especiais, criadas por atalho ou por modo.
/// </summary>
public enum AreaKind
{
    /// <summary>
    /// Área de OCR incremental: soma região a ser lida. RF-066 — persistida no perfil.
    /// </summary>
    Normal,

    /// <summary>
    /// Área de exclusão (decremental): subtrai região de dentro das incrementais.
    /// RF-066 — persistida no perfil.
    /// </summary>
    Exclusion,

    /// <summary>
    /// RF-069 — Área rápida: uma única área extra, criada por atalho, aplicada
    /// imediatamente e NÃO persistida (PARTE XI, item 16).
    /// </summary>
    Quick,

    /// <summary>
    /// RF-454 — Área que segue o mouse. Também não é persistida.
    /// </summary>
    MouseFollow,

    /// <summary>
    /// RF-070 — Área instantânea: quando presente, SUBSTITUI todas as demais.
    /// </summary>
    Snapshot,
}

/// <summary>
/// Uma moldura de área. O retângulo guardado é o da MOLDURA — a janelinha que o usuário
/// arrasta —, não o da captura: a conversão entre os dois depende da escala do monitor em
/// que a moldura está no momento (RF-073 a RF-076).
/// </summary>
public sealed class CaptureFrame
{
    public CaptureFrame(Rect frameRect, AreaKind kind = AreaKind.Normal)
    {
        FrameRect = FrameGeometry.ClampToMinimumSize(frameRect);   // RF-057
        Kind = kind;
    }

    /// <summary>Retângulo da moldura, em coordenadas absolutas de tela.</summary>
    public Rect FrameRect { get; set; }

    public AreaKind Kind { get; }

    /// <summary>
    /// RF-078 — Grupos de cor que se aplicam a esta área, um sinalizador por grupo, na
    /// ordem dos grupos. RF-063 — áreas de exclusão não oferecem os botões de cor, então a
    /// lista simplesmente não é usada para elas.
    /// </summary>
    public List<bool> ActiveColorGroups { get; } = new();

    /// <summary>RF-085 — As molduras só são visíveis enquanto o usuário está DEFININDO as áreas.</summary>
    public bool Visible { get; set; }

    public CaptureFrame Clone()
    {
        var copy = new CaptureFrame(FrameRect, Kind) { Visible = Visible };
        copy.ActiveColorGroups.AddRange(ActiveColorGroups);
        return copy;
    }

    public override string ToString() => $"{Kind} {FrameRect}";
}

/// <summary>
/// Resultado de <see cref="RegionManager.Build"/>: exatamente o que a captura e o
/// pré-processamento recebem (6.1).
/// </summary>
public sealed class BuiltAreas
{
    /// <summary>
    /// Retângulos a capturar, em coordenadas absolutas de tela, com a largura já alinhada
    /// por RF-077.
    /// </summary>
    public required IReadOnlyList<Rect> Captures { get; init; }

    /// <summary>
    /// Retângulos de exclusão. NÃO passam pelo alinhamento de largura de RF-077: eles não
    /// são entregues à captura, são subtraídos da imagem já capturada.
    /// </summary>
    public required IReadOnlyList<Rect> Exclusions { get; init; }

    /// <summary>RF-078 — Grupos de cor ativos de cada retângulo de captura, na mesma ordem.</summary>
    public required IReadOnlyList<IReadOnlyList<bool>> ColorGroups { get; init; }

    /// <summary>
    /// RF-066 — Áreas normais persistidas, SEMPRE registradas mesmo quando não entram na
    /// lista de captura (por causa de um instantâneo ou do modo "somente área do mouse").
    /// É esta lista que vai para o perfil e que numera os índices 0..N−1.
    /// </summary>
    public required IReadOnlyList<Rect> PersistedAreas { get; init; }

    public int Count => Captures.Count;

    /// <summary>
    /// RF-068 — Traduz as exclusões para as coordenadas da IMAGEM de uma região, que é o
    /// espaço em que o pré-processamento as espera (cap. 13).
    ///
    /// "Uma área decremental só tem efeito sobre a parte de si que cai dentro de alguma área
    /// incremental; fora disso ela é inócua, e isso não é erro." — daí o recorte pelo
    /// retângulo da região e o descarte das que não o tocam.
    ///
    /// As coordenadas são as da imagem ANTES da ampliação: RF-101 manda remover as porções
    /// excluídas antes de qualquer outro tratamento, e a ampliação é a última etapa.
    /// </summary>
    public IReadOnlyList<Rect> ExclusionsIn(int captureIndex)
    {
        if (captureIndex < 0 || captureIndex >= Captures.Count) return Array.Empty<Rect>();

        var region = Captures[captureIndex];
        var result = new List<Rect>();

        foreach (var exclusion in Exclusions)
        {
            var overlap = exclusion.Intersect(region);
            if (overlap.IsEmpty) continue;   // inócua: fora desta área incremental
            result.Add(overlap.Offset(-region.X, -region.Y));
        }
        return result;
    }
}
