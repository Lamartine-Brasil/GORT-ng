using Avalonia.Controls;
using Avalonia.Input;
using Gort.Core.Localization;
using Gort.Core.Regions;

namespace Gort.App.Windows;

/// <summary>
/// RF-062 / RF-533 — Gerenciamento de áreas.
///
/// Adicionar área, adicionar área de exclusão, limpar tudo, aplicar. Fica onde o controle
/// remoto está, porque é dali que o usuário vem — e porque as molduras ocupam a tela toda,
/// então uma janela centralizada cairia por cima justamente do que se está ajustando.
///
/// RF-061 — enquanto ela está aberta, as áreas são TEMPORÁRIAS: fechar sem aplicar reverte
/// tudo ao estado salvo. É o que permite experimentar com a tradução rodando sem o risco de
/// perder um conjunto de áreas que já funcionava.
/// </summary>
public partial class AreaManagerWindow : Window
{
    private readonly Localizer _loc;
    private readonly RegionManager _regions;
    private bool _applied;

    public AreaManagerWindow() : this(new Localizer(), new RegionManager()) { }

    public AreaManagerWindow(Localizer loc, RegionManager regions)
    {
        InitializeComponent();

        _loc = loc;
        _regions = regions;

        Title = _loc["areas.title"];
        TitleText.Text = _loc["areas.title"];
        AddAreaButton.Content = _loc["areas.add"];
        AddExclusionButton.Content = _loc["areas.add_exclusion"];
        ColorGroupsButton.Content = _loc["areas.color_groups"];
        ClearButton.Content = _loc["areas.clear"];
        ApplyButton.Content = _loc["areas.apply"];
        CancelButton.Content = _loc["areas.cancel"];

        AddAreaButton.Click += (_, _) => AddArea?.Invoke(AreaKind.Normal);
        AddExclusionButton.Click += (_, _) => AddArea?.Invoke(AreaKind.Exclusion);
        ColorGroupsButton.Click += (_, _) => OpenColorGroups?.Invoke();

        ClearButton.Click += (_, _) => { _regions.ClearAll(); Changed?.Invoke(); Refresh(); };
        ApplyButton.Click += (_, _) => { _applied = true; Close(); };
        CancelButton.Click += (_, _) => Close();

        // A janela não tem borda de sistema; arrastar por qualquer ponto move (RF-331).
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };

        // RF-061 — a partir daqui as áreas são temporárias.
        _regions.BeginTemporaryEditing();
        Refresh();
    }

    public Action<AreaKind>? AddArea { get; set; }
    public Action? OpenColorGroups { get; set; }

    /// <summary>Avisa que a lista mudou, para as molduras serem redesenhadas.</summary>
    public Action? Changed { get; set; }

    /// <summary>Chamado ao fechar, com verdadeiro quando o usuário aplicou.</summary>
    public Action<bool>? Finished { get; set; }

    public void Refresh()
        => CountText.Text = _loc.Format("areas.count",
                                        _regions.Areas.Count, _regions.Exclusions.Count);

    protected override void OnClosed(EventArgs e)
    {
        // RF-062 / RF-533 — aplicar confirma; fechar sem aplicar REVERTE.
        if (_applied) _regions.CommitTemporaryEditing();
        else _regions.RollbackTemporaryEditing();

        // RF-533 — ao fechar, as molduras tornam-se invisíveis.
        _regions.SetFramesVisible(false);

        Finished?.Invoke(_applied);
        base.OnClosed(e);
    }
}
