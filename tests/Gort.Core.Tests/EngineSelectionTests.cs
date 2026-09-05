using Gort.Core.Calibration;
using Gort.Core.Catalog;
using Gort.Core.Ocr;
using Gort.Core.Structuring;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>RF-124 a RF-127 — Cota do motor de OCR de nuvem.</summary>
public class CloudOcrQuotaTests
{
    private DateTime _agora = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Local);

    private CloudOcrQuota New() => new(() => _agora);

    private static string TempFile()
        => Path.Combine(Path.GetTempPath(), "gort-cota", Guid.NewGuid().ToString("N") + ".toml");

    [Fact]
    public void P_29_o_limite_padrao_e_o_calibrado()
        => Assert.Equal(P.CloudOcrMonthlyLimit, New().Limit);

    [Fact]
    public void RF_124_a_contagem_e_por_credencial()
    {
        var q = New();
        q.TryConsume("chave-a");
        q.TryConsume("chave-a");
        q.TryConsume("chave-b");

        Assert.Equal(2, q.UsedBy("chave-a"));
        Assert.Equal(1, q.UsedBy("chave-b"));
    }

    /// <summary>
    /// RF-124 — A contagem zera quando o MÊS ou o ANO mudam. A virada é aplicada na leitura:
    /// não há passo de manutenção que possa ser esquecido.
    /// </summary>
    [Fact]
    public void RF_124_a_contagem_zera_na_virada_do_mes()
    {
        var q = New();
        q.TryConsume("chave");
        Assert.Equal(1, q.UsedBy("chave"));

        _agora = new DateTime(2026, 4, 1, 0, 0, 1, DateTimeKind.Local);
        Assert.Equal(0, q.UsedBy("chave"));
    }

    [Fact]
    public void RF_124_a_contagem_zera_na_virada_do_ano()
    {
        var q = New();
        q.TryConsume("chave");

        _agora = new DateTime(2027, 3, 10, 12, 0, 0, DateTimeKind.Local);
        Assert.Equal(0, q.UsedBy("chave"));
    }

    /// <summary>
    /// RF-125 — Atingido o limite, o motor RECUSA novas chamadas. O programa impõe o próprio
    /// limite abaixo da cota real porque ultrapassá-la gera cobrança.
    /// </summary>
    [Fact]
    public void RF_125_atingido_o_limite_novas_chamadas_sao_recusadas()
    {
        var q = New();
        q.Limit = 3;

        Assert.True(q.TryConsume("chave"));
        Assert.True(q.TryConsume("chave"));
        Assert.True(q.TryConsume("chave"));

        Assert.False(q.TryConsume("chave"));
        Assert.True(q.IsExhausted("chave"));
        Assert.Equal(3, q.UsedBy("chave"));
    }

    /// <summary>RF-127 — Exibição "usadas / limite".</summary>
    [Fact]
    public void RF_127_a_exibicao_e_usadas_barra_limite()
    {
        var q = New();
        q.Limit = 950;
        q.TryConsume("chave");
        Assert.Equal("1 / 950", q.Format("chave"));
    }

    /// <summary>RF-127 — A contagem é persistida por credencial, com a data de renovação.</summary>
    [Fact]
    public void RF_127_a_contagem_sobrevive_ao_reinicio()
    {
        string file = TempFile();

        var q = New();
        q.Limit = 500;
        q.TryConsume("chave-a");
        q.TryConsume("chave-a");
        q.Save(file);

        var lido = CloudOcrQuota.Load(file, () => _agora);
        Assert.Equal(500, lido.Limit);
        Assert.Equal(2, lido.UsedBy("chave-a"));
    }

    [Fact]
    public void A_contagem_gravada_num_mes_anterior_volta_a_zero_ao_carregar()
    {
        string file = TempFile();
        var q = New();
        q.TryConsume("chave");
        q.Save(file);

        _agora = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Local);
        Assert.Equal(0, CloudOcrQuota.Load(file, () => _agora).UsedBy("chave"));
    }

    [Fact]
    public void RF_024_um_arquivo_ausente_ou_corrompido_devolve_contagem_vazia()
    {
        Assert.Equal(0, CloudOcrQuota.Load(TempFile()).UsedBy("chave"));

        string corrompido = TempFile();
        Directory.CreateDirectory(Path.GetDirectoryName(corrompido)!);
        File.WriteAllText(corrompido, "[[[ nao eh toml ===");
        Assert.Equal(0, CloudOcrQuota.Load(corrompido).UsedBy("chave"));
    }
}

/// <summary>RF-122, RF-123, RF-147, RF-149, RF-150 — Escolha e compatibilidade de motores.</summary>
public class EngineSelectionTests
{
    private static AppCatalog Catalog() => AppCatalog.Load(TestPaths.DataDirectory);

    private static FakeEngine Engine(string key, bool available = true, bool wordPositions = true)
        => new() { Key = key, IsAvailable = available, ProvidesWordPositions = wordPositions };

    /// <summary>
    /// RF-122 — O motor de nuvem NÃO pode ser usado em tradução em tempo real. Se o usuário
    /// tentar, o programa informa e não inicia.
    /// </summary>
    [Fact]
    public void RF_122_o_motor_de_nuvem_e_recusado_em_tempo_real()
    {
        var catalogo = Catalog();
        var recusa = EngineSelection.CanStart(
            Engine("cloud"), catalogo.OcrEngine("cloud"),
            realtime: true, WindowMode.Dark);

        Assert.Equal(EngineRejection.NotForRealtime, recusa);
        Assert.Contains("modo pontual", EngineSelection.Explain(recusa, "cloud"));
    }

