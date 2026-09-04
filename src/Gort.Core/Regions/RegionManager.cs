using Gort.Core.Model;

namespace Gort.Core.Regions;

/// <summary>
/// Cap. 11 — Gerenciamento da região de captura.
///
/// Contrato (6.1): recebe ações do usuário, a configuração carregada e o estado dos modos
/// especiais; devolve a lista ordenada de retângulos a capturar, a lista de exclusões e,
/// para cada área, a lista de grupos de cor ativos. Não captura imagem, não conhece OCR e
/// não desenha tradução.
///
/// Princípio do módulo: definir o retângulo é ETAPA OBRIGATÓRIA. Sem ao menos uma área não
/// há o que traduzir (RF-065). Como é obrigatória, ela não se repete a cada abertura: as
/// áreas são persistidas e restauradas exatamente onde estavam (RF-066).
/// </summary>
public sealed class RegionManager
{
    private readonly List<CaptureFrame> _areas = new();
    private readonly List<CaptureFrame> _exclusions = new();
    private readonly Func<Rect, double> _scaleAt;

    /// <summary>
    /// RF-061 — Cópia do estado salvo, para que cancelar o gerenciamento de áreas reverta
    /// as alterações temporárias.
    /// </summary>
    private (List<CaptureFrame> Areas, List<CaptureFrame> Exclusions)? _backup;

    /// <param name="scaleAt">
    /// RF-075 — Resolve a escala do monitor que contém uma moldura, NO MOMENTO da conversão.
    /// Nunca um fator global lido uma vez na abertura do programa.
    /// </param>
    public RegionManager(Func<Rect, double>? scaleAt = null)
        => _scaleAt = scaleAt ?? (_ => 1.0);

    // ── Áreas ────────────────────────────────────────────────────────────────

    /// <summary>RF-067 — Qualquer quantidade de áreas incrementais, sem limite fixo.</summary>
    public IReadOnlyList<CaptureFrame> Areas => _areas;

    /// <summary>RF-067 — Qualquer quantidade de áreas decrementais.</summary>
    public IReadOnlyList<CaptureFrame> Exclusions => _exclusions;

    /// <summary>RF-069 — A área rápida: uma única, não persistida.</summary>
    public CaptureFrame? QuickArea { get; private set; }

    /// <summary>RF-454 — A área que segue o mouse.</summary>
    public CaptureFrame? MouseFollowArea { get; private set; }

    /// <summary>RF-070 — A área instantânea; quando presente, substitui todas as demais.</summary>
    public CaptureFrame? SnapshotArea { get; private set; }

    /// <summary>RF-070 — Retângulo do último instantâneo, memorizado.</summary>
    public Rect? LastSnapshot { get; private set; }

    /// <summary>RF-459 — "usar somente a área que segue o mouse". É o padrão.</summary>
    public bool MouseFollowOnly { get; set; } = true;

    /// <summary>Estado de ativação do modo que segue o mouse.</summary>
    public bool MouseFollowActive { get; set; }

    /// <summary>
    /// RF-065 — É preciso pelo menos uma área INCREMENTAL para iniciar qualquer tradução.
    /// Sem ela a tradução não começa e o programa explica que é preciso definir a área
    /// primeiro, oferecendo abrir a camada de seleção.
    /// </summary>
    public bool HasAnyIncrementalArea
        => _areas.Count > 0 || QuickArea is not null
           || SnapshotArea is not null || MouseFollowArea is not null;

    public CaptureFrame AddArea(Rect frameRect)
    {
        var frame = new CaptureFrame(frameRect, AreaKind.Normal);
        SyncColorGroups(frame);
        _areas.Add(frame);
        return frame;
    }

    public CaptureFrame AddExclusion(Rect frameRect)
    {
        var frame = new CaptureFrame(frameRect, AreaKind.Exclusion);
        _exclusions.Add(frame);
        return frame;
    }

    /// <summary>
    /// RF-064 — Ao remover uma área, as de índice maior decrementam. É consequência direta
    /// de a lista ser ordenada: quem exibe o índice lê a posição atual.
    /// </summary>
    public bool RemoveArea(int index)
    {
        if (index < 0 || index >= _areas.Count) return false;
        _areas.RemoveAt(index);
        return true;
    }

