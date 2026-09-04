using Gort.Core.Calibration;
using Gort.Core.Model;
using Gort.Core.Rendering;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>19.3 — Modo camada.</summary>
public class LayerLayoutTests
{
    /// <summary>
    /// RF-338 — Margem de P-86 em cima e à esquerda, e o mesmo valor descontado da largura
    /// e da altura.
    /// </summary>
    [Fact]
    public void RF_338_a_margem_do_texto_e_P86_e_o_desconto_e_o_mesmo_valor()
    {
        var r = LayerLayout.TextRect(500, 300);

        Assert.Equal(P.LayerTextMargin, r.X);
        Assert.Equal(P.LayerTextMargin, r.Y);
        Assert.Equal(500 - P.LayerTextMargin, r.Width);
        Assert.Equal(300 - P.LayerTextMargin, r.Height);
    }

    [Fact]
    public void RF_338_uma_janela_menor_que_a_margem_nao_produz_dimensao_negativa()
    {
        var r = LayerLayout.TextRect(5, 5);
        Assert.True(r.Width >= 0 && r.Height >= 0);
    }

    /// <summary>
    /// RF-337 — O fundo é medido pela extensão real do texto e expandido em P-82 à esquerda,
    /// P-83 acima, P-84 na largura e P-85 na altura. 🔒
    ///
    /// As quatro expansões são valores distintos: não são uma margem uniforme.
    /// </summary>
    [Fact]
    public void RF_337_o_fundo_expande_o_texto_pelos_quatro_valores_calibrados()
    {
        var texto = new RectD(100, 50, 200, 40);
        var fundo = LayerLayout.BackgroundRect(texto);

        Assert.Equal(100 - P.LayerBackgroundExpandLeft, fundo.X);      // P-82 = 8
        Assert.Equal(50 - P.LayerBackgroundExpandTop, fundo.Y);        // P-83 = 4
        Assert.Equal(200 + P.LayerBackgroundExpandWidth, fundo.Width); // P-84 = 16
        Assert.Equal(40 + P.LayerBackgroundExpandHeight, fundo.Height);// P-85 = 8
    }

    [Fact]
    public void RF_337_as_quatro_expansoes_sao_valores_distintos()
    {
        // Se alguém as unificar numa constante só, este teste quebra — que é o ponto.
        var valores = new[]
        {
            P.LayerBackgroundExpandLeft, P.LayerBackgroundExpandTop,
            P.LayerBackgroundExpandWidth, P.LayerBackgroundExpandHeight,
        };
        Assert.True(valores.Distinct().Count() > 1);
    }

    /// <summary>
    /// RF-333 / RF-334 — Parada, a janela é semitransparente (P-79) e recebe cliques.
    /// Traduzindo, fica totalmente transparente e atravessável.
    /// </summary>
    [Fact]
    public void RF_333_parada_a_janela_e_semitransparente_e_recebe_cliques()
    {
        Assert.Equal(P.LayerIdleBackgroundAlpha,
                     LayerLayout.BackgroundAlpha(translating: false, forcedTransparency: false));
        Assert.False(LayerLayout.ClickThrough(translating: false, forcedTransparency: false));
    }

    [Fact]
    public void RF_334_traduzindo_a_janela_fica_transparente_e_atravessavel()
    {
        Assert.Equal(0, LayerLayout.BackgroundAlpha(translating: true, forcedTransparency: false));
        Assert.True(LayerLayout.ClickThrough(translating: true, forcedTransparency: false));
    }

    /// <summary>
    /// RF-335 — A transparência forçada mantém o estado de tradução mesmo depois de parar.
    /// </summary>
    [Fact]
    public void RF_335_a_transparencia_forcada_sobrevive_a_parada()
    {
        Assert.Equal(0, LayerLayout.BackgroundAlpha(translating: false, forcedTransparency: true));
        Assert.True(LayerLayout.ClickThrough(translating: false, forcedTransparency: true));
    }

    /// <summary>RF-341 — Alinhamento vertical no topo ou na base.</summary>
    [Fact]
    public void RF_341_o_alinhamento_inferior_encosta_o_texto_na_base()
    {
        var area = new RectD(0, 0, 400, 200);

        Assert.Equal(0, LayerLayout.VerticalOffset(area, 60, VerticalAlignment.Top));
        Assert.Equal(140, LayerLayout.VerticalOffset(area, 60, VerticalAlignment.Bottom));
    }

    [Fact]
    public void RF_341_texto_maior_que_a_area_nao_desloca_para_cima_da_borda()
    {
        var area = new RectD(0, 0, 400, 100);
        Assert.Equal(0, LayerLayout.VerticalOffset(area, 300, VerticalAlignment.Bottom));
    }

    /// <summary>
    /// RF-343 — A janela de tradução intersectando uma área de OCR significa que ela está
    /// sendo capturada e traduzindo a si mesma.
    /// </summary>
    [Fact]
    public void RF_343_a_intersecao_com_uma_area_de_ocr_e_detectada()
    {
        var janela = new Rect(100, 100, 400, 200);

        Assert.True(LayerLayout.WouldCaptureItself(janela, new[] { new Rect(200, 150, 100, 50) }));
        Assert.True(LayerLayout.WouldCaptureItself(janela, new[] { new Rect(450, 250, 300, 300) }));
        Assert.False(LayerLayout.WouldCaptureItself(janela, new[] { new Rect(600, 600, 100, 50) }));
        Assert.False(LayerLayout.WouldCaptureItself(janela, Array.Empty<Rect>()));
    }

