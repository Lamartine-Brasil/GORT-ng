using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Gort.Core.Structuring;

namespace Gort.App.Windows;

/// <summary>
/// 19.2 — Janela de tradução em MODO ESCURO.
///
/// É o modo mais simples dos três: uma janela retangular com fundo escuro e o texto
/// traduzido numa caixa rolável. Boa para textos longos, e a única que não depende de
/// transparência por pixel nem de "sempre no topo" — por isso é também a alternativa
/// oferecida onde o sistema não garante essas capacidades (RF-568).
/// </summary>
public partial class DarkTranslationWindow : Window, ITranslationWindow
{
    public DarkTranslationWindow()
    {
        InitializeComponent();

        // RF-331 — a janela pode ser arrastada por QUALQUER ponto do seu corpo, não só
        // pela barra de título: ela vive sobre um jogo, onde a barra costuma estar fora
        // de alcance.
        PointerPressed += OnPointerPressed;
    }

    /// <summary>RF-326 — Fechar a janela apenas a OCULTA; ela volta pelo controle remoto.</summary>
    public bool HideInsteadOfClose { get; set; } = true;

    /// <summary>RF-497 — Exibir o texto reconhecido junto da tradução.</summary>
    public bool ShowRecognizedText { get; set; } = true;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (HideInsteadOfClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    /// <summary>
    /// RF-328 — Quando a exibição do texto reconhecido está ativa, mostra a tradução, DUAS
    /// quebras de linha, o prefixo "OCR : " e o texto reconhecido.
    /// RF-329 — As quebras recebidas em qualquer formato são normalizadas antes de exibir.
    /// </summary>
    public void Show(string translated, string recognized)
    {
        string text = TextPostProcessor.ComposeDarkModeText(
            translated, recognized, ShowRecognizedText);

        Body.Text = TextPostProcessor.NormalizeNewlines(text);
        Scroller.Offset = new Avalonia.Vector(0, 0);
    }

    public void Clear() => Body.Text = "";

    /// <summary>
    /// RF-196 — No modo escuro o repintar ocioso é inócuo: não há geometria derivada da
    /// posição das áreas de OCR para reposicionar, ao contrário da sobreposição.
    /// </summary>
    public void Repaint() { }

    /// <summary>
    /// RF-327 — Indicador visível de "parado". Fica à vista porque, no modo escuro, uma
    /// janela sem texto novo é indistinguível de uma janela que parou de traduzir.
    /// </summary>
    public void SetRunning(bool running)
    {
        StatusText.Text = running ? "traduzindo" : "parado";
        StatusDot.Fill = running
            ? new SolidColorBrush(Color.FromRgb(0x4C, 0xC0, 0x60))
            : new SolidColorBrush(Color.FromRgb(0xF0, 0xA0, 0x30));
    }

    /// <summary>RF-319 / RF-320 — Estado "sempre no topo".</summary>
    public void SetAlwaysOnTop(bool value) => Topmost = value;

    /// <summary>
    /// RF-330 — Fonte própria do modo escuro, configurável nas opções avançadas, com queda
    /// para a fonte padrão do sistema quando não configurada.
    /// </summary>
    public void SetFont(string? family, double size)
    {
        if (!string.IsNullOrWhiteSpace(family))
            Body.FontFamily = new FontFamily(family);

        if (size > 0) Body.FontSize = size;
    }

    /// <summary>RF-323 — Ordenação do texto: à esquerda ou centralizado.</summary>
    public void SetTextAlignment(TextAlignment alignment) => Body.TextAlignment = alignment;
}

/// <summary>
/// RF-317 / RF-318 — O que as três janelas de tradução têm em comum. Trocar de modo destrói
/// a janela anterior e cria a nova; o resto do programa fala apenas com este contrato.
/// </summary>
public interface ITranslationWindow
{
    void Show(string translated, string recognized);
    void Clear();
    void SetRunning(bool running);
    void SetAlwaysOnTop(bool value);

    /// <summary>
    /// RF-196 / RF-197 — Repintar OCIOSO: o texto não mudou, mas a geometria pode ter
    /// mudado — o usuário moveu a área de OCR, ou a janela alvo se moveu. Reutiliza os
    /// dados já calculados; NÃO dispara OCR, tradução nem análise de cor.
    /// </summary>
    void Repaint();
}
