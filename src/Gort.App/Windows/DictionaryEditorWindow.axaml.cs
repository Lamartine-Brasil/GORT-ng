using Avalonia.Controls;
using Gort.Core.Localization;
using Gort.Core.Structuring;

namespace Gort.App.Windows;

/// <summary>
/// RF-537 — Editor de dicionário.
///
/// Campo com o texto reconhecido ATUAL pré-preenchido, campo com a correção, aceitar e
/// cancelar. Aceitar acrescenta o par ao arquivo e recarrega o dicionário.
///
/// A pré-preenchimento é o ponto do recurso: o usuário abre o editor no instante em que viu
/// o OCR errar, e o texto errado já está lá para ele corrigir — sem precisar copiá-lo à mão
/// de uma janela para outra.
/// </summary>
public partial class DictionaryEditorWindow : Window
{
    private readonly Localizer _loc;
    private readonly string _filePath;
    private readonly CorrectionDictionary? _dictionary;

    public DictionaryEditorWindow() : this(new Localizer(), "", null, "") { }

    public DictionaryEditorWindow(Localizer loc, string filePath,
                                  CorrectionDictionary? dictionary, string recognizedText)
    {
        InitializeComponent();

        _loc = loc;
        _filePath = filePath;
        _dictionary = dictionary;

        Title = _loc["dict.title"];
        TitleText.Text = _loc["dict.title"];
        FromLabel.Text = _loc["dict.from"];
        ToLabel.Text = _loc["dict.to"];
        AcceptButton.Content = _loc["dict.accept"];
        CancelButton.Content = _loc["dict.cancel"];

        FromBox.Text = recognizedText;

        AcceptButton.Click += (_, _) => Accept();
        CancelButton.Click += (_, _) => Close();

        // O foco vai para a correção: o outro campo já está preenchido.
        Opened += (_, _) => ToBox.Focus();
    }

    /// <summary>Chamado quando um par foi de fato acrescentado, para a aplicação reagir.</summary>
    public Action? Accepted { get; set; }

    private void Accept()
    {
        string from = FromBox.Text ?? "";
        string to = ToBox.Text ?? "";

        // Um par com origem vazia nunca casaria com nada, e um par de origem igual ao
        // destino é a correção que não corrige. Nenhum dos dois vira entrada de arquivo.
        if (from.Trim().Length == 0 || from == to)
        {
            StatusText.Text = _loc["dict.invalid"];
            return;
        }

        try
        {
            CorrectionDictionary.AppendToFile(_filePath, from, to);

            // RF-537 — o dicionário é RECARREGADO: a correção vale a partir do próximo
            // ciclo, sem reiniciar e sem passar por "aplicar".
            _dictionary?.Add(from, to);

            Accepted?.Invoke();
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = _loc.Format("msg.error", ex.Message);
        }
    }
}
