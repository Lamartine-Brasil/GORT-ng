using System.Text;
using Gort.Core.Caching;
using Gort.Core.ColorAnalysis;
using Gort.Core.Diagnostics;
using Gort.Core.Catalog;
using Gort.Core.Imaging;
using Gort.Core.Model;
using Gort.Core.Ocr;
using Gort.Core.Regions;
using Gort.Core.Structuring;
using Gort.Core.Translation;
using Gort.Platform.Capture;

namespace Gort.Engine;

/// <summary>Tudo o que um ciclo precisa saber, resolvido pela aplicação de configuração.</summary>
public sealed class CycleSettings
{
    public required ITranslationService Service { get; init; }
    public required TranslationContext TranslationContext { get; init; }
    public required IOcrEngine Ocr { get; init; }

    /// <summary>Código de idioma que o motor de OCR recebe.</summary>
    public required string OcrLanguage { get; init; }

    public FilterSettings Filter { get; init; } = new();
    public CaptureSource Source { get; init; } = CaptureSource.Screen;

    /// <summary>RF-158 — Fusão de linhas em blocos.</summary>
    public bool MergeLines { get; init; }

    public TextProcessingOptions Text { get; init; } = new();

    public CorrectionDictionary? Dictionary { get; init; }

    /// <summary>RF-189 — Numeração de áreas no texto exibido.</summary>
    public bool NumberAreas { get; init; }

    /// <summary>
    /// RF-098 — A imagem ORIGINAL só é pedida quando o modo é sobreposição E a cor
    /// automática está ativa.
    /// </summary>
    public bool NeedsOriginalImage { get; init; }

    /// <summary>RF-394 / RF-413 — Opções da análise automática de cor.</summary>
    public AutoColorOptions? AutoColor { get; init; }

    /// <summary>
    /// RF-498 / RF-500 — Observação do ciclo. Nulo fora do modo de depuração, e é assim que
    /// RF-490 é cumprido: desligar o modo restaura o comportamento normal sem reiniciar.
    /// </summary>
    public CycleDiagnostics? Diagnostics { get; init; }

    /// <summary>RF-554 / RF-559 — Medidor das imagens de região vivas.</summary>
    public LiveImageMeter? ImageMeter { get; init; }
}

/// <summary>O que um ciclo produziu.</summary>
public sealed class CycleResult
{
    public static readonly CycleResult Empty = new()
    {
        Regions = Array.Empty<RegionResult>(),
        RecognizedText = "",
        DisplayText = "",
    };

    /// <summary>Um resultado por área que produziu imagem.</summary>
    public required IReadOnlyList<RegionResult> Regions { get; init; }

    /// <summary>
    /// RF-192 / RF-193 — Texto reconhecido concatenado de todas as áreas, DEPOIS do
    /// tratamento textual e ANTES da tradução. É sobre ele que a detecção de mudança compara.
    /// </summary>
    public required string RecognizedText { get; init; }

    /// <summary>Texto pronto para a janela de tradução (RF-189 a RF-191).</summary>
    public required string DisplayText { get; init; }

    /// <summary>RF-236 — Mensagem de erro do serviço, quando houve.</summary>
    public string? Error { get; init; }

    /// <summary>Quantos textos foram efetivamente à rede neste ciclo.</summary>
    public int NetworkCount { get; init; }

    public bool IsEmpty => RecognizedText.Length == 0;
}

/// <summary>
/// Cap. 8 — O fluxo principal de um ciclo: capturar, pré-processar, reconhecer, agrupar,
/// tratar o texto, consultar cache, traduzir e montar o texto de exibição.
///
/// Esta classe faz os passos 7 a 13 do fluxo. O LAÇO que os repete, a detecção de mudança e
/// o desenho ficam fora dela — é o que permite executar um ciclo isolado ("traduzir uma
/// vez") com exatamente o mesmo caminho da tradução contínua.
///
/// RF-577 — Nada aqui conhece o sistema operacional: a captura entra pela abstração.
/// </summary>
public sealed class TranslationCycle
{
    private readonly ScreenCapture _capture;
    private readonly TranslationPipeline _pipeline;

    public TranslationCycle(ScreenCapture capture, TranslationPipeline pipeline)
    {
        _capture = capture;
        _pipeline = pipeline;
    }

    /// <summary>
    /// Cap. 20 — Análise de cor de cada bloco da região.
    ///
    /// A imagem passada é a CAPTURA BRUTA, sem filtro nem binarização (RF-395); os
    /// retângulos vêm no espaço da imagem tratada, e o analisador faz a conversão por escala
    /// em cada eixo.
    /// </summary>
    private static IReadOnlyList<AutoColorResult?> AnalyseColors(
        ImageBuffer original, ImageBuffer processed,
        IReadOnlyList<TranslationBlock> blocks, AutoColorOptions options)
    {
        var colors = new AutoColorResult?[blocks.Count];

        for (int i = 0; i < blocks.Count; i++)
        {
            var words = blocks[i].Lines.SelectMany(l => l.Words).Select(w => w.Box).ToList();

            colors[i] = AutoColorAnalyzer.Analyze(
                original, blocks[i].SourceBox, words,
                processed.Width, processed.Height, options);
        }
        return colors;
    }

