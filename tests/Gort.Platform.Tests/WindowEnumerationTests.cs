using Gort.Core.Model;
using Gort.Platform;
using Gort.Platform.Capture;
using Xunit;

namespace Gort.Platform.Tests;

/// <summary>C2 / C3 — RF-089 a RF-092: seletor de janelas e captura de janela anexada.</summary>
public class WindowEnumerationTests
{
    private static IWindowEnumerator Enumerator()
    {
        using var platform = PlatformServices.Create();
        return platform.Windows;
    }

    /// <summary>
    /// RF-577 — Onde C3 não existe, a implementação responde que não sabe e EXPLICA por quê.
    /// Quem pergunta não precisa saber em que sistema está.
    /// </summary>
    [Fact]
    public void C3_ou_esta_disponivel_ou_explica_por_que_nao()
    {
        var windows = Enumerator();

        if (windows.IsAvailable) Assert.Null(windows.UnavailableReason);
        else Assert.False(string.IsNullOrWhiteSpace(windows.UnavailableReason));
    }

    /// <summary>
    /// RF-089 — A lista traz janelas identificáveis: sem identificador, sem nome ou sem área
    /// nenhuma escolha do usuário significaria alguma coisa.
    /// </summary>
    [Fact]
    public void RF_089_as_janelas_listadas_sao_identificaveis()
    {
        var windows = Enumerator();
        if (!windows.IsAvailable) return;

        foreach (var window in windows.List())
        {
            Assert.True(window.Id > 0);
            Assert.False(string.IsNullOrWhiteSpace(window.DisplayName));
            Assert.False(window.Bounds.IsEmpty);
        }
    }

    /// <summary>
    /// RF-090 / RF-097 — A existência é verificável. Uma janela inventada não existe, e é
    /// por essa pergunta que o laço descobre que a janela escolhida foi fechada.
    /// </summary>
    [Fact]
    public void RF_090_uma_janela_inexistente_e_reconhecida_como_tal()
    {
        var windows = Enumerator();
        if (!windows.IsAvailable) return;

        Assert.False(windows.Exists(0));
        Assert.False(windows.Exists(ulong.MaxValue));

        var listed = windows.List().FirstOrDefault();
        if (listed is not null) Assert.True(windows.Exists(listed.Id));
    }

    /// <summary>
    /// O nome exibido põe o APLICATIVO primeiro: é por ele que o usuário procura — ele sabe
    /// que abriu o jogo, não como a janela se chama.
    /// </summary>
    [Fact]
    public void O_nome_exibido_comeca_pelo_aplicativo()
    {
        var comTitulo = new CapturableWindow(1, "Fase 3", "Jogo", new Rect(0, 0, 100, 100));
        var semTitulo = new CapturableWindow(2, "", "Jogo", new Rect(0, 0, 100, 100));

        Assert.Equal("Jogo — Fase 3", comTitulo.DisplayName);
        Assert.Equal("Jogo", semTitulo.DisplayName);
    }

    /// <summary>
    /// C2 — A fonte de janela anexada só é utilizável DEPOIS de anexada: sem janela ela não
    /// tem de onde ler, e declarar-se utilizável faria o modo falhar no meio de uma tradução
    /// em vez de ficar desabilitado antes (RF-575).
    /// </summary>
    [Fact]
    public void C2_a_fonte_anexada_so_e_utilizavel_depois_de_anexar()
    {
        using var platform = PlatformServices.Create();
        var capture = platform.Capture;

        if (!platform.Windows.IsAvailable) return;

        capture.AttachToWindow(0, 0, 0);
        bool semJanela = capture.Supports(CaptureSource.AttachedWindow);

        var alvo = platform.Windows.List().FirstOrDefault();
        if (alvo is null) return;

        capture.AttachToWindow(alvo.Id, alvo.Bounds.X, alvo.Bounds.Y);
        bool comJanela = capture.Supports(CaptureSource.AttachedWindow);

        capture.AttachToWindow(0, 0, 0);

        Assert.False(semJanela);
        Assert.True(comJanela);
    }

    /// <summary>
    /// RF-092 — A origem do conteúdo capturado é a do quadro da janela, e é ela que vem em
    /// <c>ClientOrigin</c>: é o que alinha a sobreposição ao conteúdo. Uma origem zerada
    /// deslocaria a tradução conforme o usuário movesse a janela.
    /// </summary>
    [Fact]
    public void RF_092_a_captura_anexada_devolve_a_origem_do_quadro()
    {
        using var platform = PlatformServices.Create();
        if (!platform.Windows.IsAvailable) return;

        var alvo = platform.Windows.List().FirstOrDefault(w => w.Bounds.Width > 200);
        if (alvo is null) return;

        platform.Capture.AttachToWindow(alvo.Id, alvo.Bounds.X, alvo.Bounds.Y);
        try
        {
            var regions = platform.Capture.Capture(new CaptureRequest
            {
                Rects = new[] { new Rect(alvo.Bounds.X, alvo.Bounds.Y, 200, 120) },
                Source = CaptureSource.AttachedWindow,
            });

            if (regions.Count == 0) return;   // a janela pode ter sumido entre uma linha e outra

            Assert.Equal((alvo.Bounds.X, alvo.Bounds.Y), regions[0].ClientOrigin);
            Assert.Equal(200, regions[0].Image.Width);
        }
        finally
        {
            platform.Capture.AttachToWindow(0, 0, 0);
        }
    }

    /// <summary>Onde C3 não existe, a lista é vazia e nada explode.</summary>
    [Fact]
    public void Sem_C3_a_lista_e_vazia_e_nada_explode()
    {
        var none = new NoWindowEnumerator("não há seletor aqui");

        Assert.False(none.IsAvailable);
        Assert.Equal("não há seletor aqui", none.UnavailableReason);
        Assert.Empty(none.List());
        Assert.False(none.Exists(1));
    }
}
