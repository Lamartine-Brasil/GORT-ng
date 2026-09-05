using Gort.Core.Catalog;
using Gort.Core.Configuration;
using Gort.Core.Structuring;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>
/// VII.3 / VII.4 — Robustez e evolução: RF-562, RF-564 a RF-567.
///
/// Estes requisitos descrevem PROPRIEDADES do programa inteiro, não uma classe. Os testes
/// aqui existem para que elas parem de ser afirmações: cada um exercita a propriedade pelo
/// caminho que o programa usa de verdade.
/// </summary>
public class RobustnessTests
{
    private static string TempRoot()
    {
        string dir = Path.Combine(Path.GetTempPath(), "gort-robustez",
                                  Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ── RF-562 — sobreviver a arquivo corrompido e pasta ausente ────────────

    /// <summary>
    /// RF-562 / RF-024 — Um perfil corrompido não derruba o programa: TODOS os padrões são
    /// restaurados e a execução continua.
    /// </summary>
    [Theory]
    [InlineData("isto = não [é toml")]
    [InlineData("\0\0\0\0binário\0\0")]
    [InlineData("")]
    [InlineData("[[[[[")]
    public void RF_562_um_perfil_corrompido_restaura_os_padroes(string content)
    {
        string file = Path.Combine(TempRoot(), "perfil.gort");
        File.WriteAllText(file, content);

        var profile = Profile.Load(file, out _);
        var defaults = Profile.Defaults();

        Assert.Equal(defaults.OcrEngine, profile.OcrEngine);
        Assert.Equal(defaults.FontSize, profile.FontSize);
        Assert.Equal(defaults.Scale, profile.Scale);
    }

    [Fact]
    public void RF_562_opcoes_avancadas_corrompidas_restauram_os_padroes()
    {
        string file = Path.Combine(TempRoot(), "avancado.toml");
        File.WriteAllText(file, "= = =\nlixo");

        var options = AdvancedOptions.Load(file, out _);

        Assert.Equal(AdvancedOptions.Defaults().AutoFontSizeMin, options.AutoFontSizeMin);
        Assert.Equal(AdvancedOptions.Defaults().LlmPreset, options.LlmPreset);
    }

    /// <summary>
    /// RF-562 — Uma pasta de dados AUSENTE é criada, não é erro. O programa precisa abrir
    /// numa máquina nova, onde nada disso existe ainda.
    /// </summary>
    [Fact]
    public void RF_562_uma_pasta_de_dados_ausente_e_criada()
    {
        string root = Path.Combine(Path.GetTempPath(), "gort-novo",
                                   Guid.NewGuid().ToString("N"), "nao", "existe");
        Assert.False(Directory.Exists(root));

        var paths = new UserPaths(root);

        Assert.True(Directory.Exists(paths.Root));
        Assert.True(Directory.Exists(paths.ProfilesDirectory));
        Assert.True(Directory.Exists(paths.DiagnosticsDirectory));
        Assert.True(Directory.Exists(Path.GetDirectoryName(paths.CredentialsFor("x"))!));
    }

    /// <summary>
    /// RF-562 — Um catálogo ausente não derruba o programa. Sem catálogo não há o que
    /// traduzir, mas o programa precisa ABRIR para poder dizer isso ao usuário (RF-006).
    /// </summary>
    [Fact]
    public void RF_562_um_catalogo_ausente_nao_lanca()
    {
        var catalog = AppCatalog.Load(Path.Combine(TempRoot(), "sem-dados"));

        Assert.NotNull(catalog);
        Assert.NotNull(catalog.Languages);
        Assert.NotNull(catalog.TranslationServices);
    }

    // ── RF-564 — sem compatibilidade com produto anterior ───────────────────

    /// <summary>
    /// RF-564 — Não há código de leitura de formato legado em lugar algum. Um arquivo de
    /// perfil que não seja do formato deste programa cai nos padrões, como qualquer outro
    /// arquivo ilegível — não há um segundo caminho tentando interpretá-lo.
    /// </summary>
    [Fact]
    public void RF_564_um_formato_estranho_e_apenas_um_arquivo_ilegivel()
    {
        string file = Path.Combine(TempRoot(), "perfil.gort");
        File.WriteAllLines(file, new[]
        {
            "[Settings]", "OCREngine=2", "FontSize=14", "Scale=3",   // formato de outro produto
        });

        var profile = Profile.Load(file, out _);

        // Nada foi importado: os padrões valem.
        Assert.Equal(Profile.Defaults().Scale, profile.Scale);
        Assert.Equal(Profile.Defaults().FontSize, profile.FontSize);
    }

    // ── RF-565 — compatível consigo mesmo ao longo do tempo ────────────────

    /// <summary>
    /// RF-565 / RF-038 — Chaves DESCONHECIDAS são preservadas na regravação. O usuário pode
    /// alternar entre uma versão nova e uma antiga do programa e não pode perder
    /// configuração por isso.
    /// </summary>
    [Fact]
    public void RF_565_chaves_desconhecidas_sobrevivem_a_regravacao()
    {
        string file = Path.Combine(TempRoot(), "perfil.gort");

        var profile = Profile.Defaults();
        profile.Save(file);

        // Uma versão futura acrescentou uma chave que esta não conhece.
        File.AppendAllText(file, "\nrecurso_do_futuro = \"ligado\"\n");

        var reloaded = Profile.Load(file, out var store);
        reloaded.FontSize = 33;
        reloaded.Save(file, store);

        string text = File.ReadAllText(file);
        Assert.Contains("recurso_do_futuro", text);
        Assert.Contains("ligado", text);
    }

    /// <summary>
    /// RF-565 / RF-026 — Valores de conjunto fechado são persistidos por IDENTIFICADOR
    /// TEXTUAL, não por número de posição. Um número mudaria de significado ao acrescentar
    /// uma entrada no meio do enumerado.
    /// </summary>
    [Fact]
    public void RF_565_conjuntos_fechados_sao_persistidos_por_texto()
    {
        string file = Path.Combine(TempRoot(), "perfil.gort");

        var profile = Profile.Defaults();
        profile.FilterMode = Gort.Core.Imaging.FilterMode.Hsv;
        profile.WindowMode = WindowMode.Overlay;
        profile.Save(file);

        string text = File.ReadAllText(file);

        Assert.Contains("hsv", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("overlay", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>RF-565 / RF-023 — Todo arquivo do usuário carrega a versão do esquema.</summary>
    [Fact]
    public void RF_565_todo_arquivo_carrega_a_versao_do_esquema()
    {
        string root = TempRoot();

        Profile.Defaults().Save(Path.Combine(root, "perfil.gort"));
        AdvancedOptions.Defaults().Save(Path.Combine(root, "avancado.toml"));

        foreach (string file in Directory.GetFiles(root))
        {
            Assert.Contains(TomlStore.SchemaVersionKey, File.ReadAllText(file));
        }
    }
}