    [Fact]
    public void RF_122_em_modo_pontual_o_motor_de_nuvem_e_aceito()
        => Assert.Equal(EngineRejection.None, EngineSelection.CanStart(
            Engine("cloud"), Catalog().OcrEngine("cloud"),
            realtime: false, WindowMode.Dark));

    /// <summary>RF-351 — A sobreposição exige motor com posição de palavra.</summary>
    [Fact]
    public void RF_351_a_sobreposicao_recusa_motor_sem_posicao_de_palavra()
    {
        var recusa = EngineSelection.CanStart(
            Engine("interpreted", wordPositions: false), Catalog().OcrEngine("interpreted"),
            realtime: true, WindowMode.Overlay);

        Assert.Equal(EngineRejection.NoWordPositions, recusa);
        Assert.Contains("sobreposição", EngineSelection.Explain(recusa, "interpreted"));
    }

    [Fact]
    public void RF_351_o_mesmo_motor_serve_nos_modos_escuro_e_camada()
    {
        var info = Catalog().OcrEngine("interpreted");
        var motor = Engine("interpreted", wordPositions: false);

        Assert.Equal(EngineRejection.None,
            EngineSelection.CanStart(motor, info, true, WindowMode.Dark));
        Assert.Equal(EngineRejection.None,
            EngineSelection.CanStart(motor, info, true, WindowMode.Layer));
    }

    [Fact]
    public void RF_575_um_motor_indisponivel_e_recusado_antes_de_tudo()
        => Assert.Equal(EngineRejection.Unavailable, EngineSelection.CanStart(
            Engine("modern", available: false), Catalog().OcrEngine("modern"),
            realtime: true, WindowMode.Dark));

    /// <summary>
    /// RF-123 — A priorização do motor de nuvem em modo pontual exige as TRÊS condições:
    /// opção ativa, motor disponível e dentro da cota.
    /// </summary>
    [Fact]
    public void RF_123_a_priorizacao_do_motor_de_nuvem_exige_as_tres_condicoes()
    {
        var escolhido = Engine("modern");
        var nuvem = Engine("cloud");

        Assert.Equal("cloud", EngineSelection
            .ResolveForOneShot(escolhido, nuvem, preferCloud: true, withinQuota: true).Key);

        // Opção desligada.
        Assert.Equal("modern", EngineSelection
            .ResolveForOneShot(escolhido, nuvem, preferCloud: false, withinQuota: true).Key);

        // Fora da cota: a preferência não se aplica e o escolhido continua valendo, em vez
        // de a tradução simplesmente falhar.
        Assert.Equal("modern", EngineSelection
            .ResolveForOneShot(escolhido, nuvem, preferCloud: true, withinQuota: false).Key);

        // Motor indisponível.
        Assert.Equal("modern", EngineSelection.ResolveForOneShot(
            escolhido, Engine("cloud", available: false), true, true).Key);

        // Sem motor de nuvem registrado.
        Assert.Equal("modern", EngineSelection.ResolveForOneShot(escolhido, null, true, true).Key);
    }

    /// <summary>
    /// RF-149 — Ao trocar de motor, o idioma é PRESERVADO quando o novo motor o reconhece.
    /// Sem isso, o usuário voltaria ao padrão e descobriria pelo resultado errado.
    /// </summary>
    [Fact]
    public void RF_149_trocar_de_motor_preserva_o_idioma_quando_possivel()
    {
        var catalogo = Catalog();
        var moderno = catalogo.OcrEngine("modern")!;

        Assert.Equal("ja", EngineSelection.PreserveLanguage("ja", moderno, fallback: "en"));
        Assert.Equal("en", EngineSelection.PreserveLanguage("en", moderno, fallback: "en"));
    }

    [Fact]
    public void RF_149_um_idioma_que_o_novo_motor_nao_tem_cai_para_a_reserva()
    {
        var moderno = Catalog().OcrEngine("modern")!;
        Assert.Equal("en", EngineSelection.PreserveLanguage("idioma_exotico", moderno, "en"));
    }

    /// <summary>
    /// RF-147 / RF-315 — A escolha do idioma de OCR propaga para os idiomas de origem dos
    /// serviços de tradução, quando houver correspondência.
    /// </summary>
    [Fact]
    public void RF_147_o_idioma_de_ocr_propaga_para_os_servicos()
    {
        var catalogo = Catalog();
        var propagado = EngineSelection.PropagateSourceLanguage(catalogo, catalogo.Language("ja")!);

        Assert.Equal("ja", propagado["webfree"]);
        Assert.Equal("ja", propagado["commercial_eu"]);

        // RF-308 — um serviço que não declara códigos não entra.
        Assert.False(propagado.ContainsKey("localdb"));
    }

    /// <summary>
    /// RF-150 — O "modo rápido" do motor clássico anexa um sufixo, mas SÓ para os conjuntos
    /// que têm variante rápida publicada.
    /// </summary>
    [Theory]
    [InlineData("eng", true, "eng_fast")]
    [InlineData("jpn", true, "jpn_fast")]
    [InlineData("por", true, "por")]        // sem variante rápida: nome intacto
    [InlineData("eng", false, "eng")]
    [InlineData("meu_conjunto", true, "meu_conjunto")]
    public void RF_150_o_modo_rapido_so_vale_para_eng_e_jpn(
        string conjunto, bool rapido, string esperado)
        => Assert.Equal(esperado, EngineSelection.ClassicDataset(conjunto, rapido));
}
