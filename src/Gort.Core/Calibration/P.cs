namespace Gort.Core.Calibration;

/// <summary>
/// PARTE IV — Parâmetros e calibragem.
///
/// Todo membro marcado com [CALIBRADO] corresponde a um valor 🔒 da especificação:
/// calibragem empírica confirmada, que NÃO pode ser derivada por raciocínio.
/// Antes de alterar qualquer um deles, leia a PARTE XII (política dos valores calibrados):
///   - não arredondar;
///   - não unificar valores parecidos (P-34, P-44 e P-92 são três razões distintas);
///   - não substituir pelo "padrão da biblioteca";
///   - não recalibrar por intuição;
///   - não expor na interface um valor que a coluna "Exposto" declara FIXO.
///
/// A coluna "Exposto" da Parte IV é reproduzida como [Exposto: UI|REMOTO|FIXO] em cada membro.
/// Valores UI aqui são apenas o PADRÃO de fábrica; o valor efetivo vem do perfil do usuário.
/// Valores REMOTO aqui são o embutido; a configuração remota (RF-417) pode sobrescrevê-los,
/// e um valor remoto ausente ou vazio mantém o embutido (RF-418).
/// </summary>
public static class P
{
    // ─────────────────────────────────────────────────────────────────────────
    // IV.1 — Ciclo de vida e temporização
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>P-01 — Permanência da tela de abertura. [CALIBRADO] [Exposto: FIXO]</summary>
    public static readonly TimeSpan SplashHold = TimeSpan.FromSeconds(0.7);

    /// <summary>P-02 — Desvanecimento da tela de abertura. [Exposto: FIXO]</summary>
    public static readonly TimeSpan SplashFade = TimeSpan.FromSeconds(2.0);

    /// <summary>P-03 — Prazo de espera pela thread do laço. [Exposto: FIXO]</summary>
    public static readonly TimeSpan LoopJoinTimeout = TimeSpan.FromMilliseconds(3000);

    /// <summary>
    /// P-04 — Prazo de espera quando o pedido vem do interceptador global de teclado.
    /// [CALIBRADO] [Exposto: FIXO]
    /// Acima de ~300 ms o sistema remove o gancho de baixo nível e TODOS os atalhos do
    /// programa morrem até reiniciar (RF-011, RF-450).
    /// </summary>
    public static readonly TimeSpan LoopJoinTimeoutFromHook = TimeSpan.FromMilliseconds(250);

    /// <summary>P-05 — Intervalo de ciclo, velocidade 1 (mais rápida). [CALIBRADO] [Exposto: UI]</summary>
    public const int CycleIntervalSpeed1Ms = 300;
    /// <summary>P-06 — Intervalo de ciclo, velocidade 2. [CALIBRADO] [Exposto: UI]</summary>
    public const int CycleIntervalSpeed2Ms = 1000;
    /// <summary>P-07 — Intervalo de ciclo, velocidade 3. [CALIBRADO] [Exposto: UI]</summary>
    public const int CycleIntervalSpeed3Ms = 1500;
    /// <summary>P-08 — Intervalo de ciclo, velocidade 4. [CALIBRADO] [Exposto: UI]</summary>
    public const int CycleIntervalSpeed4Ms = 2000;
    /// <summary>P-09 — Intervalo de ciclo, velocidade 5 (mais lenta). [CALIBRADO] [Exposto: UI]</summary>
    public const int CycleIntervalSpeed5Ms = 2500;

    /// <summary>
    /// Resolve o índice de velocidade 1..5 para o intervalo de ciclo (P-05 a P-09).
    /// Índices fora da faixa são saturados (RF-042 / P7).
    /// </summary>
    public static int CycleIntervalMs(int speedIndex) => speedIndex switch
    {
        <= 1 => CycleIntervalSpeed1Ms,
        2 => CycleIntervalSpeed2Ms,
        3 => CycleIntervalSpeed3Ms,
        4 => CycleIntervalSpeed4Ms,
        _ => CycleIntervalSpeed5Ms,
    };

    /// <summary>P-125 — Sono quando o intervalo entre ciclos ainda não passou. [Exposto: FIXO]</summary>
    public const int IdleLoopSleepMs = 100;

    /// <summary>P-126 — Intervalo de verificação do pedido de parada durante uma espera longa. [Exposto: FIXO]</summary>
    public const int StopCheckIntervalMs = 50;

    /// <summary>P-132 — Valor em que o contador de identificação de tarefa volta a zero. [Exposto: FIXO]</summary>
    public const int TaskCounterWrap = 100000;

