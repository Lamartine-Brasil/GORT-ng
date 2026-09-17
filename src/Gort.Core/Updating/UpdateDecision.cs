using System.Globalization;
using Gort.Core.Calibration;

namespace Gort.Core.Updating;

/// <summary>RF-420 — Os dois tipos de atualização, mais o caso de não haver nenhuma.</summary>
public enum UpdateKind
{
    /// <summary>A versão local está em dia.</summary>
    None,

    /// <summary>
    /// RF-420 — Menor: a versão local está DENTRO da faixa declarada como atualizável em
    /// linha; pode ser aplicada automaticamente.
    /// </summary>
    Minor,

    /// <summary>RF-420 — Maior: fora dessa faixa; exige download manual.</summary>
    Major,
}

/// <summary>
/// RF-420 — O arquivo de versão publicado pelo servidor de distribuição.
///
/// `MinimumInlineVersion` é o que define a faixa: versões a partir dela podem ser
/// atualizadas em linha, e as anteriores exigem download manual. É o servidor que decide, e
/// não o programa — assim uma mudança de formato que quebre a atualização automática pode
/// ser contornada publicando uma faixa nova, sem depender de quem já instalou.
/// </summary>
public sealed class VersionManifest
{
    public string Version { get; init; } = "";
    public string MinimumInlineVersion { get; init; } = "";

    public string ExecutableUrl { get; init; } = "";
    public string ChecksumUrl { get; init; } = "";
    public string ConfigUrl { get; init; } = "";
    public string DownloadPageUrl { get; init; } = "";
    public string ReleaseNotesUrl { get; init; } = "";
}

/// <summary>
/// RF-420 a RF-424, RF-427 a RF-429 — As DECISÕES da atualização.
///
/// Fica separado do download por um motivo prático: o servidor de distribuição não existe
/// nesta versão, e as decisões — qual tipo de atualização, se a publicação está completa, se
/// o período de espera vale, se a soma confere — são verificáveis sem ele. Quando o servidor
/// existir, o que falta é o transporte.
/// </summary>
public static class UpdateDecision
{
    /// <summary>
    /// RF-420 — Compara a versão local com a remota e classifica a atualização.
    ///
    /// Uma versão ilegível de qualquer lado devolve <see cref="UpdateKind.None"/>: na
    /// dúvida, não se atualiza. Um programa que interpreta lixo como "há versão nova"
    /// baixaria qualquer coisa que o servidor devolvesse por engano.
    /// </summary>
    public static UpdateKind Classify(string localVersion, VersionManifest manifest)
    {
        if (!TryParse(localVersion, out var local)) return UpdateKind.None;
        if (!TryParse(manifest.Version, out var remote)) return UpdateKind.None;

        if (remote <= local) return UpdateKind.None;

        // Sem faixa declarada, tudo é atualização MAIOR: na ausência da declaração, o
        // caminho conservador é o que pede confirmação do usuário.
        if (!TryParse(manifest.MinimumInlineVersion, out var minimum)) return UpdateKind.Major;

        return local >= minimum ? UpdateKind.Minor : UpdateKind.Major;
    }

