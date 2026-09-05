using Gort.Core.Caching;
using Gort.Core.Calibration;
using Gort.Core.Model;
using Gort.Core.Pipeline;
using Gort.Core.Regions;
using Gort.Core.Structuring;
using Gort.Core.Translation;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>
/// PARTE VIII — Comportamento em situações extremas, linha a linha.
///
/// A tabela da PARTE VIII não é uma lista de tolerâncias: cada linha diz o comportamento
/// EXIGIDO. Estes testes cobrem as que ainda não tinham teste próprio; as demais estão nos
/// arquivos do assunto correspondente, citadas ali pelo requisito.
/// </summary>
public class PartVIIITests
{
    // ── "Nenhuma área de OCR definida" ──────────────────────────────────────

    /// <summary>
    /// RF-065 — É preciso pelo menos uma área INCREMENTAL para iniciar qualquer tradução.
    ///
    /// Uma área de EXCLUSÃO não conta: ela subtrai região, e a subtração de nada é nada.
    /// Começar a traduzir com só exclusões definidas produziria captura vazia todo ciclo,
    /// e o usuário concluiria que o programa não funciona.
    /// </summary>
    [Fact]
    public void PARTE_VIII_sem_area_incremental_a_traducao_nao_pode_comecar()
    {
        var regions = new RegionManager();
        Assert.False(regions.HasAnyIncrementalArea);

        regions.AddExclusion(new Rect(10, 10, 100, 50));
        Assert.False(regions.HasAnyIncrementalArea);

        regions.AddArea(new Rect(0, 0, 200, 100));
        Assert.True(regions.HasAnyIncrementalArea);
    }

    /// <summary>
    /// RF-069 / RF-070 — A área rápida, a instantânea e a que segue o mouse também
    /// satisfazem a exigência: as três produzem captura.
    /// </summary>
    [Theory]
    [InlineData("rapida")]
    [InlineData("instantanea")]
    [InlineData("mouse")]
    public void PARTE_VIII_as_areas_especiais_tambem_habilitam_a_traducao(string kind)
    {
        var regions = new RegionManager();
        var rect = new Rect(0, 0, 200, 100);

        switch (kind)
        {
            case "rapida": regions.SetQuickArea(rect); break;
            case "instantanea": regions.SetSnapshotArea(rect); break;
            default: regions.SetMouseFollowArea(rect); break;
        }

        Assert.True(regions.HasAnyIncrementalArea);
    }

    // ── "Região capturada vazia (0 px)" ─────────────────────────────────────

    /// <summary>
    /// PARTE VIII — "A conversão força mínimo de 1 px em cada dimensão."
    ///
    /// Uma moldura menor que a soma das suas próprias bordas produziria um retângulo de
    /// captura negativo; o piso de 1 px mantém o cálculo coerente em vez de falhar.
    /// </summary>
    [Fact]
    public void PARTE_VIII_a_conversao_forca_minimo_de_um_pixel()
    {
        var metrics = FrameGeometry.MetricsFor(1.0);

        // Uma moldura no tamanho mínimo de P-12 tem pouco espaço interno; uma menor que as
        // bordas não teria nenhum.
        var tiny = new Rect(0, 0, 1, 1);
        var capture = FrameGeometry.ToCaptureRect(tiny, metrics);

        Assert.True(capture.Width >= 1);
        Assert.True(capture.Height >= 1);
    }

    // ── "Resposta com menos partes que blocos" ──────────────────────────────

    /// <summary>
    /// PARTE VIII — "Os blocos restantes ficam sem tradução; nada é exibido para eles; NÃO
    /// É ERRO."
    ///
    /// É a diferença entre um serviço que devolveu menos do que se pediu e um que falhou: o
    /// primeiro entregou o que conseguiu, e descartar tudo por causa do que faltou seria
    /// jogar fora tradução boa.
    /// </summary>
    [Fact]
    public async Task PARTE_VIII_resposta_com_menos_partes_nao_e_erro()
    {
        var pipeline = new TranslationPipeline();
        var service = new ShortService();

        var batch = await pipeline.TranslateAsync(
            new[] { "um", "dois", "tres" }, service, new TranslationContext { SourceCode = "en", TargetCode = "pt" });

        Assert.Null(batch.Error);
        Assert.Equal("PRIMEIRO", batch.Translations[0].Trim());

        // Os que faltaram ficam sem tradução; nada é exibido para eles.
        for (int i = 1; i < batch.Translations.Count; i++)
            Assert.True(string.IsNullOrWhiteSpace(batch.Translations[i]));
    }

    // ── "Texto muito longo" ─────────────────────────────────────────────────