    // ─────────────────────────────────────────────────────────────────────────
    // IV.2 — Áreas de captura
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// P-10 — Opacidade da camada de seleção: max(alfa_do_fundo, 75) ÷ 255 × 0,15.
    /// [CALIBRADO] [Exposto: parcial — a cor é UI]
    /// </summary>
    public static double SelectionOverlayOpacity(int backgroundAlpha)
        => Math.Max(backgroundAlpha, 75) / 255.0 * 0.15;

    /// <summary>
    /// P-11 — Zona sensível de borda da moldura, em px base (escalar por DPI).
    /// É exatamente P-14 + P-15 + P-16. [CALIBRADO] [Exposto: FIXO]
    /// </summary>
    public const int FrameResizeHotZone = FrameBorderThickness + FrameOuterBorderThickness + FrameTitleBarHeight; // 31

    /// <summary>P-12 — Tamanho mínimo da moldura (largura e altura). [Exposto: FIXO]</summary>
    public const int FrameMinWidth = 50;
    /// <summary>P-12 — Tamanho mínimo da moldura (largura e altura). [Exposto: FIXO]</summary>
    public const int FrameMinHeight = 50;

    /// <summary>P-13 — Intervalo mínimo entre recálculos de área durante o arraste. [CALIBRADO] [Exposto: FIXO]</summary>
    public static readonly TimeSpan FrameDragRecalcInterval = TimeSpan.FromSeconds(0.3);

    /// <summary>P-14 — Espessura da borda da moldura, px base (×DPI). [CALIBRADO] [Exposto: FIXO]</summary>
    public const int FrameBorderThickness = 3;
    /// <summary>P-15 — Espessura da segunda borda (externa), px base (×DPI). [CALIBRADO] [Exposto: FIXO]</summary>
    public const int FrameOuterBorderThickness = 8;
    /// <summary>P-16 — Altura da barra de título da moldura, px base (×DPI). [CALIBRADO] [Exposto: FIXO]</summary>
    public const int FrameTitleBarHeight = 20;

    /// <summary>
    /// P-144 — Múltiplo para o qual a largura de captura é arredondada para cima (RF-077).
    /// [CALIBRADO] [Exposto: FIXO] Alinhamento de linha da imagem.
    /// </summary>
    public const int CaptureWidthAlignment = 4;

    /// <summary>P-140 — Opacidade da moldura de área de exclusão. [Exposto: FIXO]</summary>
    public const double ExclusionFrameOpacity = 0.7;

    /// <summary>P-141 — Resolução de referência para cálculo de escala de DPI. [Exposto: FIXO]</summary>
    public const double ReferenceDpi = 96;

    /// <summary>P-145 — Abaixo disso o arraste de seleção é descartado como clique (RF-052). [Exposto: FIXO]</summary>
    public const int MinSelectionRectSize = 4;

    /// <summary>P-139 — Faixa de ampliação do conta-gotas. [Exposto: UI]</summary>
    public const int EyedropperZoomMin = 1;
    /// <summary>P-139 — Faixa de ampliação do conta-gotas. [Exposto: UI]</summary>
    public const int EyedropperZoomMax = 4;

    // ─────────────────────────────────────────────────────────────────────────
    // IV.3 — Captura de imagem
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>P-17 — Quadros mantidos em reserva no modo janela anexada. [CALIBRADO] [Exposto: FIXO]</summary>
    public const int AttachedFrameBufferSize = 5;

    /// <summary>P-18 — A cada quantos quadros o buffer é reabastecido sem pedido. [CALIBRADO] [Exposto: FIXO]</summary>
    public const int AttachedIdleCapturePeriodFrames = 10;

    /// <summary>P-19 — Idade máxima de um quadro guardado ainda reaproveitável. [CALIBRADO] [Exposto: FIXO]</summary>
    public static readonly TimeSpan AttachedFrameMaxAge = TimeSpan.FromSeconds(0.1);

    /// <summary>P-20 — Intervalo de nova tentativa de captura da janela anexada. [Exposto: FIXO]</summary>
    public const int AttachedCaptureRetryMs = 2;

    /// <summary>P-31 — Intervalo de espera pelo motor de OCR do sistema. [Exposto: FIXO]</summary>
    public const int SystemOcrPollMs = 2;

    // ─────────────────────────────────────────────────────────────────────────
    // IV.4 — Pré-processamento de imagem
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>P-21 — Limiar de binarização do modo limiar. [Exposto: UI]</summary>
    public const int DefaultThreshold = 127;

