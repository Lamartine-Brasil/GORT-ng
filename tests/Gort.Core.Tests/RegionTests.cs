using Gort.Core.Calibration;
using Gort.Core.Model;
using Gort.Core.Regions;
using Gort.Core.Ui;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>RF-073 a RF-077 — Conversão de moldura para retângulo de captura.</summary>
public class FrameGeometryTests
{
    [Fact]
    public void RF_074_as_espessuras_base_sao_P14_P15_e_P16()
    {
        var m = FrameGeometry.MetricsFor(1.0);
        Assert.Equal(P.FrameBorderThickness, m.Border);            // 3
        Assert.Equal(P.FrameOuterBorderThickness, m.OuterBorder);  // 8
        Assert.Equal(P.FrameTitleBarHeight, m.TitleBar);           // 20
    }

    [Fact]
    public void RF_056_a_zona_sensivel_e_a_soma_das_tres_espessuras_P11()
        => Assert.Equal(P.FrameResizeHotZone, FrameGeometry.MetricsFor(1.0).ResizeHotZone);

    [Fact]
    public void RF_073_a_conversao_desconta_a_borda_e_a_barra_de_titulo()
    {
        // Moldura de 400x300 em (100, 200), escala 1: borda 3, barra 20.
        var capture = FrameGeometry.ToCaptureRect(new Rect(100, 200, 400, 300),
                                                  FrameGeometry.MetricsFor(1.0));
        Assert.Equal(new Rect(103, 220, 394, 277), capture);
        //            x+3  y+20  400−2*3  300−20−3
    }

    [Fact]
    public void RF_073_dimensoes_tem_minimo_de_um_pixel()
    {
        var capture = FrameGeometry.ToCaptureRect(new Rect(0, 0, 1, 1),
                                                  FrameGeometry.MetricsFor(1.0));
        Assert.Equal(1, capture.Width);
        Assert.Equal(1, capture.Height);
    }

    /// <summary>
    /// RF-074 / RF-075 — As espessuras são escaladas pelo DPI do monitor. É o cálculo que,
    /// com um fator global único, erra em um dos monitores e desalinha a sobreposição.
    /// </summary>
    [Fact]
    public void RF_074_as_espessuras_escalam_com_o_dpi()
    {
        var m150 = FrameGeometry.MetricsFor(1.5);
        Assert.Equal(5, m150.Border);      // teto de 3 × 1,5 = 4,5 → 5
        Assert.Equal(30, m150.TitleBar);   // 20 × 1,5

        var a = FrameGeometry.ToCaptureRect(new Rect(0, 0, 400, 300), FrameGeometry.MetricsFor(1.0));
        var b = FrameGeometry.ToCaptureRect(new Rect(0, 0, 400, 300), m150);
        Assert.NotEqual(a, b);   // a mesma moldura em escalas diferentes dá retângulos diferentes
    }

    [Fact]
    public void A_conversao_de_ida_e_volta_preserva_o_retangulo_de_captura()
    {
        var m = FrameGeometry.MetricsFor(1.0);
        var capture = new Rect(103, 220, 394, 277);
        Assert.Equal(capture, FrameGeometry.ToCaptureRect(FrameGeometry.ToFrameRect(capture, m), m));
    }

    [Theory]
    // RF-077 / P-144 — arredondamento para cima até o próximo múltiplo de 4.
    [InlineData(1, 4)]
    [InlineData(3, 4)]
    [InlineData(4, 4)]      // já múltiplo: permanece
    [InlineData(5, 8)]
    [InlineData(100, 100)]
    [InlineData(101, 104)]
    public void RF_077_a_largura_e_alinhada_em_multiplos_de_4(int entrada, int esperado)
        => Assert.Equal(esperado, FrameGeometry.AlignWidth(entrada));

    [Fact]
    public void RF_077_o_alinhamento_nao_mexe_na_altura_nem_na_origem()
    {
        var r = FrameGeometry.AlignWidth(new Rect(10, 20, 101, 55));
        Assert.Equal(new Rect(10, 20, 104, 55), r);
    }

