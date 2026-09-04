using Gort.Core.Calibration;
using Gort.Core.Model;
using Gort.Core.Shortcuts;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>Cap. 22 — Atalhos de teclado.</summary>
public class ShortcutTests
{
    private static string[] Combo(params string[] keys) => keys;

    // ── Normalização (RF-437) ────────────────────────────────────────────────

    [Theory]
    [InlineData("LeftShift", "Shift")]
    [InlineData("RightShift", "Shift")]
    [InlineData("LCtrl", "Ctrl")]
    [InlineData("Control", "Ctrl")]
    [InlineData("RightAlt", "Alt")]
    [InlineData("Command", "Meta")]
    [InlineData("LWin", "Meta")]
    [InlineData("z", "Z")]
    [InlineData("F1", "F1")]
    public void RF_437_as_variantes_esquerda_e_direita_viram_um_unico_codigo(
        string entrada, string esperado)
        => Assert.Equal(esperado, KeyNames.Normalize(entrada));

    [Fact]
    public void RF_437_shift_esquerdo_e_direito_sao_equivalentes_na_pratica()
    {
        var set = ShortcutSet.WithDefaults();

        Assert.NotNull(set.Match(Combo("LeftCtrl", "RightShift", "Z")));
        Assert.NotNull(set.Match(Combo("RightCtrl", "LeftShift", "z")));
    }

    // ── Correspondência (RF-438) ─────────────────────────────────────────────

    /// <summary>
    /// RF-438 — Mesmo tamanho e mesmos elementos, INDEPENDENTEMENTE da ordem.
    /// </summary>
    [Fact]
    public void RF_438_a_ordem_das_teclas_nao_importa()
    {
        var set = ShortcutSet.WithDefaults();
        var esperado = ShortcutAction.ToggleRealtimeTranslation;

        Assert.Equal(esperado, set.Match(Combo("Ctrl", "Shift", "Z"))!.Action);
        Assert.Equal(esperado, set.Match(Combo("Z", "Ctrl", "Shift"))!.Action);
        Assert.Equal(esperado, set.Match(Combo("Shift", "Z", "Ctrl"))!.Action);
    }

    [Fact]
    public void RF_438_um_conjunto_maior_nao_casa()
    {
        var set = ShortcutSet.WithDefaults();
        Assert.Null(set.Match(Combo("Ctrl", "Shift", "Z", "Alt")));
    }

    [Fact]
    public void RF_438_um_conjunto_menor_nao_casa()
    {
        var set = ShortcutSet.WithDefaults();
        Assert.Null(set.Match(Combo("Ctrl", "Z")));
    }

    // ── Duplicatas (RF-439) ──────────────────────────────────────────────────

    /// <summary>
    /// RF-439 — Combinações duplicadas são PERMITIDAS. Vence a primeira na ordem de
    /// verificação, e a segunda nunca dispara — sem recusar a configuração e sem avisar.
    ///
    /// A ordem é a de declaração da ação: TranslateOnce vem antes de QuickArea.
    /// </summary>
    [Fact]
    public void RF_439_a_duplicata_e_aceita_em_silencio_e_a_primeira_vence()
    {
        var set = new ShortcutSet();
        set.Set(ShortcutAction.QuickArea, Combo("Ctrl", "Shift", "Q"));
        set.Set(ShortcutAction.TranslateOnce, Combo("Ctrl", "Shift", "Q"));

        // Nenhuma exceção, nenhuma recusa: as duas continuam configuradas.
        Assert.Equal(2, set.All.Count);

        // E vence a primeira da ordem de verificação, que é a ordem de declaração.
        Assert.Equal(ShortcutAction.TranslateOnce, set.Match(Combo("Ctrl", "Shift", "Q"))!.Action);
    }

