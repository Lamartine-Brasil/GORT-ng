using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Gort.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                // RF-005 — a inicialização monta catálogo, perfil, capacidades e motores.
                desktop.MainWindow = new MainWindow(AppSession.Create());
            }
            catch (Exception ex)
            {
                // RF-006 — se a inicialização lançar erro não tratado, o programa exibe a
                // descrição e encerra, em vez de morrer em silêncio.
                desktop.MainWindow = new Window
                {
                    Title = "Falha na inicialização",
                    Width = 620,
                    Height = 240,
                    Content = new TextBlock
                    {
                        Margin = new Thickness(20),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Text = "O programa não conseguiu iniciar:\n\n" + ex.Message,
                    },
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
