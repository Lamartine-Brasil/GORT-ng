using Gort.Core.Model;

namespace Gort.Core.Imaging;

/// <summary>
/// RF-570 — "se detectar que a captura devolve apenas quadros pretos, deve SUGERIR isso ao
/// usuário em vez de exibir tradução vazia repetidamente".
///
/// O sintoma de um jogo em tela cheia exclusiva é esse: a captura funciona, não dá erro, e
/// devolve preto. Sem esta detecção o usuário veria a tradução simplesmente não aparecer,
/// ciclo após ciclo, sem nada que apontasse a causa — e a causa é justamente a que ele menos
/// suspeitaria, porque o jogo está bem visível na tela dele.
/// </summary>
public static class BlankFrameDetector
{
    /// <summary>
    /// Quantos quadros em branco seguidos antes de sugerir.
    ///
    /// Não é um valor 🔒 — nenhum parâmetro da PARTE IV o define. Três existe porque um
    /// quadro preto isolado é comum e inócuo: uma transição de cena, um carregamento, a tela
    /// apagando por um instante. Sugerir na primeira vez seria acusar o usuário de algo que
    /// se resolve sozinho no ciclo seguinte.
    /// </summary>
    public const int FramesBeforeSuggesting = 3;

    /// <summary>
    /// Quanta variação um quadro pode ter e ainda contar como em branco, por canal.
    ///
    /// Não é zero porque compressão de vídeo e escalonamento de tela deixam ruído: um preto
    /// "de verdade" numa captura real chega com valores de 0 a 2.
    /// </summary>
    public const int Tolerance = 4;

    /// <summary>
    /// Verdadeiro quando a imagem é UNIFORME — todos os pixels dentro da tolerância do
    /// primeiro. Preto é o caso que RF-570 nomeia, mas branco uniforme tem a mesma causa e o
    /// mesmo remédio, e o OCR não acha texto em nenhum dos dois.
    ///
    /// A varredura é AMOSTRADA: percorrer cada pixel de uma região ampliada custaria mais
    /// que o pré-processamento inteiro, e uma imagem uniforme é uniforme em qualquer amostra.
    /// </summary>
    public static bool IsBlank(ImageBuffer image)
    {
        if (image.IsEmpty) return true;

        var (b0, g0, r0, _) = image.GetPixel(0, 0);

        // Um passo que cobre a imagem inteira em algumas centenas de leituras, qualquer que
        // seja o tamanho dela.
        int stepX = Math.Max(1, image.Width / 32);
        int stepY = Math.Max(1, image.Height / 32);

        for (int y = 0; y < image.Height; y += stepY)
        {
            for (int x = 0; x < image.Width; x += stepX)
            {
                var (b, g, r, _) = image.GetPixel(x, y);

                if (Math.Abs(b - b0) > Tolerance
                    || Math.Abs(g - g0) > Tolerance
                    || Math.Abs(r - r0) > Tolerance)
                {
                    return false;
                }
            }
        }

        return true;
    }
}

/// <summary>
/// RF-570 — Conta quadros em branco seguidos e diz quando é hora de sugerir.
///
/// A sugestão sai UMA VEZ por sequência: repeti-la a cada ciclo transformaria um aviso útil
/// em ruído, e o usuário aprenderia a ignorá-lo justamente quando ele importa.
/// </summary>
public sealed class BlankFrameWatch
{
    private int _streak;
    private bool _suggested;

    public int Threshold { get; init; } = BlankFrameDetector.FramesBeforeSuggesting;

    public int Streak => _streak;

    /// <summary>
    /// Registra um quadro. Devolve verdadeiro no ciclo em que a sugestão deve aparecer.
    /// </summary>
    public bool Observe(bool blank)
    {
        if (!blank)
        {
            // Um quadro com conteúdo zera tudo, inclusive a memória de já ter sugerido: se
            // o problema voltar depois, ele merece um aviso novo.
            _streak = 0;
            _suggested = false;
            return false;
        }

        _streak++;

        if (_streak < Threshold || _suggested) return false;

        _suggested = true;
        return true;
    }

    public void Reset()
    {
        _streak = 0;
        _suggested = false;
    }
}
