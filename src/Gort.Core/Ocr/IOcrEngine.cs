using Gort.Core.Model;

namespace Gort.Core.Ocr;

/// <summary>
/// 6.4 — Contrato do reconhecimento de texto.
///
/// Recebe: uma imagem em memória com largura, altura e formato de pixel conhecidos, e um
/// código de idioma.
/// Devolve: o resultado estruturado de <see cref="OcrResult"/>.
/// NÃO faz: não agrupa linhas em parágrafos, não corrige texto, não traduz.
/// </summary>
public interface IOcrEngine : IDisposable
{
    /// <summary>
    /// RF-026 / RF-027 — Identificador textual estável, o mesmo do catálogo de dados.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// RF-120 / RF-575 — O programa lista exatamente os motores disponíveis no sistema
    /// atual e NUNCA apresenta um motor que falhará ao ser usado.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Motivo da indisponibilidade, guardado para ser exibido UMA ÚNICA VEZ caso o usuário
    /// tente usar o motor (casos de erro do cap. 14).
    /// </summary>
    string? UnavailableReason { get; }

    /// <summary>
    /// RF-351 — O modo sobreposição só é permitido com motores que devolvem posição de
    /// palavra. Quando falso, a sobreposição fica indisponível para este motor, mas o modo
    /// escuro e o modo camada funcionam normalmente (6.4, degradação).
    /// </summary>
    bool ProvidesWordPositions { get; }

    /// <summary>Idiomas que este motor reconhece, por identificador do catálogo.</summary>
    IReadOnlyList<string> Languages { get; }

    /// <summary>
    /// Reconhece o texto da imagem tratada.
    ///
    /// RF-145 — Erros produzem um resultado marcado como VAZIO, com a mensagem de erro no
    /// campo de texto principal, e o ciclo continua. Esta implementação nunca lança.
    /// </summary>
    OcrResult Recognize(ImageBuffer image, string languageCode);
}

/// <summary>
/// RF-145 — Envoltório que garante o contrato de erro para qualquer motor: nenhuma exceção
/// escapa, e uma falha vira resultado vazio com a mensagem, para que o laço continue
/// (RF-561, P8).
/// </summary>
public sealed class SafeOcrEngine : IOcrEngine
{
    private readonly IOcrEngine _inner;

    public SafeOcrEngine(IOcrEngine inner) => _inner = inner;

    public string Key => _inner.Key;
    public bool IsAvailable => _inner.IsAvailable;
    public string? UnavailableReason => _inner.UnavailableReason;
    public bool ProvidesWordPositions => _inner.ProvidesWordPositions;
    public IReadOnlyList<string> Languages => _inner.Languages;

    public OcrResult Recognize(ImageBuffer image, string languageCode)
    {
        if (!IsAvailable)
            return OcrResult.FromError(UnavailableReason ?? "Motor de OCR indisponível.");

        try
        {
            return _inner.Recognize(image, languageCode);
        }
        catch (Exception ex)
        {
            return OcrResult.FromError(ex.Message);
        }
    }

    public void Dispose() => _inner.Dispose();
}

/// <summary>
/// RF-120 — Registro dos motores de OCR. Lista exatamente os que estão disponíveis no
/// sistema atual (RF-575).
///
/// RF-029 / RF-566 — Acrescentar um motor é acrescentar uma entrada nos dados mais a
/// implementação do adaptador; nada no laço, no agrupamento, no cache ou na renderização
/// muda por causa disso.
/// </summary>
public sealed class OcrEngineRegistry : IDisposable
{
    private readonly List<IOcrEngine> _engines = new();

    /// <summary>Registra um motor, envolvendo-o na garantia de erro de RF-145.</summary>
    public void Register(IOcrEngine engine) => _engines.Add(new SafeOcrEngine(engine));

    /// <summary>Todos os motores conhecidos, disponíveis ou não.</summary>
    public IReadOnlyList<IOcrEngine> All => _engines;

    /// <summary>
    /// RF-120 / RF-575 — Somente os efetivamente disponíveis. É esta lista que a interface
    /// oferece ao usuário.
    /// </summary>
    public IEnumerable<IOcrEngine> Available => _engines.Where(e => e.IsAvailable);

    public IOcrEngine? Find(string key)
        => _engines.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// RF-028 / RF-307 — Um motor salvo no perfil que não exista mais, ou que esteja
    /// indisponível, cai para o primeiro disponível em vez de impedir o funcionamento.
    /// </summary>
    public IOcrEngine? Resolve(string? key)
    {
        var engine = Find(key ?? "");
        if (engine is not null && engine.IsAvailable) return engine;
        return Available.FirstOrDefault();
    }

    public void Dispose()
    {
        foreach (var e in _engines) e.Dispose();
        _engines.Clear();
    }
}