    /// <summary>P-22 — Fator de ampliação da imagem antes do OCR. [CALIBRADO] [Exposto: UI]</summary>
    public const double DefaultScale = 2.0;
    /// <summary>P-23 — Ampliação mínima. [Exposto: UI]</summary>
    public const double ScaleMin = 0.1;
    /// <summary>P-24 — Ampliação máxima; valores lidos acima disso caem para P-22 (RF-114). [Exposto: UI]</summary>
    public const double ScaleMax = 10.0;
    /// <summary>P-25 — Passo do controle de ampliação. [Exposto: UI]</summary>
    public const double ScaleStep = 0.5;

    /// <summary>P-26 — Grupo HSV do assistente, texto escuro, faixa 1: S 0–8, V 0–32. [CALIBRADO]</summary>
    public static readonly (int S1, int S2, int V1, int V2) HsvDarkTextRange1 = (0, 8, 0, 32);
    /// <summary>P-27 — Grupo HSV do assistente, texto escuro, faixa 2: S 95–100, V 0–32. [CALIBRADO]</summary>
    public static readonly (int S1, int S2, int V1, int V2) HsvDarkTextRange2 = (95, 100, 0, 32);
    /// <summary>P-28 — Grupo HSV do assistente, texto claro: S 0–10, V 75–100. [CALIBRADO]</summary>
    public static readonly (int S1, int S2, int V1, int V2) HsvLightTextRange = (0, 10, 75, 100);

    /// <summary>P-146 — Matriz de luminância para conversão em tons de cinza (RF-108). [Exposto: FIXO]</summary>
    public const double GrayR = 0.30, GrayG = 0.59, GrayB = 0.11;

    // ─────────────────────────────────────────────────────────────────────────
    // IV.5 — OCR
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>P-29 — Limite mensal de chamadas do OCR de nuvem. [CALIBRADO] [Exposto: UI]</summary>
    public const int CloudOcrMonthlyLimit = 950;

    /// <summary>P-30 — Máximo de linhas reconhecidas por imagem no motor moderno. [Exposto: FIXO]</summary>
    public const int ModernOcrMaxLines = 1000;

    /// <summary>P-32 — Razão altura÷largura acima da qual o motor moderno considera a linha vertical. [CALIBRADO] [Exposto: FIXO]</summary>
    public const double ModernOcrVerticalRatio = 1.5;

    // ─────────────────────────────────────────────────────────────────────────
    // IV.6 — Estruturação e pós-processamento 🔒
    //
    // Este é o grupo mais sensível da especificação (Parte XII.4): errar aqui
    // fragmenta diálogos em blocos soltos ou cola o nome do personagem na fala.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>P-33 — Altura > largura × este valor ⇒ linha vertical (RF-155). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double LineVerticalRatio = 1.5;

    /// <summary>P-34 — Razão máxima de tamanho de fonte tolerada entre duas linhas adjacentes (RF-163). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double AdjacencyMaxFontRatio = 1.3;

    /// <summary>P-35 — Intervalo máximo no eixo de leitura, em múltiplos do tamanho médio de fonte (RF-163). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double AdjacencyFlowGapFactor = 1.25;

    /// <summary>P-36 — Sobreposição transversal mínima entre duas linhas (RF-163). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double AdjacencyMinCrossOverlap = 0.25;

    /// <summary>P-37 — Alternativa à sobreposição: proximidade dos inícios, em múltiplos do tamanho de fonte (RF-163). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double AdjacencyStartAlignFactor = 2.0;

    /// <summary>P-38 — Tamanho de fonte devolvido quando não há caixa de palavra válida (RF-164). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double FontSizeFallback = 10;

    /// <summary>P-39 — Marcadores de lista fortes (RF-166). [CALIBRADO] [Exposto: FIXO]</summary>
    public static readonly char[] StrongListMarkers =
        { '•', '●', '○', '◦', '▪', '■', '‣', '⁃', '·', '・', '･' };

    /// <summary>P-40 — Limite de caracteres para "linha curta" (RF-173). [CALIBRADO] [Exposto: FIXO]</summary>
    public const int ShortLineCharLimit = 10;

    /// <summary>P-41 — Limite de caracteres para "linha curta" com remoção de espaços (RF-173). [CALIBRADO] [Exposto: FIXO]</summary>
    public const int ShortLineCharLimitNoSpaces = 6;

    /// <summary>P-42 — Desconto do limite quando a linha é vertical (RF-173). [CALIBRADO] [Exposto: FIXO]</summary>
    public const int ShortLineVerticalDiscount = 3;

    /// <summary>P-43 — Máximo de palavras para "linha curta", só fora do modo sem espaços (RF-173). [CALIBRADO] [Exposto: FIXO]</summary>
    public const int ShortLineMaxWords = 3;

