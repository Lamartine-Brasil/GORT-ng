using Avalonia.Controls;
using Gort.Core.Localization;
using Gort.Core.Translation.Keys;

namespace Gort.App.Windows;

/// <summary>
/// RF-253 / RF-538 — Gerenciamento de chaves de tradução.
///
/// Lista com identificador, tipo e estado; campos de identificador e segredo; botões de
/// opção de tipo; um botão que alterna entre "adicionar" e "editar" conforme o identificador
/// digitado já exista; botão de remover.
///
/// O botão que alterna é o detalhe que faz a janela ser usável: sem ele o usuário teria de
/// decidir, antes de digitar, se está criando ou corrigindo — e ele quase sempre está
/// corrigindo, porque veio até aqui depois de uma chave falhar.
/// </summary>
public partial class KeyManagerWindow : Window
{
    private readonly Localizer _loc;
    private readonly TranslationKeyStore _store;
    private readonly string _filePath;

    public KeyManagerWindow() : this(new Localizer(), new TranslationKeyStore(), "") { }

    public KeyManagerWindow(Localizer loc, TranslationKeyStore store, string filePath)
    {
        InitializeComponent();

        _loc = loc;
        _store = store;
        _filePath = filePath;

        Title = _loc["keys.title"];
        TitleText.Text = _loc["keys.title"];
        IdLabel.Text = _loc["keys.id"];
        SecretLabel.Text = _loc["keys.secret"];
        FreeRadio.Content = _loc["keys.free"];
        PaidRadio.Content = _loc["keys.paid"];
        RemoveButton.Content = _loc["keys.remove"];
        CloseButton.Content = _loc["keys.close"];

        KeyList.SelectionChanged += (_, _) => LoadSelected();
        IdBox.TextChanged += (_, _) => RefreshSaveButton();
        SaveButton.Click += (_, _) => Save();
        RemoveButton.Click += (_, _) => Remove();
        CloseButton.Click += (_, _) => Close();

        Refresh();
    }

    /// <summary>
    /// RF-252 — A lista mostra identificador, tipo e ESTADO, na ordem em que o rodízio vai
    /// consumi-las. Ver a ordem é o que explica por que uma chave ainda não foi usada.
    /// </summary>
    private void Refresh()
    {
        string? selected = (KeyList.SelectedItem as ListBoxItem)?.Tag as string;

        KeyList.ItemsSource = _store.Ordered().Select(k => new ListBoxItem
        {
            Content = $"{k.Id}   ·   {_loc[k.IsFree ? "keys.free" : "keys.paid"]}" +
                      $"   ·   {_loc[StateKey(k.State)]}",
            Tag = k.Id,
        }).ToList();

        if (selected is not null)
        {
            KeyList.SelectedItem = KeyList.ItemsSource!.Cast<ListBoxItem>()
                .FirstOrDefault(i => (string?)i.Tag == selected);
        }

        StatusText.Text = _loc.Format("keys.count", _store.Keys.Count,
                                      TranslationKeyStore.Capacity);
        RefreshSaveButton();
    }

    private static string StateKey(KeyState state) => state switch
    {
        KeyState.Error => "keys.state_error",
        KeyState.Limit => "keys.state_limit",
        _ => "keys.state_normal",
    };

    private void LoadSelected()
    {
        if ((KeyList.SelectedItem as ListBoxItem)?.Tag is not string id) return;

        var key = _store.Find(id);
        if (key is null) return;

        IdBox.Text = key.Id;
        SecretBox.Text = key.Secret;
        FreeRadio.IsChecked = key.IsFree;
        PaidRadio.IsChecked = !key.IsFree;

        RefreshSaveButton();
    }

    /// <summary>
    /// RF-538 — O botão alterna entre "adicionar" e "editar" conforme o identificador
    /// digitado já exista ou não.
    /// </summary>
    private void RefreshSaveButton()
    {
        string id = IdBox.Text ?? "";
        bool exists = id.Length > 0 && _store.Find(id) is not null;

        SaveButton.Content = _loc[exists ? "keys.edit" : "keys.add"];
        RemoveButton.IsEnabled = exists;

        // RF-250 / P-55 — com o rodízio cheio, só edições passam.
        SaveButton.IsEnabled = id.Trim().Length > 0 && (exists || !_store.IsFull);
    }

    private void Save()
    {
        var key = _store.Set(IdBox.Text ?? "", SecretBox.Text ?? "",
                             FreeRadio.IsChecked == true);

        if (key is null)
        {
            StatusText.Text = _loc.Format("keys.full", TranslationKeyStore.Capacity);
            return;
        }

        Persist();
        Refresh();
    }

    private void Remove()
    {
        if (!_store.Remove(IdBox.Text ?? "")) return;

        IdBox.Text = "";
        SecretBox.Text = "";
        Persist();
        Refresh();
    }

    private void Persist()
    {
        if (_filePath.Length == 0) return;

        try
        {
            _store.Save(_filePath);
        }
        catch (Exception ex)
        {
            // P8 — uma falha de disco não pode impedir de continuar cadastrando.
            StatusText.Text = _loc.Format("msg.error", ex.Message);
        }
    }
}
