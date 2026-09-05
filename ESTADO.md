# Estado da construção

Rastreia o progresso contra a **PARTE X — ORDEM DE CONSTRUÇÃO** de `instrucoes.md`,
que é a única fonte de verdade. Cada etapa lista os requisitos que ela cobre.

**Stack (Apêndice A):** C# / .NET 9 · Avalonia · ONNX Runtime com RapidOCR ·
captura por camada específica de sistema atrás da abstração de RF-577.

```
Gort.sln
├── data/                       catálogos como DADO, não código (RF-029)
│   ├── languages.toml          tabela de idiomas (RF-308 a RF-316)
│   ├── engines.toml            motores de OCR, serviços, modelos, fontes, links
│   └── localizacao.csv         textos da interface (RF-481 a RF-489)
├── src/
│   ├── Gort.Core/              todo o pipeline, sem nenhuma dependência de plataforma
│   ├── Gort.Platform/          abstração C1–C20 (RF-577), uma implementação por sistema
│   ├── Gort.Ocr.Rapid/         motor de OCR do Apêndice A (ONNX Runtime + RapidOCR)
│   ├── Gort.Engine/            o ciclo do capítulo 8, passos 7 a 13
│   └── Gort.App/               interface em Avalonia
├── build/
│   └── macos/                  Info.plist e o roteiro que monta GORT.app
├── tools/
│   ├── Gort.CaptureProbe/      teste visual das Etapas 2, 3 e 4
│   ├── Gort.OcrProbe/          teste do motor de OCR (Etapa 5)
│   ├── Gort.CycleProbe/        ciclo completo de ponta a ponta (Etapa 7)
│   ├── Gort.LayerProbe/        desenho da camada e da sobreposição, fora da tela
│   └── Gort.OptionsProbe/      as abas de V.3 e as janelas de V.4, fora da tela
└── tests/
    ├── Gort.Core.Tests/        643 testes
    ├── Gort.Platform.Tests/     39 testes
    ├── Gort.Ocr.Tests/          40 testes
    ├── Gort.Engine.Tests/       24 testes
    └── cases/grouping/         casos de agrupamento gravados em arquivo (Etapa 6)
```

## Onde parei — 5 de setembro de 2026

Último commit: **verificação de ponta a ponta do build atual**, com um diálogo de jogo real
— 240 ms, 80% do orçamento de P-05. 750 testes passando (643 + 39 + 40 + 28).

O que resta depende de coisas de fora desta máquina:

- **Oito dos nove serviços de tradução** (RF-249 a RF-291) — credenciais que só o usuário
  tem, ou autenticação por delegação. O rodízio de chaves, os presets, o protocolo de lote e
  a API personalizada já existem; falta o adaptador de cada um.
- **Atualização automática** — RF-416 a RF-435: servidor de distribuição.
- **RF-539 a RF-542** — OCR de nuvem, instalador do motor por ambiente interpretado,
  servidor da comunidade, navegador embutido.
- **Captura de janela anexada** — C2/C3, RF-089 a RF-097, não implementadas no macOS.
- **Motores de OCR clássico, de nuvem e por ambiente interpretado** — as regras estão
  prontas; faltam os SDKs.
- **Atalhos globais** — a lógica está verificada; falta conceder a permissão de
  Acessibilidade ao GORT.app, que agora aparece na lista do sistema com nome próprio.

## Etapas concluídas

| Etapa | Requisitos | Situação |
|---|---|---|
| **1 — Esqueleto e configuração** | RF-020 a RF-046 | **Persistência completa.** Falta o ciclo de vida da aplicação (RF-001 a RF-019), que depende da interface. |
| **2 — Abstração de plataforma e captura** | RF-088, RF-100, RF-568 a RF-578 | **Completa.** C1 e C18 implementados nos três sistemas; captura verificada de ponta a ponta no macOS. |
| **3 — Regiões de captura** | RF-047 a RF-087 | **Completa.** Modelo, geometria e agora as molduras desenhadas, com mover, redimensionar e a aparência distinta das exclusões. |
| **5 — Um motor de OCR** | RF-120, RF-121, RF-141 a RF-146 | **Completa e verificada** em texto real de tela. Detecção DBNet e reconhecimento CRNN com decodificação CTC, em inglês e japonês. |
| **7 — Um serviço de tradução e o modo escuro** | RF-225 a RF-248, RF-308 a RF-331 | **Completa.** É o *primeiro produto utilizável de ponta a ponta*: captura, reconhece, traduz e mostra numa janela. |
| **8 — Laço, controle e detecção de mudança 🔒** | RF-004, RF-005, RF-009 a RF-014, RF-192 a RF-205, RF-547 a RF-551 | **Completa.** Tradução contínua, protocolo de pausa e os três critérios de aceite do capítulo 9 verificados. |
| **9 — Atalhos e controle remoto** | RF-436 a RF-453, RF-517 a RF-522 | **Lógica completa e verificada.** O controle remoto funciona; os atalhos globais dependem de permissão de Acessibilidade, ausente nesta máquina. |
| **11 — Modo camada** | RF-007, RF-332 a RF-343, RF-387 a RF-391 | **Completa e verificada** por renderização fora da tela: contorno duplo, fundo do texto, transparência e borda de destaque. |
| **12 — Modo sobreposição, layout 🔒** | RF-344 a RF-386, RF-392 | **Completa e verificada.** Colisões, tamanho automático de fonte, quebra por caractere e desenho sobre o texto original. |
| **13 — Análise automática de cor 🔒** | RF-098, RF-099, RF-393 a RF-415 | **Ligada ao ciclo.** A análise construída na primeira leva entrou em uso no passo 14 do fluxo. |
| **14 — Demais motores de OCR** | RF-122 a RF-140, RF-147 a RF-151 | **Regras completas** e o motor do sistema (C20) funcionando no macOS. Os motores clássico, de nuvem e por ambiente interpretado dependem de SDKs e credenciais que não há como exercitar aqui. |
| **16 — Recursos auxiliares** | RF-454 a RF-480 | **Completos e verificados:** área que segue o mouse, área de transferência e leitura em voz alta. A captura de janela anexada (RF-089 a RF-097) exige C2/C3, que não estão implementadas no macOS. |
| **17 — Localização e interface** | RF-481 a RF-489, RF-501 a RF-546 | **Localização completa** e as sete abas de V.1 no ar, com todo texto vindo da tabela. As janelas auxiliares de V.4 ainda não existem. |
| **4 — Pré-processamento** | RF-101 a RF-119 | **Completa** no núcleo. Conta-gotas e pré-visualização binarizada existem como função (`Preprocessor.Preview`); falta a janela. |
| **6 — Estruturação e agrupamento 🔒** | RF-152 a RF-179 | **Completa e verificada.** Todos os seis critérios de aceite do cap. 15 passam, mais 8 casos gravados em arquivo. |
| **— Tratamento textual** | RF-180 a RF-191 | **Completa.** |
| **— Detecção de mudança 🔒** | RF-192 a RF-205 | **Completa.** |
| **— Cache e fontes locais** | RF-206 a RF-224, RF-241 a RF-243 | **Completa.** |
| **13 — Análise automática de cor 🔒** | RF-393 a RF-415 | **Completa e verificada.** Os quatro critérios de aceite do cap. 20 passam. |
| **18 — Depuração e diagnóstico** | RF-490 a RF-500 | **Completa.** Retrato de análise ligado ao ciclo e ao desenho da sobreposição, contadores, gravação do resultado e os sinalizadores de RF-500. Falta a atualização automática (RF-416 a RF-435), que precisa de um servidor de distribuição. |
| **17b — Opções avançadas (V.3)** | RF-302 a RF-307, RF-447, RF-523 a RF-532, RF-545, RF-546 | **Completa e verificada** pelas sete abas renderizadas fora da tela. |
| **17 — Interface completa** | RF-481 a RF-546, RF-054 a RF-064, RF-250 a RF-253 | **Completa.** As sete abas de V.3 e as seis janelas de V.4 que não dependem de serviços externos, todas verificadas em imagem. |
| **19 — Endurecimento** | RF-001 a RF-003, RF-086, RF-087, RF-552 a RF-567, PARTE VIII | **Completa.** Instância única verificada na máquina, liberação das imagens de região, indicador de memória detalhado, aviso de mudança de monitor, robustez e evolução travadas por teste — inclusive uma varredura do código-fonte para RF-567 — e a PARTE VIII conferida linha a linha. |
| **15 — API personalizada** | RF-292 a RF-301, RF-306, RF-307 | **Completa.** É o único serviço da PARTE VI que não depende de credencial de terceiro: quem fornece o endereço é o usuário. |

