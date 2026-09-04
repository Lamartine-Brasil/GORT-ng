using Gort.Core.Calibration;
using Gort.Core.Model;
using Gort.Core.Regions;
using Gort.Engine;
using Xunit;

namespace Gort.Engine.Tests;

/// <summary>
/// Cap. 9 — Ciclo de vida do laço. Todos os critérios de aceite do capítulo, mais o
/// protocolo de pausa de RF-012.
/// </summary>
public class TranslationLoopTests
{
    /// <summary>Constrói um laço com um ciclo controlado pelo caso de teste.</summary>
    private static (TranslationLoop Loop, LoopProbe Probe) Build(
        Func<LoopProbe, Task<CycleResult>>? cycle = null, int intervalMs = 0)
    {
        var probe = new LoopProbe();

        var host = new LoopHost
        {
            Areas = () => probe.Areas,
            Settings = () => null!,
            RunCycle = (_, _) =>
            {
                probe.Cycles++;
                return cycle is not null ? cycle(probe) : Task.FromResult(probe.NextResult());
            },
            HasTranslationWindow = () => probe.HasWindow,
            Draw = r => { probe.Drawn.Add(r.RecognizedText); probe.Draws++; },
            Repaint = () => probe.Repaints++,
            ReportError = m => probe.Errors.Add(m),
            CopyToClipboard = _ => probe.Copies++,
            SideEffects = _ => probe.SideEffects++,
            FlushMemory = () => probe.Flushes++,
            CycleIntervalMs = () => intervalMs,
            Stopped = () => probe.Stops++,
        };

        return (new TranslationLoop(host), probe);
    }

    private sealed class LoopProbe
    {
        public BuiltAreas Areas { get; } = new()
        {
            Captures = new[] { new Rect(0, 0, 100, 50) },
            Exclusions = Array.Empty<Rect>(),
            ColorGroups = new[] { (IReadOnlyList<bool>)new[] { true } },
            PersistedAreas = new[] { new Rect(0, 0, 100, 50) },
        };

        public bool HasWindow { get; set; } = true;
        public int Cycles, Draws, Repaints, Copies, SideEffects, Flushes, Stops;
        public List<string> Drawn { get; } = new();
        public List<string> Errors { get; } = new();
        public List<string> Texts { get; set; } = new() { "texto" };
        private int _next;

        public CycleResult NextResult()
        {
            string text = Texts[Math.Min(_next++, Texts.Count - 1)];
            return new CycleResult
            {
                Regions = Array.Empty<RegionResult>(),
                RecognizedText = text,
                DisplayText = text,
            };
        }
    }

