using System.Globalization;
using Gort.Core.Calibration;
using Gort.Core.Catalog;
using Gort.Core.Configuration;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>V.3 — As regras de apresentação e de restauração das opções avançadas.</summary>
public class AdvancedLabelTests
{
    /// <summary>
    /// RF-526 — A temperatura aparece DIVIDIDA POR 100, com duas casas decimais. O valor
    /// guardado é inteiro porque é o que o controle deslizante move.
    /// </summary>
    [Theory]
    [InlineData(0, "0.00")]
    [InlineData(20, "0.20")]
    [InlineData(100, "1.00")]
    [InlineData(7, "0.07")]
    public void RF_526_a_temperatura_e_exibida_dividida_por_cem(int value, string expected)
    {
        var anterior = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            Assert.Equal(expected, AdvancedLabels.Temperature(value));
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    /// <summary>
    /// RF-526 — O nível de raciocínio vira uma CHAVE de localização derivada do número.
    /// Acrescentar um nível é acrescentar uma linha na tabela, não um caso num `switch`.
    /// </summary>
    [Fact]
    public void RF_526_o_nivel_de_raciocinio_vira_chave_de_localizacao()
    {
        Assert.Equal("llm.thinking.0", AdvancedLabels.ThinkingKey(0));
        Assert.Equal("llm.thinking.3", AdvancedLabels.ThinkingKey(3));
    }

    /// <summary>Todos os níveis da faixa P-71/P-72 têm texto na tabela real.</summary>
    [Fact]
    public void Todos_os_niveis_de_raciocinio_tem_texto_na_tabela()
    {
        var loc = Gort.Core.Localization.Localizer.Load(
            Path.Combine(TestPaths.DataDirectory, "localizacao.csv"));

        for (int level = P.LlmThinkingMin; level <= P.LlmThinkingMax; level++)
        {
            string key = AdvancedLabels.ThinkingKey(level);
            Assert.False(string.IsNullOrWhiteSpace(loc[key]), $"falta '{key}'");
            Assert.NotEqual(key, loc[key]);
        }
    }

    [Fact]
    public void RF_526_o_limite_de_saida_e_o_numero_que_e()
        => Assert.Equal("4000", AdvancedLabels.MaxOutput(4000));

    /// <summary>
    /// RF-532 — Ao restaurar padrões, a direção do texto vem da PROPRIEDADE do idioma de
    /// destino, não de uma lista embutida (RF-311, RF-567).
    /// </summary>
    [Fact]
    public void RF_532_restaurar_padroes_deriva_a_direcao_do_idioma_de_destino()
    {
        var daDireita = Language("ar", rightToLeft: true);
        var daEsquerda = Language("pt-BR", rightToLeft: false);

        Assert.True(AdvancedOptions.Defaults(daDireita).RightToLeft);
        Assert.False(AdvancedOptions.Defaults(daEsquerda).RightToLeft);
    }

    /// <summary>Sem idioma de destino resolvido, os padrões valem como estão.</summary>
    [Fact]
    public void Sem_idioma_de_destino_os_padroes_ficam_como_estao()
        => Assert.Equal(AdvancedOptions.Defaults().RightToLeft,
                        AdvancedOptions.Defaults(null).RightToLeft);

    private static LanguageInfo Language(string key, bool rightToLeft) => new()
    {
        Key = key,
        NameKey = $"language.{key}",
        OcrCode = key,
        Codes = new Dictionary<string, string>(),
        SeparatesWordsBySpace = true,
        SupportsVertical = false,
        RightToLeft = rightToLeft,
    };
}
