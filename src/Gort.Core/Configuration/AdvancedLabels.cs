using System.Globalization;

namespace Gort.Core.Configuration;

/// <summary>
/// RF-526 — Como os três controles deslizantes do modelo de linguagem se apresentam.
///
/// Os três guardam INTEIROS — é o que um controle deslizante manipula — mas nenhum deles se
/// apresenta como o inteiro que guarda. Esta classe é o único lugar onde essa diferença
/// existe, para que a interface não invente a sua própria.
/// </summary>
public static class AdvancedLabels
{
    /// <summary>
    /// RF-526 — A temperatura é exibida DIVIDIDA POR 100, com duas casas decimais.
    ///
    /// O valor guardado é 0 a 100 porque é o que um controle deslizante move em passos
    /// inteiros; o valor que o modelo entende é 0,00 a 1,00. A divisão é de apresentação, e
    /// mora aqui — não no perfil, nem no serviço.
    ///
    /// A cultura corrente é respeitada: quem usa vírgula decimal lê "0,20".
    /// </summary>
    public static string Temperature(int value)
        => (value / 100.0).ToString("F2", CultureInfo.CurrentCulture);

    /// <summary>
    /// RF-526 — O nível de raciocínio é exibido como TEXTO LOCALIZADO, um por nível.
    ///
    /// A chave é derivada do número, e não escolhida por um `switch`: acrescentar um nível
    /// passa a ser acrescentar uma linha na tabela de localização (RF-481).
    /// </summary>
    public static string ThinkingKey(int level) => $"llm.thinking.{level}";

    /// <summary>RF-526 — O limite de saída é exibido como o número que é.</summary>
    public static string MaxOutput(int value)
        => value.ToString(CultureInfo.CurrentCulture);
}
