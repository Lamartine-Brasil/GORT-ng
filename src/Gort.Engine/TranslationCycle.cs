using System.Text;
using Gort.Core.Caching;
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

        var regions = new List<RegionResult>(captured.Count);
        var blockTexts = new List<string>();
        var blockOwners = new List<(int RegionIndex, int BlockIndex)>();
        var recognized = new StringBuilder();

        foreach (var region in captured)
        {
            // Passo 8 — recorte pelas exclusões, filtro de cor, erosão e ampliação.
            var processed = Preprocessor.Process(
                region.Image, areas.ExclusionsIn(region.Index), settings.Filter);

            // Passo 9 — reconhecimento.
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

            regions.Add(new RegionResult
            {
                Index = region.Index,
                ScreenRect = region.ScreenRect,
                ClientOrigin = region.ClientOrigin,
                Lines = lines,
                Blocks = blocks,
                ResultBox = Rect.UnionAll(lines.Select(l => l.Box)),   // RF-156
            });
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
