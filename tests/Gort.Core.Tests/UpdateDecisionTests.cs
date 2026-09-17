using Gort.Core.Calibration;
using Gort.Core.Updating;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>
/// Cap. 26 / RF-420 a RF-429 — As decisões da atualização.
///
/// O servidor de distribuição não existe nesta versão, mas as DECISÕES existem e são
/// verificáveis sem ele: qual tipo de atualização, se a publicação está completa, se o
/// período de espera vale, se a soma confere. Quando o servidor existir, o que falta é o
/// transporte.
/// </summary>
public class UpdateDecisionTests
{
    private static VersionManifest Manifest(string version, string minimum = "1.0.0") => new()
    {
        Version = version,
        MinimumInlineVersion = minimum,
        ExecutableUrl = "https://exemplo.invalido/gort.zip",
        ChecksumUrl = "https://exemplo.invalido/gort.sha256",
    };

    // ── RF-420 — menor, maior, nenhuma ──────────────────────────────────────

    [Fact]
    public void RF_420_a_versao_em_dia_nao_e_atualizacao()
    {
        Assert.Equal(UpdateKind.None, UpdateDecision.Classify("2.0.0", Manifest("2.0.0")));
        Assert.Equal(UpdateKind.None, UpdateDecision.Classify("2.1.0", Manifest("2.0.0")));
    }

    /// <summary>
    /// RF-420 — Menor: a versão local está DENTRO da faixa declarada como atualizável em
    /// linha. Quem decide a faixa é o servidor, e não o programa: assim uma mudança de
    /// formato que quebre a atualização automática pode ser contornada publicando uma faixa
    /// nova, sem depender de quem já instalou.
    /// </summary>
    [Fact]
    public void RF_420_dentro_da_faixa_e_atualizacao_menor()
    {
        Assert.Equal(UpdateKind.Minor,
            UpdateDecision.Classify("1.4.0", Manifest("2.0.0", minimum: "1.2.0")));
    }

    [Fact]
    public void RF_420_fora_da_faixa_e_atualizacao_maior()
    {
        Assert.Equal(UpdateKind.Major,
            UpdateDecision.Classify("1.0.0", Manifest("2.0.0", minimum: "1.2.0")));
    }

    /// <summary>
    /// Sem faixa declarada, tudo é MAIOR: na ausência da declaração, o caminho conservador é
    /// o que pede confirmação do usuário.
    /// </summary>
    [Fact]
    public void Sem_faixa_declarada_tudo_e_maior()
    {
        Assert.Equal(UpdateKind.Major,
            UpdateDecision.Classify("1.4.0", Manifest("2.0.0", minimum: "")));
    }

    /// <summary>
    /// Uma versão ilegível de qualquer lado NÃO é atualização: na dúvida não se atualiza, e
    /// um programa que interpreta lixo como "há versão nova" baixaria qualquer coisa que o
    /// servidor devolvesse por engano.
    /// </summary>
    [Theory]
    [InlineData("", "2.0.0")]
    [InlineData("1.0.0", "")]
    [InlineData("versão nova", "2.0.0")]
    [InlineData("1.0.0", "<html>404</html>")]
    public void Uma_versao_ilegivel_nao_e_atualizacao(string local, string remote)
        => Assert.Equal(UpdateKind.None, UpdateDecision.Classify(local, Manifest(remote)));

    // ── RF-422 🔒 — publicação em andamento ─────────────────────────────────

    /// <summary>
    /// RF-422 🔒 — A configuração publicada ao lado do executável novo tem de declarar a
    /// MESMA versão do arquivo de versão; se não declarar, a publicação ainda está em
    /// andamento e a atualização é abortada SILENCIOSAMENTE.
    ///
    /// Os dois artefatos são publicados em lugares diferentes, e há uma janela em que um
    /// está novo e o outro não. Perguntar nessa janela faria o usuário decidir sobre uma
    /// publicação que nem existe ainda.
    /// </summary>
    [Fact]
    public void RF_422_a_publicacao_incompleta_aborta()
    {
        var manifest = Manifest("2.0.0");

        Assert.True(UpdateDecision.PublicationIsComplete(manifest, "2.0.0"));
        Assert.True(UpdateDecision.PublicationIsComplete(manifest, " 2.0.0 "));

        Assert.False(UpdateDecision.PublicationIsComplete(manifest, "1.9.0"));
        Assert.False(UpdateDecision.PublicationIsComplete(manifest, ""));
        Assert.False(UpdateDecision.PublicationIsComplete(manifest, null));
    }