    /// <summary>P-148 — Razão de comprimento exigida da linha seguinte para título por contexto (RF-172). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double ContextTitleLengthRatio = 1.5;

    /// <summary>P-44 — Razão de tamanho de fonte tolerada ao anexar uma linha a um bloco já iniciado (RF-176). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double AppendMaxFontRatio = 1.2;

    /// <summary>P-45 — Caracteres de fechamento removidos antes de checar pontuação final (RF-177). [CALIBRADO] [Exposto: FIXO]</summary>
    public static readonly char[] ClosingChars =
        { '"', '\'', '”', '’', '」', '』', '】', ')', '》' };

    /// <summary>P-149 — Pontuação que marca fim de frase (RF-177). [CALIBRADO] [Exposto: FIXO]</summary>
    public static readonly char[] SentenceEndChars =
        { '.', '?', '!', '。', '？', '！' };

    /// <summary>P-150 — Comprimento máximo do token de um marcador numerado (RF-169). [CALIBRADO] [Exposto: FIXO]</summary>
    public const int NumberedMarkerMaxLength = 3;

    /// <summary>P-46 — Passagens adicionais do dicionário de correção; faixa 0–3. [Exposto: UI]</summary>
    public const int DictionaryExtraPassesDefault = 0;
    public const int DictionaryExtraPassesMin = 0;
    public const int DictionaryExtraPassesMax = 3;

    // ─────────────────────────────────────────────────────────────────────────
    // IV.7 — Detecção de mudança e cache
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>P-47 — Intervalo de repintar ocioso quando o texto não mudou (RF-196). [CALIBRADO] [Exposto: FIXO]</summary>
    public static readonly TimeSpan IdleRepaintInterval = TimeSpan.FromMilliseconds(1000);

    /// <summary>P-48 — Máximo de entradas na memória de resultados por serviço (RF-210). [CALIBRADO] [Exposto: FIXO]</summary>
    public const int ResultMemoryMaxEntries = 10000;

    /// <summary>P-49 — Quantidade de traduções empilhadas na memória de exibição; faixa 1–10. [Exposto: UI]</summary>
    public const int DisplayMemoryCountDefault = 5;
    public const int DisplayMemoryCountMin = 1;
    public const int DisplayMemoryCountMax = 10;

    /// <summary>P-50 — Tempo de vida de cada entrada da memória de exibição, em segundos; faixa até 200. [Exposto: UI]</summary>
    public const int DisplayMemoryLifetimeSecondsDefault = 10;
    public const int DisplayMemoryLifetimeSecondsMax = 200;

    // ─────────────────────────────────────────────────────────────────────────
    // IV.8 — Tradução
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>P-51 — Token separador padrão de blocos em uma única requisição. [CALIBRADO] [Exposto: REMOTO]</summary>
    public const string SeparatorToken = "//////";

    /// <summary>P-52 — Token separador do tradutor por navegador embutido. [CALIBRADO] [Exposto: REMOTO]</summary>
    public const string SeparatorTokenEmbeddedBrowser = "@@@@@@";

    /// <summary>P-151 — Sinalizador de token avançado (RF-234). [CALIBRADO] [Exposto: REMOTO]</summary>
    public const bool AdvancedTokenDefault = false;

    /// <summary>P-53 — Duração do modo de baixa qualidade do tradutor web gratuito. [CALIBRADO] [Exposto: FIXO]</summary>
    public static readonly TimeSpan LowQualityModeDuration = TimeSpan.FromHours(1);

    /// <summary>P-54 — Tempo limite do tradutor web gratuito. [CALIBRADO] [Exposto: FIXO]</summary>
    public static readonly TimeSpan FreeWebTranslatorTimeout = TimeSpan.FromMilliseconds(2000);

    /// <summary>P-55 — Máximo de chaves de API alternáveis no rodízio. [Exposto: FIXO]</summary>
    public const int MaxRotatingApiKeys = 20;

    /// <summary>P-56 — Atraso aleatório após requisição a serviço web sem chave: 0 a 650 ms. [CALIBRADO] [Exposto: FIXO]</summary>
    public const int PostRequestRandomDelayMaxMs = 650;

    /// <summary>P-57 — Linhas mínimas da planilha de tradução. [CALIBRADO] [Exposto: FIXO]</summary>
    public const int SpreadsheetMinRows = 50;

    /// <summary>P-58 — Sufixo marcador de fim de tradução no tradutor por navegador. [CALIBRADO] [Exposto: FIXO]</summary>
    public const string EmbeddedBrowserEndMarker = "^^^^";

