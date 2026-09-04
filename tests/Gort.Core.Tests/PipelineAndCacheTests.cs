using Gort.Core.Caching;
using Gort.Core.Calibration;
using Gort.Core.Pipeline;
using Gort.Core.Structuring;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>Cap. 16 — Detecção de mudança entre quadros.</summary>
public class ChangeDetectorTests
{
    private DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private ChangeDetector New() => new(() => _now);

    [Fact]
    public void RF_194_texto_diferente_executa_o_caminho_completo()
    {
        var d = New();
        Assert.Equal(ChangeDecision.FullRedraw, d.Evaluate("olá"));
        Assert.Equal(ChangeDecision.FullRedraw, d.Evaluate("adeus"));
    }

    [Fact]
    public void RF_195_texto_igual_nao_redesenha_o_conteudo()
    {
        var d = New();
        d.Evaluate("olá");
        _now = _now.AddMilliseconds(10);
        d.Evaluate("olá");                       // consome o repintar ocioso inicial
        _now = _now.AddMilliseconds(10);
        Assert.Equal(ChangeDecision.Nothing, d.Evaluate("olá"));
    }

    /// <summary>
    /// RF-194 — Vazio é tratado como MUDANÇA: quando o diálogo some, a tradução some junto.
    /// Critério de aceite do capítulo 16.
    /// </summary>
    [Fact]
    public void RF_194_vazio_e_sempre_tratado_como_mudanca()
    {
        var d = New();
        d.Evaluate("");
        _now = _now.AddSeconds(5);
        Assert.Equal(ChangeDecision.FullRedraw, d.Evaluate(""));
    }

    /// <summary>
    /// RF-196 — Mesmo com texto igual, passado P-47 força um repintar: a geometria pode ter
    /// mudado. Critério de aceite: mover uma área com texto estático reposiciona a
    /// sobreposição em no máximo P-47.
    /// </summary>
    [Fact]
    public void RF_196_repintar_ocioso_acontece_no_intervalo_P47()
    {
        var d = New();
        d.Evaluate("estático");
        Assert.Equal(ChangeDecision.IdleRepaint, d.Evaluate("estático"));

        _now = _now.Add(P.IdleRepaintInterval - TimeSpan.FromMilliseconds(1));
        Assert.Equal(ChangeDecision.Nothing, d.Evaluate("estático"));

        _now = _now.AddMilliseconds(1);
        Assert.Equal(ChangeDecision.IdleRepaint, d.Evaluate("estático"));
    }

    [Fact]
    public void RF_198_a_memoria_do_texto_anterior_so_muda_no_caminho_completo()
    {
        var d = New();
        d.Evaluate("primeiro");
        Assert.Equal("primeiro", d.Previous);
        d.Evaluate("primeiro");
        Assert.Equal("primeiro", d.Previous);
    }

    [Fact]
    public void RF_199_ao_reiniciar_o_laco_o_primeiro_ciclo_sempre_desenha()
    {
        var d = New();
        d.Evaluate("mesmo texto");
        d.Reset();
        Assert.Equal(ChangeDecision.FullRedraw, d.Evaluate("mesmo texto"));
    }

    /// <summary>
    /// RF-200 e Parte XI item 13 — o resultado é aceito no PRIMEIRO quadro em que aparece.
    /// Nenhuma confirmação em segundo quadro, nenhuma estabilização.
    /// </summary>
    [Fact]
    public void RF_200_a_mudanca_e_aceita_no_primeiro_quadro_sem_confirmacao()
    {
        var d = New();
        d.Evaluate("a");
        // Texto oscilando por ruído de OCR: cada quadro é caminho completo. É o
        // comportamento EXIGIDO, não uma tolerância.
        Assert.Equal(ChangeDecision.FullRedraw, d.Evaluate("b"));
        Assert.Equal(ChangeDecision.FullRedraw, d.Evaluate("a"));
        Assert.Equal(ChangeDecision.FullRedraw, d.Evaluate("b"));
    }