Também prontos, transversais a tudo:

- **PARTE IV inteira** — os 163 parâmetros em `Calibration/P.cs`, cada um com o seu
  identificador `P-xx`, o valor exato, a coluna *Exposto* e a marca `[CALIBRADO]` para
  os 🔒. É o artefato que a PARTE XII protege.
- **Modelo de dados do cap. 7** — palavra, linha, bloco, imagem de região, resultado de
  região, grupo de cor, atalho, entrada de memória de exibição.
- **Catálogo (RF-029, RF-566, RF-567)** — idiomas, motores e serviços são dados. 29 testes
  leem os arquivos reais de `data/`; se alguém mover uma dessas decisões para o código,
  eles quebram.

## Etapas por construir

| Etapa | Requisitos | Observação |
|---|---|---|
| 15 — Demais serviços de tradução | RF-249 a RF-307 | Nove serviços. A maioria exige credenciais que só o usuário tem. |
| 17b — Janelas auxiliares (V.3 e V.4) | RF-523 a RF-546 | Opções avançadas, conta-gotas, editor de dicionário, gerenciamento de áreas e de chaves. |
| 18b — Atualização automática e comunidade | RF-416 a RF-435 | Precisa de um servidor de distribuição que não existe. |
| 19 — Endurecimento | RF-560 a RF-567, PARTE VIII | O que resta é revisão do que já existe, não construção. |

Lacunas conhecidas, fora da ordem de construção:

- **Captura de janela anexada** (C2/C3, RF-089 a RF-097) — não implementada no macOS.
- **Motores de OCR clássico, de nuvem e por ambiente interpretado** — dependem de SDKs e
  credenciais que não há como exercitar aqui; as regras estão prontas.
- **Atalhos globais** — a lógica está verificada, mas o registro no sistema depende da
  permissão de Acessibilidade, ausente nesta máquina.

## Camada de plataforma — o que está verificado

`PlatformServices.Create()` escolhe a implementação do sistema e apura **todas** as vinte
capacidades da PARTE IX.1 na inicialização (RF-576). Nada acima da abstração conhece o
sistema operacional (RF-577).

| Sistema | C1 captura | C18 monitores | Situação |
|---|---|---|---|
| **macOS** | CoreGraphics | CoreGraphics | **Verificada nesta máquina.** Conteúdo, orientação, cores e dimensões conferidos contra a tela real. C7 e C20 também. |
| **Windows** | GDI (`BitBlt` + `CreateDIBSection`) | `EnumDisplayMonitors` + `GetDpiForMonitor` | Compila; **não executada aqui** — falta uma máquina Windows. |
| **Linux/X11** | `XGetImage` | Xinerama | Compila; **não executada aqui**. Sob Wayland a sessão é detectada e C1/C5/C10/C12 são reportadas indisponíveis com a explicação de RF-568. |

**Latência (VII.1).** Orçamento de P-05 é 300 ms para o ciclo inteiro quando a tradução vem
do cache. Medido numa captura real de 796 × 277:

| Etapa | Custo |
|---|---|
| Captura (C1) | 16,8 ms |
| Pré-processamento — filtro HSV, erosão, ampliação 2× | 12 ms |
| OCR — detecção | 49 ms |
| OCR — reconhecimento | ~14 ms **por linha** |

O reconhecimento é por linha, então o custo cresce com a quantidade de texto.

**RF-547 verificado.** O requisito é o ciclo inteiro caber em P-05 *quando a tradução vem
do cache*. Medido de ponta a ponta com o ciclo real, numa caixa de diálogo de duas linhas:

| Situação | Tempo | Orçamento |
|---|---|---|
| Caixa de diálogo, 2 linhas, **tradução em cache** | **99 ms** | **33% de P-05** ✓ |
| Diálogo de jogo, 800×184, 3 linhas / 3 blocos, em cache | **240 ms** | **80% de P-05** ✓ |
| A mesma caixa, primeira vez (com rede) | 664 ms | dominado pela rede — RF-548 |
| Tela inteira de IDE, 51 linhas, 29 blocos, em cache | 1054 ms | estoura |

A segunda linha é a verificação mais recente, refeita depois de todo o trabalho das etapas
17 a 19 — diagnóstico ligado ao ciclo, imagens de região soltas por região, tudo. É uma
caixa de diálogo de jogo de verdade, com título e duas falas, a 2× de ampliação:

```
Texto reconhecido:
   The Old Keeper The gate has been sealed for a hundred years.
   Only the one who carries the broken sigil may pass.

O que a janela de tradução recebe:
   O Velho Guardião
   O portão está selado há cem anos.
   Somente aquele que carrega o sigilo quebrado pode passar.
```

Uma execução intermediária dessa mesma verificação pegou a página ainda carregando e leu um
bloco só, retraduzindo no ciclo seguinte. É o comportamento que a PARTE VIII EXIGE para
"OCR devolve lixo instável" — visto acontecendo, e não só testado.

O alvo do produto é a caixa de diálogo, e é para ela que a área de OCR existe. A tela
inteira estoura porque o custo cresce com o número de linhas.

**O lote foi tentado e MEDIDO: não ajuda.** A hipótese era que o custo fixo de cada chamada
ao modelo dominasse, e que agrupar as linhas — o modelo de referência agrupa de 6 em 6 —
recuperasse a margem. Medido na mesma imagem, pelos dois caminhos:

| Caminho | 9 linhas reais | 40 linhas sintéticas |
|---|---|---|
| uma linha por chamada | **66,3 ms** | **810 ms** |
| em lote de 6 | 69,6 ms | 819 ms |

Zero diferença de texto entre os dois. A razão é a largura do tensor: num lote ela é a da
linha MAIS LARGA, e as demais viajam preenchidas com zeros até lá — o cálculo desperdiçado
consome o que se economiza no custo fixo. O motor continua reconhecendo uma linha por
chamada; `TextRecognizer.RecognizeBatch` fica testado, mas não é usado, porque a conclusão é
desta máquina e deste modelo. `tools/Gort.OcrProbe` refaz a medição.

