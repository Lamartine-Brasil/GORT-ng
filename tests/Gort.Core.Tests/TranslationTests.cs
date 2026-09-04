using Gort.Core.Caching;
using Gort.Core.Calibration;
using Gort.Core.Translation;
using Gort.Core.Translation.Services;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>Serviço de teste: registra o que recebe e devolve o que mandarem.</summary>
internal sealed class FakeService : ITranslationService
{
    public string Key { get; init; } = "fake";
    public List<string> Requests { get; } = new();
    public Func<string, TranslationOutcome>? Behaviour { get; init; }

    public Task<TranslationOutcome> TranslateAsync(string text, TranslationContext context,
                                                    CancellationToken cancellation)
    {
        Requests.Add(text);
        return Task.FromResult(Behaviour?.Invoke(text) ?? TranslationOutcome.Ok(text));
    }

    public void Dispose() { }
}

/// <summary>Cap. 18.1 — Protocolo comum de tradução.</summary>
public class TranslationPipelineTests
{
    private static readonly TranslationContext Context =
        new() { SourceCode = "en", TargetCode = "pt" };

    private static string TempFile()
        => Path.Combine(Path.GetTempPath(), "gort-trad", Guid.NewGuid().ToString("N") + ".txt");

    /// <summary>RF-228 — Um pedido com texto vazio devolve vazio SEM CHAMAR NADA.</summary>
    [Fact]
    public async Task RF_228_texto_vazio_nao_chama_o_servico()
    {
        var service = new FakeService();
        using var pipeline = new TranslationPipeline();

        var result = await pipeline.TranslateAsync(new[] { "", "" }, service, Context);

        Assert.Empty(service.Requests);
        Assert.Equal(new string?[] { null, null }, result.Translations);
        Assert.Equal("", result.Combined);
    }

    /// <summary>
    /// RF-231 — Os textos não encontrados vão em UMA ÚNICA requisição, cada um precedido
    /// pelo token separador e seguido de quebra de linha.
    /// </summary>
    [Fact]
    public async Task RF_231_varios_blocos_viram_uma_unica_requisicao()
    {
        var service = new FakeService
        {
            Behaviour = _ => TranslationOutcome.Ok("//////um\n//////dois\n//////tres\n"),
        };
        using var pipeline = new TranslationPipeline();

        var result = await pipeline.TranslateAsync(new[] { "one", "two", "three" }, service, Context);

        Assert.Single(service.Requests);
        Assert.Equal("//////one\n//////two\n//////three\n", service.Requests[0]);
        Assert.Equal(new string?[] { "um", "dois", "tres" }, result.Translations);
    }

    /// <summary>
    /// Critério de aceite do cap. 18: "Com três blocos e um deles já em cache, a requisição
    /// de rede contém apenas dois textos." E: "A resposta é redistribuída na ordem correta
    /// mesmo quando o cache respondeu pelo bloco do meio."
    /// </summary>
    [Fact]
    public async Task RF_230_o_que_esta_em_cache_nao_vai_para_a_rede_e_a_ordem_se_mantem()
    {
        var memory = new ResultMemory("fake", TempFile());
        memory.Store("two", "dois em cache");

        var service = new FakeService
        {
            Behaviour = _ => TranslationOutcome.Ok("//////um\n//////tres\n"),
        };
        using var pipeline = new TranslationPipeline { Memory = memory };

        var result = await pipeline.TranslateAsync(new[] { "one", "two", "three" }, service, Context);

        // Só os dois que faltavam foram à rede.
        Assert.Equal("//////one\n//////three\n", service.Requests[0]);
        Assert.Equal(2, result.NetworkCount);

        // A redistribuição respeita as posições originais.
        Assert.Equal(new string?[] { "um", "dois em cache", "tres" }, result.Translations);
    }

    [Fact]
    public async Task RF_230_com_tudo_em_cache_nao_ha_chamada_de_rede()
    {
        var memory = new ResultMemory("fake", TempFile());
        memory.Store("one", "um");

        var service = new FakeService();
        using var pipeline = new TranslationPipeline { Memory = memory };

        var result = await pipeline.TranslateAsync(new[] { "one" }, service, Context);

        Assert.Empty(service.Requests);
        Assert.Equal(0, result.NetworkCount);
        Assert.Equal(new string?[] { "um" }, result.Translations);
    }

