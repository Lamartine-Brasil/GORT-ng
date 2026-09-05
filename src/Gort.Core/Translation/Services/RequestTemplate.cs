using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Gort.Core.Translation.Services;

/// <summary>
/// RF-296 a RF-301 — O modelo de requisição e o de resposta da API personalizada.
///
/// Fica separado do serviço porque é TRADUÇÃO DE TEXTO EM JSON, não rede: é a parte que o
/// usuário erra digitando, e a que precisa ser verificável sem endereço nenhum.
/// </summary>
public static class RequestTemplate
{
    public const string TextMarker = "{OCR_TEXT}";
    public const string SourceMarker = "{SOURCE_CODE}";
    public const string TargetMarker = "{RESULT_CODE}";
    public const string ResultMarker = "{RESULT_TEXT}";

    /// <summary>
    /// RF-296 a RF-299 — Monta o corpo da requisição.
    ///
    /// Devolve o JSON pronto, ou nulo com a mensagem de erro em <paramref name="error"/>
    /// quando o resultado não é JSON válido (RF-299). O erro descreve a falha de CONVERSÃO,
    /// e não "falha ao traduzir": quem digitou o modelo precisa saber que o problema está
    /// nele.
    /// </summary>
    public static string? Build(string template, string text, string sourceCode,
                                string targetCode, out string? error)
    {
        error = null;

        // RF-296 — os marcadores são substituídos pelos valores, com o TEXTO ESCAPADO PARA
        // JSON. Sem o escape, um texto reconhecido com aspas ou quebra de linha — que é o
        // caso comum — quebraria o JSON inteiro.
        string body = template
            .Replace(TextMarker, EscapeForJson(text))
            .Replace(SourceMarker, EscapeForJson(sourceCode))
            .Replace(TargetMarker, EscapeForJson(targetCode));

        // RF-298 — se o modelo não estiver envolvido por chaves, elas são acrescentadas.
        body = body.Trim();
        if (body.Length == 0) { error = "O modelo de requisição está vazio."; return null; }
        if (!body.StartsWith('{')) body = "{" + body + "}";

        // RF-297 — o modelo aceita JSON válido OU a sintaxe relaxada `chave = valor`.
        // A tentativa é nesta ordem porque JSON válido é também o caso em que o usuário
        // sabe o que está fazendo: convertê-lo antes poderia estragá-lo.
        if (IsValidJson(body)) return body;

        // Um modelo com chaves ou colchetes desbalanceados não é sintaxe relaxada: é
        // modelo quebrado. Sem esta verificação a conversão devolvia "{}" em silêncio — o
        // separador de topo nunca sai da profundidade aberta, nenhum par é reconhecido, e o
        // programa enviaria um corpo VAZIO em vez de recusar. Foi um teste que achou.
        if (!IsBalanced(body))
        {
            error = "O modelo de requisição tem chaves ou colchetes sem fechar: " + body;
            return null;
        }

        string converted = ConvertRelaxed(body);

        if (!IsValidJson(converted))
        {
            error = "O modelo de requisição não produziu JSON válido: " + converted;
            return null;
        }
        return converted;
    }

    /// <summary>
    /// RF-297 — Converte a sintaxe relaxada `chave = valor, chave = valor` para JSON.
    ///
    /// Preserva textos entre aspas, reconhece booleanos, números e nulo, e envolve os
    /// demais valores em aspas. Vetores são convertidos elemento a elemento.
    /// </summary>
    public static string ConvertRelaxed(string relaxed)
    {
        string inner = relaxed.Trim();
        if (inner.StartsWith('{') && inner.EndsWith('}')) inner = inner[1..^1];

        var parts = SplitTopLevel(inner, ',');
        var result = new StringBuilder("{");

        for (int i = 0; i < parts.Count; i++)
        {
            var (key, value) = SplitPair(parts[i]);
            if (key.Length == 0) continue;

            if (result.Length > 1) result.Append(',');
            result.Append(Quote(key)).Append(':').Append(ConvertValue(value));
        }

        return result.Append('}').ToString();
    }

    private static string ConvertValue(string raw)
    {
        string value = raw.Trim();
        if (value.Length == 0) return "\"\"";

        // Já entre aspas: preservado como está.
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') return value;

        // Vetor: convertido ELEMENTO A ELEMENTO, e não como um texto só.
        if (value.StartsWith('[') && value.EndsWith(']'))
        {
            var items = SplitTopLevel(value[1..^1], ',');
            return "[" + string.Join(',', items.Select(ConvertValue)) + "]";
        }

        // Objeto aninhado: a mesma conversão, recursivamente.
        if (value.StartsWith('{') && value.EndsWith('}')) return ConvertRelaxed(value);

        // Booleanos, nulo e números passam sem aspas.
        if (value is "true" or "false" or "null") return value;
        if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            return value;
        }

