using Gort.Core.Configuration;
using Gort.Core.Structuring;

namespace Gort.Core.Auxiliary;

/// <summary>
/// Cap. 24 — Tradução da área de transferência.
///
/// Traduz texto copiado por OUTROS programas, permitindo uso em conjunto com extratores de
/// texto. Não usa captura de tela nem OCR: entra no pipeline direto na etapa de tradução.
/// </summary>
public sealed class ClipboardTranslationGate
{
    private string _lastTranslated = "";
    private bool _inProgress;

    /// <summary>RF-465 — O recurso só é inicializado quando ligado.</summary>
    public bool Enabled { get; set; }

    /// <summary>RF-470 — Anexar o texto original ao final do resultado.</summary>
    public bool ShowOriginal { get; set; }

    /// <summary>RF-469 — Exibir "detectado — traduzindo" enquanto a tradução ocorre.</summary>
    public bool ShowTranslating { get; set; }

    /// <summary>
    /// RF-467 — A tradução da área de transferência só ocorre quando TODAS valem:
    ///   - o laço de tradução está ocioso;
    ///   - o programa não está em meio a um carregamento ou aplicação de configuração;
    ///   - o modo de janela NÃO é sobreposição;
    ///   - o texto é diferente do último traduzido por esta via.
    ///
    /// A exclusão da sobreposição é estrutural: ela desenha sobre o texto ORIGINAL da tela,
    /// e um texto vindo da área de transferência não tem posição na tela para ser desenhado
    /// em cima.
    ///
    /// RF-468 — Uma tradução em andamento bloqueia novas até terminar.
    /// </summary>
    public bool ShouldTranslate(string text, bool loopIdle, bool busyWithConfiguration,
                                WindowMode windowMode)
    {
        if (!Enabled) return false;
        if (_inProgress) return false;                       // RF-468
        if (!loopIdle) return false;
        if (busyWithConfiguration) return false;
        if (windowMode == WindowMode.Overlay) return false;

        if (string.IsNullOrWhiteSpace(text)) return false;
        return text != _lastTranslated;
    }

    /// <summary>RF-468 — Marca o início; libera-se em <see cref="Finish"/>.</summary>
    public void Begin(string text)
    {
        _inProgress = true;
        _lastTranslated = text;
    }

    public void Finish() => _inProgress = false;

    /// <summary>
    /// RF-472 — Aplicar configurações limpa o estado de "traduzindo pela área de
    /// transferência": uma tradução interrompida pela reconfiguração não pode deixar o
    /// recurso travado.
    /// </summary>
    public void Reset()
    {
        _inProgress = false;
        _lastTranslated = "";
    }

    /// <summary>
    /// RF-470 — Monta o resultado, com o texto original anexado ao final, separado por DUAS
    /// quebras de linha, quando a opção está ativa.
    /// </summary>
    public string Compose(string translated, string original)
        => ShowOriginal && !string.IsNullOrWhiteSpace(original)
            ? $"{translated}\n\n{original}"
            : translated;

    /// <summary>RF-469 — Mensagem exibida enquanto a tradução ocorre.</summary>
    public const string TranslatingMessage = "detectado — traduzindo";
}

/// <summary>
/// RF-473 a RF-475 — Cópia do resultado de cada ciclo para a área de transferência.
///
/// É independente do monitoramento: um serve para trazer texto de fora, o outro para levar
/// o resultado para fora.
/// </summary>
public sealed class ClipboardWriter
{
    /// <summary>RF-473 — A cópia está ligada.</summary>
    public bool Enabled { get; set; }

    /// <summary>RF-473 — Um dos três formatos selecionáveis.</summary>
    public ClipboardCopyFormat Format { get; set; } = ClipboardCopyFormat.Ocr;

    /// <summary>
    /// RF-475 — Enquanto o editor de dicionário está aberto, a cópia automática fica
    /// SUSPENSA, porque o usuário vai usar a área de transferência para editar.
    /// </summary>
    public bool Suspended { get; set; }

    /// <summary>
    /// RF-474 — A cópia ocorre somente quando o texto MUDOU. A verificação de que a área de
    /// transferência está livre, e o silêncio diante de falhas de acesso, ficam com quem
    /// escreve de fato (C14).
    /// </summary>
    public bool ShouldCopy() => Enabled && !Suspended;

    /// <summary>RF-473 — O que vai para a área de transferência, conforme o formato.</summary>
    public string Compose(string recognized, string translated) => Format switch
    {
        ClipboardCopyFormat.Ocr => recognized,
        ClipboardCopyFormat.Translation => translated,
        _ => $"{recognized}\n\n{translated}",
    };
}
