using Gort.Core.Model;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Gort.Ocr.Rapid.Detection;

/// <summary>
/// Detector de linhas de texto (DBNet).
///
/// Devolve, para cada linha encontrada, a caixa ALINHADA AOS EIXOS em coordenadas da imagem
/// recebida — a conversão do quadrilátero para caixa segue RF-142: mínimo e máximo dos
/// quatro pontos em cada eixo, NUNCA por diferença direta entre dois pontos, para não
/// produzir largura ou altura negativa em texto rotacionado.
/// </summary>
public sealed class TextDetector : IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly DbOptions _options;
    private readonly ChannelOrder _order;

    /// <summary>
    /// Piso do lado menor: imagens abaixo dele são ampliadas antes da inferência, para que
    /// texto pequeno demais ainda seja detectado.
    ///
    /// O padrão de referência do modelo é 736, pensado para fotografias, que chegam em
    /// qualquer tamanho. Aqui ele é 320, e a razão é medida, não estética: o programa já
    /// amplia a imagem por P-22 antes do OCR, e RF-113 diz que essa é "o ajuste de maior
    /// impacto na taxa de acerto com fontes pequenas". Aplicar 736 por cima de P-22 amplia
    /// duas vezes — numa captura real de 1592 × 554 isso levava a entrada do modelo a
    /// 2016 × 704 e a detecção de 47 ms para 95 ms, SEM reconhecer nenhuma linha a mais.
    ///
    /// Pior que o custo: uma segunda ampliação escondida dentro do motor tornaria o efeito
    /// de P-22 não-linear e inexplicável para quem mexe no controle.
    ///
    /// Este NÃO é um valor 🔒 — é um padrão de biblioteca, e a PARTE XII não se aplica a
    /// ele. P-22, que é da especificação, permanece intocado.
    /// </summary>
    public int LimitSideLength { get; init; } = 320;

    /// <summary>
    /// Teto do lado maior, para que uma região muito grande não estoure o orçamento de
    /// tempo do ciclo (P1 — latência acima de tudo).
    /// </summary>
    public int MaxSideLength { get; init; } = 2000;

    public TextDetector(string modelPath, DbOptions? options = null,
                        ChannelOrder order = ChannelOrder.Rgb,
                        SessionOptions? sessionOptions = null)
    {
        _session = sessionOptions is null
            ? new InferenceSession(modelPath)
            : new InferenceSession(modelPath, sessionOptions);
        _inputName = _session.InputMetadata.Keys.First();
        _options = options ?? new DbOptions();
        _order = order;
    }

    /// <summary>Uma linha detectada, já em coordenadas da imagem original.</summary>
    public readonly record struct Detection(Rect Box, double Score);

    public List<Detection> Detect(ImageBuffer image)
    {
        if (image.IsEmpty) return new List<Detection>();

        var (width, height) = TargetSize(image.Width, image.Height);
        var resized = ImageOps.ResizeTo(image, width, height);

        var tensor = new DenseTensor<float>(
            ImageOps.ToTensor(resized, _order), new[] { 1, 3, height, width });

        using var results = _session.Run(
            new[] { NamedOnnxValue.CreateFromTensor(_inputName, tensor) });

        var output = results.First().AsTensor<float>();
        var probability = output.ToArray();

        // A saída é [1, 1, H, W]: o mapa de probabilidade nas dimensões redimensionadas.
        int mapHeight = output.Dimensions[^2];
        int mapWidth = output.Dimensions[^1];

        var boxes = DbPostProcessor.ExtractBoxes(probability, mapWidth, mapHeight, _options);

        // Volta para as coordenadas da imagem recebida.
        double scaleX = (double)image.Width / mapWidth;
        double scaleY = (double)image.Height / mapHeight;

        var detections = new List<Detection>(boxes.Count);
        var bounds = new Rect(0, 0, image.Width, image.Height);

        foreach (var box in boxes)
        {
            var points = new (double X, double Y)[box.Corners.Length];
            for (int i = 0; i < box.Corners.Length; i++)
                points[i] = (box.Corners[i].X * scaleX, box.Corners[i].Y * scaleY);

            // RF-142 — caixa por mínimo e máximo dos quatro pontos.
            var rect = Rect.FromQuad(points).Intersect(bounds);
            if (rect.IsEmpty) continue;

            detections.Add(new Detection(rect, box.Score));
        }

        return detections;
    }

    /// <summary>
    /// Dimensões de entrada do modelo: o lado menor é levado a <see cref="LimitSideLength"/>,
    /// o lado maior é limitado por <see cref="MaxSideLength"/>, e ambos são arredondados
    /// para múltiplos de 32, que é o passo de subamostragem da rede.
    /// </summary>
    internal (int Width, int Height) TargetSize(int width, int height)
    {
        double ratio = 1.0;

        int minSide = Math.Min(width, height);
        if (minSide < LimitSideLength && minSide > 0) ratio = (double)LimitSideLength / minSide;

        int maxSide = (int)Math.Round(Math.Max(width, height) * ratio);
        if (maxSide > MaxSideLength) ratio *= (double)MaxSideLength / maxSide;

        static int ToMultipleOf32(double value)
            => Math.Max(32, (int)Math.Round(value / 32) * 32);

        return (ToMultipleOf32(width * ratio), ToMultipleOf32(height * ratio));
    }

    public void Dispose() => _session.Dispose();
}
