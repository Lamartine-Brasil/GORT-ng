using Gort.Core.Calibration;
using Gort.Core.Pipeline;
using Gort.Core.Regions;

namespace Gort.Engine;

/// <summary>
/// RF-008 — Os TRÊS estados do laço. Nenhuma outra combinação é observável de fora.
/// </summary>
public enum LoopState
{
    /// <summary>Nenhuma thread de tradução viva.</summary>
    Idle,
    /// <summary>Thread viva, executando ciclos.</summary>
    Processing,
    /// <summary>Parada pedida, thread ainda viva.</summary>
    Stopping,
}

/// <summary>Modo de execução do laço.</summary>
public enum LoopMode
{
    /// <summary>Tradução contínua: ciclos separados pelo intervalo de velocidade.</summary>
    Realtime,
    /// <summary>
    /// Modo pontual: um único ciclo. RF-202 — tanto o caminho completo quanto o de texto
    /// igual encerram o laço, porque "traduzir uma vez" tem de parar mesmo que o texto seja
    /// idêntico ao da última vez.
    /// </summary>
    OneShot,
}

/// <summary>Resultado do protocolo de pausa de RF-012.</summary>
public enum ApplyResult
{
    /// <summary>O laço não estava rodando; a mudança foi aplicada direto.</summary>
    AppliedWithoutPause,
    /// <summary>O laço foi parado, a mudança aplicada e o laço retomado.</summary>
    AppliedAndResumed,
    /// <summary>
    /// RF-012 — A thread não parou no prazo: a mudança foi ABORTADA e nada foi aplicado.
    /// </summary>
    Aborted,
}

/// <summary>O que o laço precisa para executar um ciclo e entregar o resultado.</summary>
public sealed class LoopHost
{
    /// <summary>Áreas de captura do ciclo, relidas a cada volta (RF-059).</summary>
    public required Func<BuiltAreas> Areas { get; init; }

    /// <summary>Configuração do ciclo, relida a cada volta.</summary>
    public required Func<CycleSettings> Settings { get; init; }

    /// <summary>
    /// Passos 7 a 13 do fluxo. É uma dependência, e não uma classe concreta, para que o
    /// laço — que é o controle — possa ser exercitado sem tela, sem OCR e sem rede.
    /// </summary>
    public required Func<BuiltAreas, CycleSettings, Task<CycleResult>> RunCycle { get; init; }

    /// <summary>
    /// Passo 6 do fluxo — se não houver janela de tradução viva, o ciclo não faz nada.
    /// </summary>
    public required Func<bool> HasTranslationWindow { get; init; }

    /// <summary>
    /// Passo 18 — despacha o desenho para a thread de INTERFACE. O laço nunca desenha.
    /// </summary>
    public required Action<CycleResult> Draw { get; init; }

    /// <summary>RF-196 — Repintar ocioso: reaproveita os dados, sem OCR nem tradução.</summary>
    public Action? Repaint { get; init; }

    /// <summary>
    /// RF-014 — Erros são registrados e a mensagem é exibida pela thread de interface.
    /// É PROIBIDO abrir diálogo modal a partir da thread do laço (P2).
    /// </summary>
    public Action<string>? ReportError { get; init; }

    /// <summary>Passo 16 — cópia para a área de transferência, quando ativa.</summary>
    public Action<CycleResult>? CopyToClipboard { get; init; }

    /// <summary>Passo 19 — efeitos colaterais: gravação em arquivo e leitura em voz alta.</summary>
    public Action<CycleResult>? SideEffects { get; init; }

    /// <summary>Passo 21 — ao sair do laço, grava os novos pares da memória de resultados.</summary>
    public Action? FlushMemory { get; init; }

    /// <summary>Intervalo entre ciclos, resolvido de P-05 a P-09 pela velocidade escolhida.</summary>
    public required Func<int> CycleIntervalMs { get; init; }

    /// <summary>
    /// RF-491 — "Destravar velocidade": ignora o intervalo entre ciclos e roda o mais rápido
    /// possível. Só no modo de depuração.
    /// </summary>
    public Func<bool>? UnlockSpeed { get; init; }

    /// <summary>Chamado quando o laço termina, para a interface refletir o estado.</summary>
    public Action? Stopped { get; init; }
}