    /// <summary>
    /// Critério de aceite do capítulo 16: "Com uma tela estática e o laço em 300 ms, o
    /// número de chamadas ao serviço de tradução após o primeiro ciclo é zero."
    /// </summary>
    [Fact]
    public void RF_192_tela_estatica_nao_gera_traducao_depois_do_primeiro_ciclo()
    {
        var d = New();
        int caminhosCompletos = 0;
        for (int ciclo = 0; ciclo < 100; ciclo++)
        {
            if (d.Evaluate("diálogo parado na tela") == ChangeDecision.FullRedraw)
                caminhosCompletos++;
            _now = _now.AddMilliseconds(P.CycleIntervalSpeed1Ms);
        }
        Assert.Equal(1, caminhosCompletos);
    }

    [Fact]
    public void RF_205_motor_nao_pronto_reutiliza_o_texto_anterior_e_nao_gera_trabalho()
    {
        var d = New();
        d.Evaluate("texto do ciclo anterior");
        _now = _now.Add(P.IdleRepaintInterval);
        d.Evaluate(d.TextWhenEngineNotReady());   // consome o repintar ocioso
        _now = _now.AddMilliseconds(1);
        Assert.Equal(ChangeDecision.Nothing, d.Evaluate(d.TextWhenEngineNotReady()));
    }
}

/// <summary>RF-203 / RF-204 — Segunda camada de descarte da sobreposição.</summary>
public class OverlayReuseCacheTests
{
    private static Gort.Core.Model.RegionResult Result(int index) => new()
    {
        Index = index,
        Lines = Array.Empty<Gort.Core.Model.Line>(),
        Blocks = Array.Empty<Gort.Core.Model.TranslationBlock>(),
    };

    [Fact]
    public void RF_203_reutiliza_o_resultado_quando_nada_mudou()
    {
        var cache = new OverlayReuseCache();
        var rect = new Gort.Core.Model.Rect(0, 0, 100, 50);
        var r = Result(0);
        cache.Store(0, rect, (0, 0), "ocr", "trad", r);

        Assert.Same(r, cache.TryReuse(0, rect, (0, 0), "ocr", "trad"));
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void RF_203_qualquer_diferenca_invalida_o_reuso(
        bool mudaRect, bool mudaCliente, bool mudaOcr, bool mudaTrad)
    {
        var cache = new OverlayReuseCache();
        var rect = new Gort.Core.Model.Rect(0, 0, 100, 50);
        cache.Store(0, rect, (0, 0), "ocr", "trad", Result(0));

        var novoRect = mudaRect ? new Gort.Core.Model.Rect(1, 0, 100, 50) : rect;
        Assert.Null(cache.TryReuse(0, novoRect,
            mudaCliente ? (5, 5) : (0, 0),
            mudaOcr ? "outro" : "ocr",
            mudaTrad ? "outra" : "trad"));
    }

    [Fact]
    public void RF_204_registros_de_areas_ausentes_no_ciclo_sao_removidos()
    {
        var cache = new OverlayReuseCache();
        var rect = new Gort.Core.Model.Rect(0, 0, 10, 10);
        cache.Store(0, rect, (0, 0), "a", "b", Result(0));
        cache.Store(1, rect, (0, 0), "a", "b", Result(1));

        cache.RetainOnly(new[] { 0 });

        Assert.NotNull(cache.TryReuse(0, rect, (0, 0), "a", "b"));
        Assert.Null(cache.TryReuse(1, rect, (0, 0), "a", "b"));
    }
}

/// <summary>Cap. 17 — Memória de resultados, coletânea e memória de exibição.</summary>
public class CacheTests
{
    private static string TempFile()
        => Path.Combine(Path.GetTempPath(), "gort-testes", Guid.NewGuid().ToString("N") + ".txt");

