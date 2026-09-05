using Gort.Core.Translation.Presets;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>RF-302 a RF-307 — As duas fontes de presets de API personalizada.</summary>
public class ApiPresetStoreTests
{
    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "gort-presets",
                                  Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteList(string file, params string[] names)
    {
        var lines = new List<string> { "schema_version = 1", "" };
        foreach (string name in names)
        {
            lines.Add("[[preset]]");
            lines.Add($"nome = \"{name}\"");
            lines.Add($"url = \"http://lista/{name}\"");
            lines.Add("");
        }
        File.WriteAllLines(file, lines);
    }

    private static void WriteSingleFile(string path, string name, string url)
        => File.WriteAllLines(path, new[]
        {
            "schema_version = 1",
            $"nome = \"{name}\"",
            $"url = \"{url}\"",
        });

    /// <summary>
    /// RF-302 — Os arquivos individuais têm PRECEDÊNCIA sobre entradas de mesmo nome na
    /// lista editável.
    /// </summary>
    [Fact]
    public void RF_302_o_arquivo_vence_a_entrada_de_mesmo_nome_da_lista()
    {
        string dir = TempDir();
        string list = Path.Combine(dir, "lista.toml");
        string folder = Path.Combine(dir, "presets");
        Directory.CreateDirectory(folder);

        WriteList(list, "meu", "outro");
        WriteSingleFile(Path.Combine(folder, "meu.toml"), "meu", "http://arquivo/meu");

        var store = ApiPresetStore.Load(list, folder);

        Assert.Equal(2, store.Presets.Count);
        var meu = store.Find("meu")!;
        Assert.Equal("http://arquivo/meu", meu.Url);
        Assert.True(meu.IsFromFile);

        // A outra entrada da lista continua editável.
        Assert.False(store.Find("outro")!.IsFromFile);
    }

    /// <summary>
    /// RF-303 — Presets vindos de arquivo não podem ser renomeados nem removidos pela
    /// interface, e são exibidos com um prefixo distintivo (RF-528).
    /// </summary>
    [Fact]
    public void RF_303_preset_de_arquivo_nao_e_renomeado_nem_removido()
    {
        string dir = TempDir();
        string folder = Path.Combine(dir, "presets");
        Directory.CreateDirectory(folder);
        WriteSingleFile(Path.Combine(folder, "fixo.toml"), "fixo", "http://x");

        var store = ApiPresetStore.Load(Path.Combine(dir, "lista.toml"), folder);
        var preset = store.Find("fixo")!;

        Assert.False(store.Rename(preset, "outro nome"));
        Assert.False(store.Remove(preset));
        Assert.Equal("fixo", preset.Name);
        Assert.Single(store.Presets);

        // RF-528 — o prefixo distingue na lista, mas não entra no nome.
        Assert.StartsWith(ApiPreset.FilePrefix, preset.DisplayName);
        Assert.Equal("fixo", preset.Name);
    }

    /// <summary>RF-304 — Um arquivo pode conter um único preset OU uma lista.</summary>
    [Fact]
    public void RF_304_o_arquivo_aceita_um_preset_ou_uma_lista()
    {
        string dir = TempDir();
        string folder = Path.Combine(dir, "presets");
        Directory.CreateDirectory(folder);

        WriteSingleFile(Path.Combine(folder, "um.toml"), "um", "http://um");
        File.WriteAllLines(Path.Combine(folder, "varios.toml"), new[]
        {
            "schema_version = 1",
            "[[preset]]", "nome = \"a\"", "url = \"http://a\"",
            "[[preset]]", "nome = \"b\"", "url = \"http://b\"",
        });

        var store = ApiPresetStore.Load(Path.Combine(dir, "lista.toml"), folder);

        Assert.Equal(3, store.Presets.Count);
        Assert.All(store.Presets, p => Assert.True(p.IsFromFile));
    }

    /// <summary>
    /// RF-304 — Nomes duplicados dentro do mesmo conjunto são IGNORADOS com registro. Não
    /// silenciosamente: o usuário precisa saber que um dos dois arquivos não foi usado.
    /// </summary>
    [Fact]
    public void RF_304_duplicado_entre_arquivos_e_ignorado_com_registro()
    {
        string dir = TempDir();
        string folder = Path.Combine(dir, "presets");
        Directory.CreateDirectory(folder);

        WriteSingleFile(Path.Combine(folder, "a.toml"), "igual", "http://primeiro");
        WriteSingleFile(Path.Combine(folder, "b.toml"), "igual", "http://segundo");

        var store = ApiPresetStore.Load(Path.Combine(dir, "lista.toml"), folder);

        Assert.Single(store.Presets);
        Assert.Equal("http://primeiro", store.Find("igual")!.Url);
        Assert.Contains(store.Notices, n => n.Contains("igual"));
    }

