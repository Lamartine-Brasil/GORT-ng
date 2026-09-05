using Gort.Core.Localization;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>RF-482 — Leitor de tabela separada por vírgulas.</summary>
public class CsvTableTests
{
    [Fact]
    public void Campos_simples_sao_separados_por_virgula()
    {
        var rows = CsvTable.Parse("a,b,c\nd,e,f");

        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "a", "b", "c" }, rows[0]);
        Assert.Equal(new[] { "d", "e", "f" }, rows[1]);
    }

    /// <summary>
    /// RF-482 — Campos entre aspas contendo VÍRGULAS. Textos de interface têm vírgula o
    /// tempo todo.
    /// </summary>
    [Fact]
    public void RF_482_um_campo_citado_pode_conter_virgula()
    {
        var rows = CsvTable.Parse("chave,\"um, dois, três\"");
        Assert.Equal(new[] { "chave", "um, dois, três" }, rows[0]);
    }

    /// <summary>
    /// RF-482 — Campos entre aspas contendo QUEBRAS DE LINHA. Mensagens longas as têm, e um
    /// leitor que dividisse por linha quebraria na primeira mensagem real.
    /// </summary>
    [Fact]
    public void RF_482_um_campo_citado_pode_conter_quebra_de_linha()
    {
        var rows = CsvTable.Parse("chave,\"primeira linha\nsegunda linha\"\noutra,valor");

        Assert.Equal(2, rows.Count);
        Assert.Equal("primeira linha\nsegunda linha", rows[0][1]);
        Assert.Equal(new[] { "outra", "valor" }, rows[1]);
    }

    [Fact]
    public void Aspas_duplas_dentro_de_um_campo_citado_viram_uma_aspa()
    {
        var rows = CsvTable.Parse("chave,\"ele disse \"\"olá\"\"\"");
        Assert.Equal("ele disse \"olá\"", rows[0][1]);
    }

    [Fact]
    public void Quebras_de_linha_do_windows_sao_aceitas()
    {
        var rows = CsvTable.Parse("a,b\r\nc,d");
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "c", "d" }, rows[1]);
    }

    [Fact]
    public void Um_campo_vazio_e_preservado()
    {
        var rows = CsvTable.Parse("a,,c");
        Assert.Equal(new[] { "a", "", "c" }, rows[0]);
    }

    [Fact]
    public void O_escape_cita_apenas_quando_necessario()
    {
        Assert.Equal("simples", CsvTable.Escape("simples"));
        Assert.Equal("\"com, vírgula\"", CsvTable.Escape("com, vírgula"));
        Assert.Equal("\"com \"\"aspas\"\"\"", CsvTable.Escape("com \"aspas\""));
        Assert.Equal("\"com\nquebra\"", CsvTable.Escape("com\nquebra"));
    }

    [Fact]
    public void Uma_entrada_vazia_nao_lanca()
        => Assert.Empty(CsvTable.Parse(""));
}

/// <summary>Cap. 26 — Localização da interface.</summary>
public class LocalizerTests
{
    private static Localizer Real()
        => Localizer.Load(Path.Combine(TestPaths.DataDirectory, "localizacao.csv"));