    public bool RemoveExclusion(int index)
    {
        if (index < 0 || index >= _exclusions.Count) return false;
        _exclusions.RemoveAt(index);
        return true;
    }

    /// <summary>RF-062 — "limpar todas".</summary>
    public void ClearAll()
    {
        _areas.Clear();
        _exclusions.Clear();
        QuickArea = null;
    }

    // ── Áreas especiais ──────────────────────────────────────────────────────

    /// <summary>RF-069 — Cria ou substitui a área rápida.</summary>
    public CaptureFrame SetQuickArea(Rect frameRect)
    {
        QuickArea = new CaptureFrame(frameRect, AreaKind.Quick);
        SyncColorGroups(QuickArea);
        return QuickArea;
    }

    public void ClearQuickArea() => QuickArea = null;

    /// <summary>RF-070 — Define a área instantânea.</summary>
    public CaptureFrame SetSnapshotArea(Rect frameRect)
    {
        SnapshotArea = new CaptureFrame(frameRect, AreaKind.Snapshot);
        SyncColorGroups(SnapshotArea);
        return SnapshotArea;
    }

    public void ClearSnapshotArea() => SnapshotArea = null;

    /// <summary>
    /// RF-071 — Ao iniciar uma tradução que NÃO é instantânea, a memória de "último
    /// instantâneo" é apagada.
    /// </summary>
    public void ForgetLastSnapshot() => LastSnapshot = null;

    /// <summary>RF-454 — Cria ou substitui a área que segue o mouse.</summary>
    public CaptureFrame SetMouseFollowArea(Rect frameRect)
    {
        MouseFollowArea = new CaptureFrame(frameRect, AreaKind.MouseFollow);
        SyncColorGroups(MouseFollowArea);
        return MouseFollowArea;
    }

    /// <summary>RF-462 — Se a área alvo for destruída, o modo se desliga automaticamente.</summary>
    public void ClearMouseFollowArea()
    {
        MouseFollowArea = null;
        MouseFollowActive = false;
    }

    /// <summary>
    /// RF-455 / RF-456 — Reposiciona a área que segue o mouse de modo que seu CENTRO fique
    /// sob o cursor. Devolve verdadeiro quando a posição efetivamente mudou — RF-457 só
    /// dispara o recálculo nesse caso.
    /// </summary>
    public bool MoveMouseFollowTo(int cursorX, int cursorY)
    {
        if (MouseFollowArea is null) return false;

        var metrics = FrameGeometry.MetricsFor(_scaleAt(MouseFollowArea.FrameRect));
        var moved = FrameGeometry.PositionUnderCursor(
            MouseFollowArea.FrameRect, cursorX, cursorY, metrics);

        if (moved == MouseFollowArea.FrameRect) return false;
        MouseFollowArea.FrameRect = moved;
        return true;
    }

    // ── Grupos de cor ────────────────────────────────────────────────────────

    /// <summary>Quantidade de grupos de cor configurados no perfil.</summary>
    public int ColorGroupCount { get; private set; } = 1;

    /// <summary>
    /// RF-079 — Ao ADICIONAR um grupo de cor, todas as áreas passam a incluí-lo por padrão.
    /// </summary>
    public void AddColorGroup()
    {
        ColorGroupCount++;
        foreach (var f in AllFrames()) f.ActiveColorGroups.Add(true);
    }

    /// <summary>
    /// RF-079 — Ao REMOVER um grupo, ele sai da lista de todas as áreas.
    /// RF-507 — Se houver apenas um grupo, a remoção é ignorada.
    /// </summary>
    public bool RemoveColorGroup(int index)
    {
        if (ColorGroupCount <= 1) return false;
        if (index < 0 || index >= ColorGroupCount) return false;

        ColorGroupCount--;
        foreach (var f in AllFrames())
        {
            if (index < f.ActiveColorGroups.Count) f.ActiveColorGroups.RemoveAt(index);
        }
        return true;
    }

    /// <summary>Ajusta a quantidade de grupos ao carregar um perfil.</summary>
    public void SetColorGroupCount(int count)
    {
        ColorGroupCount = Math.Max(1, count);
        foreach (var f in AllFrames()) SyncColorGroups(f);
    }

