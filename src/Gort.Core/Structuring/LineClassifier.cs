using Gort.Core.Calibration;
using Gort.Core.Model;

namespace Gort.Core.Structuring;

/// <summary>
/// Classificação de linhas para o agrupamento: itens de lista (RF-165 a RF-170),
/// títulos (RF-171 a RF-174) e fim de frase (RF-177). 🔒
///
/// Todas as regras operam sobre o texto da linha na forma exata de RF-152 — palavras
/// separadas por espaço, COM espaço no final. Não normalizar o texto antes de chegar aqui.
/// </summary>
public static class LineClassifier
{
    // ─────────────────────────────────────────────────────────────────────────
    // Itens de lista
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RF-166 — Marcador forte: o primeiro caractere, após remover espaços à esquerda,
    /// pertence ao conjunto P-39, e a linha tem mais de um caractere. 🔒
    /// </summary>
    public static bool HasStrongListMarker(string lineText)
    {
        string s = lineText.TrimStart();
        if (s.Length <= 1) return false;
        return Array.IndexOf(P.StrongListMarkers, s[0]) >= 0;
    }

    /// <summary>
    /// RF-167 — Candidato a marcador fraco: o primeiro caractere é '-', '*' ou '.' e a
    /// linha tem mais de um caractere.
    /// </summary>
    public static bool HasWeakListMarkerCandidate(string lineText)
    {
        string s = lineText.TrimStart();
        if (s.Length <= 1) return false;
        return s[0] is '-' or '*' or '.';
    }

    /// <summary>
    /// RF-168 — Marcador fraco explícito: é candidato a marcador fraco E o segundo
    /// caractere é espaço em branco.
    /// </summary>
    public static bool HasExplicitWeakListMarker(string lineText)
    {
        if (!HasWeakListMarkerCandidate(lineText)) return false;
        string s = lineText.TrimStart();
        return s.Length > 1 && char.IsWhiteSpace(s[1]);
    }

    /// <summary>
    /// RF-169 — Marcador numerado: opcionalmente um parêntese de abertura, seguido de 1 a
    /// P-150 caracteres alfanuméricos, seguido do fechamento correspondente — ')' se havia
    /// parêntese de abertura, '.' ou ')' caso contrário —, seguido de espaço em branco,
    /// seguido de pelo menos um caractere não branco. 🔒
    /// </summary>
    public static bool HasNumberedListMarker(string lineText)
    {
        string s = lineText.TrimStart();
        int i = 0;
        bool opened = false;
        if (i < s.Length && s[i] == '(')
        {
            opened = true;
            i++;
        }

        int start = i;
        while (i < s.Length && i - start < P.NumberedMarkerMaxLength && char.IsLetterOrDigit(s[i]))
            i++;
        int tokenLength = i - start;
        if (tokenLength < 1) return false;

        if (i >= s.Length) return false;
        char closer = s[i];
        bool closerOk = opened ? closer == ')' : (closer == '.' || closer == ')');
        if (!closerOk) return false;
        i++;

        // seguido de espaço em branco
        if (i >= s.Length || !char.IsWhiteSpace(s[i])) return false;
        i++;

        // seguido de pelo menos um caractere não branco
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        return i < s.Length;
    }

    /// <summary>
    /// RF-165 — O componente está em contexto de lista se qualquer linha começa com um
    /// marcador forte, um marcador fraco explícito ou um marcador numerado; ou se pelo
    /// menos duas linhas começam com um candidato a marcador fraco. 🔒
    /// </summary>
    public static bool IsListContext(IReadOnlyList<Line> component)
    {
        int weakCandidates = 0;
        foreach (var line in component)
        {
            string t = line.Text;
            if (HasStrongListMarker(t) || HasExplicitWeakListMarker(t) || HasNumberedListMarker(t))
                return true;
            if (HasWeakListMarkerCandidate(t)) weakCandidates++;
        }
        return weakCandidates >= 2;
    }

