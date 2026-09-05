using System.Text.Json;
using System.Text.Json.Serialization;
using Gort.Core.Model;

namespace Gort.Core.Diagnostics;

/// <summary>
/// RF-492 a RF-495 — Retrato de análise de um ciclo.
///
/// Cap. 27: "tornar observável o que o programa decidiu em um ciclo, para que erros de
/// agrupamento, de cor e de layout possam ser investigados com EVIDÊNCIA em vez de
/// impressão."
///
/// A PARTE XII.3 é mais direta: é este arquivo que transforma "ficou ruim" em evidência
/// utilizável, e sem ele não há como alterar um valor 🔒 com a disciplina que a
/// especificação exige.
/// </summary>
public sealed class AnalysisSnapshot
{
    /// <summary>Instante do ciclo.</summary>
    public DateTime Instant { get; init; } = DateTime.Now;

    public string WindowMode { get; init; } = "";
    public string OcrEngine { get; init; } = "";
    public string TranslationService { get; init; } = "";

    public string RecognizedText { get; init; } = "";
    public string TranslatedText { get; init; } = "";

    public List<SnapshotArea> Areas { get; init; } = new();

    /// <summary>
    /// RF-493 — Parte do desenho, preenchida DEPOIS que ele termina. Nula quando o ciclo
    /// seguinte começou antes: nesse caso o retrato é gravado sem ela, e NÃO descartado
    /// (RF-495).
    /// </summary>
    public SnapshotDrawing? Drawing { get; set; }

    /// <summary>
    /// RF-492 — Nome do arquivo, com data e hora até MILISSEGUNDOS: um laço de 300 ms produz
    /// mais de três retratos por segundo, e sem os milissegundos eles se sobrescreveriam.
    /// </summary>
    public string FileName => $"analise-{Instant:yyyy-MM-dd-HHmmss-fff}.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Grava o retrato na pasta dedicada.
    ///
    /// O formato é JSON: a especificação pede "arquivo estruturado" sem nomear um; os dados
    /// são profundamente aninhados — áreas, linhas, palavras, blocos — e este é o formato
    /// que um humano lê e uma ferramenta consome sem intermediário. Note que a exigência de
    /// formato de RF-023 vale para os arquivos do USUÁRIO; este é artefato de diagnóstico.
    /// </summary>
    public string Save(string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, FileName);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
        return path;
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options);
}

/// <summary>RF-492 — O que cada área contribuiu para o ciclo.</summary>
public sealed class SnapshotArea
{
    public int Index { get; init; }

    /// <summary>Verdadeiro quando veio de uma área instantânea.</summary>
    public bool IsSnapshot { get; init; }

    public SnapshotRect AreaRect { get; init; } = new();
    public SnapshotRect ResultRect { get; init; } = new();

    public string RecognizedText { get; init; } = "";
    public string TranslatedText { get; init; } = "";

    /// <summary>Cores da análise automática, um par por bloco.</summary>
    public List<SnapshotColors> AutoColors { get; init; } = new();

    /// <summary>Todas as linhas, com suas palavras e caixas.</summary>
    public List<SnapshotLine> Lines { get; init; } = new();

    /// <summary>Todos os blocos, com os seus quatro retângulos.</summary>
    public List<SnapshotBlock> Blocks { get; init; } = new();
}

public sealed class SnapshotLine
{
    public string Text { get; init; } = "";
    public string Orientation { get; init; } = "";
    public SnapshotRect Box { get; init; } = new();
    public List<SnapshotWord> Words { get; init; } = new();
}

public sealed class SnapshotWord
{
    public string Text { get; init; } = "";
    public SnapshotRect Box { get; init; } = new();
}

/// <summary>RF-492 — Cada bloco com os seus QUATRO retângulos.</summary>
public sealed class SnapshotBlock
{
    public string Text { get; init; } = "";
    public string? Translated { get; init; }
    public bool IsTitle { get; init; }
    public string Orientation { get; init; } = "";

    public SnapshotRect SourceBox { get; init; } = new();
    public SnapshotRect ViewBox { get; init; } = new();
    public SnapshotRect ContentBox { get; init; } = new();

    /// <summary>União das caixas das linhas — o quarto retângulo.</summary>
    public SnapshotRect LinesBox { get; init; } = new();
}

public sealed class SnapshotColors
{
    public string? Font { get; init; }
    public string? Background { get; init; }
    public int SupportingWords { get; init; }
    public double Contrast { get; init; }

    /// <summary>RF-409 — Recorreu a preto ou branco por falta de candidato.</summary>
    public bool UsedFallback { get; init; }

    /// <summary>RF-410 — A cor foi substituída pela verificação final de legibilidade.</summary>
    public bool ContrastCorrected { get; init; }
}

/// <summary>
/// RF-493 / RF-494 — A parte do DESENHO, só preenchida no modo sobreposição e só depois que
/// ele termina.
/// </summary>
public sealed class SnapshotDrawing
{
    public SnapshotRect WindowRect { get; init; } = new();

    /// <summary>Opções de renderização em vigor no momento do desenho.</summary>
    public Dictionary<string, object> Options { get; init; } = new();

    public List<SnapshotDrawnBlock> Blocks { get; init; } = new();

    // RF-494 — os quatro tempos do desenho e as contagens do cache de medição.
    public double TotalMs { get; init; }
    public double SizeAndPositionMs { get; init; }
    public double LayoutAndDrawMs { get; init; }
    public double PresentMs { get; init; }
    public int CacheHits { get; init; }
    public int CacheMisses { get; init; }
}

/// <summary>RF-493 — Tudo o que se sabe sobre um bloco DESENHADO.</summary>
public sealed class SnapshotDrawnBlock
{
    public string Text { get; init; } = "";
    public bool IsTitle { get; init; }
    public string Orientation { get; init; } = "";

    public SnapshotRect SourceRect { get; init; } = new();
    public SnapshotRect ViewRect { get; init; } = new();
    public SnapshotRect ContentRect { get; init; } = new();
    public SnapshotRect DrawnRect { get; init; } = new();

    public string FontFamily { get; init; } = "";
    public string FontStyle { get; init; } = "";
    public double FontSize { get; init; }

    /// <summary>RF-360 — O tamanho preferido, antes da bissecção.</summary>
    public double PreferredSize { get; init; }
    public double MinimumSize { get; init; }

    /// <summary>RF-360 passo 1 — Tamanho estimado do texto ORIGINAL.</summary>
    public double EstimatedOriginalSize { get; init; }

    public string? FontColor { get; init; }
    public string? BackgroundColor { get; init; }
    public string? Stroke1Color { get; init; }
    public string? Stroke2Color { get; init; }

    public bool UsedAutoColor { get; init; }
    public bool ContrastCorrected { get; init; }

    /// <summary>As linhas DEPOIS da quebra (RF-369).</summary>
    public List<string> Lines { get; init; } = new();

    /// <summary>RF-365 — Avanço entre linhas.</summary>
    public double LineAdvance { get; init; }

    /// <summary>
    /// Caso de erro do cap. 19 — o bloco não coube nem no tamanho mínimo e foi desenhado
    /// assim mesmo.
    /// </summary>
    public bool Clipped { get; init; }
}

/// <summary>Retângulo em forma legível no arquivo.</summary>
public sealed class SnapshotRect
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    public static SnapshotRect From(Rect r) => new()
    {
        X = r.X, Y = r.Y, Width = r.Width, Height = r.Height,
    };
}