    /// <summary>
    /// RF-422 🔒 — Antes de qualquer download, a configuração publicada ao lado do executável
    /// novo tem de declarar a MESMA versão do arquivo de versão.
    ///
    /// Quando não declara, a publicação ainda está em andamento e a atualização é abortada
    /// SILENCIOSAMENTE — sem perguntar nada ao usuário. Os dois artefatos são publicados em
    /// lugares diferentes, e há uma janela em que um está novo e o outro não; perguntar
    /// nessa janela faria o usuário decidir sobre uma publicação que nem existe ainda.
    /// </summary>
    public static bool PublicationIsComplete(VersionManifest manifest, string? publishedConfigVersion)
    {
        if (string.IsNullOrWhiteSpace(publishedConfigVersion)) return false;

        return string.Equals(publishedConfigVersion.Trim(), manifest.Version.Trim(),
                             StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// RF-424 — Toda URL recebida do arquivo de versão é forçada para protocolo seguro antes
    /// de qualquer download.
    ///
    /// Forçada, e não recusada: um servidor que publica o endereço em texto claro por
    /// descuido não deve impedir a atualização — mas o download não pode sair por ali.
    /// </summary>
    public static string ForceSecure(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return "https://" + url[7..];

        return url;
    }

    /// <summary>
    /// RF-423 — Sem endereço de soma de verificação declarado, a atualização é ABORTADA.
    /// Verificação obrigatória: sem soma não há garantia de integridade, e substituir o
    /// executável por um arquivo não verificado é o pior desfecho possível.
    /// </summary>
    public static bool CanDownload(VersionManifest manifest)
        => ForceSecure(manifest.ExecutableUrl).Length > 0
           && ForceSecure(manifest.ChecksumUrl).Length > 0;

    /// <summary>
    /// RF-427 — A soma calculada é comparada com a esperada SEM diferenciar maiúsculas: as
    /// duas formas circulam, e recusar uma atualização boa por causa de caixa seria um
    /// defeito difícil de diagnosticar.
    ///
    /// Sem soma esperada, o resultado é falso: é o mesmo caso de RF-423, visto do outro lado.
    /// </summary>
    public static bool ChecksumMatches(string? expected, string? actual)
        => !string.IsNullOrWhiteSpace(expected)
           && !string.IsNullOrWhiteSpace(actual)
           && string.Equals(expected.Trim(), actual.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// RF-428 🔒 / RF-429 — O período de espera depois de uma falha de verificação.
    ///
    /// Sem ele, um arquivo corrompido no servidor faria o programa baixar, falhar e tentar
    /// de novo a cada abertura — para sempre, consumindo a banda do usuário sem nunca ter
    /// sucesso.
    ///
    /// RF-429 — um marcador ausente, malformado ou com instante FUTURO é tratado como "sem
    /// período de espera". O instante futuro importa: o relógio do sistema pode ter sido
    /// acertado para trás, e um marcador gravado "no futuro" bloquearia a atualização por
    /// tempo indefinido.
    /// </summary>
    public static bool InCooldown(string? failureMarker, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(failureMarker)) return false;

        if (!DateTime.TryParse(failureMarker.Trim(), CultureInfo.InvariantCulture,
                               DateTimeStyles.RoundtripKind, out var failedAt))
        {
            return false;
        }

        if (failedAt > now) return false;

        return now - failedAt < P.UpdateFailureCooldown;
    }

    /// <summary>O conteúdo do marcador de falha: o instante, em formato que volta exato.</summary>
    public static string MarkerFor(DateTime instant)
        => instant.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>
    /// RF-430 — A substituição move o antigo para um nome de backup e o baixado para o nome
    /// definitivo, aguardando até P-117 tentativas espaçadas de P-118 para o arquivo ser
    /// liberado, e falhando de forma LIMPA se não conseguir.
    ///
    /// "De forma limpa" é a parte que importa: um executável meio substituído não abre, e
    /// desistir com o arquivo antigo intacto é sempre melhor que insistir.
    /// </summary>
    public static bool MoveWithRetries(string from, string to, Action<int>? sleep = null)
    {
        sleep ??= ms => Thread.Sleep(ms);

        for (int attempt = 0; attempt < P.FileMoveAttempts; attempt++)
        {
            try
            {
                if (File.Exists(to)) File.Delete(to);
                File.Move(from, to);
                return true;
            }
            catch (IOException)
            {
                // O arquivo ainda está em uso — é o caso normal logo depois de o programa
                // principal encerrar, e é por isso que se tenta de novo em vez de desistir.
                sleep(P.FileMoveRetryMs);
            }
            catch (UnauthorizedAccessException)
            {
                sleep(P.FileMoveRetryMs);
            }
        }

        return false;
    }

    /// <summary>
    /// RF-431 — Ao iniciar, o programa detecta o arquivo de BACKUP deixado pelo auxiliar e o
    /// remove.
    ///
    /// Devolve verdadeiro quando removeu. Uma falha não é erro: o processo anterior pode
    /// ainda estar encerrando, e a próxima abertura tenta de novo — deixar um arquivo a mais
    /// no disco é melhor que impedir o programa de abrir.
    /// </summary>
    public static bool RemoveBackup(string backupPath)
    {
        try
        {
            if (!File.Exists(backupPath)) return false;
            File.Delete(backupPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(text)) return false;

        return Version.TryParse(text.Trim(), out version!);
    }
}