**Permissões no macOS (RF-569).** `CGPreflightScreenCaptureAccess` dá falso negativo quando
o programa roda sob um processo responsável já autorizado (um terminal, um ambiente de
desenvolvimento). Confiar só nela faria o programa se recusar a abrir numa instalação que
captura perfeitamente. Por isso, quando ela diz que não, faz-se uma sondagem funcional de
um pixel. O caso restante — permissão negada, em que o sistema devolve o papel de parede
sem as janelas — não é distinguível por essa via e fica coberto por RF-570.

### Teste visual das Etapas 2, 3 e 4

```
dotnet run --project tools/Gort.CaptureProbe -- <pasta-de-saída>
```

Imprime o relatório de capacidades, enumera os monitores, captura uma região no canto e no
centro de cada um — incluindo coordenadas negativas —, grava tudo em PNG e mede a latência
em regime. Depois exercita o caminho inteiro das Etapas 3 e 4: uma moldura vira retângulo
de captura, uma exclusão é traduzida para as coordenadas da imagem, e o resultado passa pelo
filtro e pela ampliação, gravado como `regiao-bruta.png` e `regiao-tratada.png`.

Foi assim que se conferiu, em pixels reais, o critério de aceite do capítulo 13 — "as letras
em preto e o resto em branco" — e o objetivo declarado de RF-102: **a exclusão fica
invisível para o OCR**, indistinguível do fundo.

A opção `--ignorar-permissao` existe só nessa ferramenta, para distinguir "a ligação nativa
está errada" de "falta a permissão do sistema"; o programa em si obedece a RF-569 e não
inicia sem a permissão.

## O motor de OCR

`Gort.Ocr.Rapid` implementa o contrato de 6.4 com o stack do Apêndice A. Detecção DBNet
(acha **onde** há texto), reconhecimento CRNN com decodificação CTC gulosa (acha **o que**
está escrito). O pós-processamento do detector — binarização, dilatação, componentes
conectadas, casco convexo, retângulo de área mínima, pontuação e expansão — é próprio, sem
dependência de biblioteca de visão computacional.

Os modelos ficam em `modelos/` e **não entram no versionamento**; `modelos/LEIAME.md`
explica como obtê-los. Qual modelo atende qual idioma é **dado**, em
`data/engines.toml` → `[modern_ocr]` (RF-029).

**O japonês precisa de modelo próprio.** O modelo chinês cobre kanji, latino e pontuação
japonesa, mas tem **1 de 46 hiraganas e 3 de 46 katakanas**. Como kana é a maior parte de
uma frase japonesa, texto japonês sairia ilegível com ele — e RF-309 põe o japonês no
escopo. Verificado contando a cobertura do dicionário de cada modelo.

**Verificado:** inglês lido corretamente de uma captura real de tela; o modelo japonês
carregado com dicionário externo e lendo latino corretamente, o que confirma que o
mapeamento índice → caractere está alinhado (um erro de um índice produziria lixo). A
acurácia em texto japonês de verdade ainda **não foi medida** — falta conteúdo japonês em
tela para comparar.

## O produto de ponta a ponta

`Gort.App` é a aplicação Avalonia: janela principal com o estado do sistema, definição de
área, configuração básica e "traduzir uma vez"; camada de seleção de área sobre toda a área
de trabalho virtual; e a janela de tradução em modo escuro.

`Gort.Engine` tem o `TranslationCycle`, que executa os passos 7 a 13 do fluxo do capítulo 8.
O laço que os repete fica FORA dele — é o que permite "traduzir uma vez" percorrer
exatamente o mesmo caminho da tradução contínua, que é o que a Etapa 8 vai acrescentar.

**Verificado de ponta a ponta:** capturou a tela, reconheceu 51 linhas, agrupou em 29
blocos, traduziu todos em UMA requisição, e o segundo ciclo não foi à rede.

## O laço de tradução

`TranslationLoop` roda em **thread dedicada** e é **síncrono de ponta a ponta** dentro dela
(RF-009). O término da thread é o sinal de parada que a interface usa; se a thread
terminasse no primeiro ponto de espera, quem esperava por ela concluiria que parou e
passaria a alterar configuração com o ciclo ainda rodando.

É por isso que a tradução, que é assíncrona, é aguardada por **sondagem** em passos de
P-126 dentro da thread, e não com `await`: o `await` devolveria a thread ao chamador e
destruiria a garantia. Foi a decisão de projeto mais consequente desta etapa.

Os três critérios de aceite do capítulo 9 estão verificados em teste:

| Critério | Como está coberto |
|---|---|
| 20 acionamentos seguidos não deixam duas threads vivas nem matam o gancho de teclado | alterna início e parada 20 vezes e confere o estado final |
| Aplicar configuração nunca produz um ciclo com meia configuração antiga e meia nova | observa o estado do laço **de dentro** da ação: é sempre `Idle` |
| Fechar durante uma tradução com serviço lento encerra em no máximo P-03 | tradução de 10 s, parada medida abaixo de 1 s |

`ApplyResult` distingue "aplicado sem pausa" de "abortado", coisa que o pseudocódigo do
capítulo 9 não faz — ele devolve falso nos dois casos. RF-012 exige que o chamador seja
informado de que **nada foi aplicado**, e um único booleano não diz isso.

## Atalhos e controle remoto

A lógica dos atalhos é independente de plataforma e está inteiramente testada: normalização
das variantes esquerda/direita (RF-437), correspondência por conjunto sem ordem (RF-438),
duplicatas aceitas em silêncio com ordem de verificação estável (RF-439), repetição
automática ignorada (RF-440) e atalho vazio válido (RF-446).

**A ordem de verificação que RF-439 manda documentar** é a ordem de declaração de
`ShortcutAction` e, dentro de cada ação, o índice crescente.

**Os atalhos globais não puderam ser verificados nesta máquina.** C10 no macOS exige
permissão de Acessibilidade, e ela está ausente. O gancho por interceptação de eventos está
escrito — em modo apenas observação, para que as teclas sigam intactas para o jogo — mas
não foi executado. A degradação de RF-569 está no lugar e foi verificada em tela: o programa
informa *"O macOS exige permissão de Acessibilidade… Sem ela, use o controle remoto"*.

O controle remoto **funciona e foi verificado**: `Área · Instantâneo · Iniciar · Config. · —`,
com iniciar e parar ocupando o mesmo lugar (RF-517), movível por qualquer ponto e
redimensionável mantendo a proporção (RF-518).

## O modo camada

Uma janela transparente e sem bordas, que o usuário posiciona onde quiser. Parada, tem fundo
semitransparente (P-79) e uma borda de destaque para ser encontrada e movida; traduzindo,
fica invisível exceto pelo texto e **deixa os cliques passarem** (RF-333, RF-334).

O contorno duplo de RF-336 é desenhado como caminho vetorial, na ordem que o requisito
determina: externo de P-80 = 5 px na cor de contorno 2, interno de P-81 = 2 px na cor de
contorno 1, e só então o preenchimento. É essa ordem que produz a moldura em duas camadas
que mantém o texto legível sobre qualquer fundo de jogo.

**Verificado por renderização fora da tela** (`tools/Gort.LayerProbe`), com o mesmo motor de
desenho que a aplicação usa em tela. Quatro estados gravados em PNG: parada, traduzindo, sem
contorno e sem fundo. É o tipo de coisa que só se vê a olho, e a ferramenta torna a
verificação repetível.

RF-007 passa: o desenho vetorial funciona, inclusive com a parte japonesa da cadeia de teste.
Quando falha, o contorno é desativado em todo o programa e o texto continua legível, sem a
moldura.

## O modo sobreposição

