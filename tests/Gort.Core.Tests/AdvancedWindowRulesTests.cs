using Gort.Core.Catalog;
using Gort.Core.Configuration;
using Gort.Core.Localization;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>
/// V.3 — As regras da janela de opções avançadas que não dependem de tela.
/// </summary>
public class AdvancedWindowRulesTests
{
    private static AppCatalog Catalog() => AppCatalog.Load(TestPaths.DataDirectory);

    private static Localizer Table()
        => Localizer.Load(Path.Combine(TestPaths.DataDirectory, "localizacao.csv"));

    /// <summary>
    /// RF-481 / RF-485 — O catálogo guarda a CHAVE do nome; o nome exibido vem da tabela.
    /// Um `name_key` sem linha na tabela apareceria na interface como "svc.localdb".
    /// </summary>
    [Fact]
    public void Todo_nome_do_catalogo_tem_linha_na_tabela_de_localizacao()
    {
        var catalog = Catalog();
        var table = Table();

        var missing = catalog.OcrEngines.Select(e => e.NameKey)
            .Concat(catalog.TranslationServices.Select(s => s.NameKey))
            .Concat(catalog.Languages.Select(l => l.NameKey))
            .Where(key => !table.Has(key))
            .OrderBy(k => k)
            .ToList();

        Assert.True(missing.Count == 0,
            "Chaves de nome do catálogo ausentes da tabela:\n  " + string.Join("\n  ", missing));
    }

    /// <summary>
    /// RF-447 — Os serviços com bloco próprio de atalho de troca são DADO, não código: a
    /// especificação nomeia sete, e é o catálogo que diz quais.
    /// </summary>
    [Fact]
    public void RF_447_o_catalogo_marca_os_servicos_trocaveis_por_atalho()
    {
        var switchable = Catalog().TranslationServices
            .Where(s => s.ShortcutSwitchable)
            .Select(s => s.Key)
            .ToList();

        Assert.Equal(7, switchable.Count);

        // Os sete que RF-447 nomeia, e nenhum outro.
        Assert.Contains("localdb", switchable);
        Assert.Contains("spreadsheet", switchable);
        Assert.Contains("webfree", switchable);
        Assert.Contains("localproc", switchable);
        Assert.Contains("browser", switchable);
        Assert.DoesNotContain("llm", switchable);
        Assert.DoesNotContain("customapi", switchable);
    }

    /// <summary>
    /// A janela edita uma CÓPIA: fechar sem aplicar não muda nada. É o que dá sentido ao
    /// botão "aplicar" de RF-530.
    /// </summary>
    [Fact]
    public void A_copia_para_edicao_nao_toca_no_original()
    {
        var original = AdvancedOptions.Defaults();
        original.CollectionFiles.Add("a.txt");

        var copy = original.CloneForEditing();
        copy.MergeLines = !original.MergeLines;
        copy.CollectionFiles.Add("b.txt");
        copy.LlmTemperature = 99;

        Assert.NotEqual(original.MergeLines, copy.MergeLines);
        Assert.Single(original.CollectionFiles);
        Assert.NotEqual(99, original.LlmTemperature);
    }

    /// <summary>
    /// RF-032 — As opções avançadas são globais, e o programa inteiro segura a MESMA
    /// referência. Aplicar traz os valores de volta preservando a identidade do objeto;
    /// trocá-la faria metade do programa continuar lendo a antiga.
    /// </summary>
    [Fact]
    public void Aplicar_traz_os_valores_de_volta_sem_trocar_o_objeto()
    {
        var live = AdvancedOptions.Defaults();
        var alias = live;                       // outra parte do programa

        var edited = live.CloneForEditing();
        edited.MergeLines = !live.MergeLines;
        edited.LlmTemperature = 77;
        edited.CollectionFiles.Add("nova.txt");

        live.CopyFrom(edited);

        Assert.Same(live, alias);
        Assert.Equal(edited.MergeLines, alias.MergeLines);
        Assert.Equal(77, alias.LlmTemperature);
        Assert.Equal(new[] { "nova.txt" }, alias.CollectionFiles);

        // E a lista não é compartilhada com a cópia: editar uma não mexe na outra.
        edited.CollectionFiles.Add("outra.txt");
        Assert.Single(alias.CollectionFiles);
    }

    /// <summary>
    /// RF-524 — O mínimo nunca fica acima do máximo. A normalização é do modelo, não da
    /// janela: quem carrega um perfil editado à mão recebe a mesma garantia.
    /// </summary>
    [Fact]
    public void RF_524_a_normalizacao_impede_minimo_acima_do_maximo()
    {
        var o = AdvancedOptions.Defaults();
        o.AutoFontSizeMin = 40;
        o.AutoFontSizeMax = 10;
        o.Normalize();

        Assert.True(o.AutoFontSizeMin <= o.AutoFontSizeMax);
    }

    /// <summary>
    /// RF-525 — Trocar para um preset não personalizado sobrescreve os três valores;
    /// "personalizado" MANTÉM os valores atuais.
    /// </summary>
    [Fact]
    public void RF_525_o_preset_personalizado_mantem_os_valores_atuais()
    {
        var o = AdvancedOptions.Defaults();
        o.ApplyPreset(LlmPreset.Economy);
        int temperatura = o.LlmTemperature;

        o.LlmTemperature = 55;
        o.ApplyPreset(LlmPreset.Custom);

        Assert.Equal(55, o.LlmTemperature);
        Assert.Equal(LlmPreset.Custom, o.LlmPreset);

        o.ApplyPreset(LlmPreset.Economy);
        Assert.Equal(temperatura, o.LlmTemperature);
    }
}