    // ── RF-424 — protocolo seguro ───────────────────────────────────────────

    /// <summary>
    /// RF-424 — As URLs são FORÇADAS para protocolo seguro, não recusadas: um servidor que
    /// publica o endereço em texto claro por descuido não deve impedir a atualização — mas o
    /// download não pode sair por ali.
    /// </summary>
    [Theory]
    [InlineData("http://exemplo.invalido/a.zip", "https://exemplo.invalido/a.zip")]
    [InlineData("HTTP://exemplo.invalido/a.zip", "https://exemplo.invalido/a.zip")]
    [InlineData("https://exemplo.invalido/a.zip", "https://exemplo.invalido/a.zip")]
    [InlineData("", "")]
    public void RF_424_as_urls_sao_forcadas_para_protocolo_seguro(string dado, string esperado)
        => Assert.Equal(esperado, UpdateDecision.ForceSecure(dado));

    // ── RF-423 / RF-427 — soma de verificação ───────────────────────────────

    /// <summary>
    /// RF-423 — Sem endereço de soma declarado, a atualização é ABORTADA. Verificação
    /// obrigatória: substituir o executável por um arquivo não verificado é o pior desfecho
    /// possível.
    /// </summary>
    [Fact]
    public void RF_423_sem_endereco_de_soma_nao_se_baixa()
    {
        Assert.True(UpdateDecision.CanDownload(Manifest("2.0.0")));

        Assert.False(UpdateDecision.CanDownload(new VersionManifest
        {
            Version = "2.0.0",
            ExecutableUrl = "https://exemplo.invalido/gort.zip",
            ChecksumUrl = "",
        }));

        Assert.False(UpdateDecision.CanDownload(new VersionManifest
        {
            Version = "2.0.0",
            ExecutableUrl = "",
            ChecksumUrl = "https://exemplo.invalido/gort.sha256",
        }));
    }

    /// <summary>
    /// RF-427 — A comparação de soma NÃO diferencia maiúsculas: as duas formas circulam, e
    /// recusar uma atualização boa por causa de caixa seria um defeito difícil de
    /// diagnosticar.
    /// </summary>
    [Fact]
    public void RF_427_a_soma_e_comparada_sem_diferenciar_maiusculas()
    {
        Assert.True(UpdateDecision.ChecksumMatches("ABC123", "abc123"));
        Assert.True(UpdateDecision.ChecksumMatches(" abc123 ", "ABC123"));

        Assert.False(UpdateDecision.ChecksumMatches("abc123", "abc124"));
        Assert.False(UpdateDecision.ChecksumMatches(null, "abc123"));
        Assert.False(UpdateDecision.ChecksumMatches("abc123", ""));
    }

    // ── RF-428 🔒 / RF-429 — período de espera ──────────────────────────────

    /// <summary>
    /// RF-428 🔒 — Depois de uma falha de verificação, o programa recusa entrar em fluxo de
    /// atualização durante P-116.
    ///
    /// Sem isso, um arquivo corrompido no servidor faria o programa baixar, falhar e tentar
    /// de novo a cada abertura — para sempre, consumindo a banda do usuário sem nunca ter
    /// sucesso.
    /// </summary>
    [Fact]
    public void RF_428_o_periodo_de_espera_vale_por_P116()
    {
        var now = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
        string marker = UpdateDecision.MarkerFor(now - TimeSpan.FromMinutes(5));

        Assert.True(UpdateDecision.InCooldown(marker, now));
        Assert.Equal(TimeSpan.FromMinutes(10), P.UpdateFailureCooldown);
    }

    [Fact]
    public void RF_428_passado_o_periodo_a_atualizacao_volta_a_ser_tentada()
    {
        var now = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
        string marker = UpdateDecision.MarkerFor(now - P.UpdateFailureCooldown);

        Assert.False(UpdateDecision.InCooldown(marker, now));
    }

