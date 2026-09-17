using Gort.Core.Updating;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>RF-417 🔒 / RF-418 / RF-419 — A configuração padrão remota.</summary>
public class RemoteDefaultsTests
{
    private const string Completo = """
        {
          "separator_tokens": { "webfree": "@@@@", "llm": "<<<>>>" },
          "advanced_token": true,
          "browser_base_url": "https://exemplo.invalido",
          "browser_url_format": "{base}/?t={text}",
          "browser_extraction_script": "return document.body.innerText;"
        }
        """;

    /// <summary>
    /// RF-417 — Os valores que vêm daqui dependem de páginas de terceiros que mudam sem
    /// aviso; poder corrigi-los remotamente evita uma atualização do programa a cada mudança.
    /// </summary>
    [Fact]
    public void RF_417_o_arquivo_remoto_traz_os_cinco_valores()
    {
        var remote = RemoteDefaults.Parse(Completo);

        Assert.Equal("@@@@", remote.SeparatorTokenFor("webfree", "//////"));
        Assert.Equal("<<<>>>", remote.SeparatorTokenFor("llm", "//////"));
        Assert.True(remote.AdvancedToken);
        Assert.Equal("https://exemplo.invalido", remote.BrowserBaseUrl);
        Assert.Contains("{text}", remote.BrowserUrlFormat!);
        Assert.Contains("innerText", remote.BrowserExtractionScript!);
    }

    /// <summary>
    /// RF-418 — Valores AUSENTES mantêm os embutidos. Um arquivo remoto meio preenchido não
    /// pode apagar configuração que funciona: a regra é acrescentar, nunca esvaziar.
    /// </summary>
    [Fact]
    public void RF_418_valores_ausentes_mantem_os_embutidos()
    {
        var remote = RemoteDefaults.Parse("""{"advanced_token": false}""");

        Assert.Equal("//////", remote.SeparatorTokenFor("webfree", "//////"));
        Assert.Null(remote.BrowserBaseUrl);
        Assert.False(remote.AdvancedToken);
    }

    /// <summary>RF-418 — E valores VAZIOS também: vazio não é uma configuração nova.</summary>
    [Fact]
    public void RF_418_valores_vazios_mantem_os_embutidos()
    {
        var remote = RemoteDefaults.Parse("""
            {
              "separator_tokens": { "webfree": "" },
              "browser_base_url": "",
              "browser_url_format": ""
            }
            """);

        Assert.Equal("//////", remote.SeparatorTokenFor("webfree", "//////"));
        Assert.Null(remote.BrowserBaseUrl);
        Assert.Null(remote.BrowserUrlFormat);
    }

    /// <summary>
    /// RF-419 — Uma falha ao interpretar o arquivo remoto NÃO impede a inicialização: devolve
    /// vazio, e vazio mantém tudo o que está embutido.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("isto não é json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("<html>404 Not Found</html>")]
    [InlineData("{\"separator_tokens\": \"deveria ser objeto\"}")]
    public void RF_419_um_arquivo_remoto_ruim_nao_derruba_nada(string json)
    {
        var remote = RemoteDefaults.Parse(json);

        Assert.Equal("//////", remote.SeparatorTokenFor("webfree", "//////"));
        Assert.Null(remote.AdvancedToken);
        Assert.Null(remote.BrowserExtractionScript);
    }

    /// <summary>Um tipo errado num campo não contamina os outros.</summary>
    [Fact]
    public void Um_campo_com_tipo_errado_nao_contamina_os_demais()
    {
        var remote = RemoteDefaults.Parse("""
            {
              "advanced_token": "talvez",
              "browser_base_url": 42,
              "separator_tokens": { "webfree": "@@@@" }
            }
            """);

        Assert.Null(remote.AdvancedToken);
        Assert.Null(remote.BrowserBaseUrl);
        Assert.Equal("@@@@", remote.SeparatorTokenFor("webfree", "//////"));
    }

    /// <summary>A chave do serviço não diferencia maiúsculas, como o resto do catálogo.</summary>
    [Fact]
    public void A_chave_do_servico_nao_diferencia_maiusculas()
    {
        var remote = RemoteDefaults.Parse("""{"separator_tokens": {"WebFree": "@@@@"}}""");

        Assert.Equal("@@@@", remote.SeparatorTokenFor("webfree", "//////"));
    }
}
