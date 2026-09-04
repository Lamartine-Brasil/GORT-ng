using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Gort.App.Windows;

namespace Gort.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // A janela principal só aparece quando a inicialização termina; até lá, a tela
            // de abertura é a única coisa na tela (RF-004).
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = StartAsync(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// RF-004 / RF-005 — A tela de abertura fica visível enquanto o programa verifica
    /// atualizações, carrega as configurações padrão, enumera os idiomas de OCR disponíveis
    /// e carrega o perfil do usuário.
    /// </summary>
    private static async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var splash = new SplashWindow();
        splash.Show();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        AppSession? session = null;
        Exception? failure = null;

        try
        {
            splash.ReportStep("verificando o desenho de texto…");

            // RF-007 — a verificação é na thread de interface, porque é ela que desenha.
            string? vectorFailure = VectorTextCheck.Apply();

            splash.ReportStep("carregando catálogo e perfil…");

            // A inicialização sai da thread de interface para que a tela de abertura
            // continue desenhando: ela existe justamente para cobrir esta espera.
            session = await Task.Run(() => AppSession.Create());

            if (vectorFailure is not null) session.Notices.Add(vectorFailure);

            splash.ReportStep($"motores de OCR: " +
                              string.Join(", ", session.Engines.Available.Select(e => e.Key)));
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        await splash.FadeOutAsync(watch.Elapsed);

        // RF-006 — se a inicialização lançar erro não tratado, o programa exibe a descrição
        // e encerra, em vez de morrer em silêncio.
        if (session is null)
        {
            desktop.MainWindow = new Window
            {
                Title = "Falha na inicialização",
                Width = 620,
                Height = 220,
                Content = new TextBlock
                {
                    Margin = new Thickness(20),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Text = "O programa não conseguiu iniciar:\n\n" + failure?.Message,
                },
            };
        }
        else
        {
            desktop.MainWindow = new MainWindow(session);
        }

        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        desktop.MainWindow.Show();
    }
}
