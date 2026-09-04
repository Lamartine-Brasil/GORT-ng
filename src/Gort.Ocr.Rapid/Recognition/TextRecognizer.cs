using Gort.Core.Model;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Gort.Ocr.Rapid.Recognition;

/// <summary>
/// Reconhecedor de texto (CRNN com decodificação CTC).
///
/// O dicionário de caracteres vem dos METADADOS do próprio modelo, e não de um arquivo
/// separado: assim, trocar o modelo — por exemplo, para um treinado em japonês — é trocar
/// um arquivo, sem tocar em código nem em dados de configuração.
/// </summary>
public sealed class TextRecognizer : IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string[] _characters;
    private readonly ChannelOrder _order;

    /// <summary>Altura a que toda linha é levada antes da inferência.</summary>
    public int ImageHeight { get; init; } = 48;

    /// <summary>Largura mínima do tensor; linhas mais estreitas são preenchidas com zeros.</summary>
    public int MinImageWidth { get; init; } = 320;

    /// <summary>Índice do símbolo em branco da decodificação CTC.</summary>
    private const int BlankIndex = 0;

    /// <param name="dictionaryPath">
    /// Dicionário de caracteres em arquivo, para modelos que não o trazem nos próprios
    /// metadados. Quando nulo, o dicionário é lido dos metadados.
    /// </param>
    public TextRecognizer(string modelPath, ChannelOrder order = ChannelOrder.Rgb,
                          SessionOptions? sessionOptions = null,
                          string? dictionaryPath = null)
    {
        _session = sessionOptions is null
            ? new InferenceSession(modelPath)
            : new InferenceSession(modelPath, sessionOptions);
        _inputName = _session.InputMetadata.Keys.First();
        _order = order;
        _characters = LoadCharacters(_session, dictionaryPath);
    }

    /// <summary>Quantidade de classes do decodificador, incluindo o branco.</summary>
    public int ClassCount => _characters.Length;

    /// <summary>Texto reconhecido e a confiança média dos caracteres aceitos.</summary>
    public readonly record struct Recognition(string Text, double Confidence);

    public Recognition Recognize(ImageBuffer line)
    {
        if (line.IsEmpty) return new Recognition("", 0);

        // Altura fixa, largura proporcional; o tensor é preenchido com zeros até a largura
        // mínima, que é como o modelo foi treinado a receber linhas curtas.
        double aspect = (double)line.Width / Math.Max(1, line.Height);
        int resizedWidth = Math.Max(1, (int)Math.Ceiling(ImageHeight * aspect));
        int tensorWidth = Math.Max(MinImageWidth, resizedWidth);

        var resized = ImageOps.ResizeTo(line, resizedWidth, ImageHeight);
        var values = ImageOps.ToTensor(resized, _order, tensorWidth, ImageHeight);

        var tensor = new DenseTensor<float>(values, new[] { 1, 3, ImageHeight, tensorWidth });

        using var results = _session.Run(
            new[] { NamedOnnxValue.CreateFromTensor(_inputName, tensor) });

        var output = results.First().AsTensor<float>();
        return DecodeCtc(output);
    }

    /// <summary>
    /// Decodificação CTC gulosa: por passo de tempo toma-se a classe de maior probabilidade,
    /// descartam-se os brancos e colapsam-se as repetições consecutivas.
    /// </summary>
    private Recognition DecodeCtc(Tensor<float> output)
    {
        int steps = output.Dimensions[1];
        int classes = output.Dimensions[2];

        var text = new System.Text.StringBuilder();
        double confidenceSum = 0;
        int accepted = 0;
        int previous = -1;

        for (int t = 0; t < steps; t++)
        {
            int best = 0;
            float bestValue = float.MinValue;
            for (int c = 0; c < classes; c++)
            {
                float v = output[0, t, c];
                if (v > bestValue) { bestValue = v; best = c; }
            }

            // Colapso de repetições: só o primeiro de uma sequência igual conta.
            if (best != previous && best != BlankIndex)
            {
                if (best < _characters.Length) text.Append(_characters[best]);
                confidenceSum += bestValue;
                accepted++;
            }
            previous = best;
        }

        return new Recognition(text.ToString(),
                               accepted == 0 ? 0 : confidenceSum / accepted);
    }

    /// <summary>
    /// Monta a tabela de classes a partir do dicionário embutido no modelo.
    ///
    /// A convenção do modelo de referência é: o símbolo em branco na posição 0, depois as
    /// linhas do dicionário, e um espaço no fim. Quando a contagem não bate com a dimensão
    /// de saída, tenta-se sem o espaço final — é a única variação conhecida entre modelos
    /// dessa família, e adivinhar errado produziria texto deslocado em um caractere.
    /// </summary>
    private static string[] LoadCharacters(InferenceSession session, string? dictionaryPath)
    {
        List<string> lines;

        if (!string.IsNullOrEmpty(dictionaryPath) && File.Exists(dictionaryPath))
        {
            // Um dicionário em arquivo tem precedência: é a convenção dos modelos por
            // idioma, que não embutem a tabela nos metadados.
            lines = File.ReadAllLines(dictionaryPath).ToList();

            // Um arquivo terminado em quebra de linha produz uma entrada vazia final que
            // deslocaria toda a decodificação em um caractere.
            while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        }
        else if (session.ModelMetadata.CustomMetadataMap.TryGetValue("character", out var raw)
                 && !string.IsNullOrEmpty(raw))
        {
            lines = raw.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        }
        else
        {
            throw new InvalidOperationException(
                "O modelo de reconhecimento não traz o dicionário de caracteres nos seus " +
                "metadados e nenhum arquivo de dicionário foi informado; sem ele não é " +
                "possível decodificar a saída.");
        }

        int expected = session.OutputMetadata.Values.First().Dimensions[^1];

        var withSpace = new List<string> { "<blank>" };
        withSpace.AddRange(lines);
        withSpace.Add(" ");
        if (expected <= 0 || withSpace.Count == expected) return withSpace.ToArray();

        var withoutSpace = new List<string> { "<blank>" };
        withoutSpace.AddRange(lines);
        if (withoutSpace.Count == expected) return withoutSpace.ToArray();

        // Nenhuma das duas convenções bate: usa-se a mais comum e o desencontro é problema
        // do modelo, não do programa.
        return withSpace.ToArray();
    }

    public void Dispose() => _session.Dispose();
}