É o modo que dá nome ao produto: não há janela visível, e a tradução de cada bloco é
desenhada **sobre o bloco original**, com tamanho de fonte proporcional e cores extraídas da
própria imagem. O resultado se parece com uma versão traduzida do software.

O caminho de layout, todo calibrado:

| Passo | Requisito | O que faz |
|---|---|---|
| Colisões | RF-355 a RF-358 🔒 | separa o par de maior interseção, no eixo que perde menos área |
| Tamanho de fonte | RF-360 a RF-363 🔒 | deriva do original, satura, e busca por bissecção com atalho no caso comum |
| Quebra de linha | RF-369 a RF-372 🔒 | por **caractere**, com busca binária do maior prefixo |
| Teste de "cabe" | RF-364 🔒 | posiciona cada linha onde ela será desenhada, sem somar alturas |
| Cache de medição | RF-374 🔒 | 45 acertos para 24 medições reais num cenário de três blocos |

**Verificado por renderização fora da tela**, com o caso mais comum do produto: um nome de
personagem curto sobre uma fala longa, com os retângulos se sobrepondo. O título preservou
`(30,24 190x46)` inteiro e a fala cedeu, começando exatamente onde ele termina — que é o que
RF-357 exige, e o que impede o nome do personagem de virar texto ilegível.

A quebra sai **no meio da palavra** ("Precisamos s / air agora"). Isso é o comportamento
exigido, não um defeito: RF-369 quebra por caractere, e a PARTE XI item 15 proíbe
explicitamente hifenização e quebra por palavra.

## Motores de OCR

| Motor | Situação |
|---|---|
| **moderno** (ONNX + RapidOCR) | **Funciona.** Inglês e japonês, verificado em texto real de tela. |
| **do sistema** (Vision, no macOS) | **Funciona.** 53 linhas de uma captura de 1512 × 491 em 456 ms, com caixas corretas. |
| clássico, de nuvem, por ambiente interpretado | **Regras prontas e testadas**; os adaptadores dependem de bibliotecas nativas, credenciais e instaladores que não há como exercitar aqui. |

As regras que valem para todos eles estão completas e testadas: recusa do motor de nuvem em
tempo real (RF-122), priorização em modo pontual com as três condições (RF-123), cota mensal
por credencial com virada de mês (RF-124 a RF-127), preservação do idioma ao trocar de motor
(RF-149), propagação do idioma para os serviços (RF-147) e o sufixo de modo rápido do motor
clássico (RF-150).

**Uma armadilha do Objective-C:** as classes só existem depois que o framework que as define
é carregado no processo. Perguntar por `VNRecognizeTextRequest` antes disso devolve nulo, e o
motor se declarava indisponível numa máquina em que funciona perfeitamente. O framework é
carregado explicitamente antes da consulta.

## Recursos auxiliares

| Recurso | Situação |
|---|---|
| Área que segue o mouse (RF-454 a RF-463) | **Funciona.** Posição do cursor real, com o portão de P-123 limitando o recálculo. |
| Área de transferência (RF-464 a RF-475) | **Funciona.** Monitoramento e cópia do resultado nos três formatos. |
| Leitura em voz alta (RF-476 a RF-480) | **Funciona.** Verificada: `IsSpeaking` transiciona corretamente, que é o que RF-477 precisa. |
| Captura de janela anexada (RF-089 a RF-097) | **Não implementada.** Exige C2 e C3; no macOS isso significa ScreenCaptureKit. |

A exclusão da sobreposição do monitoramento da área de transferência (RF-467) é estrutural,
não uma limitação: a sobreposição desenha sobre o texto **original da tela**, e um texto
vindo de fora não tem posição na tela para ser desenhado em cima.

## Localização e interface

A tabela de textos é `data/localizacao.csv` — um arquivo **externo**, editável diretamente,
sem recompilar e sem etapa de exportação (RF-489). Acrescentar um idioma de interface é
acrescentar uma **coluna**.

O leitor entende aspas, vírgulas e quebras de linha dentro de um campo (RF-482). Não é
luxo: textos de interface têm vírgula o tempo todo, e mensagens longas — a explicação de uma
permissão que falta, por exemplo — têm quebras de linha.

**RF-485 é uma faca de dois gumes.** Uma chave ausente aparece como o próprio nome, o que
torna a falta visível para quem traduz — mas não para quem constrói: o programa segue
funcionando com `app.apply` no lugar de `Aplicar`. Por isso há um teste que **varre o código
da interface** atrás das chaves usadas e confere que todas existem, transformando um defeito
silencioso em falha de teste.

As sete abas de V.1 estão no ar, na ordem fixa, com a de Depuração oculta até o modo ser
ativado (RF-490). O assistente de configuração rápida (RF-515, RF-516) aplica tudo de uma
vez, e a ordem importa: parar a tradução primeiro, porque tudo o que vem depois mexe em
configuração que o laço estaria usando.

## Depuração e diagnóstico

O capítulo 27 chama o retrato de análise de "o que transforma *ficou ruim* em evidência
utilizável" — é o arquivo que permite ajustar os valores 🔒 sem adivinhar.

- `Diagnostics/AnalysisSnapshot.cs` — RF-492 a RF-494. Um JSON por ciclo, com instante, modo
  de janela, motor, serviço, os textos, e por área: retângulos, cores automáticas com os
  indicadores de qualidade, todas as linhas com suas palavras e caixas, e todos os blocos
  com os seus quatro retângulos. A parte de desenho traz o tamanho de fonte final de cada
  bloco, as linhas depois da quebra, os tempos e os acertos do cache. O nome do arquivo tem
  milissegundos, porque um laço de 300 ms produz mais de três retratos por segundo.
- `Diagnostics/DiagnosticRecorder.cs` — monta o retrato e cuida de **RF-495**: quando um
  ciclo novo começa antes de o desenho completar o retrato anterior, o pendente é gravado
  **sem** a parte de desenho, e não descartado. Perder o retrato justamente do quadro em que
  o desenho demorou seria perder a evidência do problema que se quer investigar.
- `Diagnostics/ResultFileWriter.cs` — RF-496. Grava o resultado de cada ciclo no **formato do
  banco de dados** (`/s`, `/t`, `/e`), reaproveitando `PairFile`, para que o usuário construa
  bancos a partir do uso real e depois os carregue como fonte local. Um teste grava pelo
  escritor e lê de volta pelo leitor do banco.
- `DiagnosticCounters` — RF-498. Contadores de OCR, traduções e chamadas de rede, com um
  registro de mensagens com teto, para não crescer sem limite numa sessão longa.

**Ligação ao ciclo.** O gravador entra pelo passo 18 do fluxo, antes do despacho do desenho:
no modo sobreposição o retrato fica pendente e `OverlaySurface` avisa, pelo evento `Drawn`,
que o desenho terminou. O aviso sai do **fim do desenho**, e não do fim de `SetBlocks`:
entre os dois está o agendamento do quadro, que é justamente parte do que RF-494 quer medir.
Os sinalizadores de `DebugOptions` chegam ao ciclo por `CycleSettings.Diagnostics`, que é
**nulo fora do modo de depuração** — é assim que o critério de aceite "desativar o modo
restaura o comportamento normal sem reiniciar" é cumprido literalmente: nada precisa ser
desfeito porque nada foi ligado.

## Opções avançadas e presets de API

A janela de V.3 ainda não existe, mas as regras que ela precisa cumprir já existem, testadas
fora dela — é onde moram as decisões difíceis do capítulo:

- `Translation/Presets/ApiPresetStore.cs` — RF-302 a RF-307. Duas fontes de presets: a lista
  editável da interface e arquivos individuais numa pasta dedicada. **O arquivo vence a
  entrada de mesmo nome da lista**, e o motivo é prático: um arquivo é o que se troca com
  outra pessoa, e quem o recebeu não deve descobrir que a sua própria lista o estava
  sombreando em silêncio. Presets de arquivo não são renomeados nem removidos pela
  interface e voltam para o seu arquivo ao salvar; duplicados dentro do mesmo conjunto são
  ignorados **com registro**, nunca em silêncio.
- `Configuration/AdvancedLabels.cs` — RF-526. Os três controles guardam inteiros e nenhum
  se apresenta como o inteiro que guarda. O nível de raciocínio vira uma **chave** derivada
  do número, e não um caso num `switch`: acrescentar um nível é acrescentar uma linha na
  tabela de localização.
- `AdvancedOptions.Defaults(idioma)` — RF-532. Restaurar padrões deriva a direção do texto
  da **propriedade** do idioma de destino, nunca de uma lista embutida (RF-311, RF-567).
- `LayerTranslationWindow` — RF-545 e RF-546. O menu relê o estado a cada abertura em vez
  de guardar cópia: como o efeito é imediato, outra parte do programa pode ter mudado a
  mesma opção desde a última vez que ele abriu.

## A janela de opções avançadas

Sete abas, verificadas por `tools/Gort.OptionsProbe`, que monta a janela FORA DA TELA e grava
cada aba em PNG. A janela é revelada por um botão e conferi-la a olho exigiria clicar nele —
o que nesta máquina esbarra na permissão de Acessibilidade ausente. A sonda contorna isso e,
de quebra, imprime o estado das regras que uma imagem não mostra: RF-523, RF-525, RF-526 e
RF-529.

Foi ela que achou duas faltas de verdade: as chaves `shortcut.OpenProfile`,
`shortcut.ToggleForcedTransparency` e `shortcut.SwitchTranslationService` não existiam na
tabela — a aba mostrava os nomes das chaves —, e os nomes dos motores, serviços e idiomas do
catálogo também não. Ambas viraram testes.

## As janelas auxiliares de V.4

- **Conta-gotas e pré-visualização binarizada, na mesma janela** (RF-535, RF-536). As duas
  são o mesmo trabalho visto de dois lados: o conta-gotas diz QUAL cor o texto tem, e a
  pré-visualização mostra o que o filtro faz com essa escolha. Em duas janelas, cada ajuste
  exigiria alternar entre elas. Clicar na imagem ADOTA a cor do pixel — ler o valor e
  digitá-lo à mão em três campos transformaria uma escolha visual em transcrição. A imagem
  amplia por REPETIÇÃO de pixel, não por interpolação: quem escolhe a cor de um pixel
  precisa ver aquele pixel, não uma média dele com os vizinhos.
- **Editor de dicionário** (RF-537). O texto reconhecido atual vem pré-preenchido — é o
  ponto do recurso: o usuário abre o editor no instante em que viu o OCR errar, e o texto
  errado já está lá. Enquanto ele está aberto, a cópia automática para a área de
  transferência fica suspensa.

Verificados por `tools/Gort.OptionsProbe`, que além de gravar as janelas exercita a
binarização: com limiar 128 só as barras escuras passam; movendo o deslizante para 200 a
imagem é reprocessada SEM passar pelo botão, como RF-536 exige, e as faixas intermediárias
passam a entrar.

## As molduras e o gerenciamento de áreas

A lacuna mais antiga do projeto — o desenho das molduras, pendente desde a Etapa 3 — fechou
junto com as janelas que dependiam dela.

- `Regions/FrameResize.cs` — RF-056 a RF-058, no NÚCLEO e não na janela, porque é aritmética
  de retângulos: a janela só traduz ponteiro em chamada. Dezesseis testes cobrem os cantos
  vencendo os lados, o mínimo de P-12 travando a borda que se move, e o reposicionamento de
  RF-058.
- `AreaFrameWindow` — RF-054, RF-055, RF-059, RF-063. Barra de título com tipo, índice,
  tamanho e posição em tempo real; borda dupla desenhada; vermelha e a 70% de opacidade
  para as exclusões. A notificação de recálculo é limitada a uma a cada P-13, e o fim do
  arraste sempre notifica — é o estado final, e deixá-lo esperando o próximo tique deixaria
  a captura defasada.
- `AreaManagerWindow` — RF-062, RF-533. Fica onde o controle remoto está: é dali que o
  usuário vem, e as molduras ocupam a tela toda, então uma janela centralizada cairia por
  cima do que se está ajustando. Enquanto aberta, as áreas são temporárias (RF-061).
- `ColorGroupsWindow` — RF-534. A lista mostra os VALORES de cada grupo, não só o índice:
  escolher entre "grupo 1" e "grupo 2" sem os números é adivinhar.

## O rodízio de chaves

`Translation/Keys/TranslationKeyStore.cs` — RF-250 a RF-253. Até P-55 chaves, com estado
(normal, erro, limite) e a ordem que RF-252 fixa: **gratuitas antes das pagas, começando
pela primeira em estado normal**. A ordem não é estética — é a ordem em que o rodízio
consome as chaves, e gastar as gratuitas primeiro deixa as pagas para quando não há
alternativa.

Duas decisões que os testes fixam:

- **Trocar devolve a chave que assumiu, ou nula.** O serviço precisa distinguir "troquei" de
  "não há mais": a segunda é um erro para o usuário e a primeira não é. É também o que
  permite anexar a nota de RF-251 dizendo qual chave passou a ser usada.
- **O estado não é gravado.** Ele descreve a última sessão, não a chave: uma cota estourada
  ontem pode ter virado hoje, e abrir o programa com a chave já marcada faria o rodízio
  pular uma chave boa sem nunca tentar. Editar uma chave também a devolve ao rodízio — o
  usuário acabou de corrigir o que provavelmente causou o erro.

## Endurecimento

- `Lifecycle/SingleInstanceGuard.cs` — RF-001, RF-002. **Verificado na máquina real:** a
  segunda instância informa e encerra; com o marcador `multi-instancia`, duas rodam juntas;
  e uma trava órfã, deixada por um encerramento abrupto, não impede a próxima abertura.
- `Diagnostics/MemoryReport.cs` — RF-558, RF-559. O total mais as três parcelas que o
  usuário controla sem saber: imagens de região (ampliação e número de áreas), cache
  (uso prolongado) e mapa de bits da sobreposição (tamanho da janela). O detalhamento fica
  a um passe de mouse, sem abrir diálogo.
- **RF-554** — cada imagem de região é solta assim que a região termina, e não ao fim do
  ciclo: é o que impede o pico de memória de crescer com o número de áreas, que é
  justamente quando o usuário está no limite. Três testes travam isso, incluindo um que
  confere que os PIXELS foram soltos, e não só descontados do contador.
- **RF-086 / RF-087** — o mesmo tique que amostra a memória confere a disposição dos
  monitores. Quando ela muda e alguma área fica fora da tela, o programa avisa apontando
  QUAIS e abre o gerenciamento de áreas. Nenhuma área é movida: o programa não sabe onde o
  conteúdo do jogo foi parar, e movê-la produziria uma região errada em silêncio.

## Robustez e evolução, travadas por teste

