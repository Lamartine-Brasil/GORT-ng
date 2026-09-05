using Gort.Core.Calibration;
using Gort.Core.Model;
using Gort.Core.Regions;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>RF-056 a RF-058 — Mover e redimensionar a moldura.</summary>
public class FrameResizeTests
{
    private static readonly Rect Frame = new(100, 100, 400, 300);
    private const int Zone = P.FrameResizeHotZone;         // P-11
    private const int TitleBar = P.FrameTitleBarHeight;    // P-16

    private static FrameHandle At(int x, int y)
        => FrameResize.HandleAt(Frame, x, y, Zone, TitleBar);

    /// <summary>
    /// RF-056 — Os cantos vencem os lados. Um ponto no canto casa com os dois critérios, e
    /// se o lado vencesse o arraste redimensionaria só um eixo.
    /// </summary>
    [Fact]
    public void RF_056_os_cantos_vencem_os_lados()
    {
        Assert.Equal(FrameHandle.TopLeft, At(2, 2));
        Assert.Equal(FrameHandle.TopRight, At(Frame.Width - 2, 2));
        Assert.Equal(FrameHandle.BottomLeft, At(2, Frame.Height - 2));
        Assert.Equal(FrameHandle.BottomRight, At(Frame.Width - 2, Frame.Height - 2));
    }

    [Fact]
    public void RF_056_os_quatro_lados()
    {
        int middleY = Frame.Height / 2;
        int middleX = Frame.Width / 2;

        Assert.Equal(FrameHandle.Left, At(2, middleY));
        Assert.Equal(FrameHandle.Right, At(Frame.Width - 2, middleY));
        Assert.Equal(FrameHandle.Top, At(middleX, TitleBar + 2));
        Assert.Equal(FrameHandle.Bottom, At(middleX, Frame.Height - 2));
    }

    /// <summary>RF-056 — A barra de título MOVE; o resto do topo redimensiona.</summary>
    [Fact]
    public void RF_056_a_barra_de_titulo_move()
    {
        Assert.Equal(FrameHandle.Move, At(Frame.Width / 2, 2));
        Assert.Equal(FrameHandle.Top, At(Frame.Width / 2, TitleBar + 1));
        Assert.Equal(FrameHandle.Move, At(Frame.Width / 2, Frame.Height / 2));
    }

    [Fact]
    public void Fora_da_moldura_nao_ha_alca()
    {
        Assert.Equal(FrameHandle.None, FrameResize.HandleAt(Frame, -1, 10, Zone, TitleBar));
        Assert.Equal(FrameHandle.None,
            FrameResize.HandleAt(Frame, Frame.Width, 10, Zone, TitleBar));
    }

    /// <summary>
    /// Numa moldura do tamanho mínimo de P-12, a zona sensível de P-11 é maior que metade
    /// da moldura: as bordas opostas se sobreporiam e um lado sempre venceria o outro. A
    /// zona é limitada à metade para que os dois lados continuem alcançáveis.
    /// </summary>
    [Fact]
    public void Numa_moldura_minima_os_dois_lados_continuam_alcancaveis()
    {
        var small = new Rect(0, 0, P.FrameMinWidth, P.FrameMinHeight);

        Assert.Equal(FrameHandle.TopLeft, FrameResize.HandleAt(small, 1, 1, Zone, TitleBar));
        Assert.Equal(FrameHandle.BottomRight,
            FrameResize.HandleAt(small, P.FrameMinWidth - 1, P.FrameMinHeight - 1,
                                 Zone, TitleBar));
    }

    [Fact]
    public void Mover_desloca_sem_mudar_o_tamanho()
    {
        var moved = FrameResize.Apply(Frame, FrameHandle.Move, 30, -20);

        Assert.Equal(new Rect(130, 80, 400, 300), moved);
    }