    [Fact]
    public void RF_343_areas_que_apenas_encostam_nao_contam_como_intersecao()
    {
        var janela = new Rect(100, 100, 400, 200);
        // Encostando exatamente na borda direita: não há pixel em comum.
        Assert.False(LayerLayout.WouldCaptureItself(janela, new[] { new Rect(500, 100, 50, 50) }));
    }
}

/// <summary>RF-342 — Aviso temporário.</summary>
public class TemporaryNoticeTests
{
    private DateTime _agora = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RF_342_o_aviso_e_prefixado_ao_texto_e_expira_sozinho()
    {
        var aviso = new TemporaryNotice(() => _agora);
        aviso.Show("a janela está sobre a área de OCR", P.WindowOverlapWarningDuration);

        Assert.Equal("a janela está sobre a área de OCR\n\nolá", aviso.Apply("olá"));

        // P-90 — passado o prazo, ele desaparece automaticamente.
        _agora = _agora.Add(P.WindowOverlapWarningDuration);
        Assert.Equal("olá", aviso.Apply("olá"));
        Assert.False(aviso.IsActive);
    }

    [Fact]
    public void RF_342_sem_texto_o_aviso_aparece_sozinho()
    {
        var aviso = new TemporaryNotice(() => _agora);
        aviso.Show("atenção", TimeSpan.FromSeconds(10));
        Assert.Equal("atenção", aviso.Apply(""));
    }

    [Fact]
    public void Um_aviso_novo_substitui_o_anterior()
    {
        var aviso = new TemporaryNotice(() => _agora);
        aviso.Show("primeiro", TimeSpan.FromSeconds(10));
        aviso.Show("segundo", TimeSpan.FromSeconds(10));
        Assert.Equal("segundo\n\nx", aviso.Apply("x"));
    }

    [Fact]
    public void Sem_aviso_o_texto_passa_intacto()
        => Assert.Equal("olá", new TemporaryNotice(() => _agora).Apply("olá"));
}

/// <summary>RF-387 a RF-391 — Fonte e cores.</summary>
public class FontAndColorTests
{
    private static readonly string[] Fallbacks = { "Reserva A", "Reserva B" };

    /// <summary>RF-387 — A escolha do usuário manda, quando a família existe de fato.</summary>
    [Fact]
    public void RF_387_a_familia_escolhida_pelo_usuario_tem_precedencia()
        => Assert.Equal("Minha Fonte",
            FontResolution.Resolve("Minha Fonte", "Fonte do Sistema", Fallbacks, _ => true));

    /// <summary>
    /// RF-387 — Sem escolha do usuário, usa-se a fonte de interface do SISTEMA. Fixar um
    /// nome faria o programa cair silenciosamente para uma substituta com métricas
    /// diferentes, e as métricas governam todo o layout da sobreposição.
    /// </summary>
    [Fact]
    public void RF_387_sem_escolha_usa_a_fonte_de_interface_do_sistema()
        => Assert.Equal("Fonte do Sistema",
            FontResolution.Resolve("", "Fonte do Sistema", Fallbacks, _ => true));

    [Fact]
    public void RF_387_uma_familia_escolhida_que_nao_existe_cai_para_a_do_sistema()
        => Assert.Equal("Fonte do Sistema",
            FontResolution.Resolve("Fonte Inexistente", "Fonte do Sistema", Fallbacks,
                                   f => f != "Fonte Inexistente"));

    [Fact]
    public void RF_387_sem_a_do_sistema_usa_a_primeira_da_lista_de_reserva()
        => Assert.Equal("Reserva A",
            FontResolution.Resolve("", "Fonte do Sistema", Fallbacks, f => f.StartsWith("Reserva")));

    [Fact]
    public void RF_387_a_lista_de_reserva_e_percorrida_em_ordem()
        => Assert.Equal("Reserva B",
            FontResolution.Resolve("", null, Fallbacks, f => f == "Reserva B"));

    [Fact]
    public void RF_387_sem_nenhuma_familia_disponivel_devolve_vazio()
        => Assert.Equal("", FontResolution.Resolve("", "Sistema", Fallbacks, _ => false));

    [Fact]
    public void RF_388_o_tamanho_padrao_e_P127()
        => Assert.Equal(P.DefaultFontSize, FontResolution.DefaultSize);

    /// <summary>RF-390 — As cores padrão são P-101 a P-104.</summary>
    [Fact]
    public void RF_390_as_cores_padrao_sao_as_calibradas()
    {
        var (texto, contorno1, contorno2, fundo) = TextColors.Defaults();

        Assert.Equal(new Rgba(255, 255, 255), texto);       // P-101
        Assert.Equal(new Rgba(192, 192, 192), contorno1);   // P-102
        Assert.Equal(new Rgba(0, 0, 0), contorno2);         // P-103
        Assert.Equal(new Rgba(0, 0, 0, 170), fundo);        // P-104
    }

    /// <summary>
    /// RF-391 — A caixa de amostra nunca exibe componente zero: 0 vira 1. Vale só para a
    /// amostra; a cor efetiva do desenho não muda.
    /// </summary>
    [Fact]
    public void RF_391_a_amostra_nunca_mostra_componente_zero()
    {
        var amostra = TextColors.ForSwatch(new Rgba(0, 0, 0, 170));

        Assert.Equal(1, amostra.R);
        Assert.Equal(1, amostra.G);
        Assert.Equal(1, amostra.B);
        Assert.Equal(170, amostra.A);   // o alfa não é tocado
    }

    [Fact]
    public void RF_391_componentes_nao_nulos_passam_intactos()
        => Assert.Equal(new Rgba(10, 200, 255), TextColors.ForSwatch(new Rgba(10, 200, 255)));
}