RF-561 a RF-567 descrevem PROPRIEDADES do programa inteiro, não uma classe. Os testes de
`RobustnessTests` e `DataDrivenTests` existem para que elas parem de ser afirmações:

- **RF-562** — perfil corrompido, binário, vazio ou de outro produto: todos caem nos
  padrões. Pasta de dados ausente é criada. Catálogo ausente não lança — sem catálogo não
  há o que traduzir, mas o programa precisa ABRIR para poder dizer isso (RF-006).
- **RF-565** — chaves desconhecidas sobrevivem à regravação, conjuntos fechados são
  gravados por texto e todo arquivo carrega a versão do esquema.
- **RF-567** — uma varredura do CÓDIGO-FONTE atrás de comparações com identificadores de
  idioma. Ela procura comparação e busca, não valor padrão: um campo que nasce com `"en"` é
  decisão de produto declarada (RF-309), que o usuário muda na primeira tela; o que RF-567
  proíbe é o programa DECIDIR por comparação. A varredura foi verificada injetando uma
  violação e conferindo que ela falha.

## A PARTE VIII, linha a linha

A tabela da PARTE VIII não é uma lista de tolerâncias: cada linha diz o comportamento
EXIGIDO. `PartVIIITests` cobre as situações que ainda não tinham teste próprio; as demais
estão nos arquivos do assunto, citadas ali pelo requisito.

As que mais valem registro:

- **"Sem área de OCR definida"** — uma área de EXCLUSÃO não conta. Ela subtrai região, e a
  subtração de nada é nada; começar a traduzir só com exclusões produziria captura vazia
  todo ciclo e o usuário concluiria que o programa não funciona.
- **"OCR devolve lixo instável"** — retraduzir e redesenhar a cada ciclo é o comportamento
  EXIGIDO, não uma tolerância. Um teste trava isso: qualquer amortecimento — esperar dois
  ciclos iguais, por exemplo — custaria um ciclo de latência em TODA tradução, inclusive nas
  boas.
- **"Resposta com menos partes que blocos"** — não é erro. É a diferença entre um serviço
  que devolveu menos do que se pediu e um que falhou: o primeiro entregou o que conseguiu, e
  descartar tudo por causa do que faltou seria jogar fora tradução boa.
- **"Texto muito longo"** — vai INTEIRO na requisição. Se o serviço truncar, a tradução vem
  truncada, e isso é informação para o usuário que um corte silencioso aqui esconderia.
- **"Jogo em tela cheia exclusiva"** — a linha exige que o programa DOCUMENTE a limitação.
  Está no README e num aviso permanente na primeira aba, verificado em captura de tela.

## A API personalizada

`Translation/Services/RequestTemplate.cs` e `CustomApiTranslator.cs` — RF-292 a RF-301.

O modelo de requisição fica SEPARADO do serviço porque é tradução de texto em JSON, não
rede: é a parte que o usuário erra digitando, e a que precisa ser verificável sem endereço
nenhum. Trinta testes cobrem os marcadores, a sintaxe relaxada `chave = valor`, os vetores
convertidos elemento a elemento, a validação e a busca recursiva da chave de resultado.

A busca é recursiva por necessidade: a resposta de um serviço real quase nunca é plana — o
campo útil vem dentro de `data`, `result`, `choices[0].message` — e exigir do usuário o
caminho completo tornaria o recurso inútil para quem não conhece a API de cor.

**Dois defeitos que só os testes acharam:**

- Um modelo com chaves desbalanceadas virava `{}` em silêncio. O separador de topo nunca sai
  da profundidade aberta, nenhum par é reconhecido, e o programa enviaria um corpo VAZIO em
  vez de recusar. Agora há verificação de balanceamento antes da conversão.
- `Trim('{','}')` comia a chave final de `saida = {RESULT_TEXT}`, e o marcador deixava de
  ser reconhecido. As chaves externas só saem quando envolvem o modelo inteiro.

## O pacote do macOS

`build/macos/` — `Info.plist` e o roteiro que monta `GORT.app`.

Um pacote não é enfeite: é o que dá ao programa um **identificador estável na base de
permissões do sistema**. Rodando solto, a permissão de gravação de tela ficava presa ao
processo que lançou o programa — o terminal, o editor — e sumia quando ele mudava. Era a
causa do falso negativo de `CGPreflightScreenCaptureAccess` registrado na decisão 4.

Verificado na máquina: com o pacote, o sistema passou a listar **GORT** com interruptor
próprio em *Gravação do Áudio do Sistema e da Tela*, e o menu do aplicativo diz **GORT**.

Como rodar:

```
build/macos/empacotar.sh              # arm64, autossuficiente
build/macos/empacotar.sh x64          # para Intel
build/macos/empacotar.sh arm64 . --dependente   # sem o runtime dentro
```

## A sobreposição alimentada por um ciclo real

`tools/Gort.LayerProbe -- <pasta> --real X Y L A` captura a tela de verdade, reconhece,
traduz, analisa a cor da própria imagem e desenha a sobreposição fora da tela. Tudo o mais
na sonda usa cenas sintéticas — blocos escritos à mão para exercitar o desenho. Este modo é
a única forma de ver os capítulos 19 e 20 se encontrando sobre dados que ninguém escolheu.

Verificado com um diálogo de jogo real:

```
  ciclo: 663 ms · 3 blocos
     bloco  fonte 12,5 pt (preferido 12,5) · cor auto #FFE7DAA0
             "O Velho Guardião"
     bloco  fonte 12,5 pt (preferido 12,5) · cor auto #FFDCE6F0
             "O portão está selado há cem anos."
     bloco  fonte 12,5 pt (preferido 12,5) · cor auto #FFDCE6F0
             "Somente aquele que carrega o sigilo quebrado pode passar."
     layout 1,4 ms · desenho 3,3 ms
```

**A análise de cor do capítulo 20 acertou as cores da imagem.** O corpo do diálogo era
`#dce6f0` no original e saiu `#DCE6F0` — exato. O título era `#e8d9a0` e saiu `#E7DAA0`,
um passo de diferença em cada canal. O fundo escuro da caixa também foi extraído.

E o tamanho de fonte de RF-360 🔒 saiu igual ao preferido nos três blocos: nenhum precisou
de bissecção, o que é o esperado quando a tradução cabe no espaço do original.

## Decisões registradas

Pontos onde a especificação deixou a escolha em aberto e onde ela foi feita:

1. **Formato dos arquivos do usuário (RF-023).** TOML. Satisfaz as quatro exigências —
   texto editável, valores de múltiplas linhas, comentários, versão de esquema na raiz — e
   o modelo em memória permite preservar intactas as chaves de uma versão mais nova
   (RF-038). Não há leitura de formato legado em lugar nenhum (RF-564).

2. **Valor de fundo do filtro na exclusão (RF-102).** A região excluída recebe o valor de
   fundo diretamente no resultado binário, em vez de a imagem de origem ser pintada com uma
   cor escolhida. Pintar a origem correria o risco de a cor escolhida *passar* no filtro
   ativo — a mesma falha que o requisito existe para evitar, ao contrário.

3. **Interpolação da ampliação (RF-113).** Bilinear. A especificação diz "redimensionar"
   sem nomear o método; bilinear suaviza a escada dos glifos, que é o que melhora a taxa de
   acerto com fontes pequenas — o objetivo declarado do requisito.

4. **Colchetes de canto tipográficos (RF-171).** Cobrem 「」 e 『』.

