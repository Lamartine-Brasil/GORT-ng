namespace Gort.Platform.Capabilities;

/// <summary>
/// PARTE IX.1 — Capacidades dependentes do sistema.
///
/// RF-577 — O programa mantém uma camada de abstração explícita para C1 a C12, com uma
/// implementação por sistema, de modo que os módulos de OCR, tradução, agrupamento e
/// layout sejam IDÊNTICOS em todas as plataformas.
///
/// RF-576 — Toda capacidade indisponível é detectada NA INICIALIZAÇÃO e refletida na
/// interface — controles ocultos ou desabilitados com explicação — e nunca descoberta no
/// meio de uma tradução.
/// </summary>
public enum Capability
{
    /// <summary>
    /// C1 — Capturar uma região retangular da tela como imagem: os pixels de um retângulo em
    /// coordenadas globais da área de trabalho, incluindo coordenadas negativas, com a
    /// janela do PRÓPRIO PROGRAMA excluída do resultado. Usada na captura de tela (cap. 12).
    /// </summary>
    ScreenRegionCapture = 1,

    /// <summary>
    /// C2 — Capturar uma janela específica mesmo quando coberta: um fluxo de quadros do
    /// conteúdo de uma janela escolhida, com a posição de origem em coordenadas globais.
    /// </summary>
    WindowCapture = 2,

    /// <summary>C3 — Enumerar janelas capturáveis e deixar o usuário escolher uma.</summary>
    WindowPicker = 3,

    /// <summary>
    /// C4 — Obter os limites REAIS do quadro de uma janela: o retângulo do conteúdo visível,
    /// sem sombras nem bordas invisíveis. Usada no alinhamento da sobreposição (RF-092).
    /// </summary>
    WindowFrameBounds = 4,

    /// <summary>
    /// C5 — Janela sempre no topo, inclusive sobre jogos em modo janela sem borda.
    /// Usada por molduras, janelas de tradução e controle remoto.
    /// </summary>
    AlwaysOnTop = 5,

    /// <summary>
    /// C6 — Janela com transparência por pixel: um quadro RGBA completo por atualização,
    /// com canal alfa respeitado pelo compositor. Usada nos modos camada e sobreposição.
    /// </summary>
    PerPixelTransparency = 6,

    /// <summary>
    /// C7 — Janela transparente a cliques, alternável em tempo de execução. Usada nos modos
    /// camada e sobreposição durante a tradução (RF-334).
    /// </summary>
    ClickThrough = 7,

    /// <summary>
    /// C8 — Excluir uma janela de capturas de tela e gravações, alternável em tempo de
    /// execução. Usada pela sobreposição (P4, RF-346).
    /// </summary>
    ExcludeFromCapture = 8,

    /// <summary>
    /// C9 — Sincronizar com o compositor: bloquear até o próximo quadro composto, para
    /// evitar cintilação no primeiro desenho (RF-383).
    /// </summary>
    CompositorSync = 9,

    /// <summary>
    /// C10 — Atalho global de teclado: receber eventos enquanto outro programa tem o foco,
    /// SEM consumir os eventos (RF-436).
    /// </summary>
    GlobalHotkeys = 10,

    /// <summary>
    /// C11 — Detectar o atalho de captura de tela do sistema, para RF-347.
    /// </summary>
    ScreenshotKeyDetection = 11,

    /// <summary>
    /// C12 — Obter o título e o identificador da janela em primeiro plano. Necessária para a
    /// captura de janela ativa e para a espera antes do instantâneo (RF-452).
    /// </summary>
    ForegroundWindowInfo = 12,

    /// <summary>C13 — Ícone de bandeja com menu (RF-017).</summary>
    TrayIcon = 13,

    /// <summary>C14 — Área de transferência: ler, escrever e observar mudanças (cap. 24).</summary>
    Clipboard = 14,

    /// <summary>C15 — Síntese de voz (cap. 25). RF-573 — pode não existir.</summary>
    SpeechSynthesis = 15,

    /// <summary>
    /// C16 — Desenho de texto vetorial com contorno: converter texto em caminho e
    /// traçar/preencher com espessuras diferentes (RF-336).
    /// </summary>
    VectorTextOutline = 16,

    /// <summary>
    /// C17 — Medição precisa de texto: medir a extensão de um caminho de texto e a largura
    /// de uma cadeia (RF-373). Governa todo o layout da sobreposição.
    /// </summary>
    TextMeasurement = 17,

    /// <summary>C18 — Enumerar monitores e suas escalas (RF-075, RF-344).</summary>
    MonitorEnumeration = 18,

    /// <summary>
    /// C19 — Executar processo auxiliar e comunicar por canal local (RF-284).
    /// RF-574 — Só é necessária para o tradutor local que depende de biblioteca
    /// proprietária de um sistema específico.
    /// </summary>
    AuxiliaryProcessChannel = 19,

    /// <summary>
    /// C20 — Reconhecimento de texto oferecido pelo sistema. RF-575 — varia por plataforma;
    /// o programa lista apenas os motores efetivamente disponíveis e nunca apresenta um
    /// motor que falhará ao ser usado.
    /// </summary>
    SystemTextRecognition = 20,
}

/// <summary>Metadados de cada capacidade, transcritos da tabela IX.1.</summary>
public static class CapabilityInfo
{
    /// <summary>
    /// RF-577 — A camada de abstração é EXIGIDA para C1 a C12. As demais também passam por
    /// ela, mas é sobre estas que recai a garantia de que o restante do programa é idêntico
    /// em todas as plataformas.
    /// </summary>
    public static bool RequiresAbstraction(Capability c) => (int)c is >= 1 and <= 12;

    /// <summary>
    /// Capacidades sem as quais NENHUMA tradução é possível. RF-569 — sem permissão de
    /// gravação de tela no macOS, o programa deve dizer isso e não iniciar.
    /// </summary>
    public static bool IsEssential(Capability c)
        => c is Capability.ScreenRegionCapture or Capability.MonitorEnumeration;

    public static string Name(Capability c) => c switch
    {
        Capability.ScreenRegionCapture => "C1 — captura de região da tela",
        Capability.WindowCapture => "C2 — captura de janela específica",
        Capability.WindowPicker => "C3 — seletor de janelas",
        Capability.WindowFrameBounds => "C4 — limites reais do quadro da janela",
        Capability.AlwaysOnTop => "C5 — janela sempre no topo",
        Capability.PerPixelTransparency => "C6 — transparência por pixel",
        Capability.ClickThrough => "C7 — janela transparente a cliques",
        Capability.ExcludeFromCapture => "C8 — excluir janela de capturas",
        Capability.CompositorSync => "C9 — sincronização com o compositor",
        Capability.GlobalHotkeys => "C10 — atalho global de teclado",
        Capability.ScreenshotKeyDetection => "C11 — detecção do atalho de captura do sistema",
        Capability.ForegroundWindowInfo => "C12 — janela em primeiro plano",
        Capability.TrayIcon => "C13 — ícone de bandeja",
        Capability.Clipboard => "C14 — área de transferência",
        Capability.SpeechSynthesis => "C15 — síntese de voz",
        Capability.VectorTextOutline => "C16 — texto vetorial com contorno",
        Capability.TextMeasurement => "C17 — medição de texto",
        Capability.MonitorEnumeration => "C18 — enumeração de monitores",
        Capability.AuxiliaryProcessChannel => "C19 — processo auxiliar e canal local",
        Capability.SystemTextRecognition => "C20 — reconhecimento de texto do sistema",
        _ => c.ToString(),
    };
}
