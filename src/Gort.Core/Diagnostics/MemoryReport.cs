namespace Gort.Core.Diagnostics;

/// <summary>
/// RF-558 / RF-559 — O que o indicador de memória mostra.
///
/// "Só o total diz que há um problema; o detalhamento diz qual configuração o causou." As
/// três parcelas do detalhamento são exatamente as três que o usuário controla sem saber:
/// a ampliação e o número de áreas fazem as imagens de região; o uso prolongado faz o
/// cache; o tamanho da janela de sobreposição faz o mapa de bits.
///
/// RF-560 — a leitura é amostrada em intervalo fixo e NUNCA dentro do ciclo de tradução:
/// esta classe só soma números que outras partes já mantêm.
/// </summary>
public sealed class MemoryReport
{
    /// <summary>Memória total do processo (RF-559, o mínimo exigido).</summary>
    public long ProcessBytes { get; init; }

    /// <summary>Imagens de região vivas neste instante.</summary>
    public long RegionImageBytes { get; init; }

    /// <summary>Cache de traduções: memória de resultados e coletânea.</summary>
    public long TranslationCacheBytes { get; init; }

    /// <summary>Mapa de bits da janela de sobreposição.</summary>
    public long OverlayBitmapBytes { get; init; }

    public long DetailedBytes
        => RegionImageBytes + TranslationCacheBytes + OverlayBitmapBytes;

    public static string Megabytes(long bytes) => $"{bytes / 1024.0 / 1024.0:0.#} MB";
}

/// <summary>
/// RF-554 / RF-559 — Quantos bytes de imagem de região estão vivos agora.
///
/// RF-554 exige que não haja mais de um conjunto de imagens de região vivo por vez. Um
/// contador não IMPÕE isso — quem impõe é o ciclo, soltando cada imagem —, mas torna a
/// violação visível: se o número não voltar perto de zero entre ciclos, alguma coisa está
/// segurando um conjunto antigo.
/// </summary>
public sealed class LiveImageMeter
{
    private long _bytes;

    public long Bytes => Interlocked.Read(ref _bytes);

    public void Add(long bytes) => Interlocked.Add(ref _bytes, bytes);

    public void Remove(long bytes) => Interlocked.Add(ref _bytes, -bytes);

    /// <summary>Zera — chamado no início de cada ciclo, que é quando o conjunto se renova.</summary>
    public void Reset() => Interlocked.Exchange(ref _bytes, 0);
}