    /// <summary>RF-215 — A coletânea do usuário é consultada ANTES da memória.</summary>
    [Fact]
    public async Task RF_215_a_coletanea_tem_precedencia_sobre_a_memoria()
    {
        string file = TempFile();
        PairFile.Write(file, new[] { new TranslationPair("sword", "espada da coletânea") });

        var collection = new TranslationCollection { Mode = CollectionLookupMode.Exact };
        collection.Load(new[] { file });

        var memory = new ResultMemory("fake", TempFile());
        memory.Store("sword", "espada da memória");

        var service = new FakeService();
        using var pipeline = new TranslationPipeline { Collection = collection, Memory = memory };

        var result = await pipeline.TranslateAsync(new[] { "sword" }, service, Context);
        Assert.Equal("espada da coletânea", result.Translations[0]);
    }

    /// <summary>
    /// RF-233 — Se a resposta tiver MENOS partes que textos, os restantes ficam sem
    /// tradução. Isso não é erro.
    /// </summary>
    [Fact]
    public async Task RF_233_resposta_com_menos_partes_deixa_os_restantes_sem_traducao()
    {
        var service = new FakeService
        {
            Behaviour = _ => TranslationOutcome.Ok("//////um\n"),
        };
        using var pipeline = new TranslationPipeline();

        var result = await pipeline.TranslateAsync(new[] { "one", "two" }, service, Context);

        Assert.Equal("um", result.Translations[0]);
        Assert.Null(result.Translations[1]);
        Assert.Null(result.Error);   // não é erro
    }

    /// <summary>RF-235 — Cada tradução obtida por rede é gravada na memória imediatamente.</summary>
    [Fact]
    public async Task RF_235_a_traducao_de_rede_entra_na_memoria_na_hora()
    {
        var memory = new ResultMemory("fake", TempFile());
        var service = new FakeService
        {
            Behaviour = _ => TranslationOutcome.Ok("//////olá\n"),
        };
        using var pipeline = new TranslationPipeline { Memory = memory };

        await pipeline.TranslateAsync(new[] { "hello" }, service, Context);
        Assert.Equal("olá", memory.Lookup("hello"));

        // Critério de aceite do cap. 17: traduzir a mesma frase duas vezes gera uma única
        // chamada de rede.
        await pipeline.TranslateAsync(new[] { "hello" }, service, Context);
        Assert.Single(service.Requests);
    }

    /// <summary>
    /// RF-236 — Quando o serviço devolve erro, a mensagem ocupa o lugar de TODAS as
    /// traduções e o ciclo continua.
    /// </summary>
    [Fact]
    public async Task RF_236_um_erro_ocupa_o_lugar_de_todas_as_traducoes()
    {
        var service = new FakeService
        {
            Behaviour = _ => TranslationOutcome.Failed("rede indisponível"),
        };
        using var pipeline = new TranslationPipeline();

        var result = await pipeline.TranslateAsync(new[] { "a", "b" }, service, Context);

        Assert.Equal("rede indisponível", result.Error);
        Assert.All(result.Translations, t => Assert.Equal("rede indisponível", t));
    }

    [Fact]
    public async Task RF_236_uma_excecao_do_servico_tambem_vira_mensagem()
    {
        var service = new FakeService
        {
            Behaviour = _ => throw new InvalidOperationException("estourou"),
        };
        using var pipeline = new TranslationPipeline();

        var result = await pipeline.TranslateAsync(new[] { "a" }, service, Context);
        Assert.Equal("estourou", result.Error);
    }

    /// <summary>RF-238 — Cancelamento não é erro: sem erro, sem desenho.</summary>
    [Fact]
    public async Task RF_238_cancelamento_nao_e_erro()
    {
        var service = new FakeService { Behaviour = _ => TranslationOutcome.CancelledResult };
        using var pipeline = new TranslationPipeline();

        var result = await pipeline.TranslateAsync(new[] { "a" }, service, Context);

        Assert.True(result.Cancelled);
        Assert.Null(result.Error);
    }

    /// <summary>RF-237 — A forma concatenada é token + tradução + quebra de linha.</summary>
    [Fact]
    public async Task RF_237_o_texto_final_e_a_concatenacao_com_o_token()
    {
        var service = new FakeService
        {
            Behaviour = _ => TranslationOutcome.Ok("//////um\n//////dois\n"),
        };
        using var pipeline = new TranslationPipeline();

        var result = await pipeline.TranslateAsync(new[] { "one", "two" }, service, Context);
        Assert.Equal("//////um\n//////dois\n", result.Combined);
    }