        return Quote(value);
    }

    private static (string Key, string Value) SplitPair(string part)
    {
        string text = part.Trim();

        // Aceita tanto `chave = valor` quanto `"chave": valor`, porque um modelo meio
        // convertido é o que sai quando o usuário copia um exemplo e edita parte dele.
        int at = IndexOfTopLevel(text, '=');
        if (at < 0) at = IndexOfTopLevel(text, ':');
        if (at < 0) return ("", "");

        string key = text[..at].Trim().Trim('"');
        return (key, text[(at + 1)..]);
    }

    /// <summary>Divide por um separador, ignorando o que está dentro de aspas ou chaves.</summary>
    private static List<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        int depth = 0;
        bool quoted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (quoted)
            {
                current.Append(c);
                if (c == '\\' && i + 1 < text.Length) { current.Append(text[++i]); continue; }
                if (c == '"') quoted = false;
                continue;
            }

            switch (c)
            {
                case '"': quoted = true; current.Append(c); break;
                case '{' or '[': depth++; current.Append(c); break;
                case '}' or ']': depth--; current.Append(c); break;
                default:
                    if (c == separator && depth == 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                    }
                    else current.Append(c);
                    break;
            }
        }

        if (current.Length > 0) parts.Add(current.ToString());
        return parts;
    }

    private static int IndexOfTopLevel(string text, char target)
    {
        int depth = 0;
        bool quoted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '\\') { i++; continue; }
                if (c == '"') quoted = false;
                continue;
            }
            if (c == '"') { quoted = true; continue; }
            if (c is '{' or '[') depth++;
            else if (c is '}' or ']') depth--;
            else if (c == target && depth == 0) return i;
        }
        return -1;
    }

    /// <summary>Chaves e colchetes fecham na ordem em que abriram, fora de aspas.</summary>
    private static bool IsBalanced(string text)
    {
        var stack = new Stack<char>();
        bool quoted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (quoted)
            {
                if (c == '\\') { i++; continue; }
                if (c == '"') quoted = false;
                continue;
            }

            switch (c)
            {
                case '"': quoted = true; break;
                case '{': stack.Push('}'); break;
                case '[': stack.Push(']'); break;
                case '}' or ']':
                    if (stack.Count == 0 || stack.Pop() != c) return false;
                    break;
            }
        }

        return !quoted && stack.Count == 0;
    }

    private static string Quote(string value) => JsonSerializer.Serialize(value);

    /// <summary>Escapa para JSON e remove as aspas externas que o serializador acrescenta.</summary>
    public static string EscapeForJson(string value)
    {
        string quoted = JsonSerializer.Serialize(value);
        return quoted[1..^1];
    }

    private static bool IsValidJson(string text)
    {
        try
        {
            using var _ = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // ── RF-300 — o modelo de resposta ───────────────────────────────────────

    /// <summary>
    /// RF-300 — Descobre, no modelo de resposta, qual chave contém a tradução: é a que tem
    /// <c>{RESULT_TEXT}</c> como valor.
    /// </summary>
    public static string? ResultKeyOf(string responseTemplate)
    {
        if (string.IsNullOrWhiteSpace(responseTemplate)) return null;

        // As chaves externas só saem quando ENVOLVEM o modelo inteiro. `Trim('{','}')`
        // comeria a chave final de `saida = {RESULT_TEXT}`, e o marcador deixaria de ser
        // reconhecido — outro que só apareceu no teste.
        string body = responseTemplate.Trim();
        if (body.StartsWith('{') && body.EndsWith('}')) body = body[1..^1];

        foreach (var part in SplitTopLevel(body, ','))
        {
            var (key, value) = SplitPair(part);
            if (key.Length > 0 && value.Contains(ResultMarker, StringComparison.Ordinal))
                return key;
        }

        return null;
    }

    /// <summary>
    /// RF-300 — Procura a chave RECURSIVAMENTE na resposta real, em qualquer nível de
    /// aninhamento.
    ///
    /// A busca é recursiva porque a resposta de um serviço real quase nunca é plana: o
    /// campo útil costuma vir dentro de `data`, `result`, `choices[0].message`, e exigir do
    /// usuário o caminho completo tornaria o recurso inútil para quem não conhece a API de
    /// cor.
    ///
    /// RF-294 — o valor pode ser texto ou VETOR de textos; no segundo caso as partes são
    /// concatenadas.
    /// </summary>
    public static string? FindResult(string json, string key)
    {
        try
        {
            var node = JsonNode.Parse(json);
            return node is null ? null : Search(node, key);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Search(JsonNode node, string key)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj)
                {
                    if (value is null) continue;

                    if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                    {
                        string? found = Flatten(value);
                        if (found is not null) return found;
                    }

                    string? deeper = Search(value, key);
                    if (deeper is not null) return deeper;
                }
                return null;

            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is null) continue;
                    string? found = Search(item, key);
                    if (found is not null) return found;
                }
                return null;

            default:
                return null;
        }
    }

    /// <summary>RF-294 — Texto, ou vetor de textos concatenados.</summary>
    private static string? Flatten(JsonNode node) => node switch
    {
        JsonArray array => string.Concat(array.Select(i => i?.ToString() ?? "")),
        JsonValue value => value.ToString(),
        _ => null,
    };

    // ── RF-301 — cabeçalhos adicionais ──────────────────────────────────────

    /// <summary>
    /// RF-301 — Cada cabeçalho está no formato <c>nome: valor</c>; linhas malformadas são
    /// REGISTRADAS e ignoradas.
    ///
    /// Ignorar e seguir, em vez de recusar tudo: uma linha errada entre cinco não deve
    /// impedir as outras quatro de funcionarem, e o registro é o que permite ao usuário
    /// descobrir qual era.
    /// </summary>
    public static List<(string Name, string Value)> ParseHeaders(
        string text, Action<string>? log = null)
    {
        var headers = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(text)) return headers;

        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;

            int at = line.IndexOf(':');
            if (at <= 0 || at == line.Length - 1)
            {
                log?.Invoke($"Cabeçalho ignorado, formato inesperado: {line}");
                continue;
            }

            headers.Add((line[..at].Trim(), line[(at + 1)..].Trim()));
        }

        return headers;
    }
}
