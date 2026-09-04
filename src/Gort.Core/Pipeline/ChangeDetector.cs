using Gort.Core.Calibration;

namespace Gort.Core.Pipeline;

/// <summary>Decisão devolvida pela detecção de mudança (cap. 16).</summary>
public enum ChangeDecision
{
    /// <summary>
    /// RF-194 — Caminho completo: cópia para a área de transferência, memória de exibição,
    /// desenho, gravação em arquivo e leitura em voz alta.
    /// </summary>
    FullRedraw,

    /// <summary>
    /// RF-196 — Texto igual, mas passou o intervalo P-47: força um repintar sem recalcular
    /// nada, porque a GEOMETRIA pode ter mudado (o usuário moveu a área, ou a janela alvo
    /// se moveu).
    /// </summary>
    IdleRepaint,

    /// <summary>RF-195 — Texto igual: nada a fazer.</summary>
    Nothing,
}

/// <summary>
/// Cap. 16 — Detecção de mudança entre quadros. 🔒
///
/// É o que permite rodar um laço de 300 ms sem torrar CPU nem estourar a cota dos serviços
/// de tradução.
///
/// RF-192 — O programa NÃO compara imagens entre quadros. A comparação é sobre o TEXTO
/// RECONHECIDO CONCATENADO de todas as áreas. Comparar pixels sinalizaria mudança a cada
/// animação, cursor piscando ou gradiente de fundo; comparar texto sinaliza apenas quando
/// o conteúdo mudou de verdade.
///
/// RF-200 — Assim que o texto difere, retraduz e redesenha NO MESMO CICLO. É PROIBIDO
/// exigir confirmação em um segundo quadro, aplicar média entre quadros, aguardar
/// estabilização ou qualquer outro amortecimento (ver também Parte XI, item 13). Cada uma
/// dessas técnicas acrescenta no mínimo um ciclo inteiro de latência entre o texto aparecer
/// e a tradução aparecer, e é exatamente essa latência que define se o produto acompanha um
/// diálogo que passa. A instabilidade eventual do OCR é o preço aceito por essa velocidade;
/// ela se corrige no pré-processamento, nunca no tempo.
///
/// RF-199 — A memória do texto anterior é LOCAL AO LAÇO: ao parar e iniciar de novo ela
/// recomeça vazia, garantindo que o primeiro ciclo sempre desenhe.
/// </summary>
public sealed class ChangeDetector
{
    private readonly Func<DateTime> _now;
    private string _previous = "";
    private DateTime _lastIdleRepaint = DateTime.MinValue;

    public ChangeDetector(Func<DateTime>? now = null) => _now = now ?? (() => DateTime.UtcNow);

    /// <summary>O texto do ciclo anterior. Atualizado apenas no caminho completo (RF-198).</summary>
    public string Previous => _previous;

    /// <summary>
    /// RF-193 — A comparação é de IGUALDADE EXATA de cadeia, sobre o texto DEPOIS do
    /// tratamento textual (remoção de espaços, dicionário, junção de linhas) e ANTES da
    /// tradução.
    /// </summary>
    /// <param name="currentText">Texto reconhecido tratado, concatenado de todas as áreas.</param>
    public ChangeDecision Evaluate(string currentText)
    {
        // RF-194 — texto diferente OU texto vazio executa o caminho completo.
        // Tratar vazio como mudança é deliberado: quando o diálogo some, a tradução
        // precisa sumir junto.
        if (currentText != _previous || currentText.Length == 0)
        {
            _previous = currentText;   // RF-198 — só aqui a memória é atualizada.
            return ChangeDecision.FullRedraw;
        }

        // RF-195 — texto igual: não redesenha o conteúdo nem repete efeitos colaterais.
        // RF-196 — mas se passou mais de P-47 desde o último repintar ocioso, força um
        // repintar nos modos camada e sobreposição.
        var now = _now();
        if (now - _lastIdleRepaint >= P.IdleRepaintInterval)
        {
            _lastIdleRepaint = now;
            return ChangeDecision.IdleRepaint;
        }

        return ChangeDecision.Nothing;
    }

    /// <summary>
    /// RF-199 — Reinicia a memória. Chamado ao iniciar o laço, nunca durante ele.
    /// </summary>
    public void Reset()
    {
        _previous = "";
        _lastIdleRepaint = DateTime.MinValue;
    }

    /// <summary>
    /// RF-205 — Quando o laço não consegue detectar mudança porque o motor de OCR não
    /// estava pronto, o texto do ciclo anterior é reutilizado, produzindo "nenhuma mudança"
    /// e portanto nenhum trabalho (RF-139).
    /// </summary>
    public string TextWhenEngineNotReady() => _previous;
}

/// <summary>
/// RF-203 / RF-204 — Segunda camada de descarte, na janela de sobreposição. 🔒
///
/// Por área, guarda o último retângulo de área, posição de cliente, texto reconhecido e
/// texto traduzido. Se TODOS forem idênticos ao registro, o objeto de resultado ANTERIOR é
/// reutilizado no desenho, preservando os retângulos já calculados; apenas as cores
/// automáticas são substituídas pelas novas.
///
/// Motivo: evita recalcular todo o layout e faz a sobreposição parar de tremer entre
/// quadros. É uma das otimizações obrigatórias de RF-550.
/// </summary>
public sealed class OverlayReuseCache
{
    private readonly Dictionary<int, Entry> _byArea = new();

    private sealed record Entry(
        Model.Rect AreaRect,
        (int X, int Y) ClientOrigin,
        string Recognized,
        string Translated,
        Model.RegionResult Result);

    /// <summary>
    /// Devolve o resultado anterior quando nada mudou para esta área, ou null quando o
    /// layout precisa ser recalculado.
    /// </summary>
    public Model.RegionResult? TryReuse(int areaIndex, Model.Rect areaRect,
                                        (int X, int Y) clientOrigin,
                                        string recognized, string translated)
    {
        if (_byArea.TryGetValue(areaIndex, out var e)
            && e.AreaRect == areaRect
            && e.ClientOrigin == clientOrigin
            && e.Recognized == recognized
            && e.Translated == translated)
        {
            return e.Result;
        }
        return null;
    }

    public void Store(int areaIndex, Model.Rect areaRect, (int X, int Y) clientOrigin,
                      string recognized, string translated, Model.RegionResult result)
        => _byArea[areaIndex] = new Entry(areaRect, clientOrigin, recognized, translated, result);

    /// <summary>
    /// RF-204 — Registros de áreas que não apareceram no ciclo atual são removidos.
    /// </summary>
    public void RetainOnly(IEnumerable<int> areaIndices)
    {
        var keep = new HashSet<int>(areaIndices);
        foreach (var key in _byArea.Keys.Where(k => !keep.Contains(k)).ToList())
            _byArea.Remove(key);
    }

    public void Clear() => _byArea.Clear();
}