    [Fact]
    public void RF_057_a_moldura_nao_fica_menor_que_P12()
    {
        var r = FrameGeometry.ClampToMinimumSize(new Rect(0, 0, 10, 10));
        Assert.Equal(P.FrameMinWidth, r.Width);
        Assert.Equal(P.FrameMinHeight, r.Height);
    }

    /// <summary>
    /// RF-058 — Ao soltar, a moldura volta para dentro dos limites se saiu pela ESQUERDA ou
    /// pelo TOPO. Sair pela direita ou por baixo é legítimo.
    /// </summary>
    [Fact]
    public void RF_058_a_moldura_volta_pela_esquerda_e_pelo_topo_mas_nao_pelos_outros_lados()
    {
        var desktop = new Rect(0, 0, 1920, 1080);

        Assert.Equal(new Rect(0, 0, 200, 100),
            FrameGeometry.ClampIntoDesktop(new Rect(-50, -30, 200, 100), desktop));

        // Ultrapassando à direita e por baixo: preservada.
        var transbordando = new Rect(1850, 1040, 200, 100);
        Assert.Equal(transbordando, FrameGeometry.ClampIntoDesktop(transbordando, desktop));
    }

    [Fact]
    public void RF_058_com_area_de_trabalho_de_origem_negativa_o_limite_e_o_dela()
    {
        var desktop = new Rect(-1920, 0, 3840, 1080);
        Assert.Equal(new Rect(-1920, 0, 200, 100),
            FrameGeometry.ClampIntoDesktop(new Rect(-2000, -10, 200, 100), desktop));
    }

    [Theory]
    // RF-052 / P-145 — até 4 px em qualquer dimensão é clique acidental.
    [InlineData(4, 100, true)]
    [InlineData(100, 4, true)]
    [InlineData(5, 5, false)]
    [InlineData(0, 0, true)]
    public void RF_052_retangulo_minusculo_e_descartado_como_clique(int w, int h, bool esperado)
        => Assert.Equal(esperado, FrameGeometry.IsAccidentalClick(new Rect(0, 0, w, h)));

    [Fact]
    public void RF_050_a_opacidade_da_camada_vem_do_alfa_da_cor_de_fundo_saturado_em_75()
    {
        // P-10: max(alfa, 75) ÷ 255 × 0,15
        Assert.Equal(75.0 / 255 * 0.15, FrameGeometry.SelectionOverlayOpacity(0), 9);
        Assert.Equal(75.0 / 255 * 0.15, FrameGeometry.SelectionOverlayOpacity(75), 9);
        Assert.Equal(255.0 / 255 * 0.15, FrameGeometry.SelectionOverlayOpacity(255), 9);
    }

    /// <summary>RF-456 — O CENTRO da área de captura fica sob o cursor.</summary>
    [Fact]
    public void RF_456_a_area_que_segue_o_mouse_centra_a_captura_no_cursor()
    {
        var m = FrameGeometry.MetricsFor(1.0);
        var frame = new Rect(0, 0, 206, 123);        // captura de 200 x 100
        var moved = FrameGeometry.PositionUnderCursor(frame, 500, 400, m);

        var capture = FrameGeometry.ToCaptureRect(moved, m);
        Assert.Equal(500, capture.X + capture.Width / 2);
        Assert.Equal(400, capture.Y + capture.Height / 2);
    }
}

/// <summary>Cap. 11 — Gerenciamento das regiões e a montagem da lista final.</summary>
public class RegionManagerTests
{
    private static RegionManager New(Func<Rect, double>? scale = null) => new(scale);

    /// <summary>
    /// Critério de aceite do capítulo 11: "Criar cinco áreas, remover a terceira, e as duas
    /// seguintes passam a exibir índices 3 e 4."
    /// </summary>
    [Fact]
    public void RF_064_remover_uma_area_reindexa_as_seguintes()
    {
        var m = New();
        for (int i = 0; i < 5; i++) m.AddArea(new Rect(i * 100, 0, 200, 100));

        // Índices exibidos são base 1: a terceira área é a de índice 2.
        var terceira = m.Areas[2];
        var quarta = m.Areas[3];
        var quinta = m.Areas[4];

        Assert.True(m.RemoveArea(2));

        Assert.Equal(4, m.Areas.Count);
        Assert.DoesNotContain(terceira, m.Areas);
        // A quarta passou a ocupar a posição 2 (exibida como 3) e a quinta a 3 (exibida como 4).
        Assert.Same(quarta, m.Areas[2]);
        Assert.Same(quinta, m.Areas[3]);
    }

