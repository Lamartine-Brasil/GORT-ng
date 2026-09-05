using Gort.Core.Catalog;
using Gort.Core.Structuring;

namespace Gort.Core.Ocr;

/// <summary>Por que um motor não pode ser usado agora.</summary>
public enum EngineRejection
{
    None,

    /// <summary>RF-122 — O motor de nuvem não pode ser usado em tradução em tempo real.</summary>
    NotForRealtime,

    /// <summary>RF-351 — O modo sobreposição exige posição de palavra.</summary>
    NoWordPositions,

    /// <summary>RF-575 — O motor não está disponível neste sistema.</summary>
    Unavailable,

    /// <summary>O motor não reconhece o idioma pedido.</summary>
    LanguageNotSupported,
}

/// <summary>
/// Regras de escolha e de compatibilidade de motores de OCR (cap. 14).
///
/// São separadas dos adaptadores porque valem para TODOS eles: acrescentar um motor não
/// muda nenhuma destas regras (RF-566).
/// </summary>
public static class EngineSelection
{
    /// <summary>
    /// Passo 2 do fluxo principal — verificações de pré-condição antes de iniciar.
    ///
    /// RF-122 — o motor de nuvem não serve para tempo real; RF-351 — a sobreposição exige
    /// posição de palavra.
    /// </summary>
    public static EngineRejection CanStart(IOcrEngine engine, OcrEngineInfo? info,
                                           bool realtime, WindowMode mode)
    {
        if (!engine.IsAvailable) return EngineRejection.Unavailable;

        if (realtime && info is { Realtime: false }) return EngineRejection.NotForRealtime;

        if (mode == WindowMode.Overlay && !engine.ProvidesWordPositions)
            return EngineRejection.NoWordPositions;

        return EngineRejection.None;
    }

    /// <summary>Mensagem ao usuário para cada recusa.</summary>
    public static string Explain(EngineRejection rejection, string engineKey) => rejection switch
    {
        EngineRejection.NotForRealtime =>
            $"O motor '{engineKey}' só pode ser usado em modo pontual, não em tradução " +
            "contínua: ele consome cota a cada ciclo.",

        EngineRejection.NoWordPositions =>
            $"O motor '{engineKey}' não devolve a posição das palavras, e o modo " +
            "sobreposição precisa dela para desenhar sobre o texto original. Use o modo " +
            "escuro ou camada com este motor.",

        EngineRejection.Unavailable =>
            $"O motor '{engineKey}' não está disponível neste sistema.",

        EngineRejection.LanguageNotSupported =>
            $"O motor '{engineKey}' não reconhece o idioma escolhido.",

        _ => "",
    };

    /// <summary>
    /// RF-123 — "Priorizar o motor de nuvem em modo pontual": quando ativa, disponível e
    /// DENTRO DA COTA, os modos pontuais usam o motor de nuvem mesmo que outro esteja
    /// selecionado.
    ///
    /// As três condições valem juntas: fora da cota, a preferência não se aplica e o motor
    /// escolhido pelo usuário continua valendo — em vez de a tradução simplesmente falhar.
    /// </summary>
    public static IOcrEngine ResolveForOneShot(IOcrEngine selected, IOcrEngine? cloud,
                                               bool preferCloud, bool withinQuota)
    {
        if (!preferCloud || cloud is null) return selected;
        if (!cloud.IsAvailable || !withinQuota) return selected;
        return cloud;
    }

    /// <summary>
    /// RF-149 — Ao trocar de motor, o programa tenta PRESERVAR o idioma: se o motor anterior
    /// estava num idioma que o novo também reconhece, o novo é configurado nele.
    ///
    /// Sem isso, trocar de motor jogaria o usuário de volta ao idioma padrão, e ele
    /// descobriria pelo resultado errado da tradução.
    /// </summary>
    public static string PreserveLanguage(string currentLanguage, OcrEngineInfo target,
                                          string fallback)
    {
        if (target.Languages.Contains(currentLanguage, StringComparer.OrdinalIgnoreCase))
            return currentLanguage;

        return target.Languages.Contains(fallback, StringComparer.OrdinalIgnoreCase)
            ? fallback
            : target.Languages.FirstOrDefault() ?? fallback;
    }

    /// <summary>
    /// RF-147 / RF-315 — A escolha do idioma de OCR propaga automaticamente para os idiomas
    /// de ORIGEM dos serviços de tradução, quando houver correspondência.
    ///
    /// Devolve o idioma a selecionar em cada serviço, por chave de serviço.
    /// </summary>
    public static Dictionary<string, string> PropagateSourceLanguage(
        AppCatalog catalog, LanguageInfo ocrLanguage)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var service in catalog.TranslationServices)
        {
            var match = catalog.MatchForService(ocrLanguage, service);
            if (match is not null) result[service.Key] = match.Key;
        }
        return result;
    }

    /// <summary>
    /// RF-150 — O motor local clássico aceita um nome de conjunto de dados digitado pelo
    /// usuário, e uma opção de "modo rápido" que anexa um sufixo a esse nome quando ele é
    /// `eng` ou `jpn`.
    ///
    /// O sufixo só vale para esses dois porque são os únicos que têm variante rápida
    /// publicada; anexá-lo a outro nome produziria um conjunto de dados inexistente.
    /// </summary>
    public const string FastModeSuffix = "_fast";

    public static string ClassicDataset(string dataset, bool fastMode)
    {
        if (!fastMode) return dataset;

        return dataset is "eng" or "jpn" ? dataset + FastModeSuffix : dataset;
    }
}