    [Fact]
    public void RF_206_a_memoria_e_separada_por_servico()
    {
        var a = new ResultMemory("webfree", TempFile());
        var b = new ResultMemory("commercial_eu", TempFile());
        a.Store("hello", "olá");
        Assert.Equal("olá", a.Lookup("hello"));
        Assert.Null(b.Lookup("hello"));   // trocar de serviço faz traduzir de novo
    }

    [Fact]
    public async Task RF_208_e_RF_209_a_memoria_sobrevive_ao_reinicio()
    {
        string file = TempFile();
        var a = new ResultMemory("webfree", file);
        a.Store("linha um\nlinha dois", "primeira\nsegunda");
        await a.FlushAsync();

        var b = new ResultMemory("webfree", file);
        b.Load();
        Assert.Equal("primeira\nsegunda", b.Lookup("linha um\nlinha dois"));
    }

    [Fact]
    public void RF_209_o_texto_de_origem_tem_espacos_a_direita_removidos()
    {
        var pares = PairFile.Parse(new[] { "/s", "origem   ", "/t", "destino", "/e", "" });
        Assert.Single(pares);
        Assert.Equal("origem", pares[0].Source);
    }

    [Fact]
    public void RF_210_ao_atingir_P48_a_memoria_inteira_e_descartada()
    {
        var m = new ResultMemory("webfree", TempFile());
        for (int i = 0; i < P.ResultMemoryMaxEntries; i++) m.Store($"s{i}", $"t{i}");
        Assert.Equal(P.ResultMemoryMaxEntries, m.Count);

        m.Store("a mais", "estoura");
        Assert.Equal(0, m.Count);   // sem LRU: descarta tudo (Parte XI, item 17)
    }

    [Fact]
    public void Caso_de_erro_do_cap_17_arquivo_corrompido_tem_linhas_invalidas_ignoradas()
    {
        var pares = PairFile.Parse(new[]
        {
            "lixo antes", "/s", "bom", "/t", "ok", "/e", "",
            "/s", "truncado",          // registro sem /t
        });
        Assert.Single(pares);
        Assert.Equal("bom", pares[0].Source);
    }

    [Fact]
    public void RF_218_coletanea_em_correspondencia_exata()
    {
        string f = TempFile();
        PairFile.Write(f, new[] { new TranslationPair("Fire Sword", "Espada de Fogo") });

        var c = new TranslationCollection { Mode = CollectionLookupMode.Exact };
        c.Load(new[] { f });

        Assert.Equal("Espada de Fogo", c.Lookup("Fire Sword"));
        Assert.Null(c.Lookup("Uma Fire Sword aqui"));   // exato não aceita parcial
    }

    [Fact]
    public void RF_218_coletanea_em_modo_banco_de_dados_aceita_correspondencia_parcial()
    {
        string f = TempFile();
        PairFile.Write(f, new[] { new TranslationPair("Fire Sword", "Espada de Fogo") });

        var c = new TranslationCollection { Mode = CollectionLookupMode.Database };
        c.Load(new[] { f });

        Assert.Equal("Espada de Fogo", c.Lookup("Uma Fire Sword aqui"));
    }

    [Fact]
    public void RF_219_sem_o_modo_de_banco_de_dados_disponivel_cai_para_exato()
    {
        string f = TempFile();
        PairFile.Write(f, new[] { new TranslationPair("Fire Sword", "Espada de Fogo") });

        var c = new TranslationCollection
        {
            Mode = CollectionLookupMode.Database,
            DatabaseModeAvailable = false,
        };
        c.Load(new[] { f });

        Assert.Null(c.Lookup("Uma Fire Sword aqui"));
        Assert.Equal("Espada de Fogo", c.Lookup("Fire Sword"));
    }

    [Fact]
    public void RF_216_arquivos_ausentes_saem_da_lista_ao_carregar()
    {
        string existe = TempFile();
        PairFile.Write(existe, new[] { new TranslationPair("a", "b") });
        string sumiu = TempFile();

        var c = new TranslationCollection();
        var mantidos = c.Load(new[] { existe, sumiu });

        Assert.Equal(new[] { existe }, mantidos);
    }