    /// <summary>
    /// Critério de aceite: "Uma área desenhada sobre um monitor secundário com origem
    /// negativa é capturada corretamente." (RF-100)
    /// </summary>
    [Fact]
    public void RF_100_uma_area_de_origem_negativa_e_montada_corretamente()
    {
        var m = New();
        m.AddArea(new Rect(-1800, 100, 406, 223));   // captura de 400 x 200

        var built = m.Build();
        Assert.Single(built.Captures);

        var r = built.Captures[0];
        Assert.Equal(-1797, r.X);          // −1800 + 3
        Assert.Equal(120, r.Y);            // 100 + 20
        Assert.Equal(400, r.Width);        // 406 − 6, já múltiplo de 4
        Assert.Equal(200, r.Height);       // 223 − 20 − 3
    }

    [Fact]
    public void RF_065_sem_area_incremental_a_traducao_nao_pode_comecar()
    {
        var m = New();
        Assert.False(m.HasAnyIncrementalArea);

        m.AddExclusion(new Rect(0, 0, 100, 100));
        Assert.False(m.HasAnyIncrementalArea);   // exclusão sozinha não basta

        m.AddArea(new Rect(0, 0, 200, 100));
        Assert.True(m.HasAnyIncrementalArea);
    }

    [Fact]
    public void RF_067_qualquer_quantidade_de_areas_e_exclusoes()
    {
        var m = New();
        for (int i = 0; i < 50; i++)
        {
            m.AddArea(new Rect(i * 10, 0, 200, 100));
            m.AddExclusion(new Rect(i * 10, 0, 60, 60));
        }

        var built = m.Build();
        Assert.Equal(50, built.Captures.Count);
        Assert.Equal(50, built.Exclusions.Count);
    }

    [Fact]
    public void RF_077_as_exclusoes_NAO_passam_pelo_alinhamento_de_largura()
    {
        // A largura de captura seria 101 (não múltipla de 4) nos dois casos.
        var m = New();
        m.AddArea(new Rect(0, 0, 107, 123));
        m.AddExclusion(new Rect(0, 0, 107, 123));

        var built = m.Build();
        Assert.Equal(104, built.Captures[0].Width);     // alinhada
        Assert.Equal(101, built.Exclusions[0].Width);   // preservada
    }

    /// <summary>
    /// RF-070 — Quando há área instantânea, ela SUBSTITUI todas as demais, e seu retângulo é
    /// memorizado como "último instantâneo".
    /// </summary>
    [Fact]
    public void RF_070_o_instantaneo_substitui_todas_as_demais_areas()
    {
        var m = New();
        m.AddArea(new Rect(0, 0, 200, 100));
        m.AddArea(new Rect(300, 0, 200, 100));
        m.SetSnapshotArea(new Rect(50, 50, 306, 123));

        var built = m.Build();
        Assert.Single(built.Captures);
        Assert.Equal(built.Captures[0], m.LastSnapshot);

        // "registrar em áreas_persistidas — SEMPRE, mesmo se não entrar na lista".
        Assert.Equal(2, built.PersistedAreas.Count);
    }

    [Fact]
    public void RF_071_iniciar_uma_traducao_nao_instantanea_apaga_a_memoria_do_instantaneo()
    {
        var m = New();
        m.SetSnapshotArea(new Rect(0, 0, 200, 100));
        m.Build();
        Assert.NotNull(m.LastSnapshot);

        m.ClearSnapshotArea();
        m.ForgetLastSnapshot();
        Assert.Null(m.LastSnapshot);
    }

