using System.Text.RegularExpressions;
using Gort.Core.Localization;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>
/// RF-485 — Uma chave ausente aparece na interface como o PRÓPRIO NOME DA CHAVE. Isso torna
/// a falta visível para quem está traduzindo, mas não para quem está construindo: o programa
/// segue funcionando com "app.apply" no lugar de "Aplicar".
///
/// Este teste varre o código da interface atrás das chaves efetivamente usadas e confere que
/// todas existem na tabela — transformando um defeito silencioso em falha de teste.
/// </summary>
public class LocalizationCoverageTests
{
    private static Localizer Table()
        => Localizer.Load(Path.Combine(TestPaths.DataDirectory, "localizacao.csv"));

    /// <summary>Chaves referenciadas no código, por `_loc["..."]` e `_loc.Format("...")`.</summary>
    private static IEnumerable<(string Key, string File)> ReferencedKeys()
    {
        string appDirectory = Path.Combine(TestPaths.RepositoryRoot, "src", "Gort.App");
        if (!Directory.Exists(appDirectory)) yield break;

        var pattern = new Regex(
            @"_loc(?:\[""(?<k>[^""]+)""\]|\.(?:Get|Format|Has)\(""(?<k>[^""]+)"")",
            RegexOptions.Compiled);

        foreach (var file in Directory.EnumerateFiles(appDirectory, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            foreach (Match match in pattern.Matches(source))
            {
                yield return (match.Groups["k"].Value, Path.GetFileName(file));
            }
        }
    }

    [Fact]
    public void Toda_chave_usada_na_interface_existe_na_tabela()
    {
        var table = Table();

        var missing = ReferencedKeys()
            .Where(r => !r.Key.Contains('{'))          // chaves montadas em tempo de execução
            .Where(r => !table.Has(r.Key))
            .Select(r => $"{r.Key}  ({r.File})")
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        Assert.True(missing.Count == 0,
            "Chaves de localização usadas na interface e ausentes da tabela:\n  "
            + string.Join("\n  ", missing));
    }

    [Fact]
    public void A_varredura_encontra_as_chaves_de_verdade()
    {
        // Guarda contra o teste acima passar por não achar nada.
        var keys = ReferencedKeys().Select(r => r.Key).Distinct().ToList();
        Assert.True(keys.Count > 30, $"a varredura achou só {keys.Count} chaves");
        Assert.Contains("app.apply", keys);
    }

    /// <summary>
    /// RF-444 — As sete ações com atalho dedicado têm rótulo, já que a lista de atalhos
    /// monta a chave em tempo de execução e escaparia da varredura acima.
    /// </summary>
    [Fact]
    public void RF_444_cada_acao_com_atalho_tem_rotulo_na_tabela()
    {
        var table = Table();

        foreach (var (action, _) in Gort.Core.Shortcuts.ShortcutSet.Defaults)
        {
            string key = $"shortcut.{action}";
            Assert.True(table.Has(key), $"falta o rótulo '{key}'");
        }
    }
}