5. **Fim de frase (RF-177).** Implementado ao pé da letra: apara-se o branco à direita uma
   vez e só depois removem-se os fechamentos de P-45.

6. **Resolução da captura (6.3).** No macOS captura-se com 1 pixel por PONTO, e não em
   resolução nativa. Não é preferência de qualidade: o contrato de coordenadas de 6.3 diz
   que voltar do espaço da imagem para o da tela é "dividir pelo fator de ampliação e somar
   a origem da área", o que só vale se a imagem for 1:1 com as coordenadas de tela. Capturar
   em resolução nativa numa tela Retina dobraria a escala silenciosamente e desalinharia
   toda a sobreposição.

7. **Arredondamento da escala de DPI (RF-074).** As espessuras de moldura são arredondadas
   para **cima**, e o erro é assimétrico de propósito. A coluna de efeito de P-14 diz que
   *diminuir* a espessura faz as "bordas entrarem na captura e virarem ruído", enquanto
   aumentá-la só faz a "área capturada ficar menor que a desenhada". Perder um pixel de
   conteúdo é barato; deixar a moldura entrar na imagem faz o OCR inventar caracteres na
   borda. (O arredondamento padrão do .NET levaria 3 × 1,5 = 4,5 para 4, não para 5.)

8. **Piso de ampliação do detector.** O padrão de referência do modelo leva o lado menor a
   736 px, pensado para fotografias. Aqui ele é 320, por medição: o programa já amplia por
   P-22 antes do OCR, e RF-113 diz que essa é "o ajuste de maior impacto na taxa de acerto
   com fontes pequenas". Aplicar 736 por cima de P-22 amplia duas vezes — a detecção subia
   de 49 para 93 ms sem reconhecer nenhuma linha a mais. Pior que o custo: uma segunda
   ampliação escondida dentro do motor tornaria o efeito de P-22 inexplicável para quem mexe
   no controle. Em região pequena, onde o piso ainda importa, 320 acha as mesmas regiões que
   736 em 18 ms em vez de 80. **Não é um valor 🔒** — é padrão de biblioteca, e a PARTE XII
   não se aplica a ele; P-22 permanece intocado.

9. **Recorte alinhado aos eixos.** A caixa que vai ao reconhecedor é a caixa alinhada aos
   eixos do quadrilátero detectado, e não um recorte com correção de perspectiva. É
   coerente com RF-142, que já define a caixa por mínimo e máximo dos quatro pontos, e com o
   alvo do produto: texto de jogo é horizontal ou vertical, não inclinado.

10. **Versão do Avalonia.** Fixada em 11.3.7, não na 12. Os geradores de código da 12
    exigem Roslyn 4.14, e o SDK .NET 9 instalado traz 4.12: eles são desativados
    **silenciosamente** (aviso CS9057) e `InitializeComponent` nunca é gerado, de modo que
    nem o projeto do próprio modelo compila. O Apêndice A fixa a biblioteca, não a versão.

11. **Endereço do tradutor web.** Fica em `data/engines.toml`, não no código — mesma regra
    dos demais endereços do programa (RF-513). Os identificadores de cliente de alta e de
    baixa qualidade de RF-245 também.

12. **Indicador de memória (RF-558).** Mede o CONJUNTO DE TRABALHO, não a memória privada.
    Fora do Windows o runtime devolve zero para a memória privada, e um indicador que marca
    zero para sempre é pior que indicador nenhum: daria a impressão de que não há consumo a
    acompanhar, que é o contrário do que o requisito quer. Descoberto rodando a aplicação —
    ela mostrava "memória: 0 MB".

13. **Posição inicial do controle remoto.** Explícita, na base do monitor principal. Sem
    ela a janela nasce no canto superior esquerdo, que é justamente onde costuma ficar
    debaixo da janela que o usuário está traduzindo — e RF-517 exige que ela esteja
    *sempre acessível*.

14. **Apelidos para tipos que colidem com o Avalonia.** `Rect`, `VerticalAlignment` e
    `HorizontalAlignment` existem nos dois mundos — no do programa e no da biblioteca de
    interface. Os três são apelidados explicitamente onde convivem, em vez de a distinção
    depender da ordem dos `using` ou de qual membro herdado vence. `HorizontalAlignment` em
    particular *ocultava* silenciosamente uma propriedade de layout do próprio `Control`.

15. **Variante de tema da janela principal.** Fixada em clara. O padrão do Avalonia segue o
    tema do sistema; com o sistema em modo escuro e os fundos claros que a janela principal
    usa, o texto saía quase invisível — o tema aplicava cor de texto clara sobre fundo
    claro. Só os rótulos com cor explícita no XAML apareciam. Descoberto olhando a tela: os
    testes não pegariam.

16. **Região fora da tela (PARTE VIII).** A verificação de que o retângulo toca algum monitor
   fica em `ScreenCapture`, acima da abstração, e não em cada implementação: a regra é da
   especificação, não do sistema. Alguns sistemas devolvem uma imagem vazia em vez de
   recusar, e uma imagem vazia entraria no OCR como texto em branco.

17. **Onde os sinalizadores de RF-500 são honrados.** O requisito manda "repassar ao
    mecanismo NATIVO de pré-processamento". Aqui o pré-processamento é gerenciado, não uma
    biblioteca nativa: quem honra "salvar captura" e "salvar resultado da captura" é o
    próprio ciclo, gravando as duas imagens na pasta de diagnóstico. O efeito observável — o
    par de imagens que mostra o que o filtro e a ampliação fizeram — é o mesmo, que é o que
    o requisito quer. Não é desvio de calibragem: RF-500 descreve um repasse, e o repasse
    não tem para onde ir.

18. **O tempo de apresentação de RF-494 é medido, não estimado.** O cronômetro começa
    quando o laço entrega o resultado e para no fim do desenho; a parcela de apresentação é
    o que sobra depois de descontar o dimensionamento e o layout. É a espera pelo
    compositor, que é a única parte do caminho que o programa não executa — medi-la por
    diferença é honesto, inventar um número para ela não seria.

19. **RF-497 já estava pronto.** O requisito é "exibir o texto reconhecido junto da
    tradução", e não a pasta de diagnóstico acessível, como um apontamento anterior deste
    documento dizia. Ele é a caixa *mostrar texto reconhecido* da aba básica, ligada desde a
    Etapa 7. O botão que abre a pasta dos retratos existe, mas por conveniência, não por
    requisito.

20. **Um leitor tolerante precisa de um aviso explícito.** RF-024 manda o leitor de TOML
    devolver padrões em vez de lançar quando o arquivo está corrompido. Na pasta de presets
    isso criava um preset de nome válido e URL vazia, que só falharia na hora de traduzir —
    o teste do arquivo ilegível pegou isso. O carregador agora usa a forma que informa se
    houve recuperação, e transforma o arquivo ilegível num aviso. A tolerância continua
    onde ela serve: um arquivo ruim não impede os outros.

21. **A janela edita uma cópia.** As opções avançadas são globais (RF-032) e o programa
    inteiro segura a mesma referência. A janela trabalha sobre `CloneForEditing()` e devolve
    os valores com `CopyFrom`, que preserva a IDENTIDADE do objeto: trocá-la faria metade do
    programa continuar lendo a antiga. É também o que dá sentido ao botão "aplicar" de
    RF-530 — fechar sem aplicar não muda nada, e "restaurar padrões" continua reversível
    enquanto não se aplica.