    [Fact]
    public void RF_069_a_area_rapida_entra_depois_das_normais()
    {
        var m = New();
        m.AddArea(new Rect(0, 0, 200, 100));
        m.SetQuickArea(new Rect(500, 0, 200, 100));

        var built = m.Build();
        Assert.Equal(2, built.Captures.Count);

        // Regra de índice: 0..N−1 normais, N a área rápida.
        Assert.Equal(built.Captures[1], m.ResolveAreaRect(1));
    }

    [Fact]
    public void RF_459_somente_a_area_que_segue_o_mouse_ignora_todas_as_outras()
    {
        var m = New();
        m.AddArea(new Rect(0, 0, 200, 100));
        m.SetQuickArea(new Rect(300, 0, 200, 100));
        m.SetMouseFollowArea(new Rect(600, 0, 206, 123));
        m.MouseFollowActive = true;
        m.MouseFollowOnly = true;

        var built = m.Build();
        Assert.Single(built.Captures);

        // Apenas o índice 0 é válido, e resolve para a área do mouse.
        Assert.NotNull(m.ResolveAreaRect(0));
        Assert.Null(m.ResolveAreaRect(1));
    }

    [Fact]
    public void Com_o_modo_do_mouse_ligado_mas_nao_exclusivo_ela_soma_as_demais()
    {
        var m = New();
        m.AddArea(new Rect(0, 0, 200, 100));
        m.SetMouseFollowArea(new Rect(600, 0, 206, 123));
        m.MouseFollowActive = true;
        m.MouseFollowOnly = false;

        Assert.Equal(2, m.Build().Captures.Count);
    }

    /// <summary>
    /// Regra de índice da consulta reversa: com instantâneo, QUALQUER índice resolve para o
    /// retângulo do instantâneo.
    /// </summary>
    [Fact]
    public void Com_instantaneo_qualquer_indice_resolve_para_ele()
    {
        var m = New();
        m.AddArea(new Rect(0, 0, 200, 100));
        m.SetSnapshotArea(new Rect(50, 50, 306, 123));

        var esperado = m.ResolveAreaRect(0);
        Assert.NotNull(esperado);
        Assert.Equal(esperado, m.ResolveAreaRect(7));
        Assert.Equal(esperado, m.ResolveAreaRect(99));
    }

    [Fact]
    public void RF_079_adicionar_um_grupo_de_cor_ativa_ele_em_todas_as_areas()
    {
        var m = New();
        m.AddArea(new Rect(0, 0, 200, 100));
        m.AddArea(new Rect(300, 0, 200, 100));
        Assert.All(m.Areas, a => Assert.Single(a.ActiveColorGroups));

        m.AddColorGroup();
        Assert.Equal(2, m.ColorGroupCount);
        Assert.All(m.Areas, a => Assert.Equal(new[] { true, true }, a.ActiveColorGroups));
    }

    [Fact]
    public void RF_079_remover_um_grupo_tira_ele_de_todas_as_areas()
    {
        var m = New();
        m.AddArea(new Rect(0, 0, 200, 100));
        m.AddColorGroup();
        m.AddColorGroup();
        m.Areas[0].ActiveColorGroups[1] = false;

        Assert.True(m.RemoveColorGroup(1));
        Assert.Equal(2, m.ColorGroupCount);
        Assert.Equal(new[] { true, true }, m.Areas[0].ActiveColorGroups);
    }

    /// <summary>RF-507 — Havendo apenas um grupo, a remoção é ignorada.</summary>
    [Fact]
    public void RF_507_com_um_unico_grupo_a_remocao_e_ignorada()
    {
        var m = New();
        m.AddArea(new Rect(0, 0, 200, 100));
        Assert.False(m.RemoveColorGroup(0));
        Assert.Equal(1, m.ColorGroupCount);
    }

    [Fact]
    public void RF_078_os_grupos_ativos_acompanham_cada_area_na_montagem()
    {
        var m = New();
        m.SetColorGroupCount(3);
        m.AddArea(new Rect(0, 0, 200, 100));
        m.AddArea(new Rect(300, 0, 200, 100));
        m.Areas[1].ActiveColorGroups[0] = false;

        var built = m.Build();
        Assert.Equal(new[] { true, true, true }, built.ColorGroups[0]);
        Assert.Equal(new[] { false, true, true }, built.ColorGroups[1]);
    }