    /// <summary>P-59 — Tempo limite normal do tradutor por navegador. [CALIBRADO] [Exposto: FIXO]</summary>
    public static readonly TimeSpan EmbeddedBrowserTimeout = TimeSpan.FromSeconds(5);
    /// <summary>P-60 — Tempo limite com tradutor alternativo ativo. [CALIBRADO] [Exposto: FIXO]</summary>
    public static readonly TimeSpan EmbeddedBrowserTimeoutWithFallback = TimeSpan.FromSeconds(3);
    /// <summary>P-61 — Acréscimo de tempo na primeira tradução da sessão. [CALIBRADO] [Exposto: FIXO]</summary>
    public static readonly TimeSpan EmbeddedBrowserFirstTranslationExtra = TimeSpan.FromSeconds(5);
    /// <summary>P-62 — Tempo limite quando o texto é idêntico ao da requisição anterior. [CALIBRADO] [Exposto: FIXO]</summary>
    public static readonly TimeSpan EmbeddedBrowserTimeoutRepeatedText = TimeSpan.FromSeconds(1.5);
    /// <summary>P-63 — Atraso aleatório antes de navegar: 0 a 140 ms. [CALIBRADO] [Exposto: FIXO]</summary>
    public const int EmbeddedBrowserPreNavigateRandomDelayMaxMs = 140;
    /// <summary>P-136 — Tentativas de limpar o campo de resultado, espaçadas de 50 ms. [CALIBRADO] [Exposto: FIXO]</summary>
    public const int EmbeddedBrowserClearAttempts = 4;
    public const int EmbeddedBrowserClearIntervalMs = 50;
    /// <summary>P-137 — Intervalo de sondagem do resultado na página. [Exposto: FIXO]</summary>
    public const int EmbeddedBrowserPollMs = 80;

    /// <summary>P-64 — Temperatura do preset padrão do modelo de linguagem (0–100, exibida ÷100). [CALIBRADO] [Exposto: UI]</summary>
    public const int LlmTemperatureDefault = 20;
    /// <summary>P-65 — Nível de raciocínio do preset padrão (0–3). [CALIBRADO] [Exposto: UI]</summary>
    public const int LlmThinkingDefault = 0;
    /// <summary>P-66 — Limite de tokens de saída do preset padrão. [CALIBRADO] [Exposto: UI]</summary>
    public const int LlmMaxOutputDefault = 4000;

    /// <summary>P-67 — Temperatura do preset econômico. [CALIBRADO] [Exposto: UI]</summary>
    public const int LlmTemperatureEconomy = 0;
    /// <summary>P-68 — Nível de raciocínio do preset econômico. [CALIBRADO] [Exposto: UI]</summary>
    public const int LlmThinkingEconomy = 1;
    /// <summary>P-69 — Limite de tokens de saída do preset econômico. [CALIBRADO] [Exposto: UI]</summary>
    public const int LlmMaxOutputEconomy = 2000;

    /// <summary>P-70 / P-71 — Faixa da temperatura personalizada. [Exposto: UI]</summary>
    public const int LlmTemperatureMin = 0, LlmTemperatureMax = 100;
    /// <summary>P-72 / P-73 — Faixa do nível de raciocínio personalizado. [Exposto: UI]</summary>
    public const int LlmThinkingMin = 0, LlmThinkingMax = 3;
    /// <summary>P-74 / P-75 — Faixa do limite de saída personalizado. [Exposto: UI]</summary>
    public const int LlmMaxOutputMin = 500, LlmMaxOutputMax = 10000;
    /// <summary>P-152 — Valor inicial do limite de saída no preset personalizado. [CALIBRADO] [Exposto: UI]</summary>
    public const int LlmMaxOutputCustomInitial = 1000;

    // P-153 — Os demais valores iniciais do preset personalizado são os do preset padrão
    // (P-64 e P-65); ver LlmTemperatureDefault e LlmThinkingDefault. [Exposto: FIXO]

    /// <summary>P-76 — Orçamento de raciocínio para modelos "pro" da família antiga (RF-282). [CALIBRADO] [Exposto: FIXO]</summary>
    public const int LlmProThinkingBudget = 512;

    /// <summary>P-77 — Tempo limite da requisição ao modelo de linguagem. [CALIBRADO] [Exposto: FIXO]</summary>
    public static readonly TimeSpan LlmTimeout = TimeSpan.FromSeconds(300);

    /// <summary>P-78 — Página de código usada quando a biblioteca local não expõe interface de 16 bits (RF-289). [CALIBRADO] [Exposto: FIXO]</summary>
    public const int LocalTranslatorCodePage = 932;

