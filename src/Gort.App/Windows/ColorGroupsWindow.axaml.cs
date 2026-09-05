using Avalonia;
using Avalonia.Controls;
using Gort.Core.Localization;
using Gort.Core.Model;
using Gort.Core.Regions;

namespace Gort.App.Windows;

/// <summary>
/// RF-534 — Grupos de cor POR ÁREA.
///
/// Cada área escolhe quais grupos de cor se aplicam a ela (RF-078). A lista mostra, para
/// cada grupo, o seu índice e os seus valores — R/G/B e as faixas S/V — porque um grupo sem
/// os números é indistinguível do vizinho, e escolher entre "grupo 1" e "grupo 2" às cegas
/// é adivinhar.
///
/// RF-063 — áreas de EXCLUSÃO não oferecem os botões de cor: elas subtraem região, não
/// filtram cor, e o filtro que se aplicaria a elas nunca chega a rodar.
/// </summary>
public partial class ColorGroupsWindow : Window
{
    private readonly Localizer _loc;
    private readonly CaptureFrame _frame;
    private readonly IReadOnlyList<ColorGroup> _groups;
    private readonly List<CheckBox> _boxes = new();
    private readonly List<bool> _backup;

    private bool _applied;

    public ColorGroupsWindow()
        : this(new Localizer(), new CaptureFrame(new Gort.Core.Model.Rect(0, 0, 100, 100)),
               Array.Empty<ColorGroup>()) { }

    public ColorGroupsWindow(Localizer loc, CaptureFrame frame,
                             IReadOnlyList<ColorGroup> groups)
    {
        InitializeComponent();

        _loc = loc;
        _frame = frame;
        _groups = groups;
        _backup = new List<bool>(frame.ActiveColorGroups);

        Title = _loc["groups.title"];
        TitleText.Text = _loc["groups.title"];
        CheckAllButton.Content = _loc["groups.check_all"];
        ApplyButton.Content = _loc["groups.apply"];
        CancelButton.Content = _loc["groups.cancel"];

        Build();

        CheckAllButton.Click += (_, _) =>
        {
            // Um só botão, que alterna: com todos marcados ele desmarca. "Marcar todos"
            // seguido de "desmarcar todos" seria dois botões para o mesmo gesto.
            bool target = _boxes.Any(b => b.IsChecked != true);
            foreach (var box in _boxes) box.IsChecked = target;
        };

        ApplyButton.Click += (_, _) => { _applied = true; Close(); };
        CancelButton.Click += (_, _) => Close();
    }

    private void Build()
    {
        // RF-078 — um sinalizador por grupo, na ordem dos grupos. Uma área criada antes de
        // um grupo novo tem a lista curta; ela cresce com padrão marcado.
        while (_frame.ActiveColorGroups.Count < _groups.Count)
            _frame.ActiveColorGroups.Add(true);

        for (int i = 0; i < _groups.Count; i++)
        {
            var g = _groups[i];
            _boxes.Add(new CheckBox
            {
                Content = _loc.Format("groups.entry", i + 1, g.R, g.G, g.B,
                                      g.S1, g.S2, g.V1, g.V2),
                IsChecked = _frame.ActiveColorGroups[i],
                Margin = new Thickness(0, 2),
            });
        }

        GroupList.ItemsSource = _boxes;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_applied)
        {
            for (int i = 0; i < _boxes.Count && i < _frame.ActiveColorGroups.Count; i++)
                _frame.ActiveColorGroups[i] = _boxes[i].IsChecked == true;
        }
        else
        {
            // Cancelar devolve exatamente o que havia — inclusive o comprimento da lista,
            // que `Build` pode ter aumentado.
            _frame.ActiveColorGroups.Clear();
            _frame.ActiveColorGroups.AddRange(_backup);
        }

        base.OnClosed(e);
    }
}
