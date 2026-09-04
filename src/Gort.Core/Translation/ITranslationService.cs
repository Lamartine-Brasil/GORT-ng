namespace Gort.Core.Translation;

/// <summary>Parâmetros de uma requisição de tradução, resolvidos pela camada de configuração.</summary>
public sealed class TranslationContext
{
    /// <summary>Código do idioma de origem, no formato do serviço (RF-308).</summary>
    public required string SourceCode { get; init; }

    /// <summary>Código do idioma de destino, no formato do serviço.</summary>
    public required string TargetCode { get; init; }

    /// <summary>
    /// RF-239 — Tradução ponte: traduzir para japonês e do japonês para o destino. Só se
    /// aplica quando o idioma de origem não é japonês e apenas a serviços que a declaram
    /// suportada.
    /// </summary>
    public bool Bridge { get; init; }

    /// <summary>Código do idioma-ponte, quando a tradução ponte está ativa.</summary>
    public string? BridgeCode { get; init; }
}

/// <summary>
/// 6.7 — Contrato de um serviço de tradução.
///
/// Recebe uma lista de textos de origem e devolve a lista de traduções, na MESMA ORDEM e do
/// MESMO TAMANHO, ou uma mensagem de erro única no lugar de tudo.
///
/// NÃO sabe de onde veio o texto nem para onde vai a resposta.
/// </summary>
public interface ITranslationService : IDisposable
{
    /// <summary>RF-026 — Identificador textual estável, o mesmo do catálogo.</summary>
    string Key { get; }

    /// <summary>
    /// Traduz um ÚNICO texto, que pode ser o lote já montado pelo protocolo comum.
    ///
    /// Uma falha devolve <see cref="TranslationOutcome.Failed"/> com a mensagem; o ciclo
    /// continua (RF-236, RF-561).
    /// </summary>
    Task<TranslationOutcome> TranslateAsync(string text, TranslationContext context,
                                            CancellationToken cancellation);
}

/// <summary>Resultado de uma chamada a um serviço.</summary>
public sealed record TranslationOutcome(string Text, string? Error = null, bool Cancelled = false)
{
    public bool IsError => Error is not null;

    public static TranslationOutcome Ok(string text) => new(text);

    /// <summary>RF-236 — A mensagem de erro ocupa o lugar da tradução; o laço continua.</summary>
    public static TranslationOutcome Failed(string message) => new("", message);

    /// <summary>RF-238 — Cancelamento NÃO é erro: sem erro, sem desenho.</summary>
    public static readonly TranslationOutcome CancelledResult = new("", null, true);
}

/// <summary>
/// Resultado do protocolo comum: uma tradução por texto de origem, na mesma ordem, mais a
/// forma concatenada exigida por RF-237.
/// </summary>
public sealed class BatchTranslation
{
    /// <summary>
    /// Uma entrada por texto de origem. Nulo significa "sem tradução", que acontece quando a
    /// resposta trouxe menos partes que os textos enviados — e isso NÃO é erro (RF-233).
    /// </summary>
    public required IReadOnlyList<string?> Translations { get; init; }

    /// <summary>
    /// RF-237 — Concatenação, para cada texto de origem, do token separador, da tradução e
    /// de uma quebra de linha. É o que a janela de tradução guarda como resposta bruta.
    /// </summary>
    public required string Combined { get; init; }

    /// <summary>RF-236 — Quando presente, ocupa o lugar de todas as traduções.</summary>
    public string? Error { get; init; }

    /// <summary>RF-238 — Cancelado: sem erro e sem desenho.</summary>
    public bool Cancelled { get; init; }

    /// <summary>Quantos textos foram efetivamente à rede (os demais vieram de cache).</summary>
    public int NetworkCount { get; init; }
}
