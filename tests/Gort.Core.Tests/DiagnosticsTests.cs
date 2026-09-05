using System.Text.Json;
using Gort.Core.Caching;
using Gort.Core.Diagnostics;
using Gort.Core.Model;
using Xunit;

namespace Gort.Core.Tests;

/// <summary>Cap. 27 — Depuração e diagnóstico.</summary>
public class AnalysisSnapshotTests
{
    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "gort-diag", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static IReadOnlyList<RegionResult> SampleRegions()
    {
        var line = LineBuilder.Horizontal("Hello world", 10, 20, 24);
        var block = new Gort.Core.Model.TranslationBlock(line) { TranslatedText = "Olá mundo" };

        return new[]
        {
            new RegionResult
            {
                Index = 0,
                ScreenRect = new Rect(100, 200, 800, 300),
                Lines = new[] { line },
                Blocks = new[] { block },
                ResultBox = line.Box,
                RawTranslatedText = "//////Olá mundo\n",
                UsesAutoColor = true,
                AutoColors = new AutoColorResult?[]
                {
                    new(new Rgba(255, 255, 255), new Rgba(10, 20, 60, 170), 4, 12.5, false, true),
                },
            },
        };
    }

    /// <summary>
    /// RF-492 — O retrato contém instante, modo de janela, motor, serviço, textos, e por
    /// área: índice, retângulos, textos, cores automáticas, todas as linhas com suas
    /// palavras e caixas, e todos os blocos com seus quatro retângulos.
    /// </summary>
    [Fact]
    public void RF_492_o_retrato_contem_tudo_o_que_o_ciclo_decidiu()
    {
        var snapshot = DiagnosticRecorder.Build(
            SampleRegions(), "Hello world ", "Olá mundo",
            "overlay", "modern", "webfree");

        Assert.Equal("overlay", snapshot.WindowMode);
        Assert.Equal("modern", snapshot.OcrEngine);
        Assert.Equal("webfree", snapshot.TranslationService);
        Assert.Equal("Hello world ", snapshot.RecognizedText);

        var area = Assert.Single(snapshot.Areas);
        Assert.Equal(0, area.Index);
        Assert.Equal(100, area.AreaRect.X);

        // Todas as linhas, com suas palavras e caixas.
        var line = Assert.Single(area.Lines);
        Assert.Equal(2, line.Words.Count);
        Assert.Equal("Hello", line.Words[0].Text);
        Assert.True(line.Words[0].Box.Width > 0);

        // Todos os blocos, com os QUATRO retângulos.
        var block = Assert.Single(area.Blocks);
        Assert.Equal("Olá mundo", block.Translated);
        Assert.NotNull(block.SourceBox);
        Assert.NotNull(block.ViewBox);
        Assert.NotNull(block.ContentBox);
        Assert.NotNull(block.LinesBox);

        // Cores automáticas, com os indicadores de qualidade.
        var color = Assert.Single(area.AutoColors);
        Assert.Equal(4, color.SupportingWords);
        Assert.True(color.ContrastCorrected);
    }

    /// <summary>
    /// RF-492 — O nome do arquivo tem data e hora até MILISSEGUNDOS: um laço de 300 ms
    /// produz mais de três retratos por segundo.
    /// </summary>
    [Fact]
    public void RF_492_o_nome_do_arquivo_tem_milissegundos()
    {
        var a = new AnalysisSnapshot { Instant = new DateTime(2026, 3, 4, 15, 30, 45, 123) };
        var b = new AnalysisSnapshot { Instant = new DateTime(2026, 3, 4, 15, 30, 45, 456) };

        Assert.NotEqual(a.FileName, b.FileName);
        Assert.Contains("123", a.FileName);
    }

    [Fact]
    public void O_retrato_e_gravado_na_pasta_dedicada_e_e_json_valido()
    {
        string dir = TempDir();
        var snapshot = DiagnosticRecorder.Build(
            SampleRegions(), "Hello", "Olá", "dark", "modern", "webfree");

        string path = snapshot.Save(dir);

        Assert.True(File.Exists(path));
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("dark", document.RootElement.GetProperty("WindowMode").GetString());
    }