    /// <summary>
    /// RF-234 — No modo de token avançado, envia-se o token ENCURTADO: menos 3 caracteres
    /// quando tem 7 ou mais, menos 2 quando tem 6. 🔒
    /// </summary>
    [Theory]
    [InlineData("//////", "////")]        // P-51, 6 caracteres → menos 2
    [InlineData("@@@@@@", "@@@@")]        // P-52, 6 caracteres → menos 2
    [InlineData("///////", "////")]       // 7 caracteres → menos 3
    [InlineData("////////", "/////")]     // 8 caracteres → menos 3
    [InlineData("////", "////")]          // curto demais: inalterado
    public void RF_234_o_token_avancado_e_encurtado(string token, string esperado)
    {
        using var pipeline = new TranslationPipeline
        {
            SeparatorToken = token,
            AdvancedToken = true,
        };
        Assert.Equal(esperado, pipeline.EffectiveToken());
    }

    [Fact]
    public void RF_234_sem_o_modo_avancado_o_token_vai_inteiro()
    {
        using var pipeline = new TranslationPipeline { SeparatorToken = P.SeparatorToken };
        Assert.Equal(P.SeparatorToken, pipeline.EffectiveToken());
    }

    /// <summary>
    /// RF-234 — Na resposta, removem-se das PONTAS de cada parte as repetições do primeiro
    /// caractere do token, e as partes que ficarem vazias são descartadas.
    /// </summary>
    [Fact]
    public void RF_234_a_limpeza_das_pontas_tolera_o_token_alterado()
    {
        using var pipeline = new TranslationPipeline
        {
            SeparatorToken = "//////",
            AdvancedToken = true,
        };

        var partes = pipeline.SplitResponse("////um//\n////dois\n/////\n", "////");

        Assert.Equal(new[] { "um", "dois" }, partes);
    }

    [Fact]
    public async Task RF_229_um_novo_pedido_cancela_o_anterior()
    {
        var iniciou = new TaskCompletionSource();
        var liberar = new TaskCompletionSource();
        CancellationToken capturado = default;

        var lento = new SlowService(iniciou, liberar, t => capturado = t);
        using var pipeline = new TranslationPipeline();

        var primeiro = pipeline.TranslateAsync(new[] { "a" }, lento, Context);
        await iniciou.Task;

        // O segundo pedido cancela o primeiro: se o conteúdo mudou, a tradução antiga já
        // não interessa e segurá-la atrasa a nova.
        var segundo = pipeline.TranslateAsync(new[] { "b" }, new FakeService(), Context);
        await segundo;

        Assert.True(capturado.IsCancellationRequested);
        liberar.SetResult();
        await primeiro;
    }

    private sealed class SlowService : ITranslationService
    {
        private readonly TaskCompletionSource _started, _release;
        private readonly Action<CancellationToken> _capture;

        public SlowService(TaskCompletionSource started, TaskCompletionSource release,
                           Action<CancellationToken> capture)
        { _started = started; _release = release; _capture = capture; }

        public string Key => "lento";

        public async Task<TranslationOutcome> TranslateAsync(
            string text, TranslationContext context, CancellationToken cancellation)
        {
            _capture(cancellation);
            _started.TrySetResult();
            await _release.Task;
            return TranslationOutcome.Ok("");
        }

        public void Dispose() { }
    }
}

/// <summary>VI.1 / RF-244 a RF-248 — Tradutor web gratuito.</summary>
public class FreeWebTranslatorTests
{
    private static readonly FreeWebTranslatorOptions Options = new()
    {
        Endpoint = "https://exemplo.invalido/traduzir",
        HighQualityClient = "alta",
        LowQualityClient = "baixa",
    };

    /// <summary>
    /// RF-244 — A resposta é um vetor JSON cujo primeiro elemento é um vetor de segmentos;
    /// de cada segmento extrai-se o primeiro item quando é texto.
    /// </summary>
    [Fact]
    public void RF_244_a_resposta_e_montada_a_partir_dos_segmentos()
    {
        string body = """[[["Olá mundo","Hello world",null,null,10]],null,"en"]""";
        Assert.Equal("Olá mundo", FreeWebTranslator.ParseResponse(body));
    }

    [Fact]
    public void RF_244_varios_segmentos_sao_concatenados()
    {
        string body = """[[["Primeira parte. ","A",null,null,1],["Segunda parte.","B",null,null,1]],null,"en"]""";
        Assert.Equal("Primeira parte. Segunda parte.", FreeWebTranslator.ParseResponse(body));
    }