    /// <summary>
    /// RF-061 / RF-062 — As áreas de arraste são temporárias: fechar sem aplicar reverte ao
    /// estado salvo anterior.
    /// </summary>
    [Fact]
    public void RF_061_cancelar_o_gerenciamento_reverte_as_areas_temporarias()
    {
        var m = New();
        m.AddArea(new Rect(0, 0, 200, 100));

        m.BeginTemporaryEditing();
        m.AddArea(new Rect(300, 0, 200, 100));
        m.Areas[0].FrameRect = new Rect(999, 999, 200, 100);
        Assert.Equal(2, m.Areas.Count);

        m.RollbackTemporaryEditing();
        Assert.Single(m.Areas);
        Assert.Equal(new Rect(0, 0, 200, 100), m.Areas[0].FrameRect);
    }

    [Fact]
    public void RF_062_aplicar_confirma_as_areas_temporarias()
    {
        var m = New();
        m.BeginTemporaryEditing();
        m.AddArea(new Rect(0, 0, 200, 100));
        m.CommitTemporaryEditing();

        m.RollbackTemporaryEditing();   // sem efeito depois de confirmado
        Assert.Single(m.Areas);
    }

    /// <summary>
    /// RF-066 — As áreas são persistidas e restauradas exatamente onde estavam. O usuário
    /// define a região uma vez e não repete o trabalho.
    /// </summary>
    [Fact]
    public void RF_066_as_areas_sobrevivem_a_ida_e_volta_pelo_perfil()
    {
        var original = New();
        original.SetColorGroupCount(2);
        original.AddArea(new Rect(100, 200, 406, 223));
        original.AddArea(new Rect(-500, 50, 306, 123));
        original.AddExclusion(new Rect(150, 250, 106, 73));
        original.Areas[1].ActiveColorGroups[0] = false;

        var (areas, exclusions, groups) = original.ToProfile();

        var restaurado = New();
        restaurado.SetColorGroupCount(2);
        restaurado.LoadFrom(areas, exclusions, groups.Select(g => (IReadOnlyList<bool>)g).ToList());

        var a = original.Build();
        var b = restaurado.Build();
        Assert.Equal(a.Captures, b.Captures);
        Assert.Equal(a.Exclusions, b.Exclusions);
        Assert.Equal(new[] { false, true }, b.ColorGroups[1]);
    }

    /// <summary>
    /// RF-075 / RF-076 — A escala é resolvida NO MOMENTO da conversão, a partir do monitor
    /// que contém a moldura. Duas molduras iguais em monitores de escalas diferentes
    /// produzem retângulos diferentes.
    /// </summary>
    [Fact]
    public void RF_075_cada_moldura_usa_a_escala_do_seu_proprio_monitor()
    {
        // Monitor esquerdo (x < 0) a 100%; principal a 150%.
        var m = New(frame => frame.X < 0 ? 1.0 : 1.5);
        m.AddArea(new Rect(-500, 0, 406, 223));
        m.AddArea(new Rect(500, 0, 406, 223));

        var built = m.Build();
        Assert.NotEqual(built.Captures[0].Height, built.Captures[1].Height);

        // Esquerdo: borda 3, barra 20 → altura 223 − 23 = 200.
        Assert.Equal(200, built.Captures[0].Height);
        // Principal: borda 5, barra 30 → altura 223 − 35 = 188.
        Assert.Equal(188, built.Captures[1].Height);
    }

    [Fact]
    public void RF_076_arrastar_a_moldura_para_outro_monitor_recalcula_a_escala()
    {
        var m = New(frame => frame.X < 0 ? 1.0 : 1.5);
        var area = m.AddArea(new Rect(-500, 0, 406, 223));
        int antes = m.Build().Captures[0].Height;

        area.FrameRect = area.FrameRect with { X = 500 };   // mudou de monitor
        int depois = m.Build().Captures[0].Height;

        Assert.NotEqual(antes, depois);
    }

