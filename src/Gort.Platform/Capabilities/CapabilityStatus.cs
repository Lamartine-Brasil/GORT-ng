namespace Gort.Platform.Capabilities;

/// <summary>Por que uma capacidade está indisponível — governa o que a interface oferece.</summary>
public enum UnavailabilityKind
{
    /// <summary>Disponível; nenhum motivo.</summary>
    None,

    /// <summary>
    /// O sistema oferece a capacidade, mas falta PERMISSÃO do usuário. RF-569 — o programa
    /// deve explicar em texto claro qual permissão falta e para quê, e oferecer abrir a tela
    /// de configuração correspondente.
    /// </summary>
    PermissionRequired,

    /// <summary>
    /// A capacidade simplesmente não existe neste sistema ou nesta sessão gráfica.
    /// RF-568 — notadamente Wayland para posicionamento e "sempre no topo".
    /// </summary>
    NotSupported,

    /// <summary>Existe, mas falhou ao inicializar (biblioteca ausente, arquitetura incompatível).</summary>
    InitializationFailed,
}

/// <summary>
/// RF-576 — Situação de uma capacidade, apurada NA INICIALIZAÇÃO.
///
/// A interface usa <see cref="Available"/> para ocultar ou desabilitar controles e
/// <see cref="Explanation"/> para dizer ao usuário o motivo. Quando há
/// <see cref="RemediationHint"/>, há uma tela de configuração do sistema a oferecer.
/// </summary>
public sealed record CapabilityStatus(
    Capability Capability,
    bool Available,
    UnavailabilityKind Kind = UnavailabilityKind.None,
    string Explanation = "",
    string? RemediationHint = null)
{
    public static CapabilityStatus Ok(Capability c) => new(c, true);

    public static CapabilityStatus Missing(Capability c, UnavailabilityKind kind,
                                           string explanation, string? remediation = null)
        => new(c, false, kind, explanation, remediation);

    public override string ToString()
        => Available
            ? $"{CapabilityInfo.Name(Capability)}: disponível"
            : $"{CapabilityInfo.Name(Capability)}: INDISPONÍVEL ({Kind}) — {Explanation}";
}

/// <summary>
/// RF-576 — Retrato completo das capacidades, produzido uma vez na inicialização.
/// Nada no programa consulta o sistema operacional no meio de uma tradução para saber se
/// uma capacidade existe.
/// </summary>
public sealed class CapabilityReport
{
    private readonly Dictionary<Capability, CapabilityStatus> _statuses;

    public CapabilityReport(IEnumerable<CapabilityStatus> statuses)
    {
        _statuses = statuses.ToDictionary(s => s.Capability);
        foreach (Capability c in Enum.GetValues<Capability>())
        {
            if (!_statuses.ContainsKey(c))
            {
                _statuses[c] = CapabilityStatus.Missing(c, UnavailabilityKind.NotSupported,
                    "Capacidade não avaliada nesta plataforma.");
            }
        }
    }

    public CapabilityStatus this[Capability c] => _statuses[c];

    public bool Has(Capability c) => _statuses[c].Available;

    public IEnumerable<CapabilityStatus> All => _statuses.Values.OrderBy(s => (int)s.Capability);

    public IEnumerable<CapabilityStatus> Unavailable => All.Where(s => !s.Available);

    /// <summary>
    /// RF-569 / PARTE VIII — Sem uma capacidade essencial não há tradução possível: o
    /// programa deve dizer isso e NÃO INICIAR, em vez de exibir tradução vazia
    /// repetidamente.
    /// </summary>
    public bool CanTranslate => Enum.GetValues<Capability>()
        .Where(CapabilityInfo.IsEssential)
        .All(Has);

    /// <summary>Explicação agregada das capacidades essenciais que faltam.</summary>
    public string BlockingExplanation()
        => string.Join("\n", Enum.GetValues<Capability>()
            .Where(c => CapabilityInfo.IsEssential(c) && !Has(c))
            .Select(c => _statuses[c].Explanation));
}