    /// <summary>P-135 — Tamanho máximo de mensagem do canal nomeado. [Exposto: FIXO]</summary>
    public const int NamedPipeMaxMessageBytes = 65535;
    /// <summary>P-138 — Intervalo de sondagem de inicialização do canal nomeado. [Exposto: FIXO]</summary>
    public const int NamedPipeInitPollMs = 250;
    /// <summary>P-143 — Intervalo de sondagem da resposta do canal nomeado. [Exposto: FIXO]</summary>
    public const int NamedPipeResponsePollMs = 50;
    /// <summary>P-134 — Capacidade inicial dos acumuladores de texto em chamadas nativas. [Exposto: FIXO]</summary>
    public const int NativeTextBufferCapacity = 8192;

    // ─────────────────────────────────────────────────────────────────────────
    // IV.9 — Janelas de tradução
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>P-79 — Alfa do fundo do modo camada quando a tradução está parada. [CALIBRADO] [Exposto: FIXO]</summary>
    public const byte LayerIdleBackgroundAlpha = 190;

    /// <summary>P-80 — Espessura do contorno externo do texto. [CALIBRADO] [Exposto: FIXO]</summary>
    public const double OuterStrokeWidth = 5;
    /// <summary>P-81 — Espessura do contorno interno do texto. [CALIBRADO] [Exposto: FIXO]</summary>
    public const double InnerStrokeWidth = 2;

    /// <summary>P-82 — Expansão do retângulo de fundo à esquerda, modo camada (RF-337). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double LayerBackgroundExpandLeft = 8;
    /// <summary>P-83 — Expansão do retângulo de fundo acima, modo camada. [CALIBRADO] [Exposto: FIXO]</summary>
    public const double LayerBackgroundExpandTop = 4;
    /// <summary>P-84 — Expansão da largura do retângulo de fundo, modo camada. [CALIBRADO] [Exposto: FIXO]</summary>
    public const double LayerBackgroundExpandWidth = 16;
    /// <summary>P-85 — Expansão da altura do retângulo de fundo, modo camada. [CALIBRADO] [Exposto: FIXO]</summary>
    public const double LayerBackgroundExpandHeight = 8;

    /// <summary>P-86 — Margem do texto no modo camada. [Exposto: FIXO]</summary>
    public const double LayerTextMargin = 15;

    /// <summary>P-87 / P-88 — Tamanho mínimo do modo camada. [Exposto: FIXO]</summary>
    public const int LayerMinWidth = 200, LayerMinHeight = 100;

    /// <summary>P-89 — Zona sensível de redimensionamento das janelas de tradução. [Exposto: FIXO]</summary>
    public const int TranslationWindowResizeHotZone = 30;

    /// <summary>
    /// P-133 — Posição e tamanho padrão do modo camada: (20, altura_da_tela − 300), 973 × 192.
    /// [CALIBRADO] [Exposto: FIXO]
    /// </summary>
    public const int LayerDefaultX = 20;
    public const int LayerDefaultYOffsetFromScreenBottom = 300;
    public const int LayerDefaultWidth = 973;
    public const int LayerDefaultHeight = 192;

    /// <summary>P-90 — Duração do aviso de sobreposição de janela (RF-343). [Exposto: FIXO]</summary>
    public static readonly TimeSpan WindowOverlapWarningDuration = TimeSpan.FromSeconds(10);

    /// <summary>P-91 — Tempo em que a sobreposição fica capturável após o atalho de captura de tela (RF-347). [CALIBRADO] [Exposto: FIXO]</summary>
    public static readonly TimeSpan ScreenshotCapturableWindow = TimeSpan.FromMilliseconds(5000);

    /// <summary>P-92 — Fator de folga do retângulo da janela de sobreposição (RF-349). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double OverlayRectSlackFactor = 1.3;

    /// <summary>P-93 — Redução por lado para o retângulo de conteúdo quando há contorno (RF-359). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double ContentRectInsetWithStroke = 4;
    /// <summary>P-154 — Redução inicial do retângulo de conteúdo antes do ajuste de layout. [Exposto: FIXO]</summary>
    public const double ContentRectInitialInset = 4;

    /// <summary>P-94 — Razão a partir da qual o bloco líder preserva o próprio tamanho (RF-360). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double LeadBlockOwnSizeRatio = 1.3;

    /// <summary>P-95 — Escala aplicada ao tamanho de fonte derivado do original (RF-360). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double DerivedFontSizeScale = 1.15;