    [Fact]
    public void RF_439_a_ordem_de_verificacao_e_estavel_entre_chamadas()
    {
        var set = new ShortcutSet();
        set.Set(ShortcutAction.QuickArea, Combo("Ctrl", "P"));
        set.Set(ShortcutAction.SnapshotArea, Combo("Ctrl", "P"));

        var primeiro = set.Match(Combo("Ctrl", "P"))!.Action;
        for (int i = 0; i < 20; i++)
            Assert.Equal(primeiro, set.Match(Combo("Ctrl", "P"))!.Action);
    }

    // ── Limites (RF-442, RF-446) ─────────────────────────────────────────────

    [Fact]
    public void RF_442_uma_combinacao_aceita_no_maximo_tres_teclas()
    {
        var set = new ShortcutSet();
        var config = set.Set(ShortcutAction.QuickArea, Combo("Ctrl", "Shift", "Alt", "Q", "W"));

        Assert.Equal(P.MaxShortcutKeys, config.Keys.Count);
    }

    [Fact]
    public void RF_513_teclas_repetidas_sao_ignoradas()
    {
        var set = new ShortcutSet();
        var config = set.Set(ShortcutAction.QuickArea, Combo("Ctrl", "Ctrl", "Q"));
        Assert.Equal(new[] { "Ctrl", "Q" }, config.Keys);
    }

    /// <summary>RF-446 — Um atalho vazio é válido e NUNCA dispara.</summary>
    [Fact]
    public void RF_446_um_atalho_vazio_e_valido_e_nunca_dispara()
    {
        var set = ShortcutSet.WithDefaults();
        set.Clear(ShortcutAction.ToggleRealtimeTranslation);

        var config = set.Find(ShortcutAction.ToggleRealtimeTranslation)!;
        Assert.True(config.IsEmpty);
        Assert.Null(set.Match(Combo("Ctrl", "Shift", "Z")));
        Assert.Null(set.Match(Array.Empty<string>()));
    }

    /// <summary>RF-445 — "Restaurar padrão" devolve a combinação de RF-444.</summary>
    [Fact]
    public void RF_445_restaurar_padrao_devolve_a_combinacao_da_tabela()
    {
        var set = ShortcutSet.WithDefaults();
        set.Clear(ShortcutAction.TranslateOnce);
        set.RestoreDefault(ShortcutAction.TranslateOnce);

        Assert.Equal(new[] { "Ctrl", "Shift", "C" },
                     set.Find(ShortcutAction.TranslateOnce)!.Keys);
    }

    /// <summary>RF-444 — Os sete atalhos dedicados e seus padrões.</summary>
    [Theory]
    [InlineData(ShortcutAction.ToggleRealtimeTranslation, "Z")]
    [InlineData(ShortcutAction.TranslateOnce, "C")]
    [InlineData(ShortcutAction.SnapshotArea, "A")]
    [InlineData(ShortcutAction.QuickArea, "X")]
    [InlineData(ShortcutAction.OpenDictionaryEditor, "S")]
    [InlineData(ShortcutAction.ToggleTranslationWindow, "D")]
    [InlineData(ShortcutAction.ToggleMouseFollowArea, "F")]
    public void RF_444_os_padroes_sao_os_da_tabela(ShortcutAction acao, string tecla)
    {
        var set = ShortcutSet.WithDefaults();
        Assert.Equal(new[] { "Ctrl", "Shift", tecla }, set.Find(acao)!.Keys);
    }

    [Fact]
    public void RF_444_ha_sete_acoes_com_atalho_dedicado()
        => Assert.Equal(7, ShortcutSet.Defaults.Count);
}

/// <summary>RF-436 a RF-443 — O despachante de atalhos.</summary>
public class ShortcutDispatcherTests
{
    private static ShortcutDispatcher New() => new(ShortcutSet.WithDefaults());

