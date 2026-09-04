using Gort.Core.Calibration;

namespace Gort.Core.Rendering;

/// <summary>
/// RF-387 — Resolução da família de fonte do texto traduzido.
///
/// A família NÃO é fixada por nome: usa-se a fonte de interface do sistema operacional e,
/// se ela não estiver disponível, a primeira de uma lista de reserva declarada nos dados.
///
/// Motivo, na letra do requisito: o texto traduzido está sempre no idioma de destino, e
/// nenhum nome de fonte existe nas três plataformas alvo. Fixar um nome faz o programa cair
/// SILENCIOSAMENTE para uma fonte substituta escolhida pelo sistema, que pode ter métricas
/// muito diferentes — e as métricas governam todo o cálculo de tamanho e de quebra de linha
/// da sobreposição (RF-357 a RF-372).
/// </summary>
public static class FontResolution
{
    /// <summary>
    /// Escolhe a família a usar.
    /// </summary>
    /// <param name="configured">
    /// Família escolhida pelo usuário. Vazio significa "resolver em tempo de execução".
    /// </param>
    /// <param name="systemUiFont">Nome da fonte de interface do sistema, se conhecido.</param>
    /// <param name="fallbacks">Lista de reserva vinda dos dados de configuração.</param>
    /// <param name="isAvailable">Verifica se uma família existe neste sistema.</param>
    public static string Resolve(string? configured, string? systemUiFont,
                                 IReadOnlyList<string> fallbacks,
                                 Func<string, bool> isAvailable)
    {
        // A escolha do usuário manda, quando ela existe de fato.
        if (!string.IsNullOrWhiteSpace(configured) && isAvailable(configured))
            return configured;

        if (!string.IsNullOrWhiteSpace(systemUiFont) && isAvailable(systemUiFont))
            return systemUiFont;

        foreach (var candidate in fallbacks)
        {
            if (isAvailable(candidate)) return candidate;
        }

        // Nenhuma das declaradas existe: devolve vazio para que a camada de desenho use o
        // padrão dela. É melhor que devolver um nome que já se sabe inexistente.
        return "";
    }

    /// <summary>P-127 — Tamanho de fonte padrão do texto traduzido.</summary>
    public const double DefaultSize = P.DefaultFontSize;
}

/// <summary>
/// RF-389 a RF-391 — As quatro cores configuráveis do texto traduzido.
/// </summary>
public static class TextColors
{
    /// <summary>RF-390 / P-101 a P-104 — Restaura os padrões.</summary>
    public static (Model.Rgba Text, Model.Rgba Stroke1, Model.Rgba Stroke2, Model.Rgba Background)
        Defaults() => (
            new Model.Rgba(P.DefaultTextColor.R, P.DefaultTextColor.G, P.DefaultTextColor.B),
            new Model.Rgba(P.DefaultStroke1Color.R, P.DefaultStroke1Color.G, P.DefaultStroke1Color.B),
            new Model.Rgba(P.DefaultStroke2Color.R, P.DefaultStroke2Color.G, P.DefaultStroke2Color.B),
            new Model.Rgba(P.DefaultBackgroundColor.R, P.DefaultBackgroundColor.G,
                           P.DefaultBackgroundColor.B, P.DefaultBackgroundColor.A));

    /// <summary>
    /// RF-391 — A caixa de amostra de cor na interface NUNCA exibe componente zero: valores
    /// 0 são exibidos como 1.
    ///
    /// [INFERIDO na especificação] — a razão registrada é evitar que a cor de amostra seja
    /// interpretada como transparente. Vale só para a AMOSTRA; a cor efetiva do desenho não
    /// é alterada.
    /// </summary>
    public static Model.Rgba ForSwatch(Model.Rgba color) => new(
        Math.Max((byte)1, color.R),
        Math.Max((byte)1, color.G),
        Math.Max((byte)1, color.B),
        color.A);
}
