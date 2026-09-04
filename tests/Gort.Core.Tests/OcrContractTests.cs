using Gort.Core.Model;
using Gort.Core.Ocr;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>Motor de teste, com comportamento controlado pelo caso.</summary>
internal sealed class FakeEngine : IOcrEngine
{
    public required string Key { get; init; }
    public bool IsAvailable { get; init; } = true;
    public string? UnavailableReason { get; init; }
    public bool ProvidesWordPositions { get; init; } = true;
    public IReadOnlyList<string> Languages { get; init; } = new[] { "en" };
    public Func<ImageBuffer, string, OcrResult>? Behaviour { get; init; }
    public bool Disposed { get; private set; }

    public OcrResult Recognize(ImageBuffer image, string languageCode)
        => Behaviour?.Invoke(image, languageCode) ?? OcrResult.Empty;

    public void Dispose() => Disposed = true;
}

/// <summary>Cap. 14 / 6.4 — O contrato do reconhecimento de texto.</summary>
public class OcrContractTests
{
    private static ImageBuffer Image() => ImageBuffer.Allocate(10, 10, PixelFormat.Bgra32);

    /// <summary>
    /// RF-145 — Erros do motor produzem um resultado marcado como VAZIO, com a mensagem no
    /// campo de texto principal, e o ciclo continua. Nenhuma exceção escapa (RF-561).
    /// </summary>
    [Fact]
    public void RF_145_uma_excecao_do_motor_vira_resultado_vazio_com_a_mensagem()
    {
        var engine = new SafeOcrEngine(new FakeEngine
        {
            Key = "x",
            Behaviour = (_, _) => throw new InvalidOperationException("biblioteca nativa ausente"),
        });

        var result = engine.Recognize(Image(), "en");
        Assert.True(result.IsEmpty);
        Assert.Equal("biblioteca nativa ausente", result.ErrorMessage);
    }

    [Fact]
    public void RF_145_um_motor_indisponivel_devolve_o_motivo_sem_chamar_nada()
    {
        bool chamado = false;
        var engine = new SafeOcrEngine(new FakeEngine
        {
            Key = "x",
            IsAvailable = false,
            UnavailableReason = "modelo não encontrado",
            Behaviour = (_, _) => { chamado = true; return OcrResult.Empty; },
        });

        var result = engine.Recognize(Image(), "en");
        Assert.True(result.IsEmpty);
        Assert.Equal("modelo não encontrado", result.ErrorMessage);
        Assert.False(chamado);
    }

    /// <summary>RF-120 / RF-575 — A lista oferecida contém só os motores disponíveis.</summary>
    [Fact]
    public void RF_120_apenas_os_motores_disponiveis_sao_oferecidos()
    {
        using var registry = new OcrEngineRegistry();
        registry.Register(new FakeEngine { Key = "modern" });
        registry.Register(new FakeEngine { Key = "system", IsAvailable = false });

        Assert.Equal(2, registry.All.Count);
        Assert.Equal(new[] { "modern" }, registry.Available.Select(e => e.Key));
    }

    /// <summary>
    /// RF-028 / RF-307 — Um motor salvo no perfil que não exista mais, ou que esteja
    /// indisponível, cai para o primeiro disponível em vez de impedir o funcionamento.
    /// </summary>
    [Fact]
    public void RF_028_um_motor_desconhecido_ou_indisponivel_cai_para_o_primeiro_disponivel()
    {
        using var registry = new OcrEngineRegistry();
        registry.Register(new FakeEngine { Key = "cloud", IsAvailable = false });
        registry.Register(new FakeEngine { Key = "modern" });

        Assert.Equal("modern", registry.Resolve("motor_que_nao_existe")?.Key);
        Assert.Equal("modern", registry.Resolve("cloud")?.Key);
        Assert.Equal("modern", registry.Resolve(null)?.Key);
    }

    [Fact]
    public void Sem_nenhum_motor_disponivel_a_resolucao_devolve_nulo()
    {
        using var registry = new OcrEngineRegistry();
        registry.Register(new FakeEngine { Key = "modern", IsAvailable = false });
        Assert.Null(registry.Resolve("modern"));
    }

