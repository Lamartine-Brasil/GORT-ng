namespace Gort.Core.Tests;

/// <summary>Localiza a pasta <c>data/</c> do repositório a partir do diretório de teste.</summary>
public static class TestPaths
{
    public static string RepositoryRoot { get; } = FindRoot();

    public static string DataDirectory => Path.Combine(RepositoryRoot, "data");

    public static string CasesDirectory => Path.Combine(RepositoryRoot, "tests", "cases");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Gort.sln"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Não foi possível localizar a raiz do repositório (Gort.sln).");
    }
}