    /// <summary>
    /// RF-493 — No modo sobreposição o retrato só é gravado DEPOIS que o desenho terminou.
    /// </summary>
    [Fact]
    public void RF_493_no_modo_sobreposicao_o_retrato_espera_o_desenho()
    {
        string dir = TempDir();
        var recorder = new DiagnosticRecorder(dir);

        var snapshot = DiagnosticRecorder.Build(
            SampleRegions(), "Hello", "Olá", "overlay", "modern", "webfree");

        recorder.Record(snapshot, waitsForDrawing: true);

        // Ainda não gravou.
        Assert.Empty(Directory.GetFiles(dir));
        Assert.True(recorder.HasPending);

        recorder.CompleteDrawing(new SnapshotDrawing
        {
            TotalMs = 12.5,
            LayoutAndDrawMs = 8.0,
            CacheHits = 45,
            CacheMisses = 24,
        });

        var files = Directory.GetFiles(dir);
        Assert.Single(files);

        using var document = JsonDocument.Parse(File.ReadAllText(files[0]));
        var drawing = document.RootElement.GetProperty("Drawing");
        Assert.Equal(45, drawing.GetProperty("CacheHits").GetInt32());
    }

    /// <summary>
    /// RF-495 — Se um ciclo seguinte começar antes que o desenho complete o retrato, o
    /// pendente é gravado SEM a parte de desenho, e NÃO descartado.
    ///
    /// Perder o retrato justamente do quadro em que o desenho demorou seria perder a
    /// evidência do problema que se quer investigar.
    /// </summary>
    [Fact]
    public void RF_495_um_retrato_pendente_e_gravado_sem_o_desenho_e_nao_descartado()
    {
        string dir = TempDir();
        var recorder = new DiagnosticRecorder(dir);

        var primeiro = DiagnosticRecorder.Build(
            SampleRegions(), "primeiro", "um", "overlay", "modern", "webfree");
        recorder.Record(primeiro, waitsForDrawing: true);

        // O ciclo seguinte começa antes de o desenho terminar.
        var segundo = DiagnosticRecorder.Build(
            SampleRegions(), "segundo", "dois", "overlay", "modern", "webfree");
        recorder.Record(segundo, waitsForDrawing: true);

        // O primeiro foi gravado, sem a parte de desenho.
        var files = Directory.GetFiles(dir);
        Assert.Single(files);

        using var document = JsonDocument.Parse(File.ReadAllText(files[0]));
        Assert.Equal("primeiro", document.RootElement.GetProperty("RecognizedText").GetString());
        Assert.False(document.RootElement.TryGetProperty("Drawing", out _));
    }

    /// <summary>Fora da sobreposição, o retrato é gravado na hora.</summary>
    [Fact]
    public void Fora_da_sobreposicao_o_retrato_e_gravado_imediatamente()
    {
        string dir = TempDir();
        var recorder = new DiagnosticRecorder(dir);

        recorder.Record(DiagnosticRecorder.Build(
            SampleRegions(), "x", "y", "dark", "modern", "webfree"), waitsForDrawing: false);

        Assert.Single(Directory.GetFiles(dir));
        Assert.False(recorder.HasPending);
    }

    /// <summary>
    /// RF-493 / RF-494 — O retrato do desenho traz o tamanho de fonte final de cada bloco,
    /// os quatro retângulos, as linhas após a quebra e os tempos.
    ///
    /// Critério de aceite do capítulo 27: "Ativar 'salvar resultado de análise' e executar
    /// um ciclo no modo sobreposição produz um arquivo contendo o tamanho de fonte final de
    /// cada bloco."
    /// </summary>
    [Fact]
    public void RF_493_o_arquivo_contem_o_tamanho_de_fonte_final_de_cada_bloco()
    {
        string dir = TempDir();
        var recorder = new DiagnosticRecorder(dir);

        recorder.Record(DiagnosticRecorder.Build(
            SampleRegions(), "x", "y", "overlay", "modern", "webfree"), waitsForDrawing: true);

        recorder.CompleteDrawing(new SnapshotDrawing
        {
            WindowRect = SnapshotRect.From(new Rect(0, 0, 1920, 1080)),
            TotalMs = 20, SizeAndPositionMs = 3, LayoutAndDrawMs = 12, PresentMs = 5,
            CacheHits = 45, CacheMisses = 24,
            Options = new Dictionary<string, object> { ["contorno"] = true },
            Blocks =
            {
                new SnapshotDrawnBlock
                {
                    Text = "Olá mundo",
                    FontSize = 14.7,
                    PreferredSize = 23.0,
                    MinimumSize = 10,
                    EstimatedOriginalSize = 20,
                    LineAdvance = 17.6,
                    Lines = { "Olá", "mundo" },
                    UsedAutoColor = true,
                    ContrastCorrected = true,
                    Clipped = false,
                    FontFamily = "Fonte",
                },
            },
        });

        string json = File.ReadAllText(Directory.GetFiles(dir)[0]);
        using var document = JsonDocument.Parse(json);

        var block = document.RootElement.GetProperty("Drawing").GetProperty("Blocks")[0];
        Assert.Equal(14.7, block.GetProperty("FontSize").GetDouble(), 3);
        Assert.Equal(23.0, block.GetProperty("PreferredSize").GetDouble(), 3);
        Assert.Equal(2, block.GetProperty("Lines").GetArrayLength());

        var drawing = document.RootElement.GetProperty("Drawing");
        Assert.Equal(20, drawing.GetProperty("TotalMs").GetDouble());
        Assert.Equal(24, drawing.GetProperty("CacheMisses").GetInt32());
    }
}

