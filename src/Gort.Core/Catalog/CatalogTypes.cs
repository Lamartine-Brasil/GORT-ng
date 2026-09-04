namespace Gort.Core.Catalog;

/// <summary>
/// RF-308 / RF-311 — Um idioma da tabela. As PROPRIEDADES (e não o identificador)
/// governam os comportamentos automáticos de RF-148 e RF-324 (RF-567).
/// </summary>
public sealed class LanguageInfo
{
    public required string Key { get; init; }
    public required string NameKey { get; init; }

    /// <summary>Código usado pelos motores de OCR.</summary>
    public required string OcrCode { get; init; }

    /// <summary>
    /// Código por serviço de tradução. Ausente ou vazio significa que aquele serviço
    /// não oferece este idioma, e o idioma não aparece na lista dele (RF-308, RF-511).
    /// </summary>
    public required IReadOnlyDictionary<string, string> Codes { get; init; }

    /// <summary>
    /// RF-311 / RF-148 — Quando falso, o pipeline ativa a remoção de espaços e desativa o
    /// dicionário por palavra. É o caso do japonês.
    /// </summary>
    public required bool SeparatesWordsBySpace { get; init; }

    public required bool SupportsVertical { get; init; }

    /// <summary>RF-311 / RF-324 — Liga automaticamente a direção da direita para a esquerda.</summary>
    public required bool RightToLeft { get; init; }

    /// <summary>Código deste idioma para um serviço, ou null quando o serviço não o oferece.</summary>
    public string? CodeFor(string serviceKey)
        => Codes.TryGetValue(serviceKey, out var code) && !string.IsNullOrWhiteSpace(code) ? code : null;

    public override string ToString() => Key;
}

/// <summary>RF-121 — Um motor de OCR e suas características.</summary>
public sealed class OcrEngineInfo
{
    public required string Key { get; init; }
    public required string NameKey { get; init; }
    public required bool NeedsNetwork { get; init; }

    /// <summary>
    /// Devolve posição por palavra. RF-351 — o modo sobreposição só é permitido com
    /// motores que devolvem posição de palavra.
    /// </summary>
    public required bool WordPositions { get; init; }

    public required bool LinePositions { get; init; }

    /// <summary>RF-122 — Falso para o motor de nuvem: não pode ser usado em tempo real.</summary>
    public required bool Realtime { get; init; }

    /// <summary>
    /// Idiomas que o motor sabe reconhecer. RF-151 — a lista oferecida ao usuário é a
    /// interseção desta com os idiomas de origem previstos na tabela.
    /// </summary>
    public required IReadOnlyList<string> Languages { get; init; }

    public override string ToString() => Key;
}

/// <summary>18.2 — Um serviço de tradução e suas características.</summary>
public sealed class TranslationServiceInfo
{
    public required string Key { get; init; }
    public required string NameKey { get; init; }
    public required bool NeedsNetwork { get; init; }

    /// <summary>RF-214 — Falso para banco de dados local e tradutor local por processo auxiliar.</summary>
    public required bool UsesResultMemory { get; init; }

    /// <summary>RF-239 / RF-259 — Tradução ponte, suportada no nível de serviço.</summary>
    public required bool SupportsBridge { get; init; }

    /// <summary>RF-221 — Falso para o banco de dados local: seria consulta duplicada.</summary>
    public required bool UsesCollection { get; init; }

    /// <summary>
    /// RF-232 — Token separador do serviço (P-51 ou P-52). Configurável remotamente
    /// (RF-417); um valor remoto ausente ou vazio mantém este (RF-418).
    /// </summary>
    public string SeparatorToken { get; set; } = Calibration.P.SeparatorToken;

    /// <summary>RF-250 — Serviço que aceita rodízio de múltiplas chaves.</summary>
    public bool MultipleKeys { get; init; }

    /// <summary>
    /// RF-226 — Recurso secundário: nunca é o padrão, nunca é pré-selecionado, e nenhuma
    /// outra parte do programa pode depender dele para funcionar.
    /// </summary>
    public bool Secondary { get; init; }

    public override string ToString() => Key;
}

/// <summary>RF-279 / RF-280 — Configuração de modelos de linguagem, vinda de dados.</summary>
public sealed class LlmCatalog
{
    public required string DefaultModel { get; init; }
    public required IReadOnlyList<string> Models { get; init; }

    /// <summary>RF-280 — Prefixo que identifica a família antiga de configuração de raciocínio.</summary>
    public required string LegacyFamilyPrefix { get; init; }

    /// <summary>RF-280 — Palavra cuja presença no nome deduz o porte "pro".</summary>
    public required string ProMarker { get; init; }

    /// <summary>RF-280 — [INFERIDO] quanto ao critério do prefixo ser suficiente.</summary>
    public bool IsLegacyFamily(string model)
        => model.StartsWith(LegacyFamilyPrefix, StringComparison.OrdinalIgnoreCase);

    public bool IsPro(string model)
        => model.Contains(ProMarker, StringComparison.OrdinalIgnoreCase);
}