    [Fact]
    public void RF_085_as_molduras_ficam_invisiveis_fora_da_definicao_de_areas()
    {
        var m = New();
        m.AddArea(new Rect(0, 0, 200, 100));
        m.AddExclusion(new Rect(10, 10, 60, 60));

        m.SetFramesVisible(true);
        Assert.All(m.Areas, a => Assert.True(a.Visible));

        m.SetFramesVisible(false);
        Assert.All(m.Areas, a => Assert.False(a.Visible));
        Assert.All(m.Exclusions, e => Assert.False(e.Visible));
    }

    [Fact]
    public void RF_462_destruir_a_area_do_mouse_desliga_o_modo()
    {
        var m = New();
        m.SetMouseFollowArea(new Rect(0, 0, 206, 123));
        m.MouseFollowActive = true;

        m.ClearMouseFollowArea();
        Assert.False(m.MouseFollowActive);
        Assert.Null(m.MouseFollowArea);
    }

    [Fact]
    public void RF_457_mover_para_a_mesma_posicao_nao_conta_como_mudanca()
    {
        var m = New();
        m.SetMouseFollowArea(new Rect(0, 0, 206, 123));

        Assert.True(m.MoveMouseFollowTo(500, 400));
        Assert.False(m.MoveMouseFollowTo(500, 400));   // sem mudança, sem recálculo
        Assert.True(m.MoveMouseFollowTo(501, 400));
    }
}

/// <summary>RF-059 / RF-060 / RF-457 — O portão do recálculo.</summary>
public class RecalculationGateTests
{
    /// <summary>
    /// Critério de aceite do capítulo 11: "Arrastar uma moldura por 5 segundos gera no
    /// máximo ~17 recálculos (5 s ÷ P-13), não um por evento de mouse."
    /// </summary>
    [Fact]
    public void RF_059_arrastar_por_cinco_segundos_gera_no_maximo_17_recalculos()
    {
        var agora = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var gate = new RecalculationGate(now: () => agora) { Enabled = true };

        int recalculos = 0;
        // Um evento de mouse a cada 8 ms durante 5 segundos: 625 eventos.
        for (int i = 0; i < 625; i++)
        {
            if (gate.ShouldRecalculate()) recalculos++;
            agora = agora.AddMilliseconds(8);
        }

        Assert.True(recalculos <= 17, $"gerou {recalculos} recálculos");
        Assert.True(recalculos >= 16, $"gerou só {recalculos} recálculos");
    }

    [Fact]
    public void RF_060_desabilitado_nenhuma_notificacao_passa()
    {
        var gate = new RecalculationGate { Enabled = false };
        for (int i = 0; i < 100; i++) Assert.False(gate.ShouldRecalculate());
    }

    [Fact]
    public void O_primeiro_recalculo_passa_imediatamente()
    {
        var gate = new RecalculationGate { Enabled = true };
        Assert.True(gate.ShouldRecalculate());
        Assert.False(gate.ShouldRecalculate());
    }

    [Fact]
    public void Reiniciar_o_portao_permite_aplicar_a_posicao_final_do_arraste()
    {
        var gate = new RecalculationGate { Enabled = true };
        gate.ShouldRecalculate();
        Assert.False(gate.ShouldRecalculate());

        gate.Reset();
        Assert.True(gate.ShouldRecalculate());
    }

    [Fact]
    public void RF_457_a_area_que_segue_o_mouse_usa_o_intervalo_proprio_P123()
    {
        var agora = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var gate = RecalculationGate.ForMouseFollow(() => agora);
        gate.Enabled = true;

        int recalculos = 0;
        // O temporizador de P-122 dispara a cada 30 ms; em 5 segundos são ~167 disparos.
        for (int i = 0; i < 167; i++)
        {
            if (gate.ShouldRecalculate()) recalculos++;
            agora = agora.AddMilliseconds(P.MouseFollowTimerMs);
        }

        // Critério de aceite do capítulo 23: "Mover o mouse rapidamente por 5 segundos não
        // gera mais de ~50 recálculos de área."
        Assert.True(recalculos <= 50, $"gerou {recalculos} recálculos");
    }
}

