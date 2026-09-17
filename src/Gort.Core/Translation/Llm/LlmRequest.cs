using System.Text.Json;
using System.Text.Json.Nodes;
using Gort.Core.Calibration;
using Gort.Core.Catalog;
using Gort.Core.Configuration;

namespace Gort.Core.Translation.Llm;

/// <summary>
/// RF-282 🔒 — Como o nível de raciocínio vira configuração do modelo, nas duas famílias.
/// </summary>
public readonly record struct ThinkingSetting(int? Budget, string? Level)
{
    /// <summary>O nível é OMITIDO — a família antiga faz isso nos níveis 0, 2 e 3.</summary>
    public static readonly ThinkingSetting Omitted = new(null, null);
}

/// <summary>
/// VI.4 / RF-273 a RF-283 — A montagem da requisição ao modelo de linguagem.
///
/// Separada do envio porque é aqui que estão os valores 🔒: a instrução padrão de RF-274, as
/// categorias de segurança de RF-276 e a tradução do nível de raciocínio de RF-282. Nenhum
/// deles precisa de chave para ser verificado, e todos três são fáceis de errar em silêncio
/// — um filtro de segurança mal configurado só aparece quando o modelo recusa uma fala de
/// jogo, e aí parece defeito da tradução.
/// </summary>
public static class LlmRequest
{
    /// <summary>
    /// RF-274 🔒 — A instrução padrão.
    ///
    /// Cada cláusula existe para corrigir um comportamento OBSERVADO do modelo, e é por isso
    /// que ela é calibrada: pedir tradução e mais nada não basta.
    ///
    ///  - "traduza para {idioma}" — o alvo, explícito;
    ///  - "não omita palavras" — o modelo resume quando a fala é longa;
    ///  - "não use honoríficos" — ele acrescenta tratamentos que não estão no original;
    ///  - "todos os personagens têm 22 anos ou mais" — sem isso ele recusa falas de jogo
    ///    envolvendo personagens cuja idade não está dita;
    ///  - "preserve todos os símbolos" — ele apaga marcadores que o programa usa para
    ///    separar blocos, e a resposta chega impossível de dividir;
    ///  - "devolva SOMENTE a tradução" — ele comenta o que traduziu, e o comentário vai
    ///    parar na tela do usuário como se fosse parte do texto do jogo.
    /// </summary>
    public static string DefaultInstruction(string targetLanguageName) =>
        $"Traduza o texto do usuário para {targetLanguageName}. "
        + "Não omita nenhuma palavra do original. "
        + "Não use honoríficos. "
        + "Todos os personagens têm 22 anos ou mais. "
        + "Preserve todos os símbolos e marcadores exatamente como aparecem. "
        + "Responda SOMENTE com a tradução, sem comentários, notas ou explicações.";

    /// <summary>
    /// RF-275 — Quando há instrução personalizada E a padrão continua ligada, a
    /// PERSONALIZADA vem primeiro e a padrão em seguida, separadas por espaço e quebra de
    /// linha.
    ///
    /// A ordem importa: a instrução personalizada é a do usuário para o jogo dele, e vir
    /// depois da padrão a faria competir com seis cláusulas já estabelecidas.
    /// </summary>
    public static string BuildInstruction(string? custom, bool includeDefault,
                                          string targetLanguageName)
    {
        string standard = DefaultInstruction(targetLanguageName);

        if (string.IsNullOrWhiteSpace(custom)) return includeDefault ? standard : "";
        if (!includeDefault) return custom.Trim();

        return custom.Trim() + " \n" + standard;
    }

    /// <summary>
    /// RF-276 🔒 — TODAS as categorias de filtro de segurança configuradas para NÃO BLOQUEAR.
    ///
    /// Texto de jogo tem violência, ameaça e insulto por definição — é o que os personagens
    /// dizem uns aos outros. Um filtro ativo recusa a tradução e o usuário vê uma tela vazia
    /// sem saber por quê; pior, a recusa é intermitente, porque depende da fala.
    /// </summary>
    public static readonly IReadOnlyList<string> SafetyCategories = new[]
    {
        "HARM_CATEGORY_HARASSMENT",
        "HARM_CATEGORY_HATE_SPEECH",
        "HARM_CATEGORY_SEXUALLY_EXPLICIT",
        "HARM_CATEGORY_DANGEROUS_CONTENT",
        "HARM_CATEGORY_CIVIC_INTEGRITY",
    };

