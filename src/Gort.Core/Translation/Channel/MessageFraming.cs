using System.Text;

namespace Gort.Core.Translation.Channel;

/// <summary>
/// RF-287 — O enquadramento das mensagens do canal do processo auxiliar.
///
/// Cada mensagem vai precedida de DOIS BYTES com o comprimento — byte alto primeiro —, com o
/// conteúdo em codificação de 16 bits, e truncada se exceder 65535 bytes.
///
/// O formato não é escolha nossa: é o do processo auxiliar, que carrega uma biblioteca de
/// tradução instalada no sistema e fala esse protocolo. Implementá-lo aqui separado do
/// transporte é o que torna verificável a parte que se pode errar — a ordem dos bytes do
/// comprimento e a codificação — sem ter a biblioteca instalada.
/// </summary>
public static class MessageFraming
{
    /// <summary>
    /// RF-287 — Teto do comprimento: dois bytes não representam mais que isso, e uma
    /// mensagem maior seria lida como uma mensagem curta seguida de lixo.
    /// </summary>
    public const int MaxBytes = 65535;

    /// <summary>
    /// A codificação de 16 bits do protocolo. Little-endian é o que a plataforma do processo
    /// auxiliar usa para `wchar_t`.
    /// </summary>
    public static readonly Encoding Encoding = Encoding.Unicode;

    /// <summary>
    /// RF-287 — Monta uma mensagem: dois bytes de comprimento (alto, baixo) seguidos do
    /// conteúdo.
    ///
    /// A truncagem é em BYTES, não em caracteres, e respeita o par substituto: cortar no meio
    /// de um par produziria um caractere inválido que a outra ponta interpretaria como texto
    /// corrompido em vez de texto cortado.
    /// </summary>
    public static byte[] Frame(string message)
    {
        byte[] payload = Encoding.GetBytes(message ?? "");

        if (payload.Length > MaxBytes)
        {
            int limit = MaxBytes - (MaxBytes % 2);   // nunca corta uma unidade ao meio

            // Um par substituto ocupa quatro bytes; se o corte caiu entre as duas metades,
            // recua mais uma unidade.
            if (limit >= 2)
            {
                char last = BitConverter.ToChar(payload, limit - 2);
                if (char.IsHighSurrogate(last)) limit -= 2;
            }

            Array.Resize(ref payload, limit);
        }

        var framed = new byte[payload.Length + 2];
        framed[0] = (byte)((payload.Length >> 8) & 0xFF);   // byte ALTO primeiro
        framed[1] = (byte)(payload.Length & 0xFF);
        Buffer.BlockCopy(payload, 0, framed, 2, payload.Length);

        return framed;
    }

    /// <summary>RF-287 — O comprimento declarado nos dois primeiros bytes.</summary>
    public static int LengthOf(ReadOnlySpan<byte> header)
        => header.Length < 2 ? -1 : (header[0] << 8) | header[1];

    /// <summary>
    /// Lê uma mensagem de um fluxo. Devolve nulo quando o fluxo acabou antes de a mensagem
    /// completar — que é o que acontece quando o processo auxiliar morre no meio.
    /// </summary>
    public static string? Read(Stream stream)
    {
        var header = new byte[2];
        if (!ReadExactly(stream, header, 2)) return null;

        int length = LengthOf(header);
        if (length <= 0) return "";

        var payload = new byte[length];
        if (!ReadExactly(stream, payload, length)) return null;

        return Encoding.GetString(payload);
    }

    public static void Write(Stream stream, string message)
    {
        byte[] framed = Frame(message);
        stream.Write(framed, 0, framed.Length);
        stream.Flush();
    }

    private static bool ReadExactly(Stream stream, byte[] buffer, int count)
    {
        int read = 0;
        while (read < count)
        {
            int got = stream.Read(buffer, read, count - read);
            if (got <= 0) return false;
            read += got;
        }
        return true;
    }
}

/// <summary>
/// RF-286 — O protocolo do canal: identificação do servidor, depois "verificação de
/// inicialização" repetida até sucesso ou falha, depois comandos `comando,dados`.
/// </summary>
public static class ChannelProtocol
{
    /// <summary>Mensagem com que o servidor se identifica ao abrir o canal.</summary>
    public const string ServerHello = "gort-translator-server";

    /// <summary>RF-286 — A verificação repetida até o servidor responder.</summary>
    public const string StartupCheck = "startup";

    public const string Success = "ok";
    public const string Failure = "fail";

    /// <summary>
    /// RF-286 — Um comando é `comando,dados`. A vírgula separa APENAS a primeira ocorrência:
    /// o texto a traduzir contém vírgulas com frequência, e dividir em todas partiria a
    /// frase em pedaços que o servidor receberia como comandos diferentes.
    /// </summary>
    public static string Command(string command, string data) => $"{command},{data}";

    public static (string Command, string Data) ParseCommand(string message)
    {
        if (string.IsNullOrEmpty(message)) return ("", "");

        int at = message.IndexOf(',');
        return at < 0
            ? (message, "")
            : (message[..at], message[(at + 1)..]);
    }

    /// <summary>
    /// RF-286 — A resposta da verificação de inicialização: sucesso, falha, ou nem uma coisa
    /// nem outra — caso em que se pergunta de novo.
    /// </summary>
    public static bool? InterpretStartup(string? reply) => reply switch
    {
        Success => true,
        Failure => false,
        _ => null,
    };
}
