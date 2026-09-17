using Gort.Core.Calibration;
using Gort.Core.Model;
using Gort.Core.Regions;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>RF-093 a RF-096 🔒 — O buffer de quadros da janela anexada.</summary>
public class AttachedFrameBufferTests
{
    private static ImageBuffer Frame(byte tone = 200)
    {
        var image = ImageBuffer.Allocate(40, 20, PixelFormat.Bgra32);
        for (int i = 0; i < image.Pixels.Length; i++) image.Pixels[i] = tone;
        return image;
    }

    /// <summary>Um relógio que o teste controla — medir tempo de verdade daria teste instável.</summary>
    private sealed class Clock
    {
        public DateTime Now { get; set; } = new(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
        public void Advance(TimeSpan by) => Now += by;
        public DateTime Read() => Now;
    }

    /// <summary>
    /// RF-093 — O pedido é atendido IMEDIATAMENTE com o último quadro válido. É a razão de
    /// o buffer existir: o laço é síncrono de ponta a ponta (RF-009), e esperar o próximo
    /// quadro do fluxo seria latência pura.
    /// </summary>
    [Fact]
    public void RF_093_o_primeiro_quadro_fica_disponivel_na_hora()
    {
        var buffer = new AttachedFrameBuffer();

        Assert.Null(buffer.Latest());
        Assert.True(buffer.Offer(Frame()));
        Assert.NotNull(buffer.Latest());
    }

    /// <summary>P-17 — A reserva tem cinco quadros; o mais antigo sai quando ela enche.</summary>
    [Fact]
    public void P17_a_reserva_nao_passa_de_cinco_quadros()
    {
        var buffer = new AttachedFrameBuffer();

        // `wanted` força a guarda de cada um, que é o caso do laço pedindo.
        for (int i = 0; i < 20; i++) buffer.Offer(Frame(), wanted: true);

        Assert.Equal(P.AttachedFrameBufferSize, buffer.Count);
        Assert.Equal(5, P.AttachedFrameBufferSize);
    }

    /// <summary>
    /// RF-094 / P-18 — Sem pedido, um quadro é guardado a cada dez recebidos. Guardar todos
    /// gastaria memória com quadros que ninguém vai ler — a janela pode entregar sessenta
    /// por segundo.
    /// </summary>
    [Fact]
    public void RF_094_sem_pedido_um_quadro_a_cada_dez_e_guardado()
    {
        var buffer = new AttachedFrameBuffer();

        // O primeiro entra sempre: um buffer vazio não serve para nada.
        Assert.True(buffer.Offer(Frame()));

        int kept = 0;
        for (int i = 0; i < 30; i++)
        {
            if (buffer.Offer(Frame())) kept++;
        }

        // Trinta quadros depois do primeiro: os múltiplos de dez da contagem total.
        Assert.Equal(3, kept);
        Assert.Equal(P.AttachedIdleCapturePeriodFrames, 10);
    }

    /// <summary>
    /// RF-095 / P-19 — Um quadro mais velho que 100 ms NÃO é utilizável: a tela já mudou, e
    /// desenhar sobre ela seria pôr tradução velha em conteúdo novo.
    /// </summary>
    [Fact]
    public void RF_095_um_quadro_velho_deixa_de_ser_utilizavel()
    {
        var clock = new Clock();
        var buffer = new AttachedFrameBuffer(clock.Read);

        buffer.Offer(Frame());
        Assert.NotNull(buffer.Latest());

        clock.Advance(TimeSpan.FromMilliseconds(99));
        Assert.NotNull(buffer.Latest());

        clock.Advance(TimeSpan.FromMilliseconds(2));
        Assert.Null(buffer.Latest());

        Assert.Equal(TimeSpan.FromSeconds(0.1), P.AttachedFrameMaxAge);
    }

    /// <summary>Um quadro novo revive o buffer: é o mais recente que conta, não o mais velho.</summary>
    [Fact]
    public void Um_quadro_novo_revive_o_buffer()
    {
        var clock = new Clock();
        var buffer = new AttachedFrameBuffer(clock.Read);

        buffer.Offer(Frame());
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(buffer.Latest());

        buffer.Offer(Frame(), wanted: true);
        Assert.NotNull(buffer.Latest());
    }

    /// <summary>
    /// RF-096 — Quando não há quadro utilizável, o laço tenta de novo a cada P-20 até obter
    /// um ou receber pedido de parada.
    /// </summary>
    [Fact]
    public void RF_096_a_espera_termina_quando_o_quadro_chega()
    {
        var buffer = new AttachedFrameBuffer();

        // Um quadro entra enquanto a espera corre.
        var feeder = Task.Run(() =>
        {
            Thread.Sleep(20);
            buffer.Offer(Frame(), wanted: true);
        });

        var frame = buffer.Wait(() => false, TimeSpan.FromSeconds(2));
        feeder.Wait();

        Assert.NotNull(frame);
    }

    /// <summary>RF-096 — O pedido de parada interrompe a espera.</summary>
    [Fact]
    public void RF_096_o_pedido_de_parada_interrompe_a_espera()
    {
        var buffer = new AttachedFrameBuffer();

        Assert.Null(buffer.Wait(() => true, TimeSpan.FromSeconds(30)));
    }

    /// <summary>
    /// RF-097 — O prazo existe para que uma janela fechada não deixe o laço girando aqui
    /// para sempre; esgotado o prazo, quem chamou encerra o laço.
    /// </summary>
    [Fact]
    public void RF_097_o_prazo_esgotado_devolve_nulo()
    {
        var buffer = new AttachedFrameBuffer();

        Assert.Null(buffer.Wait(() => false, TimeSpan.FromMilliseconds(30)));
    }

    [Fact]
    public void Um_quadro_vazio_nao_entra_no_buffer()
    {
        var buffer = new AttachedFrameBuffer();

        Assert.False(buffer.Offer(ImageBuffer.Allocate(0, 0, PixelFormat.Bgra32)));
        Assert.Equal(0, buffer.Count);
        Assert.Equal(0, buffer.Received);
    }

    [Fact]
    public void Limpar_esvazia_a_reserva_e_a_contagem()
    {
        var buffer = new AttachedFrameBuffer();
        for (int i = 0; i < 5; i++) buffer.Offer(Frame(), wanted: true);

        buffer.Clear();

        Assert.Equal(0, buffer.Count);
        Assert.Equal(0, buffer.Received);
        Assert.Null(buffer.Latest());
    }
}
