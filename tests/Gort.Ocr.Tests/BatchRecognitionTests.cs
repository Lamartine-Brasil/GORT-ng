using System.Diagnostics;
using Gort.Core.Model;
using Gort.Ocr.Rapid;
using Gort.Ocr.Rapid.Recognition;
using Xunit;
using Xunit.Abstractions;

namespace Gort.Ocr.Tests;

/// <summary>
/// Reconhecimento em LOTE. O ganho é de latência, mas a exigência é de EQUIVALÊNCIA: um
/// lote que reconhece diferente de uma linha por vez não é otimização, é regressão.
/// </summary>
public class BatchRecognitionTests
{
    private readonly ITestOutputHelper _output;

    public BatchRecognitionTests(ITestOutputHelper output) => _output = output;

    private static string? RecognitionModel() => ModelLocator.Find(ModelLocator.RecognitionModel);

    /// <summary>Uma faixa branca com barras escuras — o que o reconhecedor recebe de fato.</summary>
    private static ImageBuffer Line(int width, int bars)
    {
        var image = ImageBuffer.Allocate(width, 32, PixelFormat.Bgra32);
        for (int i = 0; i < image.Pixels.Length; i++) image.Pixels[i] = 255;

        for (int b = 0; b < bars; b++)
        {
            int x0 = 4 + b * 14;
            for (int y = 6; y < 26; y++)
            {
                for (int x = x0; x < Math.Min(x0 + 6, width); x++)
                {
                    int o = image.OffsetOf(x, y);
                    image.Pixels[o] = image.Pixels[o + 1] = image.Pixels[o + 2] = 20;
                }
            }
        }
        return image;
    }

    /// <summary>
    /// O lote devolve EXATAMENTE o mesmo que uma chamada por linha, e na MESMA ORDEM — o
    /// agrupamento reordena internamente por largura, e restaurar a ordem é parte do
    /// contrato.
    /// </summary>
    [Fact]
    public void O_lote_devolve_o_mesmo_que_linha_a_linha_e_na_mesma_ordem()
    {
        string? model = RecognitionModel();
        if (model is null) return;   // sem modelo instalado, nada a comparar

        using var recognizer = new TextRecognizer(model);

        // Larguras deliberadamente embaralhadas: é o que exercita a reordenação.
        var lines = new[]
        {
            Line(600, 12), Line(120, 2), Line(900, 20),
            Line(200, 4), Line(340, 8), Line(80, 1), Line(1200, 26),
        };

        var oneByOne = lines.Select(recognizer.Recognize).ToArray();
        var batched = recognizer.RecognizeBatch(lines);

        Assert.Equal(oneByOne.Length, batched.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            Assert.Equal(oneByOne[i].Text, batched[i].Text);
        }
    }

    /// <summary>Uma linha vazia continua vazia, e não desloca as demais.</summary>
    [Fact]
    public void Linhas_vazias_nao_deslocam_o_resultado()
    {
        string? model = RecognitionModel();
        if (model is null) return;

        using var recognizer = new TextRecognizer(model);

        var lines = new[]
        {
            Line(300, 6),
            ImageBuffer.Allocate(0, 0, PixelFormat.Bgra32),
            Line(300, 6),
        };

        var batched = recognizer.RecognizeBatch(lines);

        Assert.Equal(3, batched.Length);
        Assert.Equal("", batched[1].Text);
        Assert.Equal(batched[0].Text, batched[2].Text);
    }

    [Fact]
    public void Um_lote_vazio_devolve_vazio()
    {
        string? model = RecognitionModel();
        if (model is null) return;

        using var recognizer = new TextRecognizer(model);
        Assert.Empty(recognizer.RecognizeBatch(Array.Empty<ImageBuffer>()));
    }

    /// <summary>
    /// A medição que motivou o lote: com uma tela cheia de linhas, o custo fixo de uma
    /// chamada por linha domina. O teste não FALHA por tempo — medida de tempo em máquina
    /// compartilhada é instável —, mas registra os dois números para quem for ajustar.
    /// </summary>
    [Fact]
    public void Medida_do_ganho_do_lote()
    {
        string? model = RecognitionModel();
        if (model is null) { _output.WriteLine("Sem modelo instalado; medição pulada."); return; }

        using var recognizer = new TextRecognizer(model);

        var lines = Enumerable.Range(0, 40)
            .Select(i => Line(200 + i % 7 * 120, 4 + i % 9))
            .ToArray();

        // Uma passagem de aquecimento: a primeira chamada paga a preparação da sessão.
        recognizer.Recognize(lines[0]);

        var watch = Stopwatch.StartNew();
        foreach (var line in lines) recognizer.Recognize(line);
        double single = watch.Elapsed.TotalMilliseconds;

        watch.Restart();
        recognizer.RecognizeBatch(lines);
        double batch = watch.Elapsed.TotalMilliseconds;

        _output.WriteLine($"{lines.Length} linhas — uma por vez: {single:0.#} ms · "
                          + $"em lote de {recognizer.BatchSize}: {batch:0.#} ms "
                          + $"({(single <= 0 ? 0 : (1 - batch / single) * 100):0.#}% menos)");

        Assert.True(batch > 0);
    }
}