    [Theory]
    [InlineData(FrameHandle.Left, 50, 0, 150, 100, 350, 300)]
    [InlineData(FrameHandle.Right, 50, 0, 100, 100, 450, 300)]
    [InlineData(FrameHandle.Top, 0, 50, 100, 150, 400, 250)]
    [InlineData(FrameHandle.Bottom, 0, 50, 100, 100, 400, 350)]
    [InlineData(FrameHandle.TopLeft, 20, 20, 120, 120, 380, 280)]
    [InlineData(FrameHandle.BottomRight, -20, -20, 100, 100, 380, 280)]
    public void Cada_alca_move_a_sua_borda(
        FrameHandle handle, int dx, int dy, int x, int y, int width, int height)
    {
        Assert.Equal(new Rect(x, y, width, height),
                     FrameResize.Apply(Frame, handle, dx, dy));
    }

    /// <summary>
    /// RF-057 — A moldura nunca fica menor que P-12. O limite trava a BORDA QUE SE MOVE:
    /// empurrar o lado parado faria a moldura fugir do cursor.
    /// </summary>
    [Fact]
    public void RF_057_o_minimo_trava_a_borda_que_se_move()
    {
        // Arrastando a esquerda muito para a direita: a esquerda para, a direita fica.
        var result = FrameResize.Apply(Frame, FrameHandle.Left, 1000, 0);

        Assert.Equal(P.FrameMinWidth, result.Width);
        Assert.Equal(Frame.X + Frame.Width, result.X + result.Width);

        // E o mesmo pelo outro lado: a direita para, a esquerda fica.
        var other = FrameResize.Apply(Frame, FrameHandle.Right, -1000, 0);

        Assert.Equal(P.FrameMinWidth, other.Width);
        Assert.Equal(Frame.X, other.X);
    }

    [Fact]
    public void RF_057_vale_para_a_altura_tambem()
    {
        var result = FrameResize.Apply(Frame, FrameHandle.Top, 0, 1000);

        Assert.Equal(P.FrameMinHeight, result.Height);
        Assert.Equal(Frame.Y + Frame.Height, result.Y + result.Height);
    }

    /// <summary>
    /// RF-058 — Ao soltar, a moldura volta para dentro se saiu pela ESQUERDA ou pelo TOPO —
    /// e só por esses dois lados.
    ///
    /// Sair pela direita ou por baixo deixa a barra de título visível e a moldura pode ser
    /// trazida de volta; sair pela esquerda ou pelo topo leva a barra junto, e com ela some
    /// o único ponto por onde ela pode ser agarrada.
    /// </summary>
    [Fact]
    public void RF_058_so_a_esquerda_e_o_topo_trazem_a_moldura_de_volta()
    {
        var desktop = new Rect(0, 0, 1920, 1080);

        Assert.Equal(new Rect(0, 100, 400, 300),
            FrameResize.BringBack(new Rect(-250, 100, 400, 300), desktop));

        Assert.Equal(new Rect(100, 0, 400, 300),
            FrameResize.BringBack(new Rect(100, -80, 400, 300), desktop));

        // Pela direita e por baixo, nada muda.
        var outRight = new Rect(1900, 1050, 400, 300);
        Assert.Equal(outRight, FrameResize.BringBack(outRight, desktop));
    }

    /// <summary>
    /// A área de trabalho virtual pode começar em coordenada NEGATIVA — um monitor à
    /// esquerda do principal (RF-100). O limite é a borda dela, não o zero.
    /// </summary>
    [Fact]
    public void RF_058_respeita_uma_area_de_trabalho_que_comeca_em_negativo()
    {
        var desktop = new Rect(-1920, -200, 3840, 1280);

        Assert.Equal(new Rect(-1920, -200, 400, 300),
            FrameResize.BringBack(new Rect(-3000, -900, 400, 300), desktop));

        var inside = new Rect(-1000, -100, 400, 300);
        Assert.Equal(inside, FrameResize.BringBack(inside, desktop));
    }
}
