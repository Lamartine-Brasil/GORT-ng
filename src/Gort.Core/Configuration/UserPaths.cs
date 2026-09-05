namespace Gort.Core.Configuration;

/// <summary>
/// RF-578 — Os caminhos de dados do usuário seguem a convenção de cada sistema, mas o
/// FORMATO dos arquivos é idêntico, para que uma configuração criada em um sistema
/// funcione em outro.
///
/// RF-003 — A pasta do executável é o diretório de trabalho corrente, para que os caminhos
/// relativos de dados resolvam corretamente independentemente de como o programa foi
/// lançado. Os dados do usuário, porém, ficam na pasta convencional do sistema.
/// </summary>
public sealed class UserPaths
{
    public const string ProfileExtension = ".gort";

    public UserPaths(string? root = null)
    {
        Root = root ?? DefaultRoot();
        Directory.CreateDirectory(Root);
    }

    /// <summary>Pasta raiz dos dados do usuário.</summary>
    public string Root { get; }

    /// <summary>RF-020 — Perfil principal, carregado ao iniciar e salvo ao aplicar.</summary>
    public string MainProfile => Path.Combine(Root, "perfil" + ProfileExtension);

    /// <summary>RF-021 — Pasta dos perfis nomeados.</summary>
    public string ProfilesDirectory => Ensure(Path.Combine(Root, "perfis"));

    /// <summary>RF-031 — Opções avançadas, globais (RF-032).</summary>
    public string AdvancedOptions => Path.Combine(Root, "avancado.toml");

    /// <summary>RF-034 — Opções do aplicativo.</summary>
    public string AppOptions => Path.Combine(Root, "aplicativo.toml");

    /// <summary>RF-037 — Atalhos de teclado, em arquivo próprio.</summary>
    public string Shortcuts => Path.Combine(Root, "atalhos.toml");

    /// <summary>RF-035 — Credenciais, um arquivo por serviço, em texto puro.</summary>
    public string CredentialsFor(string serviceKey)
        => Path.Combine(Ensure(Path.Combine(Root, "credenciais")), $"{serviceKey}.toml");

    /// <summary>RF-208 — Memória de resultados, um arquivo por serviço.</summary>
    public string ResultMemoryFor(string serviceKey)
        => Path.Combine(Ensure(Path.Combine(Root, "memoria")), $"{serviceKey}.txt");

    /// <summary>RF-216 — Pasta dedicada aos arquivos da coletânea de tradução.</summary>
    public string CollectionDirectory => Ensure(Path.Combine(Root, "coletanea"));

    /// <summary>RF-302 — Pasta dos arquivos individuais de preset de API personalizada.</summary>
    public string ApiPresetsDirectory => Ensure(Path.Combine(Root, "presets"));

    /// <summary>RF-302 — A lista editável de presets, num arquivo próprio.</summary>
    public string ApiPresetsFile => Path.Combine(Root, "presets.toml");

    /// <summary>RF-492 — Pasta dedicada aos retratos de análise.</summary>
    public string DiagnosticsDirectory => Ensure(Path.Combine(Root, "diagnostico"));

    /// <summary>Dicionários de correção e bancos de dados de tradução.</summary>
    public string DataDirectory => Ensure(Path.Combine(Root, "dados"));

    /// <summary>RF-002 — Marcador que desativa a restrição de instância única.</summary>
    public const string MultiInstanceMarker = "multi-instancia";

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private static string DefaultRoot()
    {
        // Windows: %APPDATA%\Gort · macOS: ~/Library/Application Support/Gort
        // Linux:   $XDG_CONFIG_HOME/gort (ou ~/.config/gort)
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Gort");
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
            return Path.Combine(home, "Library", "Application Support", "Gort");

        string xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                     ?? Path.Combine(home, ".config");
        return Path.Combine(xdg, "gort");
    }
}