/// <summary>
/// Cap. 9 — Ciclo de vida do laço de tradução.
///
/// RF-009 — O laço roda em uma THREAD DEDICADA e é SÍNCRONO DE PONTA A PONTA dentro dela.
/// Nenhum ponto de espera pode devolver controle antes do fim do ciclo, porque o término da
/// thread é o sinal de parada usado pela interface.
///
/// Motivo, na letra da especificação: "se a thread terminar no primeiro ponto de espera,
/// quem esperava por ela conclui que parou e passa a alterar configuração enquanto o ciclo
/// ainda está rodando."
///
/// É por isso que a tradução, que é assíncrona, é aguardada por SONDAGEM em passos de P-126
/// dentro da thread, em vez de com `await`: o `await` devolveria a thread ao chamador e
/// destruiria a garantia.
/// </summary>
public sealed class TranslationLoop : IDisposable
{
    private readonly LoopHost _host;
    private readonly ChangeDetector _detector;
    private readonly object _gate = new();

    private Thread? _thread;

    /// <summary>
    /// Sinalizador de fim. É `volatile` porque é lido pela thread do laço e escrito pela
    /// thread de interface sem trava — é o único estado compartilhado no caminho quente.
    /// </summary>
    private volatile bool _endRequested;

    private LoopMode _mode = LoopMode.Realtime;

    public TranslationLoop(LoopHost host, ChangeDetector? detector = null)
    {
        _host = host;
        _detector = detector ?? new ChangeDetector();
    }

    /// <summary>RF-008 — O estado observável de fora.</summary>
    public LoopState State
    {
        get
        {
            var thread = _thread;
            if (thread is null || !thread.IsAlive) return LoopState.Idle;
            return _endRequested ? LoopState.Stopping : LoopState.Processing;
        }
    }

    public bool IsRunning => State != LoopState.Idle;

    /// <summary>Modo do laço em execução, usado ao retomar depois de uma pausa.</summary>
    public LoopMode Mode => _mode;