    /// <summary>
    /// PARTE VIII — "Vai INTEIRO na requisição." Nada é truncado do lado do programa: se o
    /// serviço truncar, a tradução vem truncada — e isso é informação para o usuário, que
    /// um corte silencioso aqui esconderia.
    /// </summary>
    [Fact]
    public async Task PARTE_VIII_o_texto_longo_vai_inteiro_na_requisicao()
    {
        string longo = new string('a', 50_000);
        var service = new RecordingService();

        await new TranslationPipeline().TranslateAsync(
            new[] { longo }, service, new TranslationContext { SourceCode = "en", TargetCode = "pt" });

        Assert.Contains(longo, service.Received);
    }

    // ── "Tradução vazia" ────────────────────────────────────────────────────

    /// <summary>
    /// RF-240 — Com "ignorar tradução vazia", a tela mantém o conteúdo anterior; sem ela, a
    /// tela é limpa.
    ///
    /// O pipeline apenas SINALIZA; quem decide não desenhar é a janela. Separar as duas
    /// coisas é o que permite ao modo escuro e ao modo sobreposição tratarem o vazio de
    /// formas diferentes sem duplicar a regra.
    /// </summary>
    [Fact]
    public void RF_240_o_pipeline_sinaliza_a_traducao_vazia_sem_decidir_por_ela()
    {
        var pipeline = new TranslationPipeline { IgnoreEmptyTranslation = true };
        Assert.True(pipeline.IgnoreEmptyTranslation);

        var outro = new TranslationPipeline();
        Assert.False(outro.IgnoreEmptyTranslation);
    }

    // ── "OCR não reconhece nada" ────────────────────────────────────────────

    /// <summary>
    /// RF-194 / PARTE VIII — Texto vazio é tratado como MUDANÇA, então a tradução anterior
    /// é apagada da tela.
    ///
    /// Deixar a tradução antiga no lugar seria pior que apagá-la: o usuário leria uma
    /// tradução que não corresponde mais a nada na tela e não teria como saber disso.
    /// </summary>
    [Fact]
    public void PARTE_VIII_o_vazio_apaga_a_traducao_anterior()
    {
        var detector = new ChangeDetector();

        Assert.Equal(ChangeDecision.FullRedraw, detector.Evaluate("Hello world"));
        Assert.Equal(ChangeDecision.FullRedraw, detector.Evaluate(""));
    }

    /// <summary>
    /// PARTE VIII — "OCR devolve lixo instável: o texto muda a cada ciclo e o programa
    /// retraduz e redesenha a cada ciclo. É o comportamento EXIGIDO, não uma tolerância."
    ///
    /// Qualquer amortecimento — esperar dois ciclos iguais, por exemplo — custaria um ciclo
    /// de latência em TODA tradução, inclusive nas boas.
    /// </summary>
    [Fact]
    public void PARTE_VIII_texto_instavel_redesenha_todo_ciclo_por_exigencia()
    {
        var detector = new ChangeDetector();

        foreach (string lixo in new[] { "He11o", "Hel1o", "HeIlo", "He11o" })
            Assert.Equal(ChangeDecision.FullRedraw, detector.Evaluate(lixo));
    }

    // ── "Memória de resultados anteriores" ──────────────────────────────────

    /// <summary>
    /// RF-557 — A memória de resultados tem teto (P-48) para não crescer sem limite. Um
    /// programa que fica aberto o dia inteiro traduzindo diálogo repetido encheria a
    /// memória se não houvesse teto.
    /// </summary>
    [Fact]
    public void RF_557_a_memoria_de_resultados_tem_teto()
    {
        string file = Path.Combine(Path.GetTempPath(), "gort-teto",
                                   Guid.NewGuid().ToString("N") + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        var memory = new ResultMemory("teste", file);
        for (int i = 0; i < P.ResultMemoryMaxEntries + 500; i++)
            memory.Store($"origem {i}", $"destino {i}");

        Assert.True(memory.Count <= P.ResultMemoryMaxEntries,
                    $"{memory.Count} entradas passou do teto de {P.ResultMemoryMaxEntries}.");
    }

    // ── Dublês ──────────────────────────────────────────────────────────────

    /// <summary>Devolve menos partes do que foram pedidas.</summary>
    private sealed class ShortService : ITranslationService
    {
        public string Key => "curto";
        public void Dispose() { }

        public Task<TranslationOutcome> TranslateAsync(
            string text, TranslationContext context, CancellationToken cancellation)
            => Task.FromResult(TranslationOutcome.Ok("PRIMEIRO"));
    }

    /// <summary>Guarda o que recebeu, para conferir que nada foi cortado no caminho.</summary>
    private sealed class RecordingService : ITranslationService
    {
        public string Received { get; private set; } = "";
        public string Key => "gravador";
        public void Dispose() { }

        public Task<TranslationOutcome> TranslateAsync(
            string text, TranslationContext context, CancellationToken cancellation)
        {
            Received = text;
            return Task.FromResult(TranslationOutcome.Ok(text));
        }
    }
}
