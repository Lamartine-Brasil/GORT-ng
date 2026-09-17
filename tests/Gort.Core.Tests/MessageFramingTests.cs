using System.Text;
using Gort.Core.Translation.Channel;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>RF-286 / RF-287 — O protocolo do canal do processo auxiliar.</summary>
public class MessageFramingTests
{
    /// <summary>
    /// RF-287 — Dois bytes de comprimento, BYTE ALTO PRIMEIRO, seguidos do conteúdo em
    /// codificação de 16 bits.
    ///
    /// A ordem dos bytes é o detalhe que se erra em silêncio: trocada, uma mensagem de 256
    /// bytes vira uma de 1, e a outra ponta lê um caractere e trava esperando o resto.
    /// </summary>
    [Fact]
    public void RF_287_o_comprimento_vem_em_dois_bytes_com_o_alto_primeiro()
    {
        // 130 caracteres × 2 bytes = 260 = 0x0104: exige os dois bytes para caber.
        string message = new('a', 130);
        byte[] framed = MessageFraming.Frame(message);

        Assert.Equal(0x01, framed[0]);   // byte alto
        Assert.Equal(0x04, framed[1]);   // byte baixo
        Assert.Equal(262, framed.Length);
        Assert.Equal(260, MessageFraming.LengthOf(framed));
    }

    [Fact]
    public void RF_287_o_conteudo_vai_em_codificacao_de_16_bits()
    {
        byte[] framed = MessageFraming.Frame("ab");

        Assert.Equal(4, MessageFraming.LengthOf(framed));
        Assert.Equal(new byte[] { 0x61, 0x00, 0x62, 0x00 }, framed[2..]);
    }

    [Fact]
    public void Uma_mensagem_vazia_tem_comprimento_zero()
    {
        byte[] framed = MessageFraming.Frame("");

        Assert.Equal(2, framed.Length);
        Assert.Equal(0, MessageFraming.LengthOf(framed));
    }

    /// <summary>
    /// RF-287 — Uma mensagem maior que 65535 bytes é TRUNCADA: dois bytes não representam
    /// mais que isso, e uma mensagem maior seria lida como uma curta seguida de lixo.
    /// </summary>
    [Fact]
    public void RF_287_uma_mensagem_grande_demais_e_truncada()
    {
        string huge = new('x', 60_000);   // 120.000 bytes
        byte[] framed = MessageFraming.Frame(huge);

        int length = MessageFraming.LengthOf(framed);

        Assert.True(length <= MessageFraming.MaxBytes);
        Assert.Equal(length + 2, framed.Length);
        Assert.Equal(0, length % 2);   // nunca corta uma unidade de 16 bits ao meio
    }

    /// <summary>
    /// A truncagem respeita o PAR SUBSTITUTO: cortar entre as duas metades produziria um
    /// caractere inválido, que a outra ponta interpretaria como texto corrompido em vez de
    /// texto cortado.
    /// </summary>
    [Fact]
    public void RF_287_a_truncagem_nao_parte_um_par_substituto()
    {
        // Emoji são pares substitutos; um texto só deles põe um par exatamente no limite.
        string emojis = string.Concat(Enumerable.Repeat("😀", 20_000));
        byte[] framed = MessageFraming.Frame(emojis);

        string decoded = MessageFraming.Encoding.GetString(framed[2..]);

        Assert.DoesNotContain(decoded, c => char.IsSurrogate(c) && !char.IsHighSurrogate(c)
                                            && !char.IsLowSurrogate(c));

        // O texto decodificado volta sem caractere de substituição — a marca de corte errado.
        Assert.DoesNotContain('�', decoded);
        Assert.False(char.IsHighSurrogate(decoded[^1]),
                     "a última unidade é a metade alta de um par: o corte partiu um caractere");
    }

    /// <summary>Uma mensagem enquadrada volta igual pelo leitor.</summary>
    [Theory]
    [InlineData("olá mundo")]
    [InlineData("門は百年閉ざされている")]
    [InlineData("com, vírgulas, dentro")]
    [InlineData("")]
    public void Uma_mensagem_enquadrada_volta_igual(string message)
    {
        using var stream = new MemoryStream();
        MessageFraming.Write(stream, message);
        stream.Position = 0;

        Assert.Equal(message, MessageFraming.Read(stream));
    }

    /// <summary>
    /// Um fluxo que acaba no meio devolve NULO — é o que acontece quando o processo auxiliar
    /// morre, e o serviço precisa distinguir isso de uma mensagem vazia.
    /// </summary>
    [Fact]
    public void Um_fluxo_interrompido_devolve_nulo()
    {
        using var truncated = new MemoryStream(new byte[] { 0x00, 0x10, 0x61 });
        Assert.Null(MessageFraming.Read(truncated));

        using var empty = new MemoryStream();
        Assert.Null(MessageFraming.Read(empty));
    }

    // ── RF-286 — o protocolo ────────────────────────────────────────────────

    /// <summary>
    /// RF-286 — Um comando é `comando,dados`, e a vírgula separa APENAS a primeira
    /// ocorrência: o texto a traduzir contém vírgulas com frequência, e dividir em todas
    /// partiria a frase em pedaços que o servidor receberia como comandos diferentes.
    /// </summary>
    [Fact]
    public void RF_286_a_virgula_separa_apenas_a_primeira_ocorrencia()
    {
        var (command, data) = ChannelProtocol.ParseCommand(
            "translate,Olá, mundo, tudo bem?");

        Assert.Equal("translate", command);
        Assert.Equal("Olá, mundo, tudo bem?", data);
    }

    [Fact]
    public void RF_286_um_comando_sem_dados_tem_dados_vazios()
    {
        var (command, data) = ChannelProtocol.ParseCommand("startup");

        Assert.Equal("startup", command);
        Assert.Equal("", data);
    }

    [Fact]
    public void RF_286_montar_e_interpretar_um_comando_sao_inversos()
    {
        string message = ChannelProtocol.Command("translate", "a,b,c");
        var (command, data) = ChannelProtocol.ParseCommand(message);

        Assert.Equal("translate", command);
        Assert.Equal("a,b,c", data);
    }

    /// <summary>
    /// RF-286 — A verificação de inicialização se repete até SUCESSO OU FALHA; qualquer
    /// outra resposta significa "ainda não sei", e a pergunta é refeita.
    /// </summary>
    [Fact]
    public void RF_286_a_verificacao_se_repete_ate_sucesso_ou_falha()
    {
        Assert.True(ChannelProtocol.InterpretStartup(ChannelProtocol.Success));
        Assert.False(ChannelProtocol.InterpretStartup(ChannelProtocol.Failure));

        Assert.Null(ChannelProtocol.InterpretStartup(""));
        Assert.Null(ChannelProtocol.InterpretStartup("carregando"));
        Assert.Null(ChannelProtocol.InterpretStartup(null));
    }
}