    /// <summary>
    /// RF-141 — Motores que devolvem apenas linhas produzem uma "palavra" por linha, com a
    /// caixa da própria linha.
    /// </summary>
    [Fact]
    public void RF_141_cada_linha_vira_uma_palavra_com_a_caixa_da_linha()
    {
        var result = OcrResultBuilder.FromLines(new[]
        {
            ("primeira linha", new Rect(10, 10, 200, 30)),
            ("segunda linha", new Rect(10, 50, 180, 30)),
        });

        Assert.False(result.IsEmpty);
        Assert.Equal(2, result.LineCount);
        Assert.Equal(new[] { 1, 1 }, result.WordsPerLine);
        Assert.Equal(new Rect(10, 10, 200, 30), result.Words[0].Box);

        // As linhas reconstruídas mantêm a caixa e o texto, com o espaço final de RF-152.
        var lines = result.BuildLines();
        Assert.Equal("primeira linha ", lines[0].Text);
        Assert.Equal(new Rect(10, 10, 200, 30), lines[0].Box);
    }

    [Fact]
    public void Uma_imagem_sem_texto_produz_resultado_vazio_e_nao_erro()
    {
        var vazio = OcrResultBuilder.FromLines(Array.Empty<(string, Rect)>());
        Assert.True(vazio.IsEmpty);
        Assert.Null(vazio.ErrorMessage);
        Assert.Empty(vazio.BuildLines());
    }

    [Fact]
    public void Linhas_sem_palavra_alguma_nao_entram_na_contagem()
    {
        var result = OcrResultBuilder.FromWords(new[]
        {
            new[] { new Word { Text = "a", Box = new Rect(0, 0, 10, 10) } },
            Array.Empty<Word>(),
            new[] { new Word { Text = "b", Box = new Rect(20, 0, 10, 10) } },
        });

        Assert.Equal(2, result.LineCount);
        Assert.Equal(new[] { 1, 1 }, result.WordsPerLine);
    }

    /// <summary>
    /// RF-143 — Texto de bibliotecas nativas decodificado como UTF-8, com decodificação
    /// tolerante como alternativa quando a sequência é inválida.
    /// </summary>
    [Fact]
    public void RF_143_utf8_valido_e_decodificado()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("こんにちは Olá");
        Assert.Equal("こんにちは Olá", OcrResultBuilder.DecodeUtf8(bytes));
    }

    [Fact]
    public void RF_143_bytes_invalidos_nao_descartam_a_linha_inteira()
    {
        // "abc" + um byte de continuação solto + "def"
        var bytes = new byte[] { 0x61, 0x62, 0x63, 0xFF, 0x64, 0x65, 0x66 };
        string text = OcrResultBuilder.DecodeUtf8(bytes);

        Assert.StartsWith("abc", text);
        Assert.EndsWith("def", text);
    }

    [Fact]
    public void RF_143_uma_sequencia_vazia_devolve_cadeia_vazia()
        => Assert.Equal("", OcrResultBuilder.DecodeUtf8(ReadOnlySpan<byte>.Empty));

    [Fact]
    public void O_registro_libera_os_motores_ao_ser_descartado()
    {
        var engine = new FakeEngine { Key = "modern" };
        var registry = new OcrEngineRegistry();
        registry.Register(engine);
        registry.Dispose();
        Assert.True(engine.Disposed);
    }

    /// <summary>
    /// RF-351 — O modo sobreposição só é permitido com motores que devolvem posição de
    /// palavra; a propriedade é consultável antes de iniciar.
    /// </summary>
    [Fact]
    public void RF_351_a_capacidade_de_posicao_de_palavra_e_visivel_no_motor()
    {
        using var registry = new OcrEngineRegistry();
        registry.Register(new FakeEngine { Key = "modern", ProvidesWordPositions = true });
        registry.Register(new FakeEngine { Key = "interpreted", ProvidesWordPositions = false });

        Assert.True(registry.Find("modern")!.ProvidesWordPositions);
        Assert.False(registry.Find("interpreted")!.ProvidesWordPositions);
    }
}
