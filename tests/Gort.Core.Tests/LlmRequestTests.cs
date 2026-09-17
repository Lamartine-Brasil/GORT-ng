using System.Text.Json;
using Gort.Core.Calibration;
using Gort.Core.Catalog;
using Gort.Core.Configuration;
using Gort.Core.Translation.Llm;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>VI.4 / RF-273 a RF-283 🔒 — A montagem da requisição ao modelo de linguagem.</summary>
public class LlmRequestTests
{
    private static LlmCatalog Catalog() => new()
    {
        DefaultModel = "gemini-2.0-flash",
        Models = new[] { "gemini-2.0-flash", "gemini-2.5-pro" },
        LegacyFamilyPrefix = "gemini-2.0",
        ProMarker = "pro",
    };

    private static JsonDocument Body(string model = "gemini-2.5-flash",
                                     int temperature = 20, int thinking = 0,
                                     int maxOutput = 4000)
        => JsonDocument.Parse(LlmRequest.Build(
            "Hello", "instrução", Catalog(), model, temperature, thinking, maxOutput));

    // ── RF-274 🔒 — a instrução padrão ──────────────────────────────────────

    /// <summary>
    /// RF-274 🔒 — Cada cláusula existe para corrigir um comportamento OBSERVADO do modelo.
    /// Pedir tradução e mais nada não basta: ele resume, acrescenta honoríficos, recusa
    /// falas de jogo, apaga marcadores e comenta o que traduziu.
    /// </summary>
    [Fact]
    public void RF_274_a_instrucao_padrao_tem_as_seis_clausulas()
    {
        string instruction = LlmRequest.DefaultInstruction("português do Brasil");

        Assert.Contains("português do Brasil", instruction);
        Assert.Contains("omita", instruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("honorífic", instruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("22 anos", instruction);
        Assert.Contains("símbolos", instruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SOMENTE", instruction);
    }

    /// <summary>
    /// RF-275 — Com as duas instruções, a PERSONALIZADA vem primeiro e a padrão em seguida,
    /// separadas por espaço e quebra de linha.
    ///
    /// A ordem importa: a personalizada é a do usuário para o jogo dele, e vir depois da
    /// padrão a faria competir com seis cláusulas já estabelecidas.
    /// </summary>
    [Fact]
    public void RF_275_a_personalizada_vem_primeiro()
    {
        string built = LlmRequest.BuildInstruction(
            "Chame o protagonista de Capitão.", includeDefault: true, "português");

        Assert.StartsWith("Chame o protagonista de Capitão.", built);
        Assert.Contains(" \n", built);
        Assert.Contains("SOMENTE", built);
    }

    [Fact]
    public void RF_275_sem_a_padrao_so_a_personalizada_e_enviada()
    {
        string built = LlmRequest.BuildInstruction(
            "Traduza como poesia.", includeDefault: false, "português");

        Assert.Equal("Traduza como poesia.", built);
    }

    [Fact]
    public void Sem_personalizada_vale_a_padrao()
    {
        Assert.Contains("SOMENTE",
            LlmRequest.BuildInstruction(null, includeDefault: true, "português"));

        Assert.Equal("",
            LlmRequest.BuildInstruction("   ", includeDefault: false, "português"));
    }

    // ── RF-276 🔒 — segurança ───────────────────────────────────────────────

    /// <summary>
    /// RF-276 🔒 — TODAS as categorias configuradas para NÃO BLOQUEAR.
    ///
    /// Texto de jogo tem violência, ameaça e insulto por definição — é o que os personagens
    /// dizem uns aos outros. Um filtro ativo recusa a tradução e o usuário vê tela vazia sem
    /// saber por quê; pior, a recusa é intermitente, porque depende da fala.
    /// </summary>
    [Fact]
    public void RF_276_todas_as_categorias_de_seguranca_nao_bloqueiam()
    {
        using var body = Body();
        var safety = body.RootElement.GetProperty("safetySettings");

        Assert.Equal(LlmRequest.SafetyCategories.Count, safety.GetArrayLength());

        foreach (var entry in safety.EnumerateArray())
        {
            Assert.Equal(LlmRequest.BlockNone, entry.GetProperty("threshold").GetString());
            Assert.Contains(entry.GetProperty("category").GetString()!,
                            LlmRequest.SafetyCategories);
        }
    }

    // ── RF-282 🔒 — o nível de raciocínio ───────────────────────────────────

    /// <summary>
    /// RF-282 🔒 — Família ANTIGA: níveis 0, 2 e 3 OMITEM o orçamento; o nível 1 usa P-76
    /// para modelos "pro" e 0 para os demais.
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void RF_282_na_familia_antiga_os_niveis_0_2_e_3_omitem(int level, bool pro)
    {
        var thinking = LlmRequest.Thinking(level, legacyFamily: true, isPro: pro);

        Assert.Null(thinking.Budget);
        Assert.Null(thinking.Level);
    }

    [Fact]
    public void RF_282_na_familia_antiga_o_nivel_1_usa_P76_para_pro_e_zero_para_os_demais()
    {
        Assert.Equal(P.LlmProThinkingBudget,
            LlmRequest.Thinking(1, legacyFamily: true, isPro: true).Budget);

        Assert.Equal(0, LlmRequest.Thinking(1, legacyFamily: true, isPro: false).Budget);
        Assert.Equal(512, P.LlmProThinkingBudget);
    }

    /// <summary>
    /// RF-282 🔒 — Família NOVA: 0 e 3 = alto; 1 = baixo para "pro" e mínimo para os demais;
    /// 2 = baixo para "pro" e médio para os demais.
    ///
    /// O mapeamento NÃO é monotônico — o nível 0 e o 3 dão o mesmo resultado —, e é assim
    /// mesmo: ele foi calibrado contra o comportamento de cada família, não derivado de uma
    /// escala.
    /// </summary>
    [Theory]
    [InlineData(0, false, "high")]
    [InlineData(3, false, "high")]
    [InlineData(0, true, "high")]
    [InlineData(3, true, "high")]
    [InlineData(1, true, "low")]
    [InlineData(1, false, "minimal")]
    [InlineData(2, true, "low")]
    [InlineData(2, false, "medium")]
    public void RF_282_na_familia_nova_o_nivel_vira_rotulo(int level, bool pro, string expected)
    {
        var thinking = LlmRequest.Thinking(level, legacyFamily: false, isPro: pro);

        Assert.Equal(expected, thinking.Level);
        Assert.Null(thinking.Budget);
    }

    [Fact]
    public void RF_282_o_orcamento_omitido_nao_aparece_no_corpo()
    {
        using var body = Body(model: "gemini-2.0-flash", thinking: 0);
        var generation = body.RootElement.GetProperty("generationConfig");

        Assert.False(generation.TryGetProperty("thinkingConfig", out _));
    }

    [Fact]
    public void RF_282_o_rotulo_da_familia_nova_aparece_no_corpo()
    {
        using var body = Body(model: "gemini-2.5-pro", thinking: 1);
        var config = body.RootElement
            .GetProperty("generationConfig").GetProperty("thinkingConfig");

        Assert.Equal("low", config.GetProperty("thinkingLevel").GetString());
    }

    // ── RF-273 / RF-526 — o corpo ───────────────────────────────────────────

    [Fact]
    public void RF_273_o_corpo_leva_instrucao_texto_seguranca_e_geracao()
    {
        using var body = Body();
        var root = body.RootElement;

        Assert.Equal("instrução", root.GetProperty("systemInstruction")
            .GetProperty("parts")[0].GetProperty("text").GetString());

        Assert.Equal("Hello", root.GetProperty("contents")[0]
            .GetProperty("parts")[0].GetProperty("text").GetString());

        Assert.True(root.TryGetProperty("safetySettings", out _));
        Assert.True(root.TryGetProperty("generationConfig", out _));
    }

    /// <summary>RF-526 — A temperatura é guardada de 0 a 100 e enviada dividida por 100.</summary>
    [Fact]
    public void RF_526_a_temperatura_vai_dividida_por_cem()
    {
        using var body = Body(temperature: 20);

        Assert.Equal(0.2, body.RootElement.GetProperty("generationConfig")
            .GetProperty("temperature").GetDouble(), 3);
    }

    // ── RF-281 — os presets ─────────────────────────────────────────────────

    [Fact]
    public void RF_281_os_presets_trazem_os_valores_calibrados()
    {
        var options = AdvancedOptions.Defaults();

        Assert.Equal((P.LlmTemperatureDefault, P.LlmThinkingDefault, P.LlmMaxOutputDefault),
                     LlmRequest.PresetValues(LlmPreset.Standard, options));

        Assert.Equal((P.LlmTemperatureEconomy, P.LlmThinkingEconomy, P.LlmMaxOutputEconomy),
                     LlmRequest.PresetValues(LlmPreset.Economy, options));

        options.LlmTemperature = 77;
        Assert.Equal(77, LlmRequest.PresetValues(LlmPreset.Custom, options).Temperature);
    }

    // ── RF-277 — bloqueio por conteúdo ──────────────────────────────────────

    /// <summary>
    /// RF-277 — Quando a resposta indica bloqueio, o programa refaz no tradutor web gratuito
    /// SEM sinalizar erro: o usuário não fez nada de errado, e uma mensagem de erro o faria
    /// procurar defeito na configuração dele.
    /// </summary>
    [Theory]
    [InlineData("""{"promptFeedback": {"blockReason": "SAFETY"}}""")]
    [InlineData("""{"candidates": [{"finishReason": "SAFETY"}]}""")]
    [InlineData("""{"candidates": [{"finishReason": "PROHIBITED_CONTENT"}]}""")]
    public void RF_277_o_bloqueio_e_reconhecido(string payload)
        => Assert.True(LlmRequest.WasBlocked(payload));

    [Theory]
    [InlineData("""{"candidates": [{"finishReason": "STOP"}]}""")]
    [InlineData("""{"candidates": [{"finishReason": "MAX_TOKENS"}]}""")]
    [InlineData("{}")]
    [InlineData("não é json")]
    public void Uma_resposta_normal_nao_e_bloqueio(string payload)
        => Assert.False(LlmRequest.WasBlocked(payload));
}
