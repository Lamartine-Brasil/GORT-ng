using System;
using System.IO;
using Avalonia;
using Gort.Core.Configuration;
using Gort.Platform.Lifecycle;

namespace Gort.App;

class Program
{
    /// <summary>A trava de instância única, viva enquanto o programa vive (RF-001).</summary>
    public static SingleInstanceGuard? Instance { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        // RF-003 — a pasta do executável é o diretório de trabalho corrente, para que os
        // caminhos relativos de dados resolvam corretamente independentemente de como o
        // programa foi lançado (por atalho, por terminal, por outro programa).
        try
        {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        }
        catch
        {
            // P8 — se não der, os caminhos absolutos continuam funcionando.
        }

        // RF-001 / RF-002 — uma instância por vez, salvo se o marcador existir.
        var paths = new UserPaths();
        if (!SingleInstanceGuard.MarkerPresent(AppContext.BaseDirectory, paths.Root))
        {
            var guard = SingleInstanceGuard.Acquire(Path.Combine(paths.Root, "instancia.trava"));
            if (!guard.Acquired)
            {
                // RF-001 — a segunda instância INFORMA e encerra. A mensagem vai para a
                // saída de erro porque não há interface ainda: criar uma janela só para
                // dizer isto custaria a inicialização inteira do Avalonia.
                Console.Error.WriteLine(
                    guard.OwnerProcessId is int pid
                        ? $"O GORT já está em execução (processo {pid})."
                        : "O GORT já está em execução.");
                Console.Error.WriteLine(
                    $"Para permitir várias cópias, crie o arquivo " +
                    $"'{SingleInstanceGuard.Marker}' em {paths.Root}.");
                Environment.Exit(1);
                return;
            }
            Instance = guard;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Instance?.Dispose();
        }
    }

    // Configuração do Avalonia; também usada pelo designer visual.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
