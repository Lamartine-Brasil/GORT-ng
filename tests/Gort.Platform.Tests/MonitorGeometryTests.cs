using Gort.Core.Model;
using Gort.Platform.Monitors;
using Xunit;

namespace Gort.Platform.Tests;

/// <summary>
/// C18 e a geometria que depende dela: RF-075, RF-076, RF-086, RF-100, RF-344.
/// </summary>
public class MonitorGeometryTests
{
    /// <summary>
    /// Disposição com um monitor secundário À ESQUERDA do principal, que é o caso em que a
    /// origem fica NEGATIVA (RF-100, PARTE VIII).
    /// O secundário está a 100% e o principal a 150% — é exatamente a mistura de escalas
    /// que RF-075 existe para tratar.
    /// </summary>
    private static readonly List<MonitorInfo> Mistos = new()
    {
        new MonitorInfo(new Rect(-1920, 0, 1920, 1080), 1.0, IsPrimary: false, "esquerdo"),
        new MonitorInfo(new Rect(0, 0, 2560, 1440), 1.5, IsPrimary: true, "principal"),
    };

    [Fact]
    public void RF_344_a_area_de_trabalho_virtual_e_a_uniao_dos_monitores()
    {
        var uniao = MonitorGeometry.VirtualDesktop(Mistos);
        Assert.Equal(new Rect(-1920, 0, 4480, 1440), uniao);

        // PARTE VIII — a união pode ter origem negativa, e é dela que sai o deslocamento
        // entre a origem da união e a do monitor principal.
        Assert.True(uniao.Left < 0);
    }

    [Fact]
    public void RF_100_coordenadas_negativas_sao_suportadas()
    {
        var area = new Rect(-1800, 200, 400, 100);
        Assert.True(MonitorGeometry.IsFullyVisible(Mistos, area));
    }

    /// <summary>
    /// RF-075 / RF-076 — A moldura pertence ao monitor onde está o seu CANTO SUPERIOR
    /// ESQUERDO, e o fator é recalculado quando ela muda de monitor.
    ///
    /// Motivo: com um monitor a 100% e outro a 150%, um fator único erra em um dos dois e a
    /// região capturada sai deslocada alguns pixels; na sobreposição isso aparece como a
    /// tradução desalinhada em relação ao texto original.
    /// </summary>
    [Fact]
    public void RF_075_a_escala_e_a_do_monitor_que_contem_o_canto_superior_esquerdo()
    {
        Assert.Equal(1.0, MonitorGeometry.ScaleOf(Mistos, new Rect(-500, 100, 300, 80)));
        Assert.Equal(1.5, MonitorGeometry.ScaleOf(Mistos, new Rect(500, 100, 300, 80)));
    }

    [Fact]
    public void RF_076_uma_moldura_que_atravessa_a_fronteira_segue_o_canto_e_nao_a_area()
    {
        // Canto superior esquerdo no monitor da ESQUERDA, mas a maior parte da área no
        // principal. A regra é o canto, não a maioria.
        var atravessando = new Rect(-100, 100, 900, 80);
        Assert.Equal(1.0, MonitorGeometry.ScaleOf(Mistos, atravessando));
        Assert.Equal("esquerdo", MonitorGeometry.MonitorOf(Mistos, atravessando)!.Name);
    }

    [Fact]
    public void RF_075_um_canto_fora_de_todos_os_monitores_cai_para_a_maior_intersecao()
    {
        // Acima da área de trabalho: o canto não está em monitor nenhum.
        var acima = new Rect(500, -50, 300, 200);
        Assert.Equal("principal", MonitorGeometry.MonitorOf(Mistos, acima)!.Name);
    }

    [Fact]
    public void Sem_monitores_a_escala_cai_para_1_em_vez_de_falhar()
    {
        Assert.Equal(1.0, MonitorGeometry.ScaleOf(Array.Empty<MonitorInfo>(), new Rect(0, 0, 10, 10)));
        Assert.Null(MonitorGeometry.MonitorOf(Array.Empty<MonitorInfo>(), new Rect(0, 0, 10, 10)));
    }

    /// <summary>
    /// RF-086 — Quando a disposição dos monitores muda, o programa AVISA e aponta quais
    /// áreas ficaram inválidas. Nunca as reposiciona por conta própria, porque não tem como
    /// saber onde o conteúdo do jogo foi parar.
    /// </summary>
    [Fact]
    public void RF_086_areas_fora_da_area_de_trabalho_sao_apontadas()
    {
        var areas = new List<Rect>
        {
            new(100, 100, 200, 50),        // 0 — dentro do principal
            new(-1800, 100, 200, 50),      // 1 — dentro do esquerdo
            new(3000, 100, 200, 50),       // 2 — totalmente fora
            new(2500, 100, 200, 50),       // 3 — parcialmente fora, pela direita
        };

        Assert.Equal(new[] { 2, 3 }, MonitorGeometry.InvalidAreas(Mistos, areas));
    }

    [Fact]
    public void RF_086_uma_area_que_atravessa_dois_monitores_encostados_continua_valida()
    {
        // Os dois monitores se encostam em x = 0, sem vão: a área é coberta pela união.
        var atravessando = new Rect(-200, 100, 400, 50);
        Assert.True(MonitorGeometry.IsFullyVisible(Mistos, atravessando));
        Assert.Empty(MonitorGeometry.InvalidAreas(Mistos, new[] { atravessando }));
    }

    [Fact]
    public void RF_086_uma_area_sobre_um_vao_entre_monitores_e_invalida()
    {
        // Monitores separados por um vão de 100 px.
        var comVao = new List<MonitorInfo>
        {
            new(new Rect(0, 0, 800, 600), 1.0, true, "a"),
            new(new Rect(900, 0, 800, 600), 1.0, false, "b"),
        };
        Assert.False(MonitorGeometry.IsFullyVisible(comVao, new Rect(700, 100, 300, 50)));
    }

    [Fact]
    public void Uma_area_vazia_nunca_e_considerada_visivel()
        => Assert.False(MonitorGeometry.IsFullyVisible(Mistos, new Rect(0, 0, 0, 0)));

    [Fact]
    public void P_141_o_dpi_e_derivado_da_escala_pela_resolucao_de_referencia()
    {
        Assert.Equal(96, Mistos[0].Dpi);     // escala 1,0
        Assert.Equal(144, Mistos[1].Dpi);    // escala 1,5
    }
}