    /// <summary>
    /// RF-429 — Um marcador ausente, MALFORMADO ou com instante FUTURO é tratado como "sem
    /// período de espera".
    ///
    /// O instante futuro importa: o relógio do sistema pode ter sido acertado para trás, e
    /// um marcador gravado "no futuro" bloquearia a atualização por tempo indefinido.
    /// </summary>
    [Fact]
    public void RF_429_marcador_ausente_malformado_ou_futuro_nao_bloqueia()
    {
        var now = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(UpdateDecision.InCooldown(null, now));
        Assert.False(UpdateDecision.InCooldown("", now));
        Assert.False(UpdateDecision.InCooldown("ontem à tarde", now));
        Assert.False(UpdateDecision.InCooldown(
            UpdateDecision.MarkerFor(now + TimeSpan.FromHours(3)), now));
    }

    /// <summary>O marcador volta exato: um formato que perde precisão perderia o período.</summary>
    [Fact]
    public void O_marcador_volta_exato()
    {
        var instant = new DateTime(2026, 9, 17, 12, 34, 56, 789, DateTimeKind.Utc);
        var now = instant + TimeSpan.FromMinutes(1);

        Assert.True(UpdateDecision.InCooldown(UpdateDecision.MarkerFor(instant), now));
    }
}

/// <summary>RF-430 / RF-431 — A substituição dos arquivos.</summary>
public class UpdateFileMoveTests
{
    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "gort-atualizacao",
                                  Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void RF_430_um_arquivo_livre_move_na_primeira_tentativa()
    {
        string dir = TempDir();
        string from = Path.Combine(dir, "novo.bin");
        string to = Path.Combine(dir, "gort.bin");
        File.WriteAllText(from, "novo");

        int sleeps = 0;
        Assert.True(UpdateDecision.MoveWithRetries(from, to, _ => sleeps++));

        Assert.Equal("novo", File.ReadAllText(to));
        Assert.Equal(0, sleeps);
    }

    /// <summary>
    /// RF-430 — Um destino que não se deixa substituir é tentado até P-117 vezes e então
    /// falha de forma LIMPA.
    ///
    /// "De forma limpa" é a parte que importa: um executável meio substituído não abre, e
    /// desistir com o antigo intacto é sempre melhor que insistir.
    ///
    /// O destino aqui é uma PASTA, e não um arquivo aberto. A primeira versão deste teste
    /// segurava o destino com `FileShare.None`, o que funciona no Windows e não no macOS:
    /// o POSIX deixa mover um arquivo aberto, e o teste passava por não bloquear nada. Uma
    /// pasta no lugar do arquivo recusa a substituição nos três sistemas.
    /// </summary>
    [Fact]
    public void RF_430_um_destino_indisponivel_falha_de_forma_limpa()
    {
        string dir = TempDir();
        string from = Path.Combine(dir, "novo.bin");
        string to = Path.Combine(dir, "gort.bin");

        File.WriteAllText(from, "novo");
        Directory.CreateDirectory(to);
        File.WriteAllText(Path.Combine(to, "ocupa.txt"), "x");

        int sleeps = 0;
        Assert.False(UpdateDecision.MoveWithRetries(from, to, _ => sleeps++));

        Assert.Equal(P.FileMoveAttempts, sleeps);
        Assert.True(File.Exists(from));   // o baixado continua lá, nada foi perdido
    }

    /// <summary>
    /// RF-431 — O backup deixado pelo auxiliar é removido ao iniciar. Uma falha não é erro:
    /// o processo anterior pode ainda estar encerrando, e deixar um arquivo a mais no disco
    /// é melhor que impedir o programa de abrir.
    /// </summary>
    [Fact]
    public void RF_431_o_backup_e_removido_e_a_ausencia_nao_e_erro()
    {
        string dir = TempDir();
        string backup = Path.Combine(dir, "gort.bin.antigo");

        Assert.False(UpdateDecision.RemoveBackup(backup));

        File.WriteAllText(backup, "antigo");
        Assert.True(UpdateDecision.RemoveBackup(backup));
        Assert.False(File.Exists(backup));
    }
}
