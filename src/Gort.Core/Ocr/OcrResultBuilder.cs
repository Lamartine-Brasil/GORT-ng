using System.Text;
using Gort.Core.Model;

namespace Gort.Core.Ocr;

/// <summary>
/// Montagem do resultado de 6.4 a partir do que cada motor devolve, com as conversões que a
/// especificação exige de todos eles.
/// </summary>
public static class OcrResultBuilder
{
    /// <summary>
    /// RF-141 — Para motores que devolvem apenas LINHAS (sem palavras), cada linha é
    /// convertida em uma única "palavra" com a caixa da própria linha.
    ///
    /// A sobreposição fica pior — o texto traduzido é posicionado sobre a linha inteira em
    /// vez de sobre cada palavra —, mas o modo escuro e o modo camada funcionam
    /// normalmente (6.4).
    /// </summary>
    public static OcrResult FromLines(IEnumerable<(string Text, Rect Box)> lines)
    {
        var words = new List<Word>();
        var perLine = new List<int>();

        foreach (var (text, box) in lines)
        {
            words.Add(new Word { Text = text, Box = box });
            perLine.Add(1);
        }

        return new OcrResult
        {
            Words = words,
            WordsPerLine = perLine,
            IsEmpty = words.Count == 0,
        };
    }

    /// <summary>
    /// Monta o resultado a partir de linhas que já vêm com as suas palavras separadas.
    /// Linhas sem palavra alguma são descartadas: uma linha vazia não gera bloco (RF-191) e
    /// só atrapalharia a contagem de <see cref="OcrResult.WordsPerLine"/>.
    /// </summary>
    public static OcrResult FromWords(IEnumerable<IReadOnlyList<Word>> lines)
    {
        var words = new List<Word>();
        var perLine = new List<int>();

        foreach (var line in lines)
        {
            if (line.Count == 0) continue;
            words.AddRange(line);
            perLine.Add(line.Count);
        }

        return new OcrResult
        {
            Words = words,
            WordsPerLine = perLine,
            IsEmpty = words.Count == 0,
        };
    }

    /// <summary>
    /// RF-143 — O texto devolvido por bibliotecas nativas é decodificado como UTF-8, com
    /// decodificação manual byte a byte como alternativa quando a sequência é inválida.
    ///
    /// A alternativa não descarta a cadeia inteira por causa de um byte inválido: o OCR
    /// erra caracteres o tempo todo, e perder a linha por isso seria pior que exibi-la com
    /// um caractere de substituição.
    /// </summary>
    public static string DecodeUtf8(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0) return "";

        try
        {
            // Modo estrito primeiro: sequências inválidas lançam em vez de virarem U+FFFD
            // silenciosamente.
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            // Alternativa tolerante: os bytes inválidos viram o caractere de substituição e
            // o resto da linha é preservado.
            return new UTF8Encoding(false, throwOnInvalidBytes: false).GetString(bytes);
        }
    }
}