/// <summary>
/// RF-067 / RF-068 — A composição das áreas: a união das incrementais, MENOS a união das
/// decrementais, e o que acontece quando uma decremental cai fora.
/// </summary>
public class AreaCompositionTests
{
    /// <summary>
    /// RF-068 — Uma área decremental só tem efeito sobre a parte de si que cai DENTRO de
    /// alguma área incremental; fora disso ela é inócua, e isso não é erro.
    /// </summary>
    [Fact]
    public void RF_068_uma_exclusao_fora_de_qualquer_area_e_inocua()
    {
        var m = new RegionManager();
        m.AddArea(new Rect(0, 0, 406, 223));          // captura em (3,20) 400x200
        m.AddExclusion(new Rect(5000, 5000, 106, 73));

        var built = m.Build();
        Assert.Single(built.Exclusions);              // continua na lista
        Assert.Empty(built.ExclusionsIn(0));          // mas não afeta a região
    }

    [Fact]
    public void RF_068_uma_exclusao_parcial_e_recortada_pela_area()
    {
        var m = new RegionManager();
        m.AddArea(new Rect(0, 0, 406, 223));          // captura em (3,20) 400x200

        // Exclusão cuja captura fica em (103,120) 100x50, metade para fora pela direita.
        m.AddExclusion(new Rect(350, 100, 106, 73));

        var recortadas = m.ExclusionsInRegionZero();
        Assert.Single(recortadas);

        // Traduzida para coordenadas da imagem: origem relativa ao canto da região.
        var e = recortadas[0];
        Assert.Equal(350, e.X);                       // 353 − 3
        Assert.Equal(100, e.Y);                       // 120 − 20
        Assert.True(e.Right <= 400);                  // recortada pela largura da região
        Assert.True(e.Bottom <= 200);
    }

    [Fact]
    public void RF_068_a_exclusao_vale_para_cada_area_que_ela_toca()
    {
        var m = new RegionManager();
        m.AddArea(new Rect(0, 0, 206, 123));          // captura em (3,20) 200x100
        m.AddArea(new Rect(0, 200, 206, 123));        // captura em (3,220) 200x100
        m.AddExclusion(new Rect(0, 0, 106, 73));      // captura em (3,20) 100x50

        Assert.Single(m.Build().ExclusionsIn(0));
        Assert.Empty(m.Build().ExclusionsIn(1));      // não toca a segunda área
    }

    [Fact]
    public void As_coordenadas_traduzidas_sao_relativas_ao_canto_da_regiao()
    {
        var m = new RegionManager();
        m.AddArea(new Rect(1000, 500, 406, 223));     // captura em (1003,520)
        m.AddExclusion(new Rect(1050, 550, 106, 73)); // captura em (1053,570) 100x50

        var e = m.Build().ExclusionsIn(0)[0];
        Assert.Equal(new Rect(50, 50, 100, 50), e);   // 1053−1003, 570−520
    }

    [Fact]
    public void Um_indice_de_regiao_invalido_devolve_lista_vazia_em_vez_de_lancar()
    {
        var built = new RegionManager().Build();
        Assert.Empty(built.ExclusionsIn(0));
        Assert.Empty(built.ExclusionsIn(-1));
        Assert.Empty(built.ExclusionsIn(99));
    }
}

internal static class RegionManagerTestExtensions
{
    public static IReadOnlyList<Rect> ExclusionsInRegionZero(this RegionManager m)
        => m.Build().ExclusionsIn(0);
}

/// <summary>RF-518 / RF-519 — Geometria do controle remoto.</summary>
public class RemoteControlGeometryTests
{
    private static readonly Rect Original = new(100, 100, 200, 100);   // proporção 2:1

    /// <summary>
    /// RF-518 — Ao redimensionar por UMA BORDA, a outra dimensão é derivada da proporção.
    /// </summary>
    [Fact]
    public void RF_518_arrastar_a_borda_direita_deriva_a_altura_da_proporcao()
    {
        var r = RemoteControlGeometry.Resize(Original, ResizeEdge.Right, 100, 0);

        Assert.Equal(300, r.Width);
        Assert.Equal(150, r.Height);   // proporção 2:1 mantida
        Assert.Equal(100, r.X);        // a borda esquerda não se moveu
    }