    /// <summary>P-96 — Iterações máximas da bissecção do tamanho de fonte (RF-363). [CALIBRADO] [Exposto: FIXO]</summary>
    public const int FontSizeSearchIterations = 9;
    /// <summary>P-97 — Precisão em que a bissecção do tamanho de fonte para (RF-363). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double FontSizeSearchEpsilon = 0.25;

    /// <summary>P-98 — Fator de avanço entre linhas, multiplicado pela altura da fonte (RF-365). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double LineAdvanceFactor = 1.2;

    /// <summary>P-99 — Folga adicionada aos limites medidos quando há contorno (RF-366). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double StrokeMeasurementSlack = 2.5;

    /// <summary>P-100 — Folga da quebra de linha, em múltiplos do tamanho da fonte (RF-369). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double LineBreakSlackFactor = 1.2;

    /// <summary>P-131 — Permanência do resultado na sobreposição após um ciclo pontual, em segundos. [CALIBRADO] [Exposto: UI]</summary>
    public const int OneShotResultHoldSecondsDefault = 5;

    /// <summary>P-129 — Tamanho mínimo de fonte automática; mínimo do controle 5. [CALIBRADO] [Exposto: UI]</summary>
    public const double AutoFontSizeMinDefault = 10;
    public const double AutoFontSizeControlMin = 5;
    /// <summary>P-130 — Tamanho máximo de fonte automática; mínimo do controle 5. [CALIBRADO] [Exposto: UI]</summary>
    public const double AutoFontSizeMaxDefault = 50;

    /// <summary>P-127 — Tamanho de fonte padrão do texto traduzido. [CALIBRADO] [Exposto: UI]</summary>
    public const double DefaultFontSize = 15;

    // P-163 — Família de fonte padrão: a fonte de interface do sistema, resolvida em
    // tempo de execução (RF-387). Não é um nome literal; ver FontResolution nos dados.

    /// <summary>P-128 — Tamanho mínimo de fonte aceito pelo controle da interface. [Exposto: UI]</summary>
    public const double UiFontSizeMin = 8;

    /// <summary>P-101 — Cor de texto padrão. [CALIBRADO] [Exposto: UI]</summary>
    public static readonly (byte R, byte G, byte B) DefaultTextColor = (255, 255, 255);
    /// <summary>P-102 — Cor de contorno 1 padrão (contorno interno). [CALIBRADO] [Exposto: UI]</summary>
    public static readonly (byte R, byte G, byte B) DefaultStroke1Color = (192, 192, 192);
    /// <summary>P-103 — Cor de contorno 2 padrão (contorno externo). [CALIBRADO] [Exposto: UI]</summary>
    public static readonly (byte R, byte G, byte B) DefaultStroke2Color = (0, 0, 0);
    /// <summary>P-104 — Cor de fundo padrão, com alfa. [CALIBRADO] [Exposto: UI]</summary>
    public static readonly (byte A, byte R, byte G, byte B) DefaultBackgroundColor = (170, 0, 0, 0);

    /// <summary>P-155 — Borda de destaque desenhada quando a tradução não está rodando. [Exposto: FIXO]</summary>
    public static readonly (byte R, byte G, byte B) IdleHighlightBorderColor = (40, 134, 249);
    public const double IdleHighlightBorderThickness = 3;

    /// <summary>P-156 — Cor de limpeza do quadro da sobreposição. [Exposto: FIXO]</summary>
    public static readonly (byte A, byte R, byte G, byte B) OverlayClearColor = (0, 240, 248, 255);

    /// <summary>P-157 — Cor de destaque das áreas de palavra no modo de depuração. [Exposto: FIXO]</summary>
    public static readonly (byte A, byte R, byte G, byte B) DebugWordRectColor = (90, 0, 0, 0);

    // ─────────────────────────────────────────────────────────────────────────
    // IV.10 — Análise automática de cor 🔒
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>P-105 — Máximo de amostras no retângulo de fundo do bloco. [CALIBRADO] [Exposto: FIXO]</summary>
    public const int ColorMaxSamplesBackground = 65536;
    /// <summary>P-106 — Máximo de amostras por retângulo de palavra. [CALIBRADO] [Exposto: FIXO]</summary>
    public const int ColorMaxSamplesWord = 4096;

    /// <summary>P-107 — Alfa mínimo para o pixel entrar na estatística. [CALIBRADO] [Exposto: FIXO]</summary>
    public const byte ColorMinAlpha = 128;

