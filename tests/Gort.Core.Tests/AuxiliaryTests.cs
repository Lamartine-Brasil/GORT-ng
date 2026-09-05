using Gort.Core.Auxiliary;
using Gort.Core.Calibration;
using Gort.Core.Configuration;
using Gort.Core.Structuring;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>Cap. 24 — Tradução da área de transferência.</summary>
public class ClipboardTranslationTests
{
    private static ClipboardTranslationGate New() => new() { Enabled = true };

    /// <summary>
    /// RF-467 — Todas as quatro condições têm de valer. Cada teste desliga uma.
    /// </summary>
    [Fact]
    public void RF_467_com_todas_as_condicoes_a_traducao_ocorre()
        => Assert.True(New().ShouldTranslate("texto", loopIdle: true,
                                             busyWithConfiguration: false, WindowMode.Dark));

    [Fact]
    public void RF_467_com_o_laco_rodando_nao_traduz()
        => Assert.False(New().ShouldTranslate("texto", false, false, WindowMode.Dark));

    [Fact]
    public void RF_467_durante_uma_aplicacao_de_configuracao_nao_traduz()
        => Assert.False(New().ShouldTranslate("texto", true, true, WindowMode.Dark));

    /// <summary>
    /// RF-467 — No modo sobreposição o monitoramento NÃO dispara: ela desenha sobre o texto
    /// original da tela, e um texto vindo de fora não tem posição para ser desenhado em cima.
    ///
    /// Critério de aceite do capítulo 24.
    /// </summary>
    [Fact]
    public void RF_467_no_modo_sobreposicao_o_monitoramento_nao_dispara()
        => Assert.False(New().ShouldTranslate("texto", true, false, WindowMode.Overlay));

    /// <summary>
    /// Critério de aceite do capítulo 24: "Copiar o mesmo texto duas vezes seguidas produz
    /// uma única tradução."
    /// </summary>
    [Fact]
    public void O_mesmo_texto_duas_vezes_produz_uma_unica_traducao()
    {
        var gate = New();

        Assert.True(gate.ShouldTranslate("olá", true, false, WindowMode.Dark));
        gate.Begin("olá");
        gate.Finish();

        Assert.False(gate.ShouldTranslate("olá", true, false, WindowMode.Dark));
        Assert.True(gate.ShouldTranslate("outro", true, false, WindowMode.Dark));
    }

    /// <summary>RF-468 — Uma tradução em andamento bloqueia novas até terminar.</summary>
    [Fact]
    public void RF_468_uma_traducao_em_andamento_bloqueia_as_seguintes()
    {
        var gate = New();
        gate.Begin("primeiro");

        Assert.False(gate.ShouldTranslate("segundo", true, false, WindowMode.Dark));

        gate.Finish();
        Assert.True(gate.ShouldTranslate("segundo", true, false, WindowMode.Dark));
    }

    /// <summary>
    /// RF-472 — Aplicar configurações limpa o estado: uma tradução interrompida pela
    /// reconfiguração não pode deixar o recurso travado.
    /// </summary>
    [Fact]
    public void RF_472_aplicar_configuracao_destrava_o_recurso()
    {
        var gate = New();
        gate.Begin("preso");

        gate.Reset();

        Assert.True(gate.ShouldTranslate("preso", true, false, WindowMode.Dark));
    }

    [Fact]
    public void RF_465_desligado_o_recurso_e_inerte()
        => Assert.False(new ClipboardTranslationGate { Enabled = false }
            .ShouldTranslate("texto", true, false, WindowMode.Dark));

    /// <summary>RF-470 — O original é anexado ao final, separado por duas quebras.</summary>
    [Fact]
    public void RF_470_o_original_e_anexado_ao_final()
    {
        var gate = New();
        gate.ShowOriginal = true;
        Assert.Equal("olá\n\nhello", gate.Compose("olá", "hello"));

        gate.ShowOriginal = false;
        Assert.Equal("olá", gate.Compose("olá", "hello"));
    }
}

/// <summary>RF-473 a RF-475 — Cópia do resultado para a área de transferência.</summary>
public class ClipboardWriterTests
{
    /// <summary>RF-473 — Os três formatos.</summary>
    [Theory]
    [InlineData(ClipboardCopyFormat.Ocr, "hello")]
    [InlineData(ClipboardCopyFormat.Translation, "olá")]
    [InlineData(ClipboardCopyFormat.Both, "hello\n\nolá")]
    public void RF_473_o_formato_decide_o_que_e_copiado(ClipboardCopyFormat formato, string esperado)
        => Assert.Equal(esperado,
            new ClipboardWriter { Format = formato }.Compose("hello", "olá"));

