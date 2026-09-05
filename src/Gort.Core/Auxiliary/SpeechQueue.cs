using Gort.Core.Calibration;
using Gort.Core.Structuring;

namespace Gort.Core.Auxiliary;

/// <summary>
/// Cap. 25 — Leitura em voz alta.
///
/// Lê o resultado de cada ciclo em áudio, para quem prefere ouvir a tradução em vez de
/// desviar o olhar do jogo. Não altera o texto exibido.
/// </summary>
public sealed class SpeechQueue
{
    private readonly Func<bool> _isSpeaking;

    public SpeechQueue(Func<bool>? isSpeaking = null) => _isSpeaking = isSpeaking ?? (() => false);

    /// <summary>RF-476 — O recurso está ligado.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// RF-477 — "Aguardar o fim da leitura anterior": quando ativa, uma nova leitura é
    /// DESCARTADA se a anterior ainda está tocando; quando inativa, a nova INTERROMPE a
    /// anterior.
    /// </summary>
    public bool WaitForPrevious { get; set; }

    /// <summary>
    /// RF-480 — Sem sintetizador disponível, a opção fica inerte SEM gerar erro.
    /// </summary>
    public bool SynthesizerAvailable { get; set; } = true;

    /// <summary>O que fazer com um texto novo.</summary>
    public enum Decision
    {
        /// <summary>Não ler.</summary>
        Skip,
        /// <summary>Ler, interrompendo o que estiver tocando.</summary>
        SpeakInterrupting,
        /// <summary>Ler; nada está tocando.</summary>
        Speak,
    }

    /// <summary>
    /// RF-477 / RF-479 / RF-480 — Decide o que fazer com o texto do ciclo.
    ///
    /// RF-479 — A leitura ocorre apenas quando o texto MUDOU; quem garante isso é a detecção
    /// de mudança, que só chama este caminho no ciclo completo (RF-194).
    /// </summary>
    public Decision Decide(string text)
    {
        if (!Enabled || !SynthesizerAvailable) return Decision.Skip;
        if (string.IsNullOrWhiteSpace(text)) return Decision.Skip;

        if (!_isSpeaking()) return Decision.Speak;

        // Algo já está tocando.
        return WaitForPrevious ? Decision.Skip : Decision.SpeakInterrupting;
    }

    /// <summary>
    /// RF-478 — No modo sobreposição, os tokens separadores são removidos do texto antes da
    /// leitura: eles são um artefato do protocolo de lote e seriam lidos em voz alta.
    /// </summary>
    public static string Clean(string text, WindowMode mode, string separatorToken)
    {
        if (mode != WindowMode.Overlay || separatorToken.Length == 0) return text;

        return text.Replace(separatorToken, " ").Trim();
    }
}
