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
├── tools/
│   ├── Gort.CaptureProbe/      teste visual das Etapas 2, 3 e 4
│   ├── Gort.OcrProbe/          teste do motor de OCR (Etapa 5)
│   ├── Gort.CycleProbe/        ciclo completo de ponta a ponta (Etapa 7)
│   └── Gort.LayerProbe/        desenho da camada e da sobreposição, fora da tela
└── tests/
    ├── Gort.Core.Tests/        551 testes
    ├── Gort.Platform.Tests/     33 testes
    ├── Gort.Ocr.Tests/          36 testes
    ├── Gort.Engine.Tests/       24 testes
    └── cases/grouping/         casos de agrupamento gravados em arquivo (Etapa 6)
```

## Onde parei — 5 de setembro de 2026

Último commit: **Etapa 17b, primeira leva**. 644 testes passando (551 + 33 + 36 + 24).

**Próximo passo — o que falta da Etapa 17b**, na ordem de utilidade:

1. **Janela de opções avançadas (V.3)** — as abas que RF-523 a RF-532 descrevem. A LÓGICA
   toda já está pronta e testada (dependências, presets, rótulos, restauração); falta a
   janela que a apresenta.
2. **Gerenciamento de áreas (RF-533)** e **grupos de cor por área (RF-534)**.
3. **Conta-gotas (RF-535)** e **pré-visualização binarizada (RF-536)** — `Preprocessor.Preview`
   já existe como função.
4. **Editor de dicionário (RF-537)** — RF-181 já carrega e aplica o dicionário.
5. **Gerenciamento de chaves (RF-538)**, **sobre (RF-543)** e **doação (RF-544)**.

Dependem de coisas que não existem aqui: RF-539 (OCR de nuvem), RF-540 (instalador do motor
por ambiente interpretado), RF-541 (servidor da comunidade) e RF-542 (navegador embutido).

Depois disso, a **Etapa 19** — endurecimento, RF-552 a RF-567, e toda a PARTE VIII. A
**Etapa 15** (nove serviços de tradução) e a atualização automática de RF-416 a RF-435
dependem de credenciais e de um servidor de distribuição.

## Etapas concluídas

| Etapa | Requisitos | Situação |
|---|---|---|
| **1 — Esqueleto e configuração** | RF-020 a RF-046 | **Persistência completa.** Falta o ciclo de vida da aplicação (RF-001 a RF-019), que depende da interface. |
| **2 — Abstração de plataforma e captura** | RF-088, RF-100, RF-568 a RF-578 | **Completa.** C1 e C18 implementados nos três sistemas; captura verificada de ponta a ponta no macOS. |
| **3 — Regiões de captura** | RF-047 a RF-087 | **Modelo e geometria completos** e verificados: conversão moldura→retângulo com escala por monitor, alinhamento de largura, composição, áreas especiais e a regra de índice reversa. Falta o desenho das molduras e da camada de seleção (RF-047 a RF-056, RF-063, RF-080 a RF-084), que depende da interface. |
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
| **17b — Opções avançadas: a lógica** | RF-302 a RF-307, RF-523 a RF-532, RF-545, RF-546 | **A lógica está completa e testada**, e o menu de contexto do modo camada, no ar. Falta a janela de V.3 e as auxiliares de V.4. |

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
| 19 — Endurecimento | RF-552 a RF-567 e toda a PARTE VIII | |

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
| A mesma caixa, primeira vez (com rede) | 664 ms | dominado pela rede — RF-548 |
| Tela inteira de IDE, 51 linhas, 29 blocos, em cache | 1054 ms | estoura |

O alvo do produto é a caixa de diálogo, e é para ela que a área de OCR existe. A tela
inteira estoura porque o reconhecimento é por linha; se for preciso mais margem, o caminho
é processar as linhas em lote — o modelo de referência agrupa de 6 em 6 —, o que ainda não
foi feito.

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

## Como rodar os testes

```
dotnet test
```

Para acrescentar um caso de agrupamento, basta criar um arquivo em
`tests/cases/grouping/` — nenhum código muda.
