using System.Text;

namespace Gort.Core.Structuring;

/// <summary>Modo de janela de tradução (RF-317). Persistido por identificador textual (RF-026).</summary>
public enum WindowMode
{
    /// <summary>Janela retangular com fundo escuro e caixa de texto rolável (19.2).</summary>
    Dark,
    /// <summary>Janela transparente, sem bordas, atravessável a cliques (19.3).</summary>
    Layer,
    /// <summary>Tradução desenhada sobre o texto original (19.4).</summary>
    Overlay,
}

/// <summary>Opções do tratamento textual de 15.3.</summary>
public sealed class TextProcessingOptions
{
    /// <summary>RF-180 — Remove TODOS os espaços do texto reconhecido, antes de tudo.</summary>
    public bool RemoveSpaces { get; set; }

    /// <summary>RF-181 — Aplica o dicionário de correção antes da tradução.</summary>
    public bool UseDictionary { get; set; }

    /// <summary>Modo de janela ativo: muda RF-186/RF-187.</summary>
    public WindowMode WindowMode { get; set; } = WindowMode.Overlay;

    /// <summary>RF-157 — Modo de depuração "uma linha por tradução".</summary>
    public bool OneLinePerTranslation { get; set; }

    /// <summary>RF-186 — Verdadeiro quando o serviço ativo é o banco de dados local.</summary>
    public bool ServiceIsLocalDatabase { get; set; }

    /// <summary>RF-189 — Numeração de áreas ligada.</summary>
    public bool NumberAreas { get; set; }
}

/// <summary>
/// Cap. 15.3 — Tratamento textual aplicado ao texto reconhecido antes da tradução, e
/// montagem do texto exibido.
/// </summary>
public static class TextPostProcessor
{
    /// <summary>RF-190 — Marcador de "sem resultado": traduções assim não entram no texto exibido.</summary>
    public const string NoResultMarker = "no-result";

    /// <summary>Espaço ideográfico (U+3000), tratado como espaço por RF-180.</summary>
    private const char IdeographicSpace = '　';

    /// <summary>
    /// RF-180 / RF-181 — Tratamento do texto reconhecido de um bloco, na ordem exigida:
    /// remoção de espaços primeiro, dicionário de correção depois.
    /// </summary>
    public static string Treat(string recognized, TextProcessingOptions options,
                               CorrectionDictionary? dictionary)
    {
        string text = recognized;

        // RF-180 — quando a remoção de espaços está ativa, TODOS os espaços saem antes de
        // qualquer outro tratamento.
        if (options.RemoveSpaces) text = RemoveAllSpaces(text);

        // RF-181 — o dicionário é aplicado ao texto reconhecido antes da tradução.
        if (options.UseDictionary && dictionary is not null) text = dictionary.Apply(text);

        return text;
    }

    /// <summary>RF-180 — Remove todos os espaços, mantendo as quebras de linha.</summary>
    public static string RemoveAllSpaces(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c is ' ' or '\t' or IdeographicSpace) continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// RF-186 / RF-187 — Junção das quebras de linha antes da tradução.
    ///
    /// FORA do modo sobreposição, fora do modo de depuração "uma linha por tradução", e
    /// quando o serviço não é o banco de dados local, as quebras de linha são removidas —
    /// substituídas por espaço, ou por NADA quando a remoção de espaços está ativa.
    /// Motivo: tradutores de máquina traduzem muito pior quando recebem uma frase quebrada
    /// em várias linhas.
    ///
    /// No modo sobreposição as quebras NAO sao removidas, porque cada bloco já é uma
    /// unidade e a estrutura de linhas é usada no desenho (RF-187).
    /// </summary>
    public static string JoinLineBreaks(string text, TextProcessingOptions options)
    {
        if (options.WindowMode == WindowMode.Overlay) return text;
        if (options.OneLinePerTranslation) return text;
        if (options.ServiceIsLocalDatabase) return text;

        string replacement = options.RemoveSpaces ? "" : " ";
        return text.Replace("\r\n", replacement)
                   .Replace("\r", replacement)
                   .Replace("\n", replacement);
    }

    /// <summary>
    /// RF-188 — No modo sobreposição, o texto enviado ao tradutor é montado como: para cada
    /// bloco, uma quebra de linha, o token separador do serviço, e o texto do bloco. Assim
    /// uma única requisição carrega todos os blocos.
    /// </summary>
    public static string BuildOverlayRequest(IEnumerable<string> blockTexts, string separatorToken)
    {
        var sb = new StringBuilder();
        foreach (var t in blockTexts)
        {
            sb.Append('\n');
            sb.Append(separatorToken);
            sb.Append(t);
        }
        return sb.ToString();
    }

    /// <summary>
    /// RF-189 a RF-191 — Monta o texto EXIBIDO a partir dos resultados por área.
    ///
    ///  - RF-189: com mais de uma área e numeração ativa, prefixa o número e " : "; com
    ///    numeração inativa, "- ". Com uma única área, nenhum prefixo.
    ///  - RF-190: traduções iguais ao marcador de "sem resultado" não são concatenadas.
    ///  - RF-191: blocos com texto reconhecido vazio não geram entrada.
    /// </summary>
    public static string BuildDisplayText(
        IReadOnlyList<(int AreaIndex, string Recognized, string? Translated)> entries,
        int areaCount,
        bool numberAreas)
    {
        var sb = new StringBuilder();
        foreach (var (areaIndex, recognized, translated) in entries)
        {
            // RF-191 — bloco com texto reconhecido vazio não gera entrada.
            if (string.IsNullOrWhiteSpace(recognized)) continue;

            // RF-190 — o marcador de "sem resultado" não é concatenado.
            if (translated is null || translated == NoResultMarker) continue;

            if (sb.Length > 0) sb.Append('\n');

            if (areaCount > 1)
            {
                // RF-189 — prefixo por área.
                if (numberAreas)
                {
                    sb.Append(areaIndex + 1);
                    sb.Append(" : ");
                }
                else
                {
                    sb.Append("- ");
                }
            }
            sb.Append(translated);
        }
        return sb.ToString();
    }

    /// <summary>
    /// RF-328 / RF-497 — Composição do texto do MODO ESCURO.
    ///
    /// Quando a exibição do texto reconhecido está ativa, mostra-se a tradução, DUAS quebras
    /// de linha, o prefixo "OCR : " e o texto reconhecido. É o que permite diagnosticar um
    /// erro de tradução distinguindo-o de um erro de OCR, sem sair da janela.
    ///
    /// A composição fica aqui, e não na janela, porque é regra da especificação e vale para
    /// qualquer implementação de interface.
    /// </summary>
    public static string ComposeDarkModeText(string translated, string recognized,
                                             bool showRecognized)
    {
        if (!showRecognized || string.IsNullOrWhiteSpace(recognized)) return translated;
        return $"{translated}\n\n{RecognizedPrefix}{recognized}";
    }

    /// <summary>RF-328 — Prefixo do texto reconhecido no modo escuro.</summary>
    public const string RecognizedPrefix = "OCR : ";

    /// <summary>
    /// RF-329 — Normaliza quebras de linha recebidas em qualquer formato para o formato da
    /// plataforma, antes de exibir.
    /// </summary>
    public static string NormalizeNewlines(string text)
        => text.Replace("\r\n", "\n").Replace('\r', '\n')
               .Replace("\n", Environment.NewLine);
}
