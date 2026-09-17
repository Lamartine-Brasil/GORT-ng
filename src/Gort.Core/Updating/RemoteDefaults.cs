using System.Text.Json;

namespace Gort.Core.Updating;

/// <summary>
/// RF-417 🔒 / RF-418 — A configuração padrão REMOTA.
///
/// Motivo, na letra do requisito: os valores que vêm daqui "dependem de páginas de terceiros
/// que mudam sem aviso; poder corrigi-los remotamente evita uma atualização do programa a
/// cada mudança". São exatamente os pontos em que o programa fala com serviços que não
/// controla — o token separador que cada tradutor respeita, e a forma de extrair a tradução
/// da página do navegador embutido.
///
/// RF-418 — valores AUSENTES ou VAZIOS mantêm os embutidos. Um arquivo remoto meio
/// preenchido não pode apagar configuração que funciona: a regra é acrescentar, nunca
/// esvaziar.
/// </summary>
public sealed class RemoteDefaults
{
    /// <summary>RF-417 — Token separador por serviço, pela chave do serviço.</summary>
    public IReadOnlyDictionary<string, string> SeparatorTokens { get; init; }
        = new Dictionary<string, string>();

    /// <summary>RF-417 / P-151 — Sinalizador de token avançado. Nulo mantém o embutido.</summary>
    public bool? AdvancedToken { get; init; }

    /// <summary>RF-417 — Tradutor por navegador embutido.</summary>
    public string? BrowserBaseUrl { get; init; }
    public string? BrowserUrlFormat { get; init; }
    public string? BrowserExtractionScript { get; init; }

    public static readonly RemoteDefaults Empty = new();

    /// <summary>
    /// RF-419 — Uma falha ao interpretar o arquivo remoto NÃO impede a inicialização: ela
    /// devolve vazio, e vazio mantém tudo o que está embutido.
    ///
    /// É a mesma disciplina de RF-024 para os arquivos do usuário, aplicada a um arquivo que
    /// vem de fora e sobre o qual se tem ainda menos controle.
    /// </summary>
    public static RemoteDefaults Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object) return Empty;

            return new RemoteDefaults
            {
                SeparatorTokens = ReadTokens(root),
                AdvancedToken = ReadBool(root, "advanced_token"),
                BrowserBaseUrl = ReadString(root, "browser_base_url"),
                BrowserUrlFormat = ReadString(root, "browser_url_format"),
                BrowserExtractionScript = ReadString(root, "browser_extraction_script"),
            };
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    private static Dictionary<string, string> ReadTokens(JsonElement root)
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!root.TryGetProperty("separator_tokens", out var table)
            || table.ValueKind != JsonValueKind.Object)
        {
            return tokens;
        }

        foreach (var property in table.EnumerateObject())
        {
            string? value = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : null;

            // RF-418 — um valor vazio não entra: entrar seria apagar o embutido.
            if (!string.IsNullOrEmpty(value)) tokens[property.Name] = value;
        }

        return tokens;
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String) return null;

        string? text = value.GetString();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static bool? ReadBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)) return null;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    /// <summary>
    /// RF-417 — O token separador de um serviço: o remoto quando há, o embutido quando não.
    /// </summary>
    public string SeparatorTokenFor(string serviceKey, string embedded)
        => SeparatorTokens.TryGetValue(serviceKey, out var token) ? token : embedded;
}
