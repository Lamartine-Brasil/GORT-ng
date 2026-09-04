using Gort.Core.Calibration;
using Gort.Core.Configuration;
using Gort.Core.Imaging;
using Gort.Core.Model;
using Gort.Core.Structuring;
using Xunit;

namespace Gort.Core.Tests;

public class ConfigurationTests
{
    private static string TempFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "gort-config-testes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "perfil.gort");
    }

    /// <summary>
    /// Critério de aceite do capítulo 10: "Salvar um perfil, restaurar padrões e recarregar
    /// o perfil devolve exatamente o estado anterior, incluindo áreas, grupos de cor e cores
    /// de fonte."
    /// </summary>
    [Fact]
    public void Salvar_restaurar_padroes_e_recarregar_devolve_o_estado_anterior()
    {
        string f = TempFile();

        var original = Profile.Defaults();
        original.OcrEngine = "classic";
        original.OcrLanguage = "ja";
        original.TranslationService = "commercial_eu";
        original.WindowMode = WindowMode.Layer;
        original.FilterMode = FilterMode.Hsv;
        original.Scale = 3.5;
        original.Speed = 4;
        original.Areas = new List<Rect> { new(10, 20, 300, 40), new(-500, 5, 200, 60) };
        original.Exclusions = new List<Rect> { new(15, 25, 20, 20) };
        original.ColorGroups = new List<ColorGroup>
        {
            new() { S1 = 0, S2 = 10, V1 = 75, V2 = 100 },
            new() { R = 12, G = 34, B = 56 },
        };
        original.TextColor = new Rgba(1, 2, 3);
        original.Stroke1Color = new Rgba(4, 5, 6);
        original.Stroke2Color = new Rgba(7, 8, 9);
        original.BackgroundColor = new Rgba(10, 11, 12, 200);
        original.FontSize = 22;
        original.ServiceSourceLanguage["webfree"] = "ja";
        original.ServiceTargetLanguage["webfree"] = "pt-BR";
        original.Normalize();
        original.Save(f);

        // Restaurar padrões: o objeto em memória volta ao estado de fábrica.
        var padroes = Profile.Defaults();
        Assert.Equal("modern", padroes.OcrEngine);

        // Recarregar o perfil devolve exatamente o estado anterior.
        var recarregado = Profile.Load(f, out _);

        Assert.Equal(original.OcrEngine, recarregado.OcrEngine);
        Assert.Equal(original.OcrLanguage, recarregado.OcrLanguage);
        Assert.Equal(original.TranslationService, recarregado.TranslationService);
        Assert.Equal(original.WindowMode, recarregado.WindowMode);
        Assert.Equal(original.FilterMode, recarregado.FilterMode);
        Assert.Equal(original.Scale, recarregado.Scale);
        Assert.Equal(original.Speed, recarregado.Speed);
        Assert.Equal(original.Areas, recarregado.Areas);
        Assert.Equal(original.Exclusions, recarregado.Exclusions);
        Assert.Equal(original.TextColor, recarregado.TextColor);
        Assert.Equal(original.Stroke1Color, recarregado.Stroke1Color);
        Assert.Equal(original.Stroke2Color, recarregado.Stroke2Color);
        Assert.Equal(original.BackgroundColor, recarregado.BackgroundColor);
        Assert.Equal(original.FontSize, recarregado.FontSize);
        Assert.Equal("ja", recarregado.ServiceSourceLanguage["webfree"]);

        Assert.Equal(original.ColorGroups.Count, recarregado.ColorGroups.Count);
        for (int i = 0; i < original.ColorGroups.Count; i++)
        {
            Assert.Equal(original.ColorGroups[i].ToString(), recarregado.ColorGroups[i].ToString());
        }
    }

    /// <summary>
    /// Critério de aceite: "Um perfil ao qual foram removidas linhas aleatórias ainda abre,
    /// com os campos removidos nos padrões." (RF-024, RF-025)
    /// </summary>
    [Fact]
    public void RF_024_um_perfil_com_linhas_removidas_ainda_abre_com_os_padroes()
    {
        string f = TempFile();
        var p = Profile.Defaults();
        p.OcrEngine = "classic";
        p.Speed = 5;
        p.Save(f);

        // Remove metade das linhas, ao acaso mas de forma determinística.
        var linhas = File.ReadAllLines(f);
        var restantes = linhas.Where((_, i) => i % 2 == 0).ToArray();
        File.WriteAllLines(f, restantes);

        var lido = Profile.Load(f, out _);

        // Abriu; os campos que sobreviveram valem, e os removidos estão nos padrões.
        Assert.InRange(lido.Speed, 1, 5);
        Assert.NotNull(lido.OcrEngine);
        Assert.Equal(P.DefaultThreshold, lido.Threshold);
    }

    [Fact]
    public void RF_024_um_arquivo_totalmente_corrompido_restaura_todos_os_padroes()
    {
        string f = TempFile();
        File.WriteAllText(f, "isto ((( nao eh )))) toml valido = = =\n[[[");

        var lido = Profile.Load(f, out _);
        Assert.Equal("modern", lido.OcrEngine);
        Assert.Equal(WindowMode.Overlay, lido.WindowMode);
    }

    [Fact]
    public void RF_024_arquivo_ausente_usa_os_padroes()
    {
        var lido = Profile.Load(Path.Combine(Path.GetTempPath(), "gort-nao-existe.gort"), out _);
        Assert.Equal("modern", lido.OcrEngine);
    }

    /// <summary>
    /// Critério de aceite: "Um perfil gravado por uma versão mais nova do programa, contendo
    /// uma chave que a versão atual não conhece, abre sem erro e, ao ser regravado, ainda
    /// contém aquela chave." (RF-038)
    /// </summary>
    [Fact]
    public void RF_038_chaves_de_uma_versao_mais_nova_sobrevivem_a_regravacao()
    {
        string f = TempFile();
        File.WriteAllText(f, """
            schema_version = 99
            ocr_engine = "classic"
            recurso_do_futuro = "algo que esta versao nao conhece"
            outro_futuro = 42
            """);

        var p = Profile.Load(f, out var store);
        Assert.Equal("classic", p.OcrEngine);   // abriu sem erro

        p.Save(f, store);                        // regravado com o mesmo armazenamento

        string texto = File.ReadAllText(f);
        Assert.Contains("recurso_do_futuro", texto);
        Assert.Contains("algo que esta versao nao conhece", texto);
        Assert.Contains("outro_futuro", texto);
    }

    /// <summary>
    /// RF-028 — Um identificador desconhecido (por exemplo, um serviço que deixou de
    /// existir) não pode impedir o carregamento.
    /// </summary>
    [Fact]
    public void RF_028_identificador_desconhecido_nao_impede_o_carregamento()
    {
        string f = TempFile();
        File.WriteAllText(f, """
            schema_version = 1
            window_mode = "modo_que_nao_existe"
            filter_mode = "inventado"
            copy_format = "sei_la"
            """);

        var p = Profile.Load(f, out _);
        Assert.Equal(WindowMode.Overlay, p.WindowMode);      // volta ao padrão
        Assert.Equal(FilterMode.None, p.FilterMode);
        Assert.Equal(ClipboardCopyFormat.Ocr, p.CopyFormat);
    }

    /// <summary>
    /// RF-026 / RF-027 — Valores de conjunto fechado são persistidos pelo identificador
    /// TEXTUAL, nunca pela posição numérica. Isso é o que permite reordenar o conjunto sem
    /// invalidar arquivos existentes.
    /// </summary>
    [Fact]
    public void RF_026_conjuntos_fechados_sao_persistidos_por_identificador_textual()
    {
        string f = TempFile();
        var p = Profile.Defaults();
        p.WindowMode = WindowMode.Layer;
        p.FilterMode = FilterMode.Threshold;
        p.Save(f);

        string texto = File.ReadAllText(f);
        Assert.Contains("window_mode = \"layer\"", texto);
        Assert.Contains("filter_mode = \"threshold\"", texto);
        // Nada de índices numéricos para esses campos.
        Assert.DoesNotContain("window_mode = 1", texto);
    }

    [Fact]
    public void RF_023_o_arquivo_e_texto_legivel_com_versao_de_esquema_na_raiz()
    {
        string f = TempFile();
        Profile.Defaults().Save(f);
        string texto = File.ReadAllText(f);

        Assert.Contains("schema_version", texto);
        // Comentários e cadeias de múltiplas linhas são suportados pelo formato escolhido.
        var comComentario = TomlStore.FromText("# um comentário\nchave = \"\"\"linha um\nlinha dois\"\"\"\n");
        Assert.Equal("linha um\nlinha dois", comComentario.GetString("chave", ""));
    }

    /// <summary>
    /// Critério de aceite: "Trocar de perfil não altera nenhuma opção avançada." (RF-032)
    /// </summary>
    [Fact]
    public void RF_032_trocar_de_perfil_nao_altera_as_opcoes_avancadas()
    {
        string dir = Path.Combine(Path.GetTempPath(), "gort-avancado", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string avancado = Path.Combine(dir, "avancado.toml");

        var opcoes = AdvancedOptions.Defaults();
        opcoes.TrayMode = true;
        opcoes.AutoFontSize = true;
        opcoes.DisplayMemoryCount = 7;
        opcoes.Save(avancado);

        // Dois perfis diferentes, salvos e carregados.
        string p1 = Path.Combine(dir, "a.gort");
        string p2 = Path.Combine(dir, "b.gort");
        var a = Profile.Defaults(); a.Speed = 1; a.Save(p1);
        var b = Profile.Defaults(); b.Speed = 5; b.Save(p2);
        Profile.Load(p1, out _);
        Profile.Load(p2, out _);

        var relidas = AdvancedOptions.Load(avancado, out _);
        Assert.True(relidas.TrayMode);
        Assert.True(relidas.AutoFontSize);
        Assert.Equal(7, relidas.DisplayMemoryCount);
    }

    [Fact]
    public void RF_033_opcoes_avancadas_ausentes_assumem_os_padroes()
    {
        var o = AdvancedOptions.Load(
            Path.Combine(Path.GetTempPath(), "gort-avancado-inexistente.toml"), out _);
        Assert.True(o.MouseFollowOnly);        // IV.12 — ligado por padrão
        Assert.True(o.AutoColor);
        Assert.False(o.TrayMode);
    }

    /// <summary>
    /// Critério de aceite: "Acrescentar um idioma, um motor de OCR ou um serviço de tradução
    /// novo aos dados de configuração não invalida nenhum perfil existente." (RF-566)
    /// </summary>
    [Fact]
    public void RF_566_acrescentar_um_item_ao_catalogo_nao_invalida_perfis()
    {
        string f = TempFile();
        var p = Profile.Defaults();
        p.TranslationService = "webfree";
        p.Save(f);

        // Um perfil referindo-se a um serviço que ainda não existe no catálogo abre
        // normalmente; a resolução contra o catálogo é do chamador (RF-307).
        File.WriteAllText(f, File.ReadAllText(f)
            .Replace("translation_service = \"webfree\"", "translation_service = \"servico_novo\""));

        var lido = Profile.Load(f, out _);
        Assert.Equal("servico_novo", lido.OcrEngine == "modern" ? lido.TranslationService : "");
    }

    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RF_042_valores_fora_de_faixa_sao_saturados()
    {
        var p = Profile.Defaults();
        p.Speed = 99;
        p.Threshold = 999;
        p.Scale = 50;
        p.ColorGroups = new List<ColorGroup> { new() { R = 999, S1 = 80, S2 = 5 } };
        p.Normalize();

        Assert.Equal(5, p.Speed);
        Assert.Equal(255, p.Threshold);
        Assert.Equal(P.DefaultScale, p.Scale);
        Assert.Equal(255, p.ColorGroups[0].R);
        Assert.Equal((5, 80), (p.ColorGroups[0].S1, p.ColorGroups[0].S2));
    }

    [Fact]
    public void IV_12_os_padroes_de_fabrica_sao_os_da_tabela()
    {
        var p = Profile.Defaults();
        Assert.Equal(WindowMode.Overlay, p.WindowMode);
        Assert.Equal("webfree", p.TranslationService);
        Assert.Equal("modern", p.OcrEngine);
        Assert.Equal("en", p.OcrLanguage);
        Assert.Equal("pt-BR", p.TargetLanguage);
        Assert.Equal("eng", p.ClassicDataset);
        Assert.False(p.ClassicFastMode);
        Assert.True(p.ShowRecognizedText);
        Assert.False(p.WriteResultToFile);
        Assert.False(p.CopyToClipboard);
        Assert.Equal(ClipboardCopyFormat.Ocr, p.CopyFormat);
        Assert.Equal(2, p.Speed);
        Assert.Equal(1000, p.CycleIntervalMs);          // P-06
        Assert.Equal("empty.txt", p.DatabaseFile);
        Assert.Equal("myDic.txt", p.DictionaryFile);
        Assert.True(p.UseDictionary);
        Assert.True(p.DictionaryWholeWord);
        Assert.False(p.Erosion);
        Assert.Single(p.ColorGroups);
        Assert.Equal(FilterMode.None, p.FilterMode);
        Assert.Equal(127, p.Threshold);
        Assert.Empty(p.Areas);
        Assert.Empty(p.Exclusions);
        Assert.Equal(TextOrder.Left, p.TextOrder);
        Assert.False(p.RemoveSpaces);
        Assert.False(p.CaptureActiveWindow);
        Assert.True(p.TextBackground);
        Assert.False(p.NumberAreas);
        Assert.Equal(2.0, p.Scale);
        Assert.False(p.SpeakResult);
        Assert.Equal(-1, p.LayerX);
        Assert.Equal(15, p.FontSize);                    // P-127
    }

    [Fact]
    public void IV_12_os_padroes_das_opcoes_avancadas_sao_os_da_tabela()
    {
        var o = AdvancedOptions.Defaults();
        Assert.False(o.MergeLines);
        Assert.False(o.PreserveOrientation);
        Assert.True(o.AutoColor);
        Assert.True(o.AutoBackgroundColor);
        Assert.True(o.AutoFontColor);
        Assert.False(o.FontStroke);
        Assert.False(o.UseBackgroundTransparency);
        Assert.False(o.AutoFontSize);
        Assert.False(o.LayerAlignBottom);
        Assert.False(o.LayerAlignRight);
        Assert.False(o.AlwaysOnTopOnlyWhileTranslating);
        Assert.False(o.IgnoreEmptyTranslation);
        Assert.False(o.HideAlsoTranslates);
        Assert.False(o.DisplayMemoryEnabled);
        Assert.False(o.RemoteAlwaysOnTop);
        Assert.False(o.TrayMode);
        Assert.False(o.ShowCaptureBorder);
        Assert.False(o.RightToLeft);
        Assert.False(o.MouseFollowCompatible);
        Assert.True(o.MouseFollowOnly);
        Assert.Equal(Rgba.Black, o.SelectionHighlight);
        Assert.Equal(Rgba.White, o.SelectionBackground);
        Assert.Equal(Gort.Core.Caching.CollectionLookupMode.Database, o.CollectionMode);
        Assert.True(o.CollectionIgnoreCase);
        Assert.False(o.BridgeTranslation);
        Assert.True(o.FallbackTranslator);
        Assert.True(o.CustomApiSameLanguageCodesAsWeb);
        Assert.Equal("en", o.CustomApiSource);
        Assert.Equal("pt-BR", o.CustomApiTarget);
        Assert.Equal("http://localhost:8080/translator", o.CustomApiUrl);
        Assert.Equal("", o.LlmCustomInstruction);
        Assert.Equal("gemini-2.0-flash", o.LlmCustomModel);
        Assert.False(o.LlmDisableDefaultInstruction);
        Assert.Equal(0, o.DictionaryExtraPasses);
        Assert.False(o.ClipboardTranslation);
        Assert.False(o.PreferCloudOcrOneShot);
        Assert.Equal(950, o.CloudOcrMonthlyLimit);       // P-29
    }

    [Fact]
    public void IV_12_os_padroes_do_aplicativo_sao_os_da_tabela()
    {
        var a = AppOptions.Defaults();
        Assert.Equal("pt-BR", a.InterfaceLanguage);      // RF-487
        Assert.True(a.CheckForUpdates);
        Assert.True(a.TranslationWindowAlwaysOnTop);
    }

    /// <summary>
    /// RF-148 / RF-044 — Os ajustes automáticos vêm das PROPRIEDADES do idioma (RF-311),
    /// nunca de uma comparação com o identificador (RF-567).
    /// </summary>
    [Fact]
    public void RF_148_as_propriedades_do_idioma_governam_espacos_e_dicionario()
    {
        var catalogo = Gort.Core.Catalog.AppCatalog.Load(TestPaths.DataDirectory);
        var p = Profile.Defaults();

        p.ApplyLanguageProperties(catalogo.Language("ja")!);
        Assert.True(p.RemoveSpaces);            // não separa por espaço
        Assert.False(p.DictionaryWholeWord);

        p.ApplyLanguageProperties(catalogo.Language("en")!);
        Assert.False(p.RemoveSpaces);           // separa por espaço
        Assert.True(p.DictionaryWholeWord);
    }

    /// <summary>
    /// RF-041 — Posição e tamanho do modo camada validados contra os monitores presentes.
    /// </summary>
    [Fact]
    public void RF_041_posicao_da_camada_e_validada_contra_os_monitores()
    {
        var monitores = new List<Rect> { new(0, 0, 1920, 1080) };
        var p = Profile.Defaults();

        // Sem posição salva: usa o padrão de P-133.
        var padrao = p.ResolveLayerPlacement(monitores, 1080);
        Assert.Equal(P.LayerDefaultX, padrao.X);
        Assert.Equal(1080 - P.LayerDefaultYOffsetFromScreenBottom, padrao.Y);
        Assert.Equal(P.LayerDefaultWidth, padrao.Width);
        Assert.Equal(P.LayerDefaultHeight, padrao.Height);

        // Retângulo que não intersecta nenhum monitor: cai para o padrão.
        p.LayerX = 9000; p.LayerY = 9000; p.LayerWidth = 400; p.LayerHeight = 200;
        Assert.Equal(padrao, p.ResolveLayerPlacement(monitores, 1080));

        // Intersecta parcialmente: é deslocado para dentro dos limites daquele monitor.
        p.LayerX = 1800; p.LayerY = 1000; p.LayerWidth = 400; p.LayerHeight = 200;
        var ajustado = p.ResolveLayerPlacement(monitores, 1080);
        Assert.True(ajustado.Right <= 1920 && ajustado.Bottom <= 1080);
        Assert.True(ajustado.Left >= 0 && ajustado.Top >= 0);

        // Cabe inteiro: preservado.
        p.LayerX = 100; p.LayerY = 100; p.LayerWidth = 400; p.LayerHeight = 200;
        Assert.Equal(new Rect(100, 100, 400, 200), p.ResolveLayerPlacement(monitores, 1080));
    }

    [Fact]
    public void RF_524_o_tamanho_minimo_nunca_fica_acima_do_maximo()
    {
        var o = AdvancedOptions.Defaults();
        o.AutoFontSizeMin = 60;
        o.AutoFontSizeMax = 20;
        o.Normalize();
        Assert.True(o.AutoFontSizeMin <= o.AutoFontSizeMax);
    }

    [Fact]
    public void RF_281_os_presets_do_modelo_de_linguagem_carregam_os_valores_calibrados()
    {
        var o = AdvancedOptions.Defaults();

        o.ApplyPreset(LlmPreset.Standard);
        Assert.Equal((P.LlmTemperatureDefault, P.LlmThinkingDefault, P.LlmMaxOutputDefault),
                     (o.LlmTemperature, o.LlmThinking, o.LlmMaxOutput));

        o.ApplyPreset(LlmPreset.Economy);
        Assert.Equal((P.LlmTemperatureEconomy, P.LlmThinkingEconomy, P.LlmMaxOutputEconomy),
                     (o.LlmTemperature, o.LlmThinking, o.LlmMaxOutput));

        // RF-525 — "personalizado" habilita os controles mantendo os valores atuais.
        o.ApplyPreset(LlmPreset.Custom);
        Assert.Equal(P.LlmTemperatureEconomy, o.LlmTemperature);
    }

    [Fact]
    public void RF_578_o_formato_dos_arquivos_e_o_mesmo_em_qualquer_sistema()
    {
        // Os caminhos mudam por sistema, mas o conteúdo gravado é idêntico.
        string dirA = Path.Combine(Path.GetTempPath(), "gort-sisA", Guid.NewGuid().ToString("N"));
        string dirB = Path.Combine(Path.GetTempPath(), "gort-sisB", Guid.NewGuid().ToString("N"));
        var a = new UserPaths(dirA);
        var b = new UserPaths(dirB);

        var p = Profile.Defaults();
        p.Speed = 3;
        p.Save(a.MainProfile);
        p.Save(b.MainProfile);

        Assert.Equal(File.ReadAllText(a.MainProfile), File.ReadAllText(b.MainProfile));
    }
}