    /// <summary>
    /// Critério de aceite do capítulo 22: "Pressionar e segurar o atalho de tradução dispara
    /// a ação uma única vez." (RF-440)
    /// </summary>
    [Fact]
    public void RF_440_segurar_o_atalho_dispara_uma_unica_vez()
    {
        var d = New();
        d.KeyDown("Ctrl");
        d.KeyDown("Shift");

        Assert.NotNull(d.KeyDown("Z"));

        // A repetição automática do teclado reenvia a mesma tecla dezenas de vezes.
        for (int i = 0; i < 30; i++) Assert.Null(d.KeyDown("Z"));
    }

    /// <summary>RF-441 — Soltar qualquer tecla limpa o conjunto inteiro.</summary>
    [Fact]
    public void RF_441_soltar_qualquer_tecla_limpa_o_conjunto()
    {
        var d = New();
        d.KeyDown("Ctrl");
        d.KeyDown("Shift");
        d.KeyDown("Z");
        Assert.Equal(3, d.Pressed.Count);

        d.KeyUp("Shift");
        Assert.Empty(d.Pressed);
    }

    [Fact]
    public void Depois_de_soltar_o_mesmo_atalho_dispara_de_novo()
    {
        var d = New();
        d.KeyDown("Ctrl"); d.KeyDown("Shift");
        Assert.NotNull(d.KeyDown("Z"));

        d.KeyUp("Z");

        d.KeyDown("Ctrl"); d.KeyDown("Shift");
        Assert.NotNull(d.KeyDown("Z"));
    }

    /// <summary>
    /// RF-443 — Os atalhos ficam inertes enquanto a camada de seleção está aberta, um campo
    /// de captura tem foco, ou a janela de opções avançadas está aberta.
    ///
    /// Critério de aceite: "Configurar um atalho enquanto o campo tem foco não dispara
    /// nenhuma ação."
    /// </summary>
    [Fact]
    public void RF_443_suspenso_nenhum_atalho_dispara()
    {
        var d = New();
        d.Suspended = true;

        d.KeyDown("Ctrl");
        d.KeyDown("Shift");
        Assert.Null(d.KeyDown("Z"));

        // Ao retomar, volta a funcionar.
        d.Reset();
        d.Suspended = false;
        d.KeyDown("Ctrl"); d.KeyDown("Shift");
        Assert.NotNull(d.KeyDown("Z"));
    }

    [Fact]
    public void RF_443_suspenso_o_conjunto_de_pressionadas_continua_sendo_rastreado()
    {
        // As teclas continuam sendo contadas: ao retomar, o estado do teclado está correto
        // em vez de precisar de uma soltura para se sincronizar.
        var d = New();
        d.Suspended = true;
        d.KeyDown("Ctrl");
        Assert.Contains("Ctrl", d.Pressed);
    }

    /// <summary>
    /// Critério de aceite do capítulo 22: "Após 50 acionamentos rápidos de iniciar/parar,
    /// os atalhos continuam funcionando."
    /// </summary>
    [Fact]
    public void Cinquenta_acionamentos_rapidos_nao_quebram_o_despachante()
    {
        var d = New();

        for (int i = 0; i < 50; i++)
        {
            d.KeyDown("Ctrl");
            d.KeyDown("Shift");
            Assert.NotNull(d.KeyDown("Z"));
            d.KeyUp("Z");
        }

        d.KeyDown("Ctrl"); d.KeyDown("Shift");
        Assert.NotNull(d.KeyDown("Z"));
    }

    [Fact]
    public void Uma_tecla_desconhecida_nao_lanca()
    {
        var d = New();
        Assert.Null(d.KeyDown(""));
        Assert.Null(d.KeyDown("TeclaQueNaoExiste"));
    }
}

/// <summary>RF-037 / RF-453 — Persistência dos atalhos.</summary>
public class ShortcutStoreTests
{
    private static string TempFile()
        => Path.Combine(Path.GetTempPath(), "gort-atalhos",
                        Guid.NewGuid().ToString("N") + ".toml");