    /// <summary>
    /// RF-079 — Toda área conhece todos os grupos. Um grupo recém-visto entra ativo, que é
    /// o padrão do requisito.
    /// </summary>
    private void SyncColorGroups(CaptureFrame frame)
    {
        while (frame.ActiveColorGroups.Count < ColorGroupCount) frame.ActiveColorGroups.Add(true);
        while (frame.ActiveColorGroups.Count > ColorGroupCount)
            frame.ActiveColorGroups.RemoveAt(frame.ActiveColorGroups.Count - 1);
    }

    private IEnumerable<CaptureFrame> AllFrames()
    {
        foreach (var f in _areas) yield return f;
        if (QuickArea is not null) yield return QuickArea;
        if (MouseFollowArea is not null) yield return MouseFollowArea;
        if (SnapshotArea is not null) yield return SnapshotArea;
    }

    // ── RF-061: áreas temporárias ────────────────────────────────────────────

    /// <summary>
    /// RF-061 — As áreas resultantes de arraste são aplicadas como TEMPORÁRIAS: se o usuário
    /// cancelar o gerenciamento sem confirmar, elas voltam ao estado salvo anterior.
    /// </summary>
    public void BeginTemporaryEditing()
        => _backup = (_areas.Select(f => f.Clone()).ToList(),
                      _exclusions.Select(f => f.Clone()).ToList());

    /// <summary>RF-062 — "Aplicar" confirma as áreas temporárias.</summary>
    public void CommitTemporaryEditing() => _backup = null;

    /// <summary>RF-062 / RF-533 — Fechar sem aplicar reverte ao estado salvo.</summary>
    public void RollbackTemporaryEditing()
    {
        if (_backup is null) return;

        _areas.Clear();
        _areas.AddRange(_backup.Value.Areas);
        _exclusions.Clear();
        _exclusions.AddRange(_backup.Value.Exclusions);
        _backup = null;
    }

    /// <summary>RF-085 — As molduras só ficam visíveis enquanto o usuário define as áreas.</summary>
    public void SetFramesVisible(bool visible)
    {
        foreach (var f in _areas) f.Visible = visible;
        foreach (var f in _exclusions) f.Visible = visible;
        if (QuickArea is not null) QuickArea.Visible = visible;
    }

    // ── Montagem da lista final ──────────────────────────────────────────────

    /// <summary>
    /// Comportamento detalhado do cap. 11 — <c>montar_áreas()</c>.
    ///
    /// Segue literalmente o pseudocódigo da especificação, inclusive na ordem em que os
    /// retângulos entram na lista, que é o que define os índices usados pela consulta
    /// reversa do desenho da sobreposição.
    /// </summary>
    public BuiltAreas Build()
    {
        var captures = new List<Rect>();
        var colorGroups = new List<IReadOnlyList<bool>>();

        bool onlyMouse = MouseFollowActive && MouseFollowOnly;
        bool hasSnapshot = SnapshotArea is not null;

        void Add(CaptureFrame frame)
        {
            captures.Add(FrameGeometry.ToCaptureRect(frame.FrameRect, _scaleAt));
            colorGroups.Add(frame.ActiveColorGroups.ToList());
        }

        if (hasSnapshot && !onlyMouse)
        {
            Add(SnapshotArea!);
            // RF-070 — o retângulo do instantâneo é memorizado como "último instantâneo".
            LastSnapshot = captures[^1];
        }

        // "para cada área normal: registrar em áreas_persistidas — SEMPRE, mesmo se não
        //  entrar na lista".
        var persisted = new List<Rect>(_areas.Count);
        foreach (var frame in _areas)
        {
            persisted.Add(FrameGeometry.ToCaptureRect(frame.FrameRect, _scaleAt));
            if (!hasSnapshot && !onlyMouse) Add(frame);
        }

        var exclusions = _exclusions
            .Select(f => FrameGeometry.ToCaptureRect(f.FrameRect, _scaleAt))
            .ToList();

        if (QuickArea is not null && !hasSnapshot && !onlyMouse) Add(QuickArea);

        if (MouseFollowActive && MouseFollowArea is not null && (!hasSnapshot || onlyMouse))
            Add(MouseFollowArea);

        // RF-077 — a largura de cada retângulo entregue à CAPTURA é arredondada para cima
        // até o próximo múltiplo de 4. As exclusões não passam por aqui: elas não são
        // capturadas, são subtraídas da imagem.
        for (int i = 0; i < captures.Count; i++) captures[i] = FrameGeometry.AlignWidth(captures[i]);

        return new BuiltAreas
        {
            Captures = captures,
            Exclusions = exclusions,
            ColorGroups = colorGroups,
            PersistedAreas = persisted,
        };
    }