    public const string BlockNone = "BLOCK_NONE";

    /// <summary>
    /// RF-282 🔒 — O nível de raciocínio traduzido para o formato do modelo.
    ///
    /// Família ANTIGA: níveis 0, 2 e 3 OMITEM o orçamento; o nível 1 usa P-76 para modelos
    /// "pro" e 0 para os demais.
    ///
    /// Família NOVA: o nível vira um RÓTULO — 0 e 3 = alto; 1 = baixo para "pro" e mínimo
    /// para os demais; 2 = baixo para "pro" e médio para os demais.
    ///
    /// O mapeamento não é monotônico, e é assim mesmo: ele foi calibrado contra o
    /// comportamento de cada família, não derivado de uma escala.
    /// </summary>
    public static ThinkingSetting Thinking(int level, bool legacyFamily, bool isPro)
    {
        if (legacyFamily)
        {
            return level == 1
                ? new ThinkingSetting(isPro ? P.LlmProThinkingBudget : 0, null)
                : ThinkingSetting.Omitted;
        }

        string label = level switch
        {
            0 or 3 => "high",
            1 => isPro ? "low" : "minimal",
            2 => isPro ? "low" : "medium",
            _ => "high",
        };

        return new ThinkingSetting(null, label);
    }

    /// <summary>RF-281 — Os valores de geração do preset escolhido.</summary>
    public static (int Temperature, int Thinking, int MaxOutput) PresetValues(LlmPreset preset,
                                                                             AdvancedOptions options)
        => preset switch
        {
            LlmPreset.Standard => (P.LlmTemperatureDefault, P.LlmThinkingDefault,
                                   P.LlmMaxOutputDefault),
            LlmPreset.Economy => (P.LlmTemperatureEconomy, P.LlmThinkingEconomy,
                                  P.LlmMaxOutputEconomy),
            _ => (options.LlmTemperature, options.LlmThinking, options.LlmMaxOutput),
        };

    /// <summary>
    /// RF-273 — O corpo da requisição: instrução de sistema, texto do usuário, configurações
    /// de segurança e configurações de geração.
    /// </summary>
    public static string Build(string text, string instruction, LlmCatalog catalog,
                               string model, int temperature, int thinkingLevel, int maxOutput)
    {
        bool legacy = catalog.IsLegacyFamily(model);
        bool pro = model.Contains(catalog.ProMarker, StringComparison.OrdinalIgnoreCase);

        var generation = new JsonObject
        {
            // RF-526 — a temperatura é guardada de 0 a 100 e enviada dividida por 100.
            ["temperature"] = temperature / 100.0,
            ["maxOutputTokens"] = maxOutput,
        };

        var thinking = Thinking(thinkingLevel, legacy, pro);

        if (thinking.Budget is int budget)
            generation["thinkingConfig"] = new JsonObject { ["thinkingBudget"] = budget };
        else if (thinking.Level is string level)
            generation["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = level };

        var safety = new JsonArray();
        foreach (string category in SafetyCategories)
        {
            safety.Add(new JsonObject
            {
                ["category"] = category,
                ["threshold"] = BlockNone,
            });
        }

        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray { new JsonObject { ["text"] = instruction } },
            },
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = new JsonArray { new JsonObject { ["text"] = text } },
                },
            },
            ["safetySettings"] = safety,
            ["generationConfig"] = generation,
        };

        return body.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// RF-277 — A resposta indica bloqueio por conteúdo proibido.
    ///
    /// Nesse caso o programa refaz a requisição no tradutor web gratuito SEM sinalizar erro:
    /// o usuário não fez nada de errado, e uma mensagem de erro o faria procurar defeito na
    /// configuração dele.
    /// </summary>
    public static bool WasBlocked(string payload)
    {
        try
        {
            var root = JsonNode.Parse(payload);
            if (root is not JsonObject obj) return false;

            // Bloqueio no prompt: o serviço nem chega a gerar.
            if (obj["promptFeedback"]?["blockReason"] is not null) return true;

            // Bloqueio na resposta: a geração parou por segurança.
            if (obj["candidates"] is JsonArray candidates)
            {
                foreach (var candidate in candidates)
                {
                    string? reason = candidate?["finishReason"]?.GetValue<string>();
                    if (reason is "SAFETY" or "PROHIBITED_CONTENT" or "BLOCKLIST") return true;
                }
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