    /// <summary>
    /// RF-170 — Uma linha é item de lista quando o componente está em contexto de lista e
    /// a linha começa com algum dos marcadores. Itens de lista nunca são fundidos com o
    /// item seguinte e têm precedência sobre títulos (RF-174).
    /// </summary>
    public static bool IsListItem(Line line, bool listContext)
    {
        if (!listContext) return false;
        string t = line.Text;
        return HasStrongListMarker(t)
               || HasExplicitWeakListMarker(t)
               || HasNumberedListMarker(t)
               || HasWeakListMarkerCandidate(t);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Títulos
    // ─────────────────────────────────────────────────────────────────────────

    // RF-171 — pares de delimitação que caracterizam título explícito.
    // "colchetes de canto tipográficos" cobre os dois pares de canto usados em japonês.
    private static readonly (char Open, char Close)[] TitleWrappers =
    {
        ('[', ']'),
        ('「', '」'),   // 「 」
        ('『', '』'),   // 『 』
        ('<', '>'),
    };

    // RF-171 — dois-pontos na versão ASCII e na de largura total.
    private static readonly char[] TitleColons = { ':', '：' };

    /// <summary>
    /// RF-171 — Título explícito: após remover espaços nas pontas, a linha está
    /// inteiramente envolvida por colchetes, por colchetes de canto tipográficos ou por
    /// sinais de menor/maior; ou termina com dois-pontos. 🔒
    /// </summary>
    public static bool IsExplicitTitle(string lineText)
    {
        string s = lineText.Trim();
        if (s.Length == 0) return false;

        if (Array.IndexOf(TitleColons, s[^1]) >= 0) return true;

        if (s.Length >= 2)
        {
            foreach (var (open, close) in TitleWrappers)
            {
                if (s[0] == open && s[^1] == close) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// RF-173 — Linha "curta". 🔒
    ///
    /// O limite é a soma dos comprimentos das PALAVRAS (não do texto da linha, que carrega
    /// os separadores): P-40 normalmente, P-41 quando a remoção de espaços está ativa, e
    /// desses valores subtrai-se P-42 quando a linha é vertical.
    /// Adicionalmente, FORA do modo de remoção de espaços, uma linha com até P-43 palavras
    /// também é considerada curta.
    ///
    /// Motivo (da especificação): em japonês/chinês sem espaços a contagem de palavras não
    /// significa nada, então só o número de caracteres vale, e o limiar é menor porque cada
    /// caractere carrega mais informação.
    /// </summary>
    public static bool IsShortLine(Line line, bool removeSpaces)
    {
        int limit = removeSpaces ? P.ShortLineCharLimitNoSpaces : P.ShortLineCharLimit;
        if (line.Orientation == Orientation.Vertical) limit -= P.ShortLineVerticalDiscount;

        int charCount = 0;
        foreach (var w in line.Words) charCount += w.Text.Length;
        if (charCount <= limit) return true;

        if (!removeSpaces && line.Words.Count <= P.ShortLineMaxWords) return true;

        return false;
    }

    /// <summary>
    /// RF-172 — Título por contexto, aplicável apenas à PRIMEIRA linha de um componente:
    /// existe uma linha seguinte; ambas têm a mesma orientação; a linha é curta segundo
    /// RF-173; e a quantidade de caracteres NÃO BRANCOS da linha seguinte é maior ou igual
    /// ao teto de P-148 vezes a da linha atual. 🔒
    /// </summary>
    public static bool IsContextTitle(Line line, Line? next, bool removeSpaces)
    {
        if (next is null) return false;
        if (line.Orientation != next.Orientation) return false;
        if (!IsShortLine(line, removeSpaces)) return false;

        int current = CountNonWhitespace(line.Text);
        int following = CountNonWhitespace(next.Text);
        int required = (int)Math.Ceiling(P.ContextTitleLengthRatio * current);
        return following >= required;
    }

    private static int CountNonWhitespace(string s)
    {
        int n = 0;
        foreach (char c in s)
        {
            if (!char.IsWhiteSpace(c)) n++;
        }
        return n;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fim de frase
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RF-177 — Uma linha TERMINA FRASE quando, removendo espaços à direita e depois
    /// removendo repetidamente quaisquer caracteres de fechamento do conjunto P-45, o
    /// último caractere restante pertence a P-149. Uma linha vazia ou que só contém
    /// caracteres de fechamento não termina frase. 🔒
    ///
    /// A ordem é literal: apara-se o branco UMA vez, e só depois se removem os
    /// fechamentos. Uma linha como ". " (fechamento seguido de espaço no meio) não termina
    /// frase — é o comportamento especificado.
    /// </summary>
    public static bool EndsSentence(string lineText)
    {
        string s = lineText.TrimEnd();

        while (s.Length > 0 && Array.IndexOf(P.ClosingChars, s[^1]) >= 0)
            s = s[..^1];

        if (s.Length == 0) return false;
        return Array.IndexOf(P.SentenceEndChars, s[^1]) >= 0;
    }

    public static bool EndsSentence(Line line) => EndsSentence(line.Text);
}