    [Fact]
    public void RF_518_arrastar_a_borda_inferior_deriva_a_largura_da_proporcao()
    {
        var r = RemoteControlGeometry.Resize(Original, ResizeEdge.Bottom, 0, 50);

        Assert.Equal(300, r.Width);
        Assert.Equal(150, r.Height);
    }

    /// <summary>
    /// RF-518 — Ao redimensionar por um CANTO, usa-se o MAIOR fator dos dois eixos.
    /// </summary>
    [Fact]
    public void RF_518_no_canto_vence_o_maior_fator_dos_dois_eixos()
    {
        // O eixo X pede largura 250; o eixo Y pede altura 200, ou seja, largura 400.
        var r = RemoteControlGeometry.Resize(Original, ResizeEdge.BottomRight, 50, 100);

        Assert.Equal(400, r.Width);
        Assert.Equal(200, r.Height);
    }

    /// <summary>
    /// As bordas esquerda e superior movem a ORIGEM, para que a borda oposta fique parada —
    /// do contrário a janela escaparia debaixo do cursor.
    /// </summary>
    [Fact]
    public void RF_518_a_borda_esquerda_move_a_origem_e_mantem_a_direita_parada()
    {
        var r = RemoteControlGeometry.Resize(Original, ResizeEdge.Left, -100, 0);

        Assert.Equal(300, r.Width);
        Assert.Equal(Original.Right, r.Right);
    }

    [Fact]
    public void RF_518_a_borda_superior_mantem_a_inferior_parada()
    {
        var r = RemoteControlGeometry.Resize(Original, ResizeEdge.Top, 0, -50);
        Assert.Equal(Original.Bottom, r.Bottom);
    }

    [Fact]
    public void A_proporcao_e_preservada_em_qualquer_borda()
    {
        double esperado = (double)Original.Height / Original.Width;

        foreach (var edge in new[]
        {
            ResizeEdge.Left, ResizeEdge.Right, ResizeEdge.Top, ResizeEdge.Bottom,
            ResizeEdge.TopLeft, ResizeEdge.TopRight,
            ResizeEdge.BottomLeft, ResizeEdge.BottomRight,
        })
        {
            var r = RemoteControlGeometry.Resize(Original, edge, 70, 40);
            Assert.Equal(esperado, (double)r.Height / r.Width, 2);
        }
    }

    [Fact]
    public void A_janela_nao_encolhe_abaixo_do_minimo()
    {
        var r = RemoteControlGeometry.Resize(Original, ResizeEdge.Right, -1000, 0);
        Assert.Equal(RemoteControlGeometry.MinimumWidth, r.Width);
    }

    [Fact]
    public void Sem_borda_o_retangulo_nao_muda()
        => Assert.Equal(Original, RemoteControlGeometry.Resize(Original, ResizeEdge.None, 50, 50));

    /// <summary>RF-519 — Os controles internos escalam proporcionalmente.</summary>
    [Fact]
    public void RF_519_a_escala_do_conteudo_acompanha_a_largura()
    {
        Assert.Equal(1.0, RemoteControlGeometry.ContentScale(200, 200));
        Assert.Equal(2.0, RemoteControlGeometry.ContentScale(400, 200));
        Assert.Equal(0.5, RemoteControlGeometry.ContentScale(100, 200));
    }

    /// <summary>
    /// RF-518 — No miolo o gesto é ARRASTAR a janela, não redimensionar: ela é movível por
    /// qualquer ponto.
    /// </summary>
    [Fact]
    public void No_miolo_nao_ha_borda_e_o_gesto_e_arrastar()
    {
        Assert.Equal(ResizeEdge.None, RemoteControlGeometry.EdgeAt(100, 50, 200, 100, 8));
        Assert.Equal(ResizeEdge.Right, RemoteControlGeometry.EdgeAt(196, 50, 200, 100, 8));
        Assert.Equal(ResizeEdge.TopLeft, RemoteControlGeometry.EdgeAt(2, 2, 200, 100, 8));
        Assert.Equal(ResizeEdge.BottomRight, RemoteControlGeometry.EdgeAt(198, 98, 200, 100, 8));
    }
}