    public async Task<CycleResult> RunAsync(BuiltAreas areas, CycleSettings settings)
    {
        // Passo 7 — obter a imagem de cada área. Um retângulo que não produz imagem tem seu
        // índice ausente, e isso não é erro (6.2).
        var captured = _capture.Capture(new CaptureRequest
        {
            Rects = areas.Captures,
            Source = settings.Source,
            NeedsOriginal = settings.NeedsOriginalImage,
        });

        if (captured.Count == 0) return CycleResult.Empty;

        // RF-554 — não há mais de um conjunto de imagens de região vivo por vez: o conjunto
        // anterior já foi solto no ciclo passado, e o medidor recomeça com este.
        settings.ImageMeter?.Reset();

        var regions = new List<RegionResult>(captured.Count);
        var blockTexts = new List<string>();
        var blockOwners = new List<(int RegionIndex, int BlockIndex)>();
        var recognized = new StringBuilder();

        foreach (var region in captured)
        {
            settings.ImageMeter?.Add(region.Image.ByteCount);

            // RF-500 — "salvar captura": a imagem que entrou, antes de qualquer tratamento.
            var debug = settings.Diagnostics;
            if (debug?.Options.NativeSaveCapture == true)
                debug.SaveImage?.Invoke($"captura-area{region.Index}", region.Image);

            // Passo 8 — recorte pelas exclusões, filtro de cor, erosão e ampliação.
            var processed = Preprocessor.Process(
                region.Image, areas.ExclusionsIn(region.Index), settings.Filter);

            // RF-500 — "salvar resultado da captura": a imagem que o OCR realmente vê. É o
            // par da anterior: a diferença entre as duas é o efeito do filtro e da ampliação.
            if (debug?.Options.NativeSaveResult == true)
                debug.SaveImage?.Invoke($"tratada-area{region.Index}", processed);

            // Passo 9 — reconhecimento. RF-498 — a tentativa é contada aqui, antes do
            // resultado, porque o que interessa medir é quantas vezes o motor foi chamado.
            debug?.Counters?.RecordOcr();
            var ocr = settings.Ocr.Recognize(processed, settings.OcrLanguage);

            // RF-145 — o erro do motor é conteúdo, não exceção: ele aparece no lugar do
            // texto e a comparação de mudança funciona normalmente.
            if (ocr.ErrorMessage is not null)
            {
                return new CycleResult
                {
                    Regions = Array.Empty<RegionResult>(),
                    RecognizedText = ocr.ErrorMessage,
                    DisplayText = ocr.ErrorMessage,
                    Error = ocr.ErrorMessage,
                };
            }

            // Passo 10 — linhas e blocos.
            var lines = ocr.BuildLines();
            var blocks = BlockGrouper.Group(lines, settings.MergeLines,
                                            settings.Text.RemoveSpaces,
                                            settings.Text.OneLinePerTranslation);

            // Passo 11 — tratamento textual de cada bloco.
            for (int b = 0; b < blocks.Count; b++)
            {
                string treated = TextPostProcessor.Treat(
                    blocks[b].SourceText, settings.Text, settings.Dictionary);
                treated = TextPostProcessor.JoinLineBreaks(treated, settings.Text);

                blockTexts.Add(treated);
                blockOwners.Add((regions.Count, b));
                recognized.Append(treated);
            }

            var result = new RegionResult
            {
                Index = region.Index,
                ScreenRect = region.ScreenRect,
                ClientOrigin = region.ClientOrigin,
                Lines = lines,
                Blocks = blocks,
                ResultBox = Rect.UnionAll(lines.Select(l => l.Box)),   // RF-156
            };

            // Passo 14 — se o modo é sobreposição e a cor automática está ativa, a análise
            // roda para cada bloco usando a imagem ORIGINAL (RF-394, RF-395).
            //
            // RF-099 — a imagem original é liberada assim que a análise da região termina;
            // com ampliação, cada região ocupa dezenas de megabytes.
            if (settings.NeedsOriginalImage && settings.AutoColor is { Enabled: true })
            {
                result.UsesAutoColor = true;
                result.AutoColors = AnalyseColors(region.Image, processed, blocks, settings.AutoColor);
            }

            regions.Add(result);

            // RF-554 / RF-099 — a partir daqui esta região não é mais necessária: o OCR já
            // leu a imagem tratada e a análise de cor já leu a original. Soltar aqui, e não
            // ao fim do ciclo, é o que impede o pico de memória de crescer com o número de
            // áreas — que é justamente quando o usuário está no limite.
            settings.ImageMeter?.Remove(region.Image.ByteCount);
            region.Release();
        }

        string recognizedText = recognized.ToString();

        // Passo 12 — tradução. O pipeline consulta a coletânea e a memória antes de
        // qualquer chamada de rede e envia só o que falta, em uma única requisição.
        var batch = await _pipeline.TranslateAsync(blockTexts, settings.Service,
                                                   settings.TranslationContext)
                                   .ConfigureAwait(false);

        // Passo 13 — distribuir a resposta pelos blocos.
        for (int i = 0; i < blockOwners.Count && i < batch.Translations.Count; i++)
        {
            var (regionIndex, blockIndex) = blockOwners[i];
            regions[regionIndex].Blocks[blockIndex].TranslatedText = batch.Translations[i];
        }

        foreach (var region in regions) region.RawTranslatedText = batch.Combined;

        // RF-498 — uma tradução por ciclo, com quantos textos foram de fato à rede.
        settings.Diagnostics?.Counters?.RecordTranslation(batch.NetworkCount);

        // RF-189 a RF-191 — montagem do texto exibido.
        var entries = new List<(int AreaIndex, string Recognized, string? Translated)>();
        for (int i = 0; i < blockOwners.Count; i++)
        {
            var (regionIndex, blockIndex) = blockOwners[i];
            entries.Add((regions[regionIndex].Index, blockTexts[i],
                         regions[regionIndex].Blocks[blockIndex].TranslatedText));
        }

        return new CycleResult
        {
            Regions = regions,
            RecognizedText = recognizedText,
            DisplayText = TextPostProcessor.BuildDisplayText(
                entries, regions.Count, settings.NumberAreas),
            Error = batch.Error,
            NetworkCount = batch.NetworkCount,
        };
    }
}