    private static Localizer FromText(string csv)
    {
        string file = Path.Combine(Path.GetTempPath(), "gort-loc",
                                   Guid.NewGuid().ToString("N") + ".csv");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, csv);
        return Localizer.Load(file);
    }

    /// <summary>RF-489 — A tabela é um arquivo de dados externo, e ele existe de fato.</summary>
    [Fact]
    public void RF_489_a_tabela_e_um_arquivo_externo_distribuido_com_o_programa()
    {
        var loc = Real();
        Assert.True(loc.Count > 50, $"a tabela tem só {loc.Count} chaves");
    }

    /// <summary>RF-487 — O idioma inicial é o português do Brasil.</summary>
    [Fact]
    public void RF_487_o_idioma_inicial_e_portugues_do_brasil()
    {
        Assert.Equal("pt-BR", Localizer.InitialLanguage);
        Assert.Contains("pt-BR", Real().Languages);
    }

    [Fact]
    public void As_chaves_reais_devolvem_texto_em_portugues()
    {
        var loc = Real();
        Assert.Equal("Aplicar", loc["app.apply"]);
        Assert.Equal("Área de OCR", loc["area.title"]);
    }

    /// <summary>
    /// RF-485 — Uma chave ausente resulta no PRÓPRIO NOME DA CHAVE, para tornar a falta
    /// visível. Devolver vazio faria o rótulo em branco passar despercebido.
    ///
    /// Critério de aceite do capítulo 26.
    /// </summary>
    [Fact]
    public void RF_485_uma_chave_ausente_aparece_como_o_proprio_nome()
    {
        var loc = Real();
        Assert.Equal("chave.que.nao.existe", loc["chave.que.nao.existe"]);
        Assert.False(loc.Has("chave.que.nao.existe"));
    }

    /// <summary>
    /// RF-482 — A tabela real tem campos com vírgula, e eles chegam inteiros: é para isso
    /// que o leitor precisa entender aspas.
    /// </summary>
    [Fact]
    public void RF_482_a_tabela_real_tem_campos_com_virgula()
    {
        var loc = Real();

        string texto = loc["msg.applied_resumed"];
        Assert.Contains(",", texto);
        Assert.Contains("tradução retomada", texto);

        // E o campo NÃO foi partido em dois pela vírgula.
        Assert.StartsWith("configuração aplicada", texto);
    }

    /// <summary>
    /// RF-484 — Sem escolha do usuário, o idioma vem do sistema, com queda para o inicial.
    ///
    /// Critério de aceite do capítulo 26: "Com o sistema em português e sem escolha
    /// explícita, a interface abre em português."
    /// </summary>
    [Fact]
    public void RF_484_sem_escolha_o_idioma_vem_do_sistema()
    {
        var loc = Real();

        loc.SelectLanguage(chosen: null, systemLanguage: "pt-BR");
        Assert.Equal("pt-BR", loc.Language);
    }

    [Fact]
    public void RF_484_uma_variante_do_sistema_encontra_o_idioma_pela_subetiqueta()
    {
        var loc = Real();
        // Um sistema em português de Portugal encontra o português do Brasil antes de cair
        // para o inicial.
        loc.SelectLanguage(chosen: null, systemLanguage: "pt-PT");
        Assert.Equal("pt-BR", loc.Language);
    }

    [Fact]
    public void RF_484_um_sistema_sem_correspondencia_cai_para_o_idioma_inicial()
    {
        var loc = Real();
        loc.SelectLanguage(chosen: null, systemLanguage: "fi-FI");
        Assert.Equal(Localizer.InitialLanguage, loc.Language);
    }

    [Fact]
    public void A_escolha_do_usuario_tem_precedencia_sobre_o_sistema()
    {
        var loc = FromText("chave,pt-BR,en\napp.apply,Aplicar,Apply\n");
        loc.SelectLanguage(chosen: "en", systemLanguage: "pt-BR");

        Assert.Equal("en", loc.Language);
        Assert.Equal("Apply", loc["app.apply"]);
    }

    /// <summary>
    /// RF-483 — Acrescentar um idioma de interface é acrescentar uma COLUNA de dados, sem
    /// tocar em código.
    /// </summary>
    [Fact]
    public void RF_483_um_idioma_novo_e_apenas_uma_coluna_a_mais()
    {
        var loc = FromText("chave,pt-BR,en,ja\napp.apply,Aplicar,Apply,適用\n");

        Assert.Equal(new[] { "pt-BR", "en", "ja" }, loc.Languages);

        loc.SelectLanguage("ja");
        Assert.Equal("適用", loc["app.apply"]);
    }

    /// <summary>Uma coluna vazia para um idioma cai para o inicial, em vez de sumir.</summary>
    [Fact]
    public void Uma_traducao_parcial_cai_para_o_idioma_inicial()
    {
        var loc = FromText("chave,pt-BR,en\napp.apply,Aplicar,\n");
        loc.SelectLanguage("en");

        Assert.Equal("Aplicar", loc["app.apply"]);
    }

    /// <summary>
    /// RF-486 — Trocar o idioma exige reinício, e o aviso é dado NA LÍNGUA NOVA: avisar na
    /// antiga seria mostrar a mensagem no idioma que o usuário acabou de abandonar.
    /// </summary>
    [Fact]
    public void RF_486_o_aviso_de_reinicio_vem_na_lingua_nova()
    {
        var loc = FromText(
            "chave,pt-BR,en\nui.restart_required,Reinicie o programa,Please restart\n");

        Assert.Equal("pt-BR", loc.Language);
        Assert.Equal("Please restart", loc.RestartWarningIn("en"));

        // O idioma ativo NÃO muda por consultar o aviso: a troca só vale depois do reinício.
        Assert.Equal("pt-BR", loc.Language);
    }

    [Fact]
    public void Os_marcadores_posicionais_sao_substituidos()
    {
        var loc = Real();
        Assert.Equal("memória: 233 MB", loc.Format("app.memory", 233));
    }

    [Fact]
    public void Um_marcador_malformado_nao_derruba_a_interface()
    {
        var loc = FromText("chave,pt-BR\nquebrado,\"{0} e {\"\n");
        Assert.Equal("{0} e {", loc.Format("quebrado", 1));
    }

    [Fact]
    public void P8_uma_tabela_ausente_deixa_cada_chave_como_o_proprio_nome()
    {
        var loc = Localizer.Load(Path.Combine(Path.GetTempPath(), "gort-sem-tabela.csv"));
        Assert.Equal(0, loc.Count);
        Assert.Equal("app.apply", loc["app.apply"]);
    }

    /// <summary>Linhas de comentário na tabela não viram chaves.</summary>
    [Fact]
    public void As_linhas_de_comentario_sao_ignoradas()
    {
        var loc = FromText("chave,pt-BR\n# um comentário,ignorado\napp.apply,Aplicar\n");
        Assert.Equal(1, loc.Count);
        Assert.Equal("Aplicar", loc["app.apply"]);
    }
}
