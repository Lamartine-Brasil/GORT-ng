using Avalonia.Media;
using Gort.App.Windows;

namespace Gort.App;

/// <summary>
/// RF-007 — Verificação, na inicialização, de que a biblioteca gráfica de desenho de texto
/// vetorial funciona.
///
/// A cadeia de teste tem caracteres LATINOS e JAPONESES de propósito: é comum a construção
/// do caminho funcionar para o alfabeto latino e falhar para escritas que exigem outra
/// fonte, e é exatamente nessas que o produto vive.
///
/// Se falhar, o desenho vetorial é desativado em TODO o programa — o texto passa a ser
/// desenhado simples, sem contorno — o usuário é avisado, e o link da solução conhecida é
/// oferecido.
/// </summary>
public static class VectorTextCheck
{
    /// <summary>Cadeia com latino, japonês e numerais.</summary>
    public const string Probe = "Aa1 こんにちは 日本語";

    /// <summary>
    /// Executa a verificação e devolve a mensagem de falha, ou null quando tudo funciona.
    /// Precisa rodar na thread de interface.
    /// </summary>
    public static string? Run()
    {
        try
        {
            var formatted = new FormattedText(
                Probe,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                Gort.Core.Calibration.P.DefaultFontSize,
                Brushes.White);

            var geometry = formatted.BuildGeometry(new Avalonia.Point(0, 0));

            if (geometry is null)
            {
                return "A biblioteca gráfica não conseguiu converter texto em caminho vetorial.";
            }

            // Um caminho sem extensão significa que nada seria desenhado — falha silenciosa,
            // que é justamente a que este requisito existe para pegar.
            var bounds = geometry.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return "A conversão de texto em caminho vetorial produziu um contorno vazio.";
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"Falha ao desenhar texto vetorial: {ex.Message}";
        }
    }

    /// <summary>
    /// RF-007 — Aplica o resultado: desativa o desenho vetorial em todo o programa quando a
    /// verificação falha, e devolve o aviso a exibir.
    /// </summary>
    public static string? Apply()
    {
        string? failure = Run();

        LayerTextSurface.VectorTextAvailable = failure is null;

        if (failure is null) return null;

        return failure + " O contorno do texto foi desativado; a tradução continua legível, " +
               "mas sem a moldura que a destaca do fundo.";
    }
}