/// <summary>RF-496 — Gravação do resultado em arquivo, no formato do banco de dados.</summary>
public class ResultFileWriterTests
{
    private static string TempFile()
        => Path.Combine(Path.GetTempPath(), "gort-resultado",
                        Guid.NewGuid().ToString("N") + ".txt");

    /// <summary>
    /// RF-496 — O formato é o MESMO do banco de dados, para que o usuário construa bancos a
    /// partir do uso real e depois os carregue como fonte local.
    /// </summary>
    [Fact]
    public void RF_496_o_par_gravado_e_lido_de_volta_pelo_banco_de_dados()
    {
        string file = TempFile();
        var writer = new ResultFileWriter(file) { Enabled = true };

        Assert.True(writer.Write("Hello world", "Olá mundo"));

        var pairs = PairFile.Load(file);
        var pair = Assert.Single(pairs);
        Assert.Equal("Hello world", pair.Source);
        Assert.Equal("Olá mundo", pair.Target);
    }

    [Fact]
    public void Desligada_a_gravacao_nao_acontece()
    {
        string file = TempFile();
        Assert.False(new ResultFileWriter(file) { Enabled = false }.Write("a", "b"));
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void Um_texto_reconhecido_vazio_nao_vira_entrada_de_banco()
    {
        var writer = new ResultFileWriter(TempFile()) { Enabled = true };
        Assert.False(writer.Write("", "olá"));
        Assert.False(writer.Write("   ", "olá"));
    }

    [Fact]
    public void O_mesmo_par_duas_vezes_seguidas_e_gravado_uma_vez()
    {
        string file = TempFile();
        var writer = new ResultFileWriter(file) { Enabled = true };

        Assert.True(writer.Write("Hello", "Olá"));
        Assert.False(writer.Write("Hello", "Olá"));
        Assert.True(writer.Write("Bye", "Tchau"));

        Assert.Equal(2, PairFile.Load(file).Count);
    }
}

/// <summary>RF-498 — Contadores e registro de mensagens.</summary>
public class DiagnosticCountersTests
{
    [Fact]
    public void RF_498_os_contadores_acompanham_ocr_traducoes_e_rede()
    {
        var counters = new DiagnosticCounters();

        counters.RecordOcr();
        counters.RecordOcr();
        counters.RecordTranslation(networkCalls: 3);
        counters.RecordError("falhou");

        Assert.Equal(2, counters.OcrAttempts);
        Assert.Equal(1, counters.Translations);
        Assert.Equal(3, counters.NetworkCalls);
        Assert.Equal(1, counters.Errors);
        Assert.Contains(counters.Messages, m => m.Contains("falhou"));
    }

    /// <summary>O registro tem teto, para não crescer sem limite numa sessão longa.</summary>
    [Fact]
    public void O_registro_de_mensagens_tem_teto()
    {
        var counters = new DiagnosticCounters { MaxMessages = 10 };
        for (int i = 0; i < 50; i++) counters.Log($"mensagem {i}");

        Assert.Equal(10, counters.Messages.Count);
        Assert.Contains(counters.Messages, m => m.Contains("mensagem 49"));
        Assert.DoesNotContain(counters.Messages, m => m.Contains("mensagem 0"));
    }

    [Fact]
    public void Reiniciar_zera_tudo()
    {
        var counters = new DiagnosticCounters();
        counters.RecordOcr();
        counters.Log("algo");
        counters.Reset();

        Assert.Equal(0, counters.OcrAttempts);
        Assert.Empty(counters.Messages);
    }
}