    [Fact]
    public void Os_atalhos_sobrevivem_a_ida_e_volta_pelo_arquivo()
    {
        string file = TempFile();

        var original = ShortcutSet.WithDefaults();
        original.Set(ShortcutAction.QuickArea, new[] { "Alt", "Q" });
        original.Clear(ShortcutAction.OpenDictionaryEditor);
        original.Set(ShortcutAction.OpenProfile, new[] { "Ctrl", "1" }, index: 0, data: "jogo.gort");
        ShortcutStore.Save(file, original);

        var lido = ShortcutStore.Load(file);

        Assert.Equal(new[] { "Alt", "Q" }, lido.Find(ShortcutAction.QuickArea)!.Keys);
        Assert.True(lido.Find(ShortcutAction.OpenDictionaryEditor)!.IsEmpty);

        var perfil = lido.Find(ShortcutAction.OpenProfile)!;
        Assert.Equal("jogo.gort", perfil.Data);
        Assert.Equal(new[] { "Ctrl", "1" }, perfil.Keys);
    }

    /// <summary>RF-446 — Um atalho limpo pelo usuário continua limpo na próxima abertura.</summary>
    [Fact]
    public void Um_atalho_limpo_nao_volta_ao_padrao()
    {
        string file = TempFile();
        var set = ShortcutSet.WithDefaults();
        set.Clear(ShortcutAction.ToggleRealtimeTranslation);
        ShortcutStore.Save(file, set);

        Assert.True(ShortcutStore.Load(file).Find(ShortcutAction.ToggleRealtimeTranslation)!.IsEmpty);
    }

    /// <summary>
    /// RF-025 — Uma ação que o arquivo não menciona ganha o padrão. É o que faz um atalho
    /// novo do programa aparecer configurado para quem já tinha o arquivo.
    /// </summary>
    [Fact]
    public void Uma_acao_ausente_do_arquivo_recebe_o_padrao()
    {
        string file = TempFile();
        var parcial = new ShortcutSet();
        parcial.Set(ShortcutAction.QuickArea, new[] { "Alt", "Q" });
        ShortcutStore.Save(file, parcial);

        var lido = ShortcutStore.Load(file);
        Assert.Equal(new[] { "Ctrl", "Shift", "Z" },
                     lido.Find(ShortcutAction.ToggleRealtimeTranslation)!.Keys);
    }

    [Fact]
    public void RF_024_um_arquivo_ausente_ou_corrompido_devolve_os_padroes()
    {
        Assert.Equal(new[] { "Ctrl", "Shift", "Z" },
            ShortcutStore.Load(TempFile()).Find(ShortcutAction.ToggleRealtimeTranslation)!.Keys);

        string corrompido = TempFile();
        Directory.CreateDirectory(Path.GetDirectoryName(corrompido)!);
        File.WriteAllText(corrompido, "[[[ isto não é toml =====");

        Assert.Equal(new[] { "Ctrl", "Shift", "Z" },
            ShortcutStore.Load(corrompido).Find(ShortcutAction.ToggleRealtimeTranslation)!.Keys);
    }

    /// <summary>RF-028 — Um identificador de ação desconhecido não impede o carregamento.</summary>
    [Fact]
    public void RF_028_uma_acao_desconhecida_e_ignorada_sem_derrubar_o_arquivo()
    {
        string file = TempFile();
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """
            schema_version = 1

            [[shortcut]]
            action = "AcaoQueNaoExisteMais"
            keys = ["Ctrl", "K"]

            [[shortcut]]
            action = "QuickArea"
            keys = ["Alt", "Q"]
            """);

        var lido = ShortcutStore.Load(file);
        Assert.Equal(new[] { "Alt", "Q" }, lido.Find(ShortcutAction.QuickArea)!.Keys);
    }

    [Fact]
    public void RF_026_as_acoes_sao_persistidas_por_identificador_textual()
    {
        string file = TempFile();
        ShortcutStore.Save(file, ShortcutSet.WithDefaults());
        string texto = File.ReadAllText(file);

        Assert.Contains("ToggleRealtimeTranslation", texto);
        Assert.Contains("\"Ctrl\"", texto);
    }
}