22. **Quais serviços têm atalho de troca é dado.** RF-447 nomeia sete, mas o catálogo tem
    dez entradas. Um número solto no código envelheceria em silêncio na primeira vez que um
    serviço fosse acrescentado; a marca `shortcut_switchable` em `data/engines.toml` é quem
    decide, e um teste confere que são exatamente sete.

23. **Um controle com modelo não desenha solto.** `TabControl`, `CheckBox` e `Slider` só
    materializam o visual quando pertencem a uma janela apresentada; medir e desenhar o
    painel isolado devolve uma imagem em branco — foi o que a primeira versão da sonda
    produziu. A plataforma "headless" com Skia dá o que falta: uma janela de verdade, sem
    tela, cujo quadro se captura com `CaptureRenderedFrame`.

24. **O que RF-535 pede e ainda não tem onde acontecer.** O requisito diz que, enquanto o
    conta-gotas está aberto, as molduras deixam de ser "sempre no topo". As molduras ainda
    não existem como janela — RF-047 a RF-056 seguem pendentes —, então não há o que
    rebaixar. O lugar onde isso vai acontecer está marcado no código, junto da regra de
    instância única, que essa sim já vale.

25. **Uma janela transparente sai preta na sonda.** As molduras têm o interior vazado — o
    usuário precisa ver o que está sendo capturado —, mas na captura fora da tela o interior
    aparece preto. Não é defeito delas: a sonda renderiza a janela de sobreposição junto,
    como CONTROLE, e ela sai preta do mesmo jeito, embora tenha sido verificada transparente
    na tela. A captura compõe sobre um fundo opaco; a imagem serve para conferir estrutura,
    não transparência.

26. **Tema fixo nas janelas de moldura escura.** A janela de áreas e o controle remoto têm
    chrome escuro próprio, mas seguiam o tema do sistema: num sistema em tema claro os
    botões sairiam claros com texto claro. É a mesma armadilha da decisão 15, e desta vez
    apareceu antes de chegar à tela — a janela de áreas saiu ilegível na sonda. Os dois
    passaram a fixar o tema escuro.

27. **RF-552 contra RF-558.** Um pede "nenhum temporizador ativo" em ociosidade; o outro
    pede um indicador de memória atualizado periodicamente. Literalmente, os dois não cabem.
    A resolução: os temporizadores só correm quando há o que mostrar — o de estado enquanto
    a janela principal está visível, o dos contadores enquanto a aba de depuração está à
    vista. Quem escondeu a janela é justamente quem está jogando, e é dele que RF-552 fala.
    Deixar um temporizador correndo e não fazer nada no tique é um temporizador ativo do
    mesmo jeito.

28. **Trava de instância por arquivo, não por mutex nomeado.** Mutex nomeado é conceito do
    Windows, e a abstração de RF-577 não teria o que oferecer nos outros sistemas. O arquivo
    com trava exclusiva funciona nos três e tem uma propriedade que o mutex não tem: guarda
    o identificador do processo dono, então uma trava órfã é reconhecível e removível em vez
    de bloquear o programa para sempre. Um teste achou o caso que faltava — `pasta
    inexistente` lança `DirectoryNotFoundException`, que deriva de `IOException`, e o
    programa se recusava a abrir alegando que já estava aberto.

29. **Três violações reais de RF-567, achadas pela varredura.** O idioma-ponte de RF-239
    estava fixo no código; a configuração rápida comparava com `"ja"` num ramo cujos dois
    lados devolviam o mesmo valor; e o motor de OCR do sistema tinha uma lista de idiomas
    conhecidos dentro dele, que faria um idioma novo em `languages.toml` ser ignorado sem
    explicação. Os três viraram dado: `bridge_language` em `engines.toml`, o padrão do
    catálogo, e a interseção de RF-151 recebendo os idiomas de fora.

30. **Um defeito silencioso no formato do catálogo.** `default_translation_service` e
    `bridge_language` estavam DEPOIS dos `[[ocr_engine]]` no arquivo — e em TOML uma chave
    solta pertence à última tabela aberta, não à raiz. A leitura falhava e caía no padrão
    do código, que por coincidência era o mesmo valor, então nada parecia errado. As chaves
    foram para o topo do arquivo e um teste confere que um valor DIFERENTE do padrão é
    lido.

31. **O pacote leva o runtime dentro.** São 149 MB, e a alternativa não funciona: um
    pacote dependente do runtime não abre por duplo clique quando o .NET está instalado
    fora do caminho padrão — pelo Homebrew, por exemplo —, porque o Finder não passa `PATH`
    nem `DOTNET_ROOT`. Descoberto tentando: a primeira versão do pacote abriu e morreu com
    "You must install .NET to run this application". A opção `--dependente` continua lá para
    quem tiver o runtime no lugar padrão.

32. **Quem nomeia o menu no macOS é `Application.Name`, não o `CFBundleName`.** O
    `Info.plist` estava certo e o menu continuava dizendo "Avalonia Application"; o nome sai
    da propriedade do Avalonia, que nunca tinha sido definida. Só a captura da barra de
    menus mostrou isso — o `Info.plist` parecia resolver.

33. **O reconhecimento em lote não ajuda — medido, não suposto.** A hipótese era que o
    custo fixo de cada chamada ao modelo dominasse. Na mesma imagem, pelos dois caminhos, o
    lote saiu 4,9% MAIS LENTO com resultado idêntico. A largura do tensor num lote é a da
    linha mais larga, e o cálculo desperdiçado com o preenchimento das outras consome o que
    se economizaria. O código fica, testado e não usado, porque a conclusão é desta máquina
    e deste modelo — com um provedor de execução por GPU a conta pode inverter, e então é
    uma linha no motor.

34. **A primeira comparação não queria dizer nada.** Rodei a sonda duas vezes, uma com cada
    caminho, e comparei 592 ms com 389 ms — só que a sonda captura a tela VIVA, e entre as
    duas execuções ela mudou: 25 linhas contra 9. A medição só passou a significar alguma
    coisa quando os dois caminhos passaram a rodar sobre a MESMA imagem, dentro da mesma
    execução.

35. **Um lote muda o que o CTC decodifica.** A primeira versão do lote agrupava as linhas
    só por tamanho de grupo, e um teste de equivalência mostrou uma linha que sozinha
    resultava em texto vazio ganhando um caractere inventado ao ser esticada ao triplo com
    zeros. O limite de 1,5× na variação de largura dentro de um grupo é correção, não
    economia — sem ele o lote não é a mesma operação mais rápida, é outra operação.

36. **"Idas à rede" era rótulo errado.** A sonda do ciclo dizia "3 idas à rede" para três
    blocos, e o contador da aba de depuração se chamava `NetworkCalls`. Os dois estavam
    contando TEXTOS, não requisições — a requisição é uma só, como RF-231 manda. O número
    fazia parecer que havia uma violação onde não havia. Renomeado para `NetworkTexts`, e o
    rótulo da sonda agora cita o requisito.

37. **`await` trava numa sonda do Avalonia.** A primeira versão do modo `--real` ficou
    pendurada sem imprimir nada: configurar o Avalonia instala um contexto de sincronização,
    e aguardar nele a partir do fluxo principal trava. O ciclo passou a ser aguardado por
    `Task.Run(...).GetAwaiter().GetResult()`, que é a mesma razão pela qual RF-009 manda o
    laço de tradução sondar em vez de usar `await`.

## Como rodar os testes

```
dotnet test
```

Para acrescentar um caso de agrupamento, basta criar um arquivo em
`tests/cases/grouping/` — nenhum código muda.