    /// <summary>P-108 — Espessura da faixa de borda da palavra, como fração do lado menor. [CALIBRADO] [Exposto: FIXO]</summary>
    public const double ProbeBandRatio = 0.15;
    /// <summary>P-109 — Espessura máxima da faixa de borda. [CALIBRADO] [Exposto: FIXO]</summary>
    public const int ProbeBandMaxThickness = 4;

    /// <summary>P-110 — Sondas mínimas para aceitar um fundo local (RF-400). [CALIBRADO] [Exposto: FIXO]</summary>
    public const int ProbeMinCount = 3;

    /// <summary>P-111 — Apoio mínimo entre palavras para aceitar o fundo global (RF-401). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double GlobalBackgroundMinSupport = 0.4;

    /// <summary>P-112 — Largura do anel ao redor da palavra, como fração do lado menor. [CALIBRADO] [Exposto: FIXO]</summary>
    public const double RingPaddingRatio = 0.2;
    /// <summary>P-113 — Largura mínima do anel. [CALIBRADO] [Exposto: FIXO]</summary>
    public const int RingPaddingMin = 1;
    /// <summary>P-114 — Largura máxima do anel. [CALIBRADO] [Exposto: FIXO]</summary>
    public const int RingPaddingMax = 4;

    /// <summary>P-115 — Contraste mínimo texto/fundo, em razão de luminância (RF-406, RF-410). [CALIBRADO] [Exposto: FIXO]</summary>
    public const double MinContrastRatio = 2.5;

    /// <summary>P-158 — Bits descartados por canal na quantização de cor: 3 bits ⇒ 32 níveis (RF-398). [CALIBRADO] [Exposto: FIXO]</summary>
    public const int ColorQuantizationBitsDropped = 3;

    /// <summary>P-159 — Critério de aceitação da sonda de borda: pelo menos 2 cantos OU 5 sondas (RF-400). [CALIBRADO] [Exposto: FIXO]</summary>
    public const int ProbeRequiredCorners = 2;
    public const int ProbeRequiredTotal = 5;

    /// <summary>P-160 — Coeficientes de luminância relativa (RF-411). [Exposto: FIXO]</summary>
    public const double LumR = 0.2126, LumG = 0.7152, LumB = 0.0722;

    /// <summary>P-161 — Constantes de linearização sRGB (RF-411). [Exposto: FIXO]</summary>
    public const double SrgbCutoff = 0.04045, SrgbSlope = 12.92,
                        SrgbScale = 1.055, SrgbOffset = 0.055, SrgbGamma = 2.4;

    /// <summary>P-162 — Constante somada às luminâncias na razão de contraste (RF-411). [Exposto: FIXO]</summary>
    public const double ContrastConstant = 0.05;

    // ─────────────────────────────────────────────────────────────────────────
    // IV.11 — Atualização, atalhos e recursos auxiliares
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>P-116 — Período de espera após falha de verificação de integridade (RF-428). [CALIBRADO] [Exposto: FIXO]</summary>
    public static readonly TimeSpan UpdateFailureCooldown = TimeSpan.FromMinutes(10);

    /// <summary>P-117 — Tentativas de mover um arquivo bloqueado durante a atualização. [Exposto: FIXO]</summary>
    public const int FileMoveAttempts = 10;
    /// <summary>P-118 — Intervalo entre tentativas de mover. [Exposto: FIXO]</summary>
    public const int FileMoveRetryMs = 500;

    /// <summary>P-119 — Quantidade de atalhos dedicados a abrir perfil. [Exposto: FIXO]</summary>
    public const int ProfileShortcutCount = 4;

    /// <summary>RF-442 — Máximo de teclas por combinação. [Exposto: FIXO]</summary>
    public const int MaxShortcutKeys = 3;

    /// <summary>P-120 — Verificações de janela ativa antes do instantâneo (RF-452). [CALIBRADO] [Exposto: FIXO]</summary>
    public const int SnapshotForegroundChecks = 15;
    /// <summary>P-121 — Intervalo entre essas verificações. [CALIBRADO] [Exposto: FIXO]</summary>
    public const int SnapshotForegroundCheckIntervalMs = 100;

    /// <summary>P-122 — Intervalo do temporizador da área que segue o mouse. [CALIBRADO] [Exposto: FIXO]</summary>
    public const int MouseFollowTimerMs = 30;
    /// <summary>P-123 — Intervalo mínimo de recálculo das áreas pela área que segue o mouse. [CALIBRADO] [Exposto: FIXO]</summary>
    public const int MouseFollowRecalcMinIntervalMs = 100;
    /// <summary>P-124 — Tempo em que a área que segue o mouse pisca visível ao ser criada. [Exposto: FIXO]</summary>
    public const int MouseFollowFlashMs = 500;
}