    /// <summary>
    /// RF-305 / RF-529 — Nomes duplicados criados pela interface recebem sufixo numérico
    /// entre parênteses.
    /// </summary>
    [Fact]
    public void RF_305_nome_duplicado_ganha_sufixo_numerico()
    {
        var store = new ApiPresetStore();

        Assert.Equal("meu", store.Add("meu").Name);
        Assert.Equal("meu (2)", store.Add("meu").Name);
        Assert.Equal("meu (3)", store.Add("meu").Name);
        Assert.Equal("outro", store.Add("outro").Name);
    }

    [Fact]
    public void Renomear_para_um_nome_ja_usado_tambem_ganha_sufixo()
    {
        var store = new ApiPresetStore();
        store.Add("a");
        var b = store.Add("b");

        Assert.True(store.Rename(b, "a"));
        Assert.Equal("a (2)", b.Name);
    }

    /// <summary>Renomear um preset para o nome que ele já tem não o transforma em "(2)".</summary>
    [Fact]
    public void Renomear_para_o_proprio_nome_nao_muda_nada()
    {
        var store = new ApiPresetStore();
        var a = store.Add("a");

        Assert.True(store.Rename(a, "a"));
        Assert.Equal("a", a.Name);
    }

    /// <summary>RF-306 — Cada preset é uma entrada separada na lista de serviços.</summary>
    [Fact]
    public void RF_306_cada_preset_vira_uma_entrada_de_servico()
    {
        var store = new ApiPresetStore();
        store.Add("meu servidor");

        Assert.Equal(new[] { "Custom – meu servidor" }, store.ServiceEntries());
    }

    /// <summary>
    /// RF-307 — Um serviço salvo que não existe mais cai para o BANCO DE DADOS LOCAL, e não
    /// para o primeiro da lista: é o único que funciona sem rede e sem credencial.
    /// </summary>
    [Fact]
    public void RF_307_servico_inexistente_cai_para_o_banco_local()
    {
        var disponiveis = new[] { "webfree", "localdb", "Custom – meu" };

        Assert.Equal("Custom – meu",
            ApiPresetStore.ResolveService("Custom – meu", disponiveis));
        Assert.Equal("localdb",
            ApiPresetStore.ResolveService("Custom – removido", disponiveis));
    }

    /// <summary>
    /// RF-303 — Ao salvar, os presets de arquivo voltam para o SEU arquivo, e a lista
    /// editável fica só com os seus.
    /// </summary>
    [Fact]
    public void RF_303_salvar_devolve_cada_preset_ao_seu_arquivo()
    {
        string dir = TempDir();
        string list = Path.Combine(dir, "lista.toml");
        string folder = Path.Combine(dir, "presets");
        Directory.CreateDirectory(folder);

        WriteList(list, "editavel");
        string file = Path.Combine(folder, "de-arquivo.toml");
        WriteSingleFile(file, "de-arquivo", "http://antigo");

        var store = ApiPresetStore.Load(list, folder);
        store.Find("de-arquivo")!.Url = "http://novo";
        store.Find("editavel")!.Url = "http://tambem-novo";
        store.Save(list);

        var recarregado = ApiPresetStore.Load(list, folder);
        Assert.Equal("http://novo", recarregado.Find("de-arquivo")!.Url);
        Assert.Equal("http://tambem-novo", recarregado.Find("editavel")!.Url);

        // A lista editável não engorda com os presets de arquivo.
        Assert.DoesNotContain("de-arquivo", File.ReadAllText(list));
    }

    /// <summary>RF-024 — Um arquivo ilegível é registrado e não impede os demais.</summary>
    [Fact]
    public void Arquivo_ilegivel_nao_impede_os_outros()
    {
        string dir = TempDir();
        string folder = Path.Combine(dir, "presets");
        Directory.CreateDirectory(folder);

        File.WriteAllText(Path.Combine(folder, "quebrado.toml"), "isto = não [é toml");
        WriteSingleFile(Path.Combine(folder, "bom.toml"), "bom", "http://bom");

        var store = ApiPresetStore.Load(Path.Combine(dir, "lista.toml"), folder);

        Assert.NotNull(store.Find("bom"));
        Assert.Contains(store.Notices, n => n.Contains("quebrado"));
    }

    /// <summary>Um arquivo sem nome usa o nome do próprio arquivo.</summary>
    [Fact]
    public void Preset_sem_nome_herda_o_nome_do_arquivo()
    {
        string dir = TempDir();
        string folder = Path.Combine(dir, "presets");
        Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder, "servidor-de-casa.toml"), new[]
        {
            "schema_version = 1", "url = \"http://casa\"",
        });

        var store = ApiPresetStore.Load(Path.Combine(dir, "lista.toml"), folder);

        Assert.NotNull(store.Find("servidor-de-casa"));
    }
}
