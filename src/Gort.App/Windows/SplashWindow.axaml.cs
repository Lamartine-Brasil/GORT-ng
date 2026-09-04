using System.Reflection;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Gort.App.Windows;

/// <summary>
/// RF-004 / RF-005 — Tela de abertura.
///
/// Exibe o número da versão e a data de compilação, e fica visível enquanto a inicialização
/// acontece: verificação de atualização, configurações padrão remotas, enumeração dos
/// idiomas de OCR disponíveis e carregamento do perfil do usuário.
///
/// P-01 é o tempo em que ela permanece antes de começar a desaparecer, e P-02 a duração do
/// desvanecimento. A coluna de efeito de P-01 avisa o que está em jogo: diminuí-lo faz as
/// verificações da inicialização não terminarem a tempo.
/// </summary>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        VersionText.Text = $"versão {Version} · compilado em {BuildDate:dd/MM/yyyy}";
    }

    /// <summary>Versão do programa, lida do próprio assembly.</summary>
    public static string Version
        => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>
    /// Data de compilação. Vem do carimbo de tempo do arquivo do assembly: é o dado que
    /// existe sem exigir um passo de geração de código na compilação.
    /// </summary>
    public static DateTime BuildDate
    {
        get
        {
            try
            {
                string path = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return File.GetLastWriteTime(path);
            }
            catch
            {
                // P8 — sem data, a tela de abertura ainda abre.
            }
            return DateTime.Now;
        }
    }

    /// <summary>Descreve o passo corrente da inicialização (RF-005).</summary>
    public void ReportStep(string step)
        => Dispatcher.UIThread.Post(() => StepText.Text = step);

    /// <summary>
    /// RF-004 — Mantém a tela por P-01 e depois a remove com um desvanecimento de P-02.
    ///
    /// A espera é do TEMPO RESTANTE: se a inicialização já consumiu P-01, a tela sai na
    /// hora, em vez de somar as duas esperas e atrasar a abertura sem motivo.
    /// </summary>
    public async Task FadeOutAsync(TimeSpan elapsed)
    {
        var remaining = Gort.Core.Calibration.P.SplashHold - elapsed;
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining);

        var fade = Gort.Core.Calibration.P.SplashFade;
        var watch = System.Diagnostics.Stopwatch.StartNew();

        while (watch.Elapsed < fade)
        {
            double progress = watch.Elapsed.TotalMilliseconds / fade.TotalMilliseconds;
            Opacity = Math.Clamp(1 - progress, 0, 1);
            await Task.Delay(16);
        }

        Close();
    }
}