    [Fact]
    public void RF_241_o_marcador_de_sem_resultado_vira_vazio()
    {
        string f = TempFile();
        PairFile.Write(f, new[] { new TranslationPair("x", TextPostProcessor.NoResultMarker) });

        var db = new LocalDatabase();
        db.Load(f);
        Assert.Equal("", db.Lookup("x"));
    }

    [Fact]
    public void RF_242_correspondencia_parcial_prefere_a_origem_mais_especifica()
    {
        string f = TempFile();
        PairFile.Write(f, new[]
        {
            new TranslationPair("Sword", "Espada"),
            new TranslationPair("Fire Sword", "Espada de Fogo"),
        });

        var db = new LocalDatabase { PartialMultiline = true };
        db.Load(f);
        Assert.Equal("Espada de Fogo", db.Lookup("A Fire Sword brilhava"));
    }

    [Fact]
    public void RF_242_ignorar_maiusculas_e_minusculas()
    {
        string f = TempFile();
        PairFile.Write(f, new[] { new TranslationPair("Fire Sword", "Espada de Fogo") });

        var db = new LocalDatabase { IgnoreCase = true };
        db.Load(f);
        Assert.Equal("Espada de Fogo", db.Lookup("fire sword"));
    }
}

/// <summary>RF-222 a RF-224 — Memória de exibição.</summary>
public class DisplayMemoryTests
{
    private DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Critério de aceite do capítulo 17: "Com a memória de exibição em 3 entradas e 10
    /// segundos, três diálogos rápidos ficam visíveis simultaneamente e somem um a um."
    /// </summary>
    [Fact]
    public void Tres_dialogos_rapidos_ficam_visiveis_e_somem_um_a_um()
    {
        var m = new DisplayMemory(() => _now)
        {
            Enabled = true, Capacity = 3, LifetimeSeconds = 10,
        };

        m.Apply("um");
        _now = _now.AddSeconds(1);
        m.Apply("dois");
        _now = _now.AddSeconds(1);
        string visivel = m.Apply("três");

        // RF-222 — empilhadas da mais recente para a mais antiga.
        Assert.Equal("três\n\n\ndois\n\n\num", visivel);

        // Passados 10 s do primeiro, ele expira; os outros continuam.
        _now = _now.AddSeconds(8);
        Assert.Equal("três\n\n\ndois", m.Apply(""));

        _now = _now.AddSeconds(1);
        Assert.Equal("três", m.Apply(""));

        _now = _now.AddSeconds(1);
        Assert.Equal("", m.Apply(""));
    }

    [Fact]
    public void RF_222_a_capacidade_descarta_a_entrada_mais_antiga()
    {
        var m = new DisplayMemory(() => _now) { Enabled = true, Capacity = 2, LifetimeSeconds = 100 };
        m.Apply("um");
        m.Apply("dois");
        Assert.Equal("três\n\n\ndois", m.Apply("três"));
    }

    /// <summary>
    /// RF-224 — Com texto atual vazio, o exibido é composto só pelas entradas ainda vivas:
    /// mantém o diálogo anterior legível enquanto não há texto novo na tela.
    /// </summary>
    [Fact]
    public void RF_224_texto_vazio_mantem_as_entradas_vivas()
    {
        var m = new DisplayMemory(() => _now) { Enabled = true, Capacity = 5, LifetimeSeconds = 10 };
        m.Apply("fala anterior");
        Assert.Equal("fala anterior", m.Apply(""));
    }

    [Fact]
    public void Desligada_a_memoria_de_exibicao_e_transparente()
    {
        var m = new DisplayMemory(() => _now) { Enabled = false };
        Assert.Equal("atual", m.Apply("atual"));
        Assert.Equal("", m.Apply(""));
    }
}