    /// <summary>
    /// Regra de índice para CONSULTA REVERSA, usada pelo desenho da sobreposição (cap. 11):
    ///
    ///   - os índices 0..N−1 são as áreas normais persistidas;
    ///   - o índice N é a área rápida, se existir;
    ///   - o índice seguinte é a área que segue o mouse, se existir;
    ///   - quando há instantâneo, QUALQUER índice resolve para o retângulo do instantâneo;
    ///   - quando "somente área do mouse" está ativo, apenas o índice 0 é válido e resolve
    ///     para a área do mouse.
    /// </summary>
    public Rect? ResolveAreaRect(int index)
    {
        bool onlyMouse = MouseFollowActive && MouseFollowOnly;

        if (onlyMouse)
        {
            return index == 0 && MouseFollowArea is not null
                ? Convert(MouseFollowArea)
                : null;
        }

        if (SnapshotArea is not null) return Convert(SnapshotArea);

        if (index < 0) return null;
        if (index < _areas.Count) return Convert(_areas[index]);

        int next = _areas.Count;
        if (QuickArea is not null)
        {
            if (index == next) return Convert(QuickArea);
            next++;
        }

        if (MouseFollowActive && MouseFollowArea is not null && index == next)
            return Convert(MouseFollowArea);

        return null;
    }

    private Rect Convert(CaptureFrame frame)
        => FrameGeometry.AlignWidth(FrameGeometry.ToCaptureRect(frame.FrameRect, _scaleAt));

    // ── Persistência ─────────────────────────────────────────────────────────

    /// <summary>
    /// RF-040 / RF-066 — Recria as molduras a partir dos retângulos de CAPTURA salvos no
    /// perfil, nas mesmas posições, com o mesmo tamanho e a mesma ordem.
    /// </summary>
    public void LoadFrom(IReadOnlyList<Rect> areas, IReadOnlyList<Rect> exclusions,
                         IReadOnlyList<IReadOnlyList<bool>>? colorGroups = null)
    {
        _areas.Clear();
        _exclusions.Clear();
        _backup = null;

        for (int i = 0; i < areas.Count; i++)
        {
            var metrics = FrameGeometry.MetricsFor(_scaleAt(areas[i]));
            var frame = new CaptureFrame(FrameGeometry.ToFrameRect(areas[i], metrics));
            SyncColorGroups(frame);

            if (colorGroups is not null && i < colorGroups.Count)
            {
                for (int g = 0; g < frame.ActiveColorGroups.Count && g < colorGroups[i].Count; g++)
                    frame.ActiveColorGroups[g] = colorGroups[i][g];
            }
            _areas.Add(frame);
        }

        foreach (var rect in exclusions)
        {
            var metrics = FrameGeometry.MetricsFor(_scaleAt(rect));
            _exclusions.Add(new CaptureFrame(FrameGeometry.ToFrameRect(rect, metrics),
                                             AreaKind.Exclusion));
        }
    }

    /// <summary>
    /// RF-066 — O que vai para o perfil: os retângulos de CAPTURA, não os das molduras.
    /// RF-069 / PARTE XI item 16 — a área rápida e a que segue o mouse NÃO são persistidas.
    /// </summary>
    public (List<Rect> Areas, List<Rect> Exclusions, List<List<bool>> ColorGroups) ToProfile()
        => (_areas.Select(f => FrameGeometry.ToCaptureRect(f.FrameRect, _scaleAt)).ToList(),
            _exclusions.Select(f => FrameGeometry.ToCaptureRect(f.FrameRect, _scaleAt)).ToList(),
            _areas.Select(f => f.ActiveColorGroups.ToList()).ToList());
}