    /// <summary>
    /// RF-013 — Um novo pedido de iniciar primeiro para o laço anterior. Se não conseguir
    /// parar dentro do prazo, o novo laço NÃO é iniciado.
    /// </summary>
    public bool Start(LoopMode mode, TimeSpan? stopTimeout = null)
    {
        lock (_gate)
        {
            if (IsRunning && !StopCore(stopTimeout ?? P.LoopJoinTimeout)) return false;

            _mode = mode;
            _endRequested = false;

            // RF-199 — a memória do texto anterior é local ao laço: ao iniciar de novo ela
            // recomeça vazia, garantindo que o primeiro ciclo sempre desenhe.
            _detector.Reset();

            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "gort-laço-de-tradução",
            };
            _thread.Start();
            return true;
        }
    }

    /// <summary>
    /// RF-010 / RF-011 — Sinaliza o fim e espera a thread terminar por até o prazo dado.
    ///
    /// O prazo é P-03 no caso comum e P-04 quando o pedido vem do interceptador global de
    /// teclado: o sistema remove um gancho de teclado de baixo nível que fique preso por
    /// mais de ~300 ms, o que mataria TODOS os atalhos do programa até reiniciar.
    ///
    /// RF-010 — Se a thread não terminar no prazo, o sinalizador de fim NÃO é revertido.
    /// </summary>
    public bool Stop(TimeSpan? timeout = null)
    {
        lock (_gate) return StopCore(timeout ?? P.LoopJoinTimeout);
    }

    /// <summary>RF-011 / RF-450 — Parada pedida de dentro do interceptador de teclado.</summary>
    public bool StopFromKeyboardHook() => Stop(P.LoopJoinTimeoutFromHook);

    private bool StopCore(TimeSpan timeout)
    {
        var thread = _thread;
        if (thread is null || !thread.IsAlive)
        {
            _endRequested = false;
            return true;
        }

        _endRequested = true;

        if (!thread.Join(timeout))
        {
            // RF-010 — a thread não morreu: o sinalizador PERMANECE ativo, para que o
            // próximo pedido tente de novo em vez de deixar dois laços vivos.
            return false;
        }

        _thread = null;
        _endRequested = false;
        return true;
    }

    /// <summary>
    /// RF-012 — Protocolo de pausa: cancelar tradução em curso, parar a thread, executar a
    /// mudança, recriar a thread se ela estava viva.
    ///
    /// Segue o pseudocódigo do capítulo 9 literalmente, com uma diferença deliberada: o
    /// resultado distingue "aplicado sem pausa" de "abortado", porque RF-012 exige que o
    /// chamador seja informado de que NADA FOI APLICADO — e o pseudocódigo devolve falso
    /// nos dois casos.
    /// </summary>
    public ApplyResult PauseAndResume(Action action, TimeSpan? timeout = null)
    {
        lock (_gate)
        {
            var thread = _thread;
            bool needsResume = thread is not null && thread.IsAlive;

            if (needsResume)
            {
                if (!StopCore(timeout ?? P.LoopJoinTimeout))
                {
                    // A mudança é abortada; o sinalizador de fim permanece ativo (RF-010).
                    return ApplyResult.Aborted;
                }
            }

            action();

            if (!needsResume) return ApplyResult.AppliedWithoutPause;

            _endRequested = false;
            _detector.Reset();
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "gort-laço-de-tradução",
            };
            _thread.Start();
            return ApplyResult.AppliedAndResumed;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // O laço
    // ─────────────────────────────────────────────────────────────────────────

    private void Run()
    {
        try
        {
            RunCycles();
        }
        catch (Exception ex)
        {
            // RF-014 / RF-563 — nenhuma exceção escapa da thread do laço. Ela é registrada
            // e a mensagem é exibida pela thread de interface; o laço termina limpo.
            _host.ReportError?.Invoke(ex.Message);
        }
        finally
        {
            // Passo 21 — ao sair do laço, os novos pares da memória de resultados vão para
            // o disco.
            try { _host.FlushMemory?.Invoke(); } catch { /* P8 */ }
            try { _host.Stopped?.Invoke(); } catch { /* P8 */ }
        }
    }

    private void RunCycles()
    {
        var lastCycle = DateTime.MinValue;

        while (!_endRequested)
        {
            // Passo 5 — se o intervalo ainda não passou, dorme P-125 e volta ao passo 5.
            // A espera é granular de propósito: um sono do tamanho do intervalo inteiro
            // faria a parada demorar o intervalo inteiro.
            bool unlocked = _host.UnlockSpeed?.Invoke() ?? false;
            if (!unlocked)
            {
                var elapsed = DateTime.UtcNow - lastCycle;
                if (elapsed < TimeSpan.FromMilliseconds(_host.CycleIntervalMs()))
                {
                    Thread.Sleep(P.IdleLoopSleepMs);
                    continue;
                }
            }
            lastCycle = DateTime.UtcNow;

            // Passo 6 — sem janela de tradução viva, o ciclo não faz nada.
            if (!_host.HasTranslationWindow())
            {
                Thread.Sleep(P.IdleLoopSleepMs);
                continue;
            }

            var areas = _host.Areas();
            var settings = _host.Settings();

            // Passos 7 a 13. A tradução é assíncrona; o laço a aguarda por SONDAGEM, para
            // não devolver a thread (RF-009).
            var task = _host.RunCycle(areas, settings);
            var result = WaitFor(task);

            if (result is null) return;          // pedido de parada durante a espera
            if (_endRequested) return;

            // Passo 15 — detecção de mudança.
            var decision = _detector.Evaluate(result.RecognizedText);

            if (decision == ChangeDecision.FullRedraw)
            {
                // Passo 16 — cópia para a área de transferência.
                try { _host.CopyToClipboard?.Invoke(result); } catch { /* P8 */ }

                // Passos 17 e 18 — memória de exibição e desenho, na thread de interface.
                _host.Draw(result);

                // Passo 19 — efeitos colaterais.
                try { _host.SideEffects?.Invoke(result); } catch { /* P8 */ }
            }
            else if (decision == ChangeDecision.IdleRepaint)
            {
                // RF-197 — o repintar ocioso reutiliza os dados já calculados; não dispara
                // OCR, tradução nem análise de cor.
                try { _host.Repaint?.Invoke(); } catch { /* P8 */ }
            }

            // RF-202 — em modo pontual, TANTO o caminho completo quanto o de texto igual
            // encerram o laço.
            if (_mode == LoopMode.OneShot) return;
        }
    }

    /// <summary>
    /// Passos 7, 9 e 12 — espera com verificação periódica do pedido de parada.
    ///
    /// Devolve null quando a parada foi pedida antes de o resultado chegar. A sondagem é de
    /// P-126, que é o que faz "parar" nunca demorar mais que o limite estabelecido
    /// (RF-551).
    /// </summary>
    private CycleResult? WaitFor(Task<CycleResult> task)
    {
        while (!task.IsCompleted)
        {
            if (_endRequested) return null;
            task.Wait(P.StopCheckIntervalMs);
        }

        if (task.IsFaulted)
        {
            // RF-014 — o erro é do ciclo, não da thread: ele vira mensagem e o laço decide
            // se continua.
            _host.ReportError?.Invoke(task.Exception?.GetBaseException().Message ?? "falha no ciclo");
            return CycleResult.Empty;
        }

        return task.IsCanceled ? null : task.Result;
    }

    public void Dispose() => Stop();
}