    private static void WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var limit = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < limit && !condition()) Thread.Sleep(5);
    }

    // ── Estados (RF-008) ─────────────────────────────────────────────────────

    [Fact]
    public void RF_008_o_laco_comeca_ocioso()
    {
        var (loop, _) = Build();
        Assert.Equal(LoopState.Idle, loop.State);
        Assert.False(loop.IsRunning);
    }

    [Fact]
    public void RF_008_iniciar_leva_a_processando_e_parar_devolve_a_ocioso()
    {
        var (loop, probe) = Build(intervalMs: 10);

        Assert.True(loop.Start(LoopMode.Realtime));
        WaitUntil(() => probe.Cycles > 0);
        Assert.Equal(LoopState.Processing, loop.State);

        Assert.True(loop.Stop());
        Assert.Equal(LoopState.Idle, loop.State);
    }

    /// <summary>
    /// RF-202 — Em modo pontual o laço executa um ciclo e para sozinho, mesmo quando o
    /// texto é idêntico ao da última vez.
    /// </summary>
    [Fact]
    public void RF_202_o_modo_pontual_executa_um_ciclo_e_para()
    {
        var (loop, probe) = Build();

        loop.Start(LoopMode.OneShot);
        WaitUntil(() => loop.State == LoopState.Idle);

        Assert.Equal(1, probe.Cycles);
        Assert.Equal(1, probe.Draws);
        Assert.Equal(LoopState.Idle, loop.State);
    }

    [Fact]
    public void RF_202_o_modo_pontual_para_mesmo_com_texto_igual()
    {
        var (loop, probe) = Build();

        loop.Start(LoopMode.OneShot);
        WaitUntil(() => loop.State == LoopState.Idle);
        loop.Start(LoopMode.OneShot);          // mesmo texto: caminho de "texto igual"
        WaitUntil(() => loop.State == LoopState.Idle);

        Assert.Equal(2, probe.Cycles);
        Assert.Equal(LoopState.Idle, loop.State);
    }

    // ── Início e parada (RF-010, RF-013) ─────────────────────────────────────

    /// <summary>
    /// Critério de aceite do capítulo 9: "Pressionar o atalho de tradução 20 vezes seguidas
    /// em intervalos de 100 ms não deixa duas threads de laço vivas nem mata o interceptador
    /// de teclado."
    /// </summary>
    [Fact]
    public void Vinte_acionamentos_seguidos_nao_deixam_duas_threads_vivas()
    {
        var (loop, probe) = Build(intervalMs: 10);

        for (int i = 0; i < 20; i++)
        {
            if (loop.IsRunning) loop.StopFromKeyboardHook();
            else loop.Start(LoopMode.Realtime);
            Thread.Sleep(10);
        }

        loop.Stop();
        Assert.Equal(LoopState.Idle, loop.State);

        // Um único laço rodou por vez: o contador de paradas nunca passa o de inícios.
        Assert.True(probe.Stops <= 20, $"parou {probe.Stops} vezes");
    }

    /// <summary>
    /// RF-013 — Um novo pedido de iniciar para o laço anterior primeiro. Se não conseguir
    /// parar no prazo, o novo laço NÃO é iniciado.
    /// </summary>
    [Fact]
    public void RF_013_iniciar_com_o_laco_preso_nao_cria_um_segundo_laco()
    {
        var preso = new ManualResetEventSlim(false);
        var (loop, probe) = Build(cycle: _ =>
        {
            preso.Wait(TimeSpan.FromSeconds(5));
            return Task.FromResult(CycleResult.Empty);
        });

        loop.Start(LoopMode.Realtime);
        WaitUntil(() => probe.Cycles > 0);

        // A thread está presa dentro do ciclo: o prazo curto não a alcança.
        Assert.False(loop.Start(LoopMode.Realtime, TimeSpan.FromMilliseconds(50)));

        preso.Set();
        loop.Stop();
        Assert.Equal(LoopState.Idle, loop.State);
    }

    /// <summary>
    /// RF-010 — Se a thread não termina no prazo, o sinalizador de fim NÃO é revertido: o
    /// laço permanece em "parando" e o próximo pedido tenta de novo.
    /// </summary>
    [Fact]
    public void RF_010_uma_parada_que_falha_deixa_o_laco_em_parando()
    {
        var preso = new ManualResetEventSlim(false);
        var (loop, probe) = Build(cycle: _ =>
        {
            preso.Wait(TimeSpan.FromSeconds(5));
            return Task.FromResult(CycleResult.Empty);
        });

        loop.Start(LoopMode.Realtime);
        WaitUntil(() => probe.Cycles > 0);

        Assert.False(loop.Stop(TimeSpan.FromMilliseconds(50)));
        Assert.Equal(LoopState.Stopping, loop.State);

        preso.Set();
        WaitUntil(() => loop.State == LoopState.Idle);
        Assert.Equal(LoopState.Idle, loop.State);
    }

    /// <summary>
    /// RF-011 / P-04 — A parada vinda do interceptador de teclado usa o prazo curto. Acima
    /// de ~300 ms o sistema remove o gancho e TODOS os atalhos do programa morrem.
    /// </summary>
    [Fact]
    public void RF_011_a_parada_pelo_atalho_respeita_o_prazo_curto_de_P04()
    {
        var preso = new ManualResetEventSlim(false);
        var (loop, probe) = Build(cycle: _ =>
        {
            preso.Wait(TimeSpan.FromSeconds(5));
            return Task.FromResult(CycleResult.Empty);
        });

        loop.Start(LoopMode.Realtime);
        WaitUntil(() => probe.Cycles > 0);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        bool stopped = loop.StopFromKeyboardHook();
        watch.Stop();

        Assert.False(stopped);
        // O gancho não pode ficar preso muito além de P-04.
        Assert.True(watch.Elapsed < P.LoopJoinTimeoutFromHook + TimeSpan.FromMilliseconds(150),
            $"a espera durou {watch.ElapsedMilliseconds} ms, acima de P-04 ({P.LoopJoinTimeoutFromHook.TotalMilliseconds} ms)");

        preso.Set();
        WaitUntil(() => loop.State == LoopState.Idle);
    }

    // ── Protocolo de pausa (RF-012) ──────────────────────────────────────────

    /// <summary>
    /// Critério de aceite do capítulo 9: "Aplicar configuração enquanto o laço roda nunca
    /// produz um ciclo usando meia configuração antiga e meia nova."
    ///
    /// A garantia vem de a mudança só rodar com a thread do laço MORTA.
    /// </summary>
    [Fact]
    public void RF_012_a_mudanca_so_roda_com_a_thread_do_laco_morta()
    {
        var (loop, probe) = Build(intervalMs: 5);
        loop.Start(LoopMode.Realtime);
        WaitUntil(() => probe.Cycles > 0);

        LoopState duranteAMudanca = LoopState.Processing;
        var resultado = loop.PauseAndResume(() => duranteAMudanca = loop.State);

        Assert.Equal(ApplyResult.AppliedAndResumed, resultado);
        Assert.Equal(LoopState.Idle, duranteAMudanca);   // nenhum ciclo em voo
        Assert.True(loop.IsRunning);                      // e o laço voltou

        loop.Stop();
    }

    [Fact]
    public void RF_012_com_o_laco_parado_a_mudanca_e_aplicada_sem_pausa()
    {
        var (loop, _) = Build();
        bool aplicou = false;

        var resultado = loop.PauseAndResume(() => aplicou = true);

        Assert.Equal(ApplyResult.AppliedWithoutPause, resultado);
        Assert.True(aplicou);
        Assert.False(loop.IsRunning);
    }

    /// <summary>
    /// RF-012 — Se a parada falhar por tempo, a mudança é ABORTADA e o chamador é informado
    /// de que nada foi aplicado.
    /// </summary>
    [Fact]
    public void RF_012_se_a_parada_falha_a_mudanca_e_abortada()
    {
        var preso = new ManualResetEventSlim(false);
        var (loop, probe) = Build(cycle: _ =>
        {
            preso.Wait(TimeSpan.FromSeconds(5));
            return Task.FromResult(CycleResult.Empty);
        });

        loop.Start(LoopMode.Realtime);
        WaitUntil(() => probe.Cycles > 0);

        bool aplicou = false;
        var resultado = loop.PauseAndResume(() => aplicou = true, TimeSpan.FromMilliseconds(50));

        Assert.Equal(ApplyResult.Aborted, resultado);
        Assert.False(aplicou);   // nada foi aplicado

        preso.Set();
        WaitUntil(() => loop.State == LoopState.Idle);
    }

    // ── Detecção de mudança dentro do laço (RF-194 a RF-197) ─────────────────

    [Fact]
    public void RF_195_texto_igual_nao_redesenha_nem_repete_efeitos_colaterais()
    {
        var (loop, probe) = Build(intervalMs: 0);
        probe.Texts = new List<string> { "mesmo texto" };

        loop.Start(LoopMode.Realtime);
        WaitUntil(() => probe.Cycles >= 8);
        loop.Stop();

        // O primeiro ciclo desenha; os demais, com o mesmo texto, não.
        Assert.Equal(1, probe.Draws);
        Assert.Equal(1, probe.Copies);
        Assert.Equal(1, probe.SideEffects);
        Assert.True(probe.Cycles >= 8, $"rodou só {probe.Cycles} ciclos");
    }

    [Fact]
    public void RF_194_cada_texto_novo_dispara_o_caminho_completo()
    {
        var (loop, probe) = Build(intervalMs: 0);
        probe.Texts = new List<string> { "um", "dois", "tres", "tres" };

        loop.Start(LoopMode.Realtime);
        WaitUntil(() => probe.Cycles >= 6);
        loop.Stop();

        Assert.Equal(new[] { "um", "dois", "tres" }, probe.Drawn);
    }

    [Fact]
    public void RF_196_com_texto_igual_o_repintar_ocioso_acontece()
    {
        var (loop, probe) = Build(intervalMs: 0);
        probe.Texts = new List<string> { "estático" };

        loop.Start(LoopMode.Realtime);
        WaitUntil(() => probe.Repaints > 0);
        loop.Stop();

        Assert.True(probe.Repaints > 0);
        Assert.Equal(1, probe.Draws);   // o conteúdo não foi redesenhado
    }

    // ── Passos do fluxo ──────────────────────────────────────────────────────

    /// <summary>Passo 6 — sem janela de tradução viva, o ciclo não faz nada.</summary>
    [Fact]
    public void Sem_janela_de_traducao_nenhum_ciclo_executa()
    {
        var (loop, probe) = Build(intervalMs: 0);
        probe.HasWindow = false;

        loop.Start(LoopMode.Realtime);
        Thread.Sleep(200);
        loop.Stop();

        Assert.Equal(0, probe.Cycles);
    }

    /// <summary>Passo 21 — ao sair do laço, a memória de resultados vai para o disco.</summary>
    [Fact]
    public void Passo_21_ao_sair_do_laco_a_memoria_e_gravada()
    {
        var (loop, probe) = Build(intervalMs: 0);

        loop.Start(LoopMode.Realtime);
        WaitUntil(() => probe.Cycles > 0);
        loop.Stop();

        Assert.Equal(1, probe.Flushes);
    }

    /// <summary>
    /// RF-014 / RF-563 — Nenhuma exceção escapa da thread do laço: ela vira mensagem e o
    /// laço termina limpo.
    /// </summary>
    [Fact]
    public void RF_014_uma_excecao_no_ciclo_nao_derruba_a_thread()
    {
        var (loop, probe) = Build(cycle: _ => throw new InvalidOperationException("estourou"));

        loop.Start(LoopMode.Realtime);
        WaitUntil(() => loop.State == LoopState.Idle || probe.Errors.Count > 0);
        loop.Stop();

        Assert.Contains("estourou", string.Join(" ", probe.Errors));
        Assert.Equal(LoopState.Idle, loop.State);
        Assert.Equal(1, probe.Flushes);   // o encerramento limpo aconteceu
    }

    [Fact]
    public void RF_014_uma_falha_assincrona_do_ciclo_tambem_e_reportada()
    {
        var (loop, probe) = Build(
            cycle: _ => Task.FromException<CycleResult>(new InvalidOperationException("falhou")));

        loop.Start(LoopMode.OneShot);
        WaitUntil(() => loop.State == LoopState.Idle);

        Assert.Contains("falhou", string.Join(" ", probe.Errors));
    }

    /// <summary>
    /// RF-551 — Parar leva no máximo P-03. Este caso mede a parada durante uma espera longa,
    /// que é o cenário do critério de aceite "fechar o programa durante uma tradução com
    /// serviço remoto lento".
    /// </summary>
    [Fact]
    public void RF_551_parar_durante_uma_traducao_lenta_respeita_o_prazo()
    {
        var (loop, probe) = Build(cycle: async _ =>
        {
            // Um serviço remoto lento: bem mais que o prazo de parada.
            await Task.Delay(TimeSpan.FromSeconds(10));
            return CycleResult.Empty;
        });

        loop.Start(LoopMode.Realtime);
        WaitUntil(() => probe.Cycles > 0);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        bool stopped = loop.Stop();
        watch.Stop();

        Assert.True(stopped, "o laço deveria parar mesmo com a tradução em voo");
        Assert.True(watch.Elapsed < P.LoopJoinTimeout,
            $"a parada levou {watch.ElapsedMilliseconds} ms, acima de P-03");

        // A espera é abandonada pela sondagem de P-126, não pela conclusão da tradução.
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1),
            $"a parada esperou a tradução terminar ({watch.ElapsedMilliseconds} ms)");
    }
}
