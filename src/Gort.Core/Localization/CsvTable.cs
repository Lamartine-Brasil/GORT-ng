using System.Text;

namespace Gort.Core.Localization;

/// <summary>
/// RF-482 — Leitor de tabela separada por vírgulas, TOLERANTE a campos entre aspas contendo
/// vírgulas e QUEBRAS DE LINHA.
///
/// A tolerância não é luxo: textos de interface têm vírgula o tempo todo, e mensagens
/// longas — como a explicação de uma permissão que falta — têm quebras de linha. Um leitor
/// que dividisse por vírgula e por linha quebraria na primeira mensagem real.
/// </summary>
public static class CsvTable
{
    /// <summary>Lê o conteúdo inteiro em linhas de campos.</summary>
    public static List<List<string>> Parse(string content)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();

        bool inQuotes = false;
        bool fieldStarted = false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Duas aspas seguidas dentro de um campo citado são uma aspa literal.
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"' when field.Length == 0:
                    inQuotes = true;
                    fieldStarted = true;
                    break;

                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                    break;

                case '\r':
                    // Absorvido; a quebra é decidida pelo '\n'.
                    break;

                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                    rows.Add(row);
                    row = new List<string>();
                    break;

                default:
                    field.Append(c);
                    fieldStarted = true;
                    break;
            }
        }

        // Última linha sem quebra ao final.
        if (field.Length > 0 || fieldStarted || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>Escreve um campo, citando-o quando necessário.</summary>
    public static string Escape(string field)
    {
        bool needsQuotes = field.Contains(',') || field.Contains('"')
                           || field.Contains('\n') || field.Contains('\r');

        if (!needsQuotes) return field;
        return '"' + field.Replace("\"", "\"\"") + '"';
    }
}
