using Avalonia.Controls;
using Gort.Core.Localization;

namespace Gort.App.Windows;

/// <summary>
/// RF-543 — Sobre: versão, data de compilação, versões dos dicionários, links do autor.
///
/// Clicar no logotipo reexibe a tela de abertura. Não é enfeite: a tela de abertura é onde
/// as mensagens de inicialização aparecem (RF-005), e quem quer saber por que um motor não
/// apareceu na lista precisa de um jeito de revê-las sem reiniciar.
/// </summary>
public partial class AboutWindow : Window
{
    private readonly Localizer _loc;

    public AboutWindow() : this(new Localizer(), Array.Empty<(string, int)>()) { }

    public AboutWindow(Localizer loc, IReadOnlyList<(string Name, int Entries)> dictionaries)
    {
        InitializeComponent();

        _loc = loc;

        Title = _loc["about.title"];
        NameText.Text = _loc["about.name"];
        VersionText.Text = _loc.Format("about.version", SplashWindow.Version);
        BuildText.Text = _loc.Format("about.build", SplashWindow.BuildDate);
        DictionariesLabel.Text = _loc["about.dictionaries"];

        DictionariesText.Text = dictionaries.Count == 0
            ? _loc["about.no_dictionaries"]
            : string.Join("\n", dictionaries.Select(
                d => _loc.Format("about.dictionary", d.Name, d.Entries)));

        RepositoryButton.Content = _loc["about.repository"];
        ProjectButton.Content = _loc["about.project"];
        CloseButton.Content = _loc["about.close"];

        RepositoryButton.Click += (_, _) => OpenLink?.Invoke("repository");
        ProjectButton.Click += (_, _) => OpenLink?.Invoke("project_page");
        CloseButton.Click += (_, _) => Close();

        Logo.PointerPressed += (_, _) => ShowSplash?.Invoke();
    }

    /// <summary>RF-513 — Os endereços são dados de configuração; quem os abre é a aplicação.</summary>
    public Action<string>? OpenLink { get; set; }

    /// <summary>RF-543 — Clicar no logotipo reexibe a tela de abertura.</summary>
    public Action? ShowSplash { get; set; }
}