    [Fact]
    public void RF_244_segmentos_sem_texto_sao_ignorados()
    {
        string body = """[[["ok","A",null,null,1],[null,"B"],[]],null,"en"]""";
        Assert.Equal("ok", FreeWebTranslator.ParseResponse(body));
    }

    [Fact]
    public void Uma_resposta_vazia_ou_inesperada_nao_lanca()
    {
        Assert.Equal("", FreeWebTranslator.ParseResponse(""));
        Assert.Equal("", FreeWebTranslator.ParseResponse("[]"));
        Assert.Equal("", FreeWebTranslator.ParseResponse("""{"erro":1}"""));
    }

    /// <summary>
    /// RF-245 / RF-246 — Ao receber 429, troca-se para o cliente de baixa qualidade e
    /// repete-se UMA vez; o modo dura P-53 e depois volta sozinho ao normal.
    /// </summary>
    [Fact]
    public async Task RF_245_um_429_degrada_para_baixa_qualidade_e_repete_uma_vez()
    {
        var agora = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var handler = new StubHandler();
        handler.Responses.Enqueue((System.Net.HttpStatusCode.TooManyRequests, ""));
        handler.Responses.Enqueue((System.Net.HttpStatusCode.OK, """[[["olá","hi",null,null,1]]]"""));

        using var translator = new FreeWebTranslator(Options, new HttpClient(handler), () => agora);
        var result = await translator.TranslateAsync("hi",
            new TranslationContext { SourceCode = "en", TargetCode = "pt" }, default);

        Assert.False(result.IsError);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("client=alta", handler.Requests[0]);
        Assert.Contains("client=baixa", handler.Requests[1]);

        // RF-247 — o resultado vem prefixado com o marcador visível.
        Assert.StartsWith(Options.LowQualityMarker, result.Text);
        Assert.True(translator.IsLowQuality);

        // RF-246 — passado P-53, o modo normal volta automaticamente.
        agora = agora.Add(P.LowQualityModeDuration);
        Assert.False(translator.IsLowQuality);
    }

    /// <summary>RF-245 — Já em baixa qualidade, um novo 429 vira mensagem de cota esgotada.</summary>
    [Fact]
    public async Task RF_245_ja_em_baixa_qualidade_o_429_devolve_cota_esgotada()
    {
        var agora = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var handler = new StubHandler();
        for (int i = 0; i < 4; i++)
            handler.Responses.Enqueue((System.Net.HttpStatusCode.TooManyRequests, ""));

        using var translator = new FreeWebTranslator(Options, new HttpClient(handler), () => agora);
        var context = new TranslationContext { SourceCode = "en", TargetCode = "pt" };

        await translator.TranslateAsync("hi", context, default);   // entra em baixa qualidade
        var segundo = await translator.TranslateAsync("hi", context, default);

        Assert.True(segundo.IsError);
        Assert.Contains("cota horária", segundo.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RF_236_um_codigo_de_erro_vira_mensagem_e_nao_excecao()
    {
        var handler = new StubHandler();
        handler.Responses.Enqueue((System.Net.HttpStatusCode.ServiceUnavailable, ""));

        using var translator = new FreeWebTranslator(Options, new HttpClient(handler));
        var result = await translator.TranslateAsync("hi",
            new TranslationContext { SourceCode = "en", TargetCode = "pt" }, default);

        Assert.True(result.IsError);
        Assert.Contains("503", result.Error!);
    }

    [Fact]
    public async Task Texto_vazio_devolve_vazio_sem_chamar_nada()
    {
        var handler = new StubHandler();
        using var translator = new FreeWebTranslator(Options, new HttpClient(handler));

        var result = await translator.TranslateAsync("",
            new TranslationContext { SourceCode = "en", TargetCode = "pt" }, default);

        Assert.Equal("", result.Text);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void RF_225_o_endereco_do_servico_vem_dos_dados()
    {
        var catalogo = Gort.Core.Catalog.AppCatalog.Load(TestPaths.DataDirectory);
        Assert.NotNull(catalogo.FreeWebTranslator);
        Assert.StartsWith("https://", catalogo.FreeWebTranslator!.Endpoint);
        Assert.NotEqual(catalogo.FreeWebTranslator.HighQualityClient,
                        catalogo.FreeWebTranslator.LowQualityClient);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public Queue<(System.Net.HttpStatusCode, string)> Responses { get; } = new();
        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            var (status, body) = Responses.Count > 0
                ? Responses.Dequeue()
                : (System.Net.HttpStatusCode.OK, "[[]]");

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body),
            });
        }
    }
}
