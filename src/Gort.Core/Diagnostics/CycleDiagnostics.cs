namespace Gort.Core.Diagnostics;

/// <summary>
/// O que o ciclo precisa para se deixar observar: os sinalizadores do modo de depuração, a
/// pasta onde os artefatos vão, e os contadores de RF-498.
///
/// É passado ao ciclo como DEPENDÊNCIA OPCIONAL: quando o modo de depuração está desligado
/// ele não existe, e o caminho quente não paga nada por ele — RF-490 exige que desativar o
/// modo restaure o comportamento normal sem reiniciar.
/// </summary>
public sealed class CycleDiagnostics
{
    public required DebugOptions Options { get; init; }

    /// <summary>RF-492 — Pasta dedicada aos artefatos de diagnóstico.</summary>
    public required string Directory { get; init; }

    /// <summary>RF-498 — Contadores de tentativas de OCR e de traduções.</summary>
    public DiagnosticCounters? Counters { get; init; }

    /// <summary>
    /// Grava uma imagem, quando o sinalizador correspondente está ligado.
    ///
    /// RF-500 pede que os sinalizadores de "salvar captura" e "salvar resultado da captura"
    /// sejam repassados ao mecanismo de pré-processamento. Aqui o pré-processamento é
    /// gerenciado, e não uma biblioteca nativa: quem honra os sinalizadores é o próprio
    /// ciclo. O EFEITO observável — as imagens na pasta de diagnóstico — é o mesmo, que é o
    /// que o requisito quer.
    /// </summary>
    public Action<string, object>? SaveImage { get; init; }

    public void Log(string message) => Counters?.Log(message);
}