    [Fact]
    public void IV_12_o_formato_padrao_e_so_o_texto_reconhecido()
        => Assert.Equal(ClipboardCopyFormat.Ocr, new ClipboardWriter().Format);

    /// <summary>
    /// RF-475 — O editor de dicionário suspende a cópia automática, porque o usuário vai
    /// usar a área de transferência para editar.
    /// </summary>
    [Fact]
    public void RF_475_o_editor_de_dicionario_suspende_a_copia()
    {
        var writer = new ClipboardWriter { Enabled = true };
        Assert.True(writer.ShouldCopy());

        writer.Suspended = true;
        Assert.False(writer.ShouldCopy());

        writer.Suspended = false;
        Assert.True(writer.ShouldCopy());
    }

    [Fact]
    public void Desligada_a_copia_nao_acontece()
        => Assert.False(new ClipboardWriter { Enabled = false }.ShouldCopy());
}

/// <summary>Cap. 25 — Leitura em voz alta.</summary>
public class SpeechQueueTests
{
    /// <summary>
    /// Critério de aceite do capítulo 25: "Com 'aguardar o fim' ligado e um texto longo,
    /// traduções sucessivas não se sobrepõem em áudio." (RF-477)
    /// </summary>
    [Fact]
    public void RF_477_com_aguardar_o_fim_a_nova_leitura_e_descartada()
    {
        bool falando = true;
        var fila = new SpeechQueue(() => falando)
        {
            Enabled = true,
            WaitForPrevious = true,
        };

        Assert.Equal(SpeechQueue.Decision.Skip, fila.Decide("nova fala"));

        falando = false;
        Assert.Equal(SpeechQueue.Decision.Speak, fila.Decide("nova fala"));
    }

    /// <summary>RF-477 — Sem "aguardar o fim", a nova leitura INTERROMPE a anterior.</summary>
    [Fact]
    public void RF_477_sem_aguardar_o_fim_a_nova_leitura_interrompe()
    {
        var fila = new SpeechQueue(() => true)
        {
            Enabled = true,
            WaitForPrevious = false,
        };

        Assert.Equal(SpeechQueue.Decision.SpeakInterrupting, fila.Decide("nova fala"));
    }

    /// <summary>RF-480 — Sem sintetizador, a opção fica inerte SEM gerar erro.</summary>
    [Fact]
    public void RF_480_sem_sintetizador_a_opcao_fica_inerte()
    {
        var fila = new SpeechQueue(() => false)
        {
            Enabled = true,
            SynthesizerAvailable = false,
        };

        Assert.Equal(SpeechQueue.Decision.Skip, fila.Decide("texto"));
    }

    [Fact]
    public void Desligada_a_leitura_nao_acontece()
        => Assert.Equal(SpeechQueue.Decision.Skip,
            new SpeechQueue(() => false) { Enabled = false }.Decide("texto"));

    [Fact]
    public void Texto_vazio_nao_e_lido()
        => Assert.Equal(SpeechQueue.Decision.Skip,
            new SpeechQueue(() => false) { Enabled = true }.Decide("   "));

    /// <summary>
    /// RF-478 — No modo sobreposição os tokens separadores saem antes da leitura: eles são
    /// artefato do protocolo de lote e seriam lidos em voz alta.
    /// </summary>
    [Fact]
    public void RF_478_os_tokens_separadores_saem_antes_da_leitura()
    {
        string comTokens = $"{P.SeparatorToken}primeiro{P.SeparatorToken}segundo";

        string limpo = SpeechQueue.Clean(comTokens, WindowMode.Overlay, P.SeparatorToken);
        Assert.DoesNotContain(P.SeparatorToken, limpo);
        Assert.Contains("primeiro", limpo);
        Assert.Contains("segundo", limpo);
    }

    [Fact]
    public void RF_478_fora_da_sobreposicao_o_texto_passa_intacto()
    {
        string texto = $"{P.SeparatorToken}algo";
        Assert.Equal(texto, SpeechQueue.Clean(texto, WindowMode.Dark, P.SeparatorToken));
    }
}
