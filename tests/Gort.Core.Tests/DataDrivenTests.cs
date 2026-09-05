using System.Text.RegularExpressions;
using Gort.Core.Catalog;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>
/// RF-566 / RF-567 — Acrescentar um idioma, um motor ou um serviço é alteração de DADOS.
///
/// Os testes do catálogo já leem os arquivos reais. Estes vão além: varrem o CÓDIGO atrás
/// dos literais que denunciariam a decisão tendo voltado para dentro dele, e carregam um
/// catálogo com um idioma inventado para conferir que o comportamento vem das PROPRIEDADES.
/// </summary>
public class DataDrivenTests
{
    /// <summary>
    /// RF-567 — "Nenhum ponto do programa compara com 'ja' ou 'en'."
    ///
    /// Esta varredura foi escrita depois de encontrar três violações reais: o idioma-ponte
    /// fixo no código, um ramo de configuração rápida que comparava com "ja" (e cujos dois
    /// ramos devolviam o mesmo valor), e uma lista de idiomas conhecidos dentro do motor de
    /// OCR do sistema, que faria um idioma novo em `languages.toml` ser ignorado sem
    /// explicação.
    /// </summary>
    [Fact]
    public void RF_567_nenhum_ponto_do_codigo_compara_com_um_identificador_de_idioma()
    {
        var offenders = new List<string>();

        // Os identificadores do catálogo real, mais os que a especificação nomeia.
        var keys = AppCatalog.Load(TestPaths.DataDirectory).Languages
            .Select(l => l.Key)
            .Concat(new[] { "ja", "en", "pt-BR" })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // A varredura procura COMPARAÇÕES e BUSCAS, não valores padrão. RF-309 fixa o
        // escopo inicial e RF-487 o idioma inicial da interface: um campo que NASCE com
        // "en" é decisão de produto declarada, que o usuário muda na primeira tela. O que
        // RF-567 proíbe é o programa DECIDIR por comparação.
        string alt = string.Join('|', keys.Select(Regex.Escape));
        var pattern = new Regex(
            "(?:" +
            "[!=]=\\s*\"(?:" + alt + ")\"" +          // == "ja"
            "|\"(?:" + alt + ")\"\\s*[!=]=" +          // "ja" ==
            "|\\.Equals\\(\\s*\"(?:" + alt + ")\"" +  // .Equals("ja"
            "|case\\s+\"(?:" + alt + ")\"" +           // case "ja"
            "|\"(?:" + alt + ")\"\\s*=>" +             // "ja" =>
            "|Language\\(\\s*\"(?:" + alt + ")\"\\s*\\)" +  // Language("ja")
            ")",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        foreach (string file in SourceFiles())
        {
            // O carregador do catálogo pode citar identificadores: é ele quem os lê.
            string name = Path.GetFileName(file);
            if (name is "AppCatalog.cs" or "CatalogTypes.cs") continue;

            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // Comentários explicam a regra citando os identificadores; é o código que
                // não pode compará-los.
                string code = line.TrimStart();
                if (code.StartsWith("//") || code.StartsWith("///") || code.StartsWith("*"))
                    continue;

                if (pattern.IsMatch(line))
                    offenders.Add($"{name}:{i + 1}  {line.Trim()}");
            }
        }

        Assert.True(offenders.Count == 0,
            "RF-567 — o código compara com identificadores de idioma:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// RF-566 — Um idioma acrescentado APENAS nos dados é carregado com todas as suas
    /// propriedades, e é delas que o comportamento depende.
    /// </summary>
    [Fact]
    public void RF_566_um_idioma_novo_e_so_uma_entrada_nos_dados()
    {
        string dir = Path.Combine(Path.GetTempPath(), "gort-catalogo",
                                  Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        foreach (string file in Directory.GetFiles(TestPaths.DataDirectory))
            File.Copy(file, Path.Combine(dir, Path.GetFileName(file)));

        // Um idioma que não existe no programa: escrita da direita para a esquerda, sem
        // espaços entre palavras e com suporte a vertical.
        File.AppendAllText(Path.Combine(dir, "languages.toml"), """

            [[language]]
            key = "xx"
            name_key = "lang.xx"
            ocr = "xx"
            separates_words_by_space = false
            supports_vertical = true
            right_to_left = true

            [language.codes]
            webfree = "xx"
            """);

        var catalog = AppCatalog.Load(dir);
        var novo = catalog.Language("xx");

        Assert.NotNull(novo);
        Assert.False(novo!.SeparatesWordsBySpace);   // RF-148 — remoção de espaços
        Assert.True(novo.SupportsVertical);
        Assert.True(novo.RightToLeft);               // RF-324 — direção do texto
        Assert.Equal("xx", novo.CodeFor("webfree"));

        // E os idiomas que já existiam continuam lá.
        Assert.NotNull(catalog.Language("pt-BR"));
    }

    /// <summary>
    /// RF-566 — O mesmo para um serviço de tradução: uma entrada nos dados basta para ele
    /// aparecer no catálogo com todas as suas propriedades.
    /// </summary>
    [Fact]
    public void RF_566_um_servico_novo_e_so_uma_entrada_nos_dados()
    {
        string dir = Path.Combine(Path.GetTempPath(), "gort-catalogo",
                                  Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        foreach (string file in Directory.GetFiles(TestPaths.DataDirectory))
            File.Copy(file, Path.Combine(dir, Path.GetFileName(file)));

        File.AppendAllText(Path.Combine(dir, "engines.toml"), """

            [[translation_service]]
            key = "inventado"
            name_key = "svc.inventado"
            needs_network = false
            uses_result_memory = false
            supports_bridge = true
            uses_collection = true
            shortcut_switchable = true
            """);

        var service = AppCatalog.Load(dir).Service("inventado");

        Assert.NotNull(service);
        Assert.False(service!.NeedsNetwork);
        Assert.False(service.UsesResultMemory);
        Assert.True(service.SupportsBridge);
        Assert.True(service.ShortcutSwitchable);
    }

    /// <summary>
    /// RF-239 / RF-567 — Qual é o idioma-ponte também é dado. Vazio desliga a ponte, o que
    /// é o comportamento certo para uma configuração que não a define.
    /// </summary>
    [Fact]
    public void RF_239_o_idioma_ponte_vem_dos_dados()
    {
        var catalog = AppCatalog.Load(TestPaths.DataDirectory);

        Assert.NotEmpty(catalog.BridgeLanguage);
        Assert.NotNull(catalog.Language(catalog.BridgeLanguage));
    }

    /// <summary>
    /// RF-225 / RF-029 — As chaves de RAIZ do catálogo são lidas da raiz.
    ///
    /// Este teste existe por causa de um defeito silencioso: `default_translation_service`
    /// e `bridge_language` estavam DEPOIS dos `[[ocr_engine]]` no arquivo, e em TOML uma
    /// chave solta pertence à última tabela aberta — não à raiz. O padrão do arquivo
    /// coincidia com o padrão do código, então a leitura falhava sem que nada mudasse.
    /// </summary>
    [Fact]
    public void RF_225_as_chaves_de_raiz_sao_lidas_da_raiz()
    {
        string dir = Path.Combine(Path.GetTempPath(), "gort-catalogo",
                                  Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        foreach (string file in Directory.GetFiles(TestPaths.DataDirectory))
            File.Copy(file, Path.Combine(dir, Path.GetFileName(file)));

        string engines = Path.Combine(dir, "engines.toml");
        File.WriteAllText(engines, File.ReadAllText(engines)
            .Replace("default_translation_service = \"webfree\"",
                     "default_translation_service = \"localdb\""));

        // Um valor DIFERENTE do padrão do código: se a leitura falhasse, viria "webfree".
        Assert.Equal("localdb", AppCatalog.Load(dir).DefaultTranslationService);
    }

    private static IEnumerable<string> SourceFiles()
    {
        foreach (string project in new[]
                 { "Gort.Core", "Gort.Engine", "Gort.Ocr.Rapid", "Gort.Platform", "Gort.App" })
        {
            string dir = Path.Combine(TestPaths.RepositoryRoot, "src", project);
            if (!Directory.Exists(dir)) continue;

            foreach (string file in Directory.EnumerateFiles(dir, "*.cs",
                                                            SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }
                yield return file;
            }
        }
    }
}
