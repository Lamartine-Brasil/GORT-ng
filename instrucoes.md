# instrucoes.md — Especificação do tradutor de tela em tempo real

> Este documento é a especificação completa do programa a ser construído.
> É prescritivo: descreve o que o programa **deve** fazer.
> O programa é construído **do zero**, sem compatibilidade com nenhum produto
> anterior. Os valores numéricos marcados com 🔒 vêm de calibragem empírica
> acumulada ao longo de mais de dez anos de uso real e devem ser preservados
> exatamente — eles não podem ser redescobertos por raciocínio.
>
> Convenções:
> - `RF-xxx` — requisito funcional, identificador único e permanente.
> - `P-xx` — parâmetro ajustável, catalogado na Parte IV.
> - `[INFERIDO]` — dedução do autor da especificação, não constatação direta.
> - 🔒 — valor calibrado empiricamente e **confirmado**; reproduzir exatamente.
>   A política que rege esses valores está na Parte XII — leia antes de alterar
>   qualquer número marcado assim.

---

# PARTE I — O PRODUTO

## 1. Visão geral

O programa é um **tradutor de tela em tempo real**. Ele captura continuamente
uma ou mais regiões retangulares da tela do computador, reconhece o texto
contido nessas regiões por OCR, traduz esse texto para o idioma do usuário e
exibe a tradução sobre a tela — sem que o usuário precise sair da aplicação que
está usando.

**Para quem serve.** Pessoas que consomem software cujo texto não está no seu
idioma e que não pode ser modificado: principalmente jogos (visual novels, RPGs,
jogos de estratégia, jogos indie sem localização), mas também aplicativos
legados, leitores de documentos e qualquer janela que exiba texto como pixels.

**Que problema resolve.** Quando um programa não expõe seu texto de forma
acessível (sem arquivos de tradução, sem API, sem seleção de texto), a única
fonte disponível é a imagem da tela. O programa transforma essa imagem em texto,
traduz e devolve a tradução no lugar certo, com latência baixa o bastante para
acompanhar diálogos que passam rápido.

**O que o distingue.** Três coisas:

1. **Velocidade.** O ciclo completo — capturar, reconhecer, traduzir, desenhar —
   roda em intervalos configuráveis a partir de 300 ms. Isso só é possível
   porque o programa evita trabalho: descarta quadros idênticos ao anterior,
   reaproveita traduções já feitas, e limita a captura às regiões que o usuário
   marcou.
2. **Sobreposição posicionada.** No modo mais avançado, a tradução não vai para
   uma janela separada: ela é desenhada exatamente sobre o texto original, no
   mesmo lugar, com tamanho de fonte e cores derivados do próprio texto
   original. O resultado se parece com uma versão traduzida do software.
3. **Amplitude de motores.** O usuário escolhe entre vários motores de OCR e
   vários serviços de tradução (locais e remotos, gratuitos e pagos), e pode
   plugar um serviço de tradução próprio via HTTP.

O programa nunca modifica o software que está sendo traduzido. Ele apenas lê
pixels da tela e desenha por cima.

**Escopo desta versão.** Traduz de **japonês** e de **inglês** para **português
do Brasil**, com a interface em português do Brasil. Esse recorte é uma decisão
de produto para a primeira entrega, não um limite de arquitetura: idiomas,
motores de OCR e serviços de tradução são dados de configuração, e acrescentar
qualquer um deles depois não pode exigir mexer no núcleo (RF-566, RF-310).

**Este programa é construído do zero.** Ele não é uma continuação de nenhum
produto anterior, não lê arquivos de nenhum produto anterior e não replica
formatos de nenhum produto anterior. Os valores numéricos marcados com 🔒 nesta
especificação vêm de calibragem empírica acumulada e devem ser preservados —
mas o código, os formatos de arquivo e a estrutura são inteiramente novos.

---

## 2. Jornada do usuário

**Primeira abertura.** O usuário inicia o programa. Aparece uma tela de abertura
por poucos segundos enquanto o programa verifica se há versão nova, carrega o
arquivo de configuração do usuário, inicializa os motores de OCR disponíveis e
monta as janelas. Ao final, três coisas estão na tela: a **janela principal de
configuração**, um **controle remoto** pequeno e flutuante com quatro ou cinco
botões, e um **ícone na bandeja do sistema**.

Se é a primeira vez, a janela principal abre na aba de **configuração rápida**,
que é um assistente de três passos: (1) qual é a cor do texto que você quer
traduzir — claro, escuro, ou não sei; (2) selecione na tela a área onde o texto
aparece; (3) pronto. O assistente escolhe sozinho o motor de OCR disponível mais
capaz, escolhe o tradutor, ativa o filtro de cor apropriado, e aplica tudo.

**Uso normal — marcando a região.** O usuário abre seu jogo em modo janela ou
janela sem borda (tela cheia exclusiva não funciona — ver Parte VIII). Volta ao
controle remoto e clica no botão de seleção de área. A tela inteira escurece e
o cursor vira uma cruz. O usuário arrasta um retângulo sobre a caixa de diálogo
do jogo. Ao soltar, aparece uma **moldura de área de OCR**: um retângulo com
borda colorida, barra de título mostrando índice, tamanho e posição, e três
botõezinhos (fechar, conta-gotas de cor, seleção de grupos de cor). Essa moldura
pode ser arrastada e redimensionada a qualquer momento.

O usuário pode adicionar mais de uma área — por exemplo, uma para o nome do
personagem e outra para a fala. Pode também adicionar **áreas de exclusão**:
retângulos vermelhos que recortam pedaços indesejados de dentro de uma área de
OCR (um retrato, um ícone, um contador). Quando termina, clica em aplicar na
janelinha de gerenciamento de áreas.

**Uso normal — traduzindo.** O usuário clica no botão de tradução do controle
remoto, ou pressiona o atalho global (por padrão `Ctrl+Shift+Z`). A moldura das
áreas some, a janela de tradução entra em modo transparente e deixa de receber
cliques, e o laço começa: a cada intervalo escolhido o programa captura as
áreas, reconhece, traduz e mostra.

**O que ele vê.** Depende do modo de janela de tradução escolhido:
- **Escuro:** uma janela retangular com fundo escuro e o texto traduzido em uma
  caixa de texto rolável. Simples e legível, boa para textos longos.
- **Camada:** uma janela transparente e sem bordas que o usuário posiciona onde
  quiser. Enquanto está traduzindo, ela fica invisível exceto pelo texto, com
  contorno duplo para legibilidade, e deixa os cliques passarem através dela.
- **Sobreposição:** não há janela visível. A tradução de cada bloco de texto é
  desenhada diretamente sobre o bloco original, com o tamanho de fonte
  proporcional ao original e — se ativado — a cor do texto e do fundo extraídas
  da própria imagem. Este é o modo que exige informação de posição do OCR.

**Ajustando.** Quase sempre o primeiro resultado não é perfeito. O usuário volta
à janela principal e mexe em: velocidade do laço; filtro de cor (extrair só os
pixels de determinada saturação/brilho, que limpa fundos ruidosos); ampliação da
imagem antes do OCR (2× por padrão, ajuda muito com fontes pequenas); fonte,
tamanho, cor e contorno do texto traduzido; dicionário de correção (para
consertar erros recorrentes do OCR); banco de dados de traduções fixas (para
termos próprios). Cada mudança exige clicar em **aplicar**, que pausa a tradução,
reconfigura tudo e retoma.

**Modos pontuais.** Além do laço contínuo há:
- **Traduzir uma vez** (`Ctrl+Shift+C`): executa um único ciclo nas áreas atuais.
- **Instantâneo** (`Ctrl+Shift+A`): permite desenhar um retângulo novo na hora e
  traduz só aquilo, uma vez; o resultado da sobreposição permanece visível por
  alguns segundos e depois some.
- **Área rápida** (`Ctrl+Shift+X`): adiciona uma área temporária que não é
  salva na configuração.
- **Área que segue o mouse** (`Ctrl+Shift+F`): uma área de OCR que acompanha o
  cursor, útil para passar por cima de itens e legendas curtas.

**Encerrando.** O usuário pressiona o atalho de tradução de novo para parar, ou
clica em parar no controle remoto. Ao fechar a janela principal, o programa
pergunta se quer mesmo sair; se o modo bandeja estiver ativo, ele apenas some
para a bandeja e continua rodando. A configuração é salva automaticamente
sempre que o usuário clica em aplicar.

---

## 3. Glossário

| Termo | Definição |
|---|---|
| **Área de OCR** | Retângulo em coordenadas de tela que delimita o que será capturado e reconhecido. Numeradas a partir de 1. |
| **Área de exclusão** | Retângulo que remove sua região da imagem capturada antes do OCR, mesmo estando dentro de uma área de OCR. |
| **Área rápida** | Área de OCR temporária, criada por atalho, não persistida na configuração. |
| **Área instantânea (snapshot)** | Área desenhada na hora que substitui todas as outras para um único ciclo de tradução. |
| **Área que segue o mouse** | Área de OCR cujo centro é reposicionado continuamente sobre o cursor. |
| **Ciclo** | Uma passagem completa: capturar → pré-processar → OCR → pós-processar → traduzir → desenhar. |
| **Laço de tradução** | Repetição contínua de ciclos, separada pelo intervalo de velocidade. |
| **Modo pontual** | Execução de um único ciclo (traduzir uma vez, instantâneo). |
| **Palavra** | Unidade mínima devolvida pelo OCR: texto + caixa delimitadora em coordenadas da imagem. |
| **Linha** | Sequência de palavras que o OCR agrupou como uma linha. Tem caixa delimitadora, orientação e texto concatenado. |
| **Bloco de tradução** | Conjunto de uma ou mais linhas agrupadas pelo pós-processamento, que é traduzido e desenhado como uma unidade. |
| **Orientação** | Horizontal ou vertical. Uma linha é vertical quando sua altura supera 1,5× sua largura. |
| **Título** | Bloco curto que o pós-processamento decide não fundir com o bloco seguinte (nome de personagem, cabeçalho). |
| **Grupo de cor** | Conjunto de faixas RGB ou HSV usado para filtrar quais pixels contam como texto. O usuário pode ter vários e escolher quais valem para cada área. |
| **Ampliação** | Fator pelo qual a imagem capturada é redimensionada antes do OCR. |
| **Token separador** | Marcador textual inserido entre blocos ao montar uma única requisição de tradução, para depois separar as respostas. |
| **Dicionário de correção** | Lista de substituições texto→texto aplicada ao resultado do OCR antes da tradução. |
| **Banco de tradução (DB)** | Arquivo de pares texto-original / texto-traduzido usado como tradutor exato, sem rede. |
| **Coletânea de tradução** | Conjunto de arquivos de pares que o usuário ativa; consultado antes de chamar o tradutor remoto. |
| **Memória de resultados anteriores** | Cache em memória e em disco de traduções já obtidas, por serviço de tradução. |
| **Memória de exibição** | Recurso que mantém as N últimas traduções visíveis simultaneamente por alguns segundos. |
| **Janela de tradução** | Superfície onde a tradução aparece. Existe em três modos: escuro, camada e sobreposição. |
| **Modo transparente a cliques** | Estado em que a janela é visível mas os cliques do mouse passam através dela para a janela de baixo. |
| **Captura anexada** | Modo em que a fonte de imagem não é a tela inteira, mas uma janela específica escolhida pelo usuário, capturada mesmo se estiver parcialmente coberta. |
| **Controle remoto** | Janela pequena, sempre acessível, com os botões de ação mais usados. |
| **Tradução ponte** | Traduzir do idioma de origem para japonês e do japonês para o destino, para melhorar qualidade em certos pares. |
| **Modo de baixa qualidade** | Estado degradado do tradutor gratuito da web após esgotar cota, usando um endpoint alternativo de menor qualidade. |

---

## 4. Princípios de projeto

Estas são as qualidades inegociáveis do produto. Quando houver conflito entre
elas e qualquer outra consideração, elas vencem.

**P1 — Latência acima de tudo.** O usuário está lendo diálogo que passa. Um
ciclo que demora 3 segundos é inútil mesmo que perfeito. Todo o desenho do
sistema — descarte de quadros idênticos, cache de traduções, cache de medições
de texto, busca binária em vez de varredura linear — existe para reduzir o tempo
do ciclo. Nenhuma funcionalidade nova pode aumentar o tempo do ciclo comum.

**P2 — Nunca roubar o foco.** O programa jamais ativa suas próprias janelas
durante a tradução, jamais traz uma janela para frente, jamais abre diálogo
modal a partir do laço de tradução. Se ele roubar o foco, o jogo pausa ou
minimiza. Mensagens de erro geradas durante a tradução são enfileiradas para a
interface e a thread de trabalho continua ou termina limpa.

**P3 — Não atrapalhar visualmente.** Enquanto traduz, as janelas do programa
que ficam sobre o jogo são transparentes a cliques e não desenham decoração.
Quando o usuário não está traduzindo, elas voltam a ser visíveis e clicáveis
para poderem ser movidas.

**P4 — A janela de sobreposição não aparece em capturas de tela.** Ela é marcada
para ser excluída de gravação e captura de tela do sistema, para não poluir
prints e transmissões do usuário. Exceção deliberada: quando o usuário aciona o
atalho de captura do sistema operacional, ela se torna capturável por alguns
segundos, porque nesse caso ele quer capturar a tradução.

**P5 — Funcionar sem internet quando possível.** Motores de OCR locais e
tradução por banco de dados local devem funcionar offline. A ausência de rede
degrada o produto, não o quebra.

**P6 — Construção do zero, preparada para crescer.** Este programa **não** tem
nenhuma compatibilidade com produtos anteriores: não lê seus arquivos, não imita
seus formatos e não preserva suas convenções. Em troca, é obrigado a nascer
escalável: os dados do usuário são versionados desde a primeira linha, os
conjuntos de valores (idiomas, motores de OCR, serviços de tradução) são dados e
não código, e acrescentar um item novo a qualquer um desses conjuntos não pode
exigir alterar o núcleo nem invalidar os arquivos já gravados pelo usuário.

**P7 — O usuário controla tudo, e erra em segurança.** Todo parâmetro relevante
é exposto. Valores fora de faixa são saturados, não rejeitados. Um arquivo de
configuração corrompido cai para os padrões em vez de impedir a abertura.

**P8 — Degradação silenciosa.** Uma área que não produz imagem é pulada. Um
motor de OCR indisponível é sinalizado uma vez e desabilitado. Uma tradução que
falha devolve a mensagem de erro no lugar do texto, e o laço continua.

---

# PARTE II — ARQUITETURA FUNCIONAL

## 5. Blocos funcionais

```
                        ┌──────────────────────────────┐
                        │  Configuração e persistência │
                        │  (perfis, opções avançadas,  │
                        │   credenciais, atalhos)      │
                        └───────────┬──────────────────┘
                                    │ lê/escreve
                                    ▼
┌───────────────┐        ┌────────────────────────────┐
│  Atalhos      │───────▶│   Ciclo de vida / Controle │
│  globais      │        │   (iniciar, parar, pausar, │
└───────────────┘        │    aplicar, modo pontual)  │
┌───────────────┐        └──────────┬─────────────────┘
│  Controle     │───────▶           │ comanda
│  remoto / UI  │                   ▼
└───────────────┘        ┌────────────────────────────┐
                         │      LAÇO DE TRADUÇÃO      │
                         └──────────┬─────────────────┘
                                    │
        ┌───────────────────────────┼───────────────────────────┐
        ▼                           ▼                           ▼
┌───────────────┐          ┌────────────────┐          ┌────────────────┐
│ Gerenciamento │─ regiões▶│ Captura de tela│          │ Detecção de    │
│ de regiões    │          │ (tela inteira  │          │ mudança 🔒     │
│ (áreas, excl.,│          │  ou janela     │          │ (compara texto │
│  grupos cor)  │          │  anexada)      │          │  com o anterior)│
└───────────────┘          └───────┬────────┘          └────────┬───────┘
                                   │ imagem bruta               │ decide
                                   ▼                            │ redesenhar
                         ┌────────────────────┐                 │
                         │ Pré-processamento  │                 │
                         │ (recorte, exclusão,│                 │
                         │  filtro de cor,    │                 │
                         │  limiar, erosão,   │                 │
                         │  ampliação)        │                 │
                         └───────┬────────────┘                 │
                                 │ imagem tratada +             │
                                 │ imagem original              │
                                 ▼                              │
                         ┌────────────────────┐                 │
                         │  OCR (motor        │                 │
                         │  selecionável)     │                 │
                         └───────┬────────────┘                 │
                                 │ palavras + caixas            │
                                 ▼                              │
                    ┌─────────────────────────────┐             │
                    │ Estruturação e              │             │
                    │ pós-processamento 🔒        │             │
                    │ (linhas → blocos, títulos,  │             │
                    │  listas, orientação,        │             │
                    │  dicionário de correção,    │             │
                    │  remoção de espaços)        │             │
                    └───────┬─────────────────────┘             │
                            │ blocos de texto                   │
                            ▼                                   │
              ┌──────────────────────────┐                      │
              │ Cache / coletânea / DB   │                      │
              └───────┬──────────────────┘                      │
                      │ o que não estava em cache                │
                      ▼                                          │
              ┌──────────────────────────┐                       │
              │ Tradução (serviço        │                       │
              │ selecionável, local ou   │                       │
              │ remoto)                  │                       │
              └───────┬──────────────────┘                       │
                      │ traduções por bloco                      │
                      ▼                                          ▼
       ┌──────────────────────────────────────────────────────────────┐
       │  Análise automática de cor 🔒  (só no modo sobreposição)      │
       │  usa a imagem ORIGINAL + caixas das palavras                 │
       └───────────────────────────┬──────────────────────────────────┘
                                   ▼
       ┌──────────────────────────────────────────────────────────────┐
       │  Overlay e renderização                                      │
       │  (escuro | camada | sobreposição; layout, colisão de blocos, │
       │   tamanho automático de fonte, quebra de linha, contorno)    │
       └───────────────────────────┬──────────────────────────────────┘
                                   ▼
                    ┌────────────────────────────┐
                    │ Saídas auxiliares:         │
                    │ área de transferência, TTS,│
                    │ arquivo de log de resultado│
                    └────────────────────────────┘
```

Blocos transversais, ligados a quase todos os outros:

- **Localização da interface** — traduz os rótulos do próprio programa.
- **Atualização automática** — verifica versão e dicionários ao iniciar.
- **Depuração** — grava um retrato JSON completo de um ciclo.

---

## 6. Contrato de cada bloco

### 6.1 Gerenciamento de regiões

- **Recebe:** ações do usuário (desenhar, mover, redimensionar, remover área);
  a configuração carregada; o estado atual de modos especiais (instantâneo,
  segue-o-mouse).
- **Devolve:** uma lista ordenada de retângulos em coordenadas absolutas de
  tela — as áreas a capturar —, uma lista de retângulos de exclusão, e, para
  cada área, a lista de grupos de cor ativos.
- **Não faz:** não captura imagem, não conhece OCR, não desenha tradução.
- **Invariante:** a largura de cada retângulo entregue à captura é sempre
  múltipla de 4 (arredondada para cima), por exigência de alinhamento de linha
  de imagem.

### 6.2 Captura de tela

- **Recebe:** uma lista de retângulos; um sinalizador de fonte (tela inteira,
  janela ativa, ou janela anexada); um sinalizador indicando se a imagem
  original (sem tratamento) também é necessária.
- **Devolve:** para cada retângulo, uma imagem em memória com largura, altura,
  número de canais (1, 3 ou 4) e os bytes dos pixels; opcionalmente uma segunda
  imagem, a original sem tratamento, com as mesmas dimensões lógicas; e a
  posição de origem do cliente capturado (relevante no modo janela anexada).
- **Não faz:** não decide quando capturar, não interpreta o conteúdo.
- **Casos vazios:** se um retângulo não produz imagem, aquele índice é
  simplesmente ausente da lista devolvida — não é um erro.

### 6.3 Pré-processamento de imagem

- **Recebe:** a imagem capturada de uma região, os retângulos de exclusão que a
  intersectam, a configuração de filtro (RGB exato, faixas HSV, ou limiar), o
  fator de ampliação, e o sinalizador de erosão.
- **Devolve:** uma imagem tratada, pronta para OCR (em geral binarizada e
  ampliada), e, quando pedido, a imagem original não tratada.
- **Não faz:** não reconhece texto.
- **Contrato de coordenadas:** as caixas devolvidas pelo OCR estão no espaço da
  imagem **tratada** (portanto ampliadas). Para voltar ao espaço da tela é
  preciso dividir pelo fator de ampliação e somar a origem da área.

### 6.4 Reconhecimento de texto (OCR)

- **Recebe:** uma imagem em memória com largura, altura e formato de pixel
  conhecidos; um código de idioma.
- **Devolve:** um resultado estruturado contendo: número de linhas; um vetor de
  todas as palavras em ordem de leitura; para cada palavra, coordenada x, y,
  largura e altura em pixels da imagem recebida; e um vetor com a quantidade de
  palavras de cada linha. Adicionalmente um indicador de resultado vazio.
- **Não faz:** não agrupa linhas em parágrafos, não corrige texto, não traduz.
- **Degradação:** motores que só devolvem texto, sem coordenadas, produzem um
  resultado com uma "palavra" por linha e caixas degeneradas; a sobreposição
  fica pior mas o modo escuro e o modo camada funcionam normalmente.

### 6.5 Estruturação e pós-processamento

- **Recebe:** o resultado estruturado do OCR de uma região; o sinalizador de
  fusão de linhas; o sinalizador de remoção de espaços; a configuração do
  dicionário de correção.
- **Devolve:** uma lista de **blocos de tradução**, cada um com: o texto
  concatenado, a lista de linhas que o compõem, a caixa delimitadora da união
  dessas linhas, a orientação, e um sinalizador de "é título".
- **Não faz:** não chama tradutor, não desenha.

### 6.6 Cache e fontes locais de tradução

- **Recebe:** um texto de origem e a identidade do serviço de tradução ativo.
- **Devolve:** a tradução já conhecida, ou nada.
- **Ordem de consulta:** coletânea do usuário → memória de resultados
  anteriores. Se nenhuma responder, o texto vai para o serviço.
- **Não faz:** não traduz.

### 6.7 Tradução

- **Recebe:** uma lista de textos de origem (um por bloco), o serviço escolhido
  e seus parâmetros (par de idiomas, credenciais, opções).
- **Devolve:** uma lista de textos traduzidos, na mesma ordem e mesmo tamanho,
  ou uma mensagem de erro única no lugar de tudo.
- **Não faz:** não sabe de onde veio o texto nem para onde vai a resposta.
- **Protocolo de lote:** quando há mais de um bloco, os textos são unidos em uma
  única requisição separados por um token; a resposta é dividida pelo mesmo
  token. Se a divisão produzir menos partes que blocos, os blocos restantes
  ficam sem tradução (não é erro).

### 6.8 Análise automática de cor

- **Recebe:** a imagem **original** (não tratada) da região, com largura, altura
  e número de canais; o retângulo do bloco nessa imagem; e a lista de retângulos
  das palavras do bloco.
- **Devolve:** uma cor de fonte e uma cor de fundo, mais indicadores de
  qualidade (quantas palavras sustentaram a escolha, qual o contraste obtido, se
  houve correção forçada), ou uma indicação de falha.
- **Não faz:** não desenha; não altera a imagem.

### 6.9 Overlay e renderização

- **Recebe:** a lista de blocos com texto traduzido, caixas, orientação, título
  e cores automáticas; a configuração de fonte, cor, contorno e alinhamento; o
  modo de janela ativo.
- **Devolve:** nada (efeito é o desenho na tela). Atualiza, para cada bloco, os
  retângulos finais de visualização e de conteúdo — que são o registro do que
  foi efetivamente desenhado onde.
- **Não faz:** não captura, não traduz, não decide quando redesenhar (recebe a
  ordem).

### 6.10 Ciclo de vida / controle

- **Recebe:** comandos de iniciar, parar, pausar-e-retomar, aplicar
  configuração, executar ciclo único.
- **Devolve:** o estado atual (ocioso, processando) e transições confiáveis.
- **Garantia crítica:** enquanto o laço estiver vivo, nenhum outro componente
  pode alterar configuração, áreas de captura ou motor de OCR. Toda mudança
  passa por "pausar → aplicar → retomar", e se a pausa não completar dentro do
  prazo, a mudança é **cancelada**, não aplicada em paralelo.

---

## 7. Modelo de dados

### 7.1 Imagem de região

| Campo | Significado |
|---|---|
| índice | Qual área de OCR originou esta imagem (base 0). |
| largura, altura | Dimensões em pixels da imagem tratada. |
| canais | 1 (cinza), 3 (BGR) ou 4 (BGRA). |
| bytes | Dados dos pixels, linha a linha, sem preenchimento. |
| largura/altura/canais originais | Dimensões da imagem original não tratada. |
| bytes originais | Pixels da imagem original; ausentes quando não solicitada. |

Regra: a imagem original só é solicitada quando o modo é sobreposição **e** a
cor automática está ativa. Ela é liberada assim que a análise de cor termina.

### 7.2 Palavra

| Campo | Significado |
|---|---|
| texto | Cadeia reconhecida. |
| x, y | Canto superior esquerdo na imagem tratada. |
| largura, altura | Dimensões da caixa. |

A caixa é convertida para retângulo inteiro **expandindo para fora**: piso no
canto superior esquerdo, teto no canto inferior direito. Isso evita perder
pixels de borda do glifo.

### 7.3 Linha

| Campo | Significado |
|---|---|
| lista de palavras | Textos, em ordem de leitura. |
| lista de caixas de palavras | Uma por palavra. |
| texto da linha | Concatenação das palavras separadas por um espaço, com espaço final. |
| caixa da linha | União das caixas das palavras. |
| orientação | Horizontal ou vertical. |

### 7.4 Bloco de tradução

| Campo | Significado |
|---|---|
| lista de linhas | Uma ou mais linhas fundidas. |
| texto traduzido | Preenchido depois da tradução. |
| é título | Verdadeiro se o bloco foi classificado como título e não deve absorver a linha seguinte. |
| orientação | Herdada da primeira linha. |
| caixa de origem | União das caixas das linhas, em coordenadas da imagem. |
| caixa de visualização | Retângulo na tela, depois de resolver colisões e expansões. |
| caixa de conteúdo | Caixa de visualização menos a margem de contorno; é onde o texto realmente cabe. |

### 7.5 Resultado de região

| Campo | Significado |
|---|---|
| índice | Área de OCR de origem. |
| é instantâneo | Verdadeiro quando veio de uma área instantânea. |
| lista de linhas | Todas as linhas reconhecidas. |
| lista de blocos | Resultado do agrupamento. |
| caixa de resultado | União de todas as linhas. |
| texto traduzido bruto | A resposta inteira do tradutor, antes de dividir por token. |
| usa cor automática | Verdadeiro se a análise de cor rodou. |
| lista de cores automáticas | Um par (fonte, fundo) por bloco, na mesma ordem. |

### 7.6 Grupo de cor

| Campo | Significado |
|---|---|
| R, G, B | Cor exata a extrair no modo RGB. Faixa 0–255 cada. |
| S inicial, S final | Faixa de saturação no modo HSV. Faixa 0–100. |
| V inicial, V final | Faixa de brilho no modo HSV. Faixa 0–100. |

Invariante: se o valor inicial for maior que o final, os dois são trocados ao
carregar e ao aplicar.

### 7.7 Configuração de atalho

| Campo | Significado |
|---|---|
| tipo de ação | Qual comando o atalho dispara. |
| lista de teclas | Até três teclas; modificadores esquerdo e direito são normalizados para um único código. |
| índice | Usado quando há várias instâncias da mesma ação (ex.: quatro atalhos de "abrir perfil"). |
| dado extra | Parâmetro da ação (ex.: nome do arquivo de perfil a abrir). |

### 7.8 Entrada de memória de exibição

| Campo | Significado |
|---|---|
| texto | Tradução exibida. |
| instante de criação | Usado para expirar a entrada. |

---

## 8. Fluxo principal

Passo a passo do acionamento até a tradução na tela. Marcações: **[S]** =
síncrono na thread do laço; **[P]** = executa em paralelo/assíncrono e o laço
espera; **[UI]** = despachado para a thread de interface sem esperar.

1. **[UI]** O usuário aciona iniciar (atalho, botão do controle remoto, menu da
   bandeja). Se já estiver traduzindo, o mesmo acionamento para.
2. **[UI]** Verificações de pré-condição: existe ao menos uma área de OCR?
   O motor de OCR escolhido está disponível? O motor escolhido é compatível com
   o modo de janela escolhido? Certos serviços de tradução exigem um aviso de
   primeira vez, exibido uma única vez por sessão. Se alguma verificação falha,
   o fluxo termina aqui com uma mensagem.
3. **[UI]** A janela de tradução é preparada: limpa o conteúdo anterior, entra em
   modo transparente, e — no modo sobreposição — força uma sincronização com o
   compositor da área de trabalho para que o primeiro quadro não pisque.
4. **[S]** Uma thread dedicada é criada e o laço começa. Toda a lógica do ciclo
   roda **de forma síncrona** nessa thread; nenhum ponto de espera assíncrona
   pode encerrar a thread prematuramente, porque a interface usa o término dessa
   thread como sinal de "parou de verdade".
5. **[S]** Início do ciclo: se o tempo decorrido desde o ciclo anterior for
   menor que o intervalo configurado, dorme 100 ms e volta ao passo 5.
6. **[S]** Verifica se existe alguma janela de tradução viva. Se não, não faz
   nada neste ciclo.
7. **[S]** Para cada área de OCR, obtém a imagem. Se a fonte for a janela
   anexada, primeiro solicita um quadro novo e espera (em passos de 2 ms) até
   que um quadro esteja disponível.
8. **[S]** Cada imagem é recortada pelas áreas de exclusão, filtrada pelos
   grupos de cor ativos daquela área, opcionalmente erodida, e ampliada.
9. **[S]** Para cada imagem, chama-se o motor de OCR. Dependendo do motor a
   chamada é síncrona ou **[P]** assíncrona; no segundo caso o laço espera em
   passos de 50 ms, verificando a cada passo se veio pedido de parada.
10. **[S]** O resultado do OCR é convertido em linhas, e as linhas em blocos
    (Parte III, capítulo de pós-processamento).
11. **[S]** O texto de cada bloco recebe o tratamento textual: remoção de
    espaços (se ativa), dicionário de correção (se ativo), e — fora do modo
    sobreposição — junção de quebras de linha.
12. **[P]** Os textos são enviados ao tradutor. Antes da chamada de rede,
    consulta-se a coletânea do usuário e a memória de resultados anteriores;
    apenas o que não foi encontrado entra na requisição. O laço espera o
    resultado em passos de 50 ms, abortando se vier pedido de parada.
13. **[S]** A resposta é dividida pelo token separador e distribuída aos blocos.
14. **[S]** Se o modo é sobreposição e a cor automática está ativa, a análise de
    cor roda para cada bloco usando a imagem original.
15. **[S]** **Detecção de mudança:** compara-se o texto reconhecido deste ciclo
    com o do ciclo anterior.
    - **Diferente (ou vazio):** segue para o passo 16.
    - **Igual:** não redesenha o conteúdo. Apenas, se passou mais de 1 s desde
      o último redesenho ocioso, força um repintar (a geometria pode ter mudado
      mesmo com texto igual). Vai para o passo 20.
16. **[UI]** Se a cópia para a área de transferência estiver ativa e a área
    estiver livre, despacha a cópia.
17. **[S]** A memória de exibição é aplicada ao texto final, se ativa.
18. **[UI]** Despacha o desenho para a janela de tradução ativa.
19. **[S]** Efeitos colaterais: gravação em arquivo do par OCR/tradução (se
    ativa); leitura em voz alta (se ativa).
20. **[S]** Se o modo é pontual, marca fim de laço e **[UI]** despacha a parada.
    Caso contrário volta ao passo 5.
21. **[S]** Ao sair do laço, grava em disco os novos pares da memória de
    resultados anteriores.

**Onde há descarte:** passo 15 (texto idêntico), passo 7 (área sem imagem),
passo 12 (texto já em cache não vai para a rede).

**Onde há espera:** passos 7, 9 e 12 — todas com verificação periódica de pedido
de parada, para que parar nunca demore mais que o limite estabelecido.

---

# PARTE III — ESPECIFICAÇÃO FUNCIONAL POR MÓDULO

## 9. Ciclo de vida da aplicação

**Responsabilidade:** iniciar o programa em estado utilizável, coordenar
início/parada/pausa da tradução com segurança de thread, e encerrar limpo.

**Entradas / Saídas:** recebe comandos do usuário e o resultado do carregamento
de configuração; devolve o estado do laço (ocioso ou processando) e garante que
nenhuma mudança de configuração ocorra em paralelo com um ciclo em andamento.

**Requisitos:**

RF-001 — O programa deve permitir apenas uma instância em execução por vez. Se
já houver uma instância, a segunda deve informar isso ao usuário e encerrar.

RF-002 — O programa deve oferecer uma forma explícita de desativar a restrição
de instância única, para permitir múltiplas cópias simultâneas (por exemplo, a
presença de um arquivo marcador na pasta do programa).

RF-003 — Ao iniciar, o programa deve definir a pasta do executável como diretório
de trabalho corrente, para que todos os caminhos relativos de dados do usuário
resolvam corretamente independentemente de como o programa foi lançado.

RF-004 — Durante a inicialização o programa deve exibir uma tela de abertura com
o número da versão e a data de compilação, mantida por P-01 e depois removida
com um desvanecimento de P-02.

RF-005 — Enquanto a tela de abertura está visível, o programa deve: verificar
atualizações (capítulo 26), carregar as configurações padrão remotas (RF-417),
enumerar os idiomas de OCR disponíveis no sistema, e carregar o perfil de
configuração do usuário.

RF-006 — Se a inicialização lançar erro não tratado, o programa deve exibir a
descrição do erro, abrir a página de ajuda de erros conhecidos e encerrar.

RF-007 — O programa deve verificar, na inicialização, se a biblioteca gráfica de
desenho de texto vetorial funciona, desenhando uma cadeia de teste com
caracteres latinos e japoneses. Se falhar, deve desativar o desenho vetorial em
todo o programa (caindo para desenho simples de texto, sem contorno), avisar o
usuário e oferecer o link para a solução conhecida.

RF-008 — O programa deve manter três estados de laço: **ocioso** (nenhuma thread
de tradução viva), **processando** (thread viva) e **parando**. Nenhuma outra
combinação é observável de fora.

RF-009 — O laço de tradução deve rodar em uma thread dedicada e ser **síncrono
de ponta a ponta** dentro dessa thread. Nenhum ponto de espera pode devolver
controle antes do fim do ciclo, porque o término da thread é o sinal de parada
usado pela interface. **Motivo:** se a thread terminar no primeiro ponto de
espera, quem esperava por ela conclui que parou e passa a alterar configuração
enquanto o ciclo ainda está rodando.

RF-010 — Ao pedir parada, o programa deve sinalizar a flag de fim e esperar a
thread terminar por até P-03. Se a thread não terminar nesse prazo, a flag de
fim **não** deve ser revertida e a operação que motivou a parada **não** deve
ser executada.

RF-011 — Quando o pedido de parada vem de dentro do interceptador global de
teclado, o prazo de espera deve ser P-04 em vez de P-03. **Motivo:** o sistema
operacional remove um interceptador de teclado de baixo nível que fique preso
por mais de ~300 ms, o que mataria todos os atalhos do programa até reiniciar.

RF-012 — Aplicar configuração deve seguir o protocolo: cancelar tradução em
curso → parar a thread → executar a mudança → recriar a thread se ela estava
viva. Se a parada falhar por tempo, a mudança deve ser abortada e o chamador
deve ser informado de que nada foi aplicado.

RF-013 — Enquanto o laço estiver vivo, um novo pedido de iniciar deve primeiro
parar o laço anterior. Se não conseguir parar dentro de P-03, o novo laço **não**
deve ser iniciado.

RF-014 — Erros não tratados dentro do laço devem ser registrados e a mensagem
deve ser exibida pela thread de interface. O laço deve terminar limpo. É
proibido abrir diálogo modal a partir da thread do laço.

RF-015 — Ao fechar a janela principal, o programa deve pedir confirmação. Se o
modo bandeja estiver ativo, deve apenas ocultar a janela e continuar rodando.

RF-016 — Ao encerrar de fato, o programa deve: parar o laço, fechar o processo
auxiliar de tradução local (se houver), remover o ícone da bandeja e liberar o
interceptador global de teclado.

RF-017 — O programa deve oferecer um ícone de bandeja com menu contendo, no
mínimo: abrir configurações, mostrar janela de tradução, mostrar controle
remoto, alternar "janela de tradução sempre no topo", alternar dicionário de
correção, definir área de OCR, iniciar/parar tradução, salvar perfil, carregar
perfil, restaurar padrões, sobre, verificar atualização, sair.

RF-018 — O rótulo do item de iniciar/parar do menu de bandeja deve refletir o
estado atual do laço no momento em que o menu é aberto.

RF-019 — Duplo clique no ícone da bandeja deve restaurar e ativar a janela
principal, recriando a janela de tradução e o controle remoto se necessário.

**Comportamento detalhado (protocolo de pausa):**

```
pausar_e_retomar(acao, prazo):
    cancelar_tradução_em_curso()
    precisa_retomar := thread_do_laço != nulo E thread_está_viva
    se precisa_retomar:
        marcar_fim := verdadeiro
        se NÃO thread.esperar(prazo):
            # a thread não morreu; NÃO reverter marcar_fim
            devolver falso            # nada foi aplicado
        marcar_fim := falso
    acao()
    se precisa_retomar:
        iniciar_thread_do_laço(modo_anterior)
    devolver precisa_retomar
```

**Parâmetros usados:** P-01, P-02, P-03, P-04.

**Casos de erro:**
- Segunda instância → mensagem e saída silenciosa.
- Falha na enumeração de idiomas de OCR do sistema → o motor de OCR do sistema
  é marcado indisponível, guardando a mensagem de erro para exibir caso o
  usuário tente usá-lo.
- Falha no desenho vetorial → desativação global e degradação para desenho
  simples.

**Critérios de aceite:**
- Pressionar o atalho de tradução 20 vezes seguidas em intervalos de 100 ms não
  deixa duas threads de laço vivas nem mata o interceptador de teclado.
- Aplicar configuração enquanto o laço roda nunca produz um ciclo usando meia
  configuração antiga e meia nova.
- Fechar o programa durante uma tradução com serviço remoto lento encerra o
  processo em no máximo P-03 + o tempo do repintar final.

---

## 10. Configuração e persistência

**Responsabilidade:** carregar, aplicar, salvar e restaurar toda a configuração
do usuário, em formatos que permaneçam legíveis por versões futuras.

**Entradas / Saídas:** recebe arquivos de texto do disco e valores da interface;
devolve um conjunto coerente de parâmetros para todos os outros módulos.

**Requisitos:**

RF-020 — O programa deve manter um **perfil principal** carregado
automaticamente ao iniciar e salvo automaticamente sempre que o usuário aplica
configurações.

RF-021 — O programa deve permitir salvar e carregar perfis nomeados em uma pasta
dedicada, com extensão própria, através de diálogos de arquivo.

RF-022 — O programa deve permitir restaurar todos os valores para os padrões,
com confirmação do usuário. A restauração também deve descartar a posição e o
tamanho salvos da janela de tradução.

RF-023 — O formato de todos os arquivos de dados do usuário é **livre e novo**:
não há compatibilidade com nenhum produto anterior. A escolha concreta de
serialização cabe a quem constrói, desde que satisfaça: ser texto legível e
editável por uma pessoa; suportar valores de múltiplas linhas; suportar
comentários; e carregar um **número de versão de esquema** na raiz.

RF-024 — O leitor do perfil deve ser tolerante: linhas desconhecidas são
ignoradas; a ausência de uma chave mantém o valor padrão; qualquer exceção
durante a leitura restaura **todos** os padrões e continua.

RF-025 — Antes de interpretar o perfil, o leitor deve aplicar os padrões. Assim,
um perfil parcial produz um estado completo e coerente.

RF-026 — Todo valor de um conjunto fechado (modo de janela, motor de OCR,
serviço de tradução, ordenação, tipo de endpoint, idioma) deve ser persistido
pelo seu **identificador textual**, nunca pela sua posição numérica.
**Motivo:** é isso que permite acrescentar, remover ou reordenar itens do
conjunto sem invalidar os arquivos já gravados pelo usuário.

RF-027 — Os identificadores textuais devem ser estáveis, em minúsculas, sem
espaços e independentes do idioma da interface. Um identificador já publicado
nunca é reaproveitado para outro significado.

RF-028 — Um identificador desconhecido lido de um arquivo (por exemplo, um
serviço de tradução que deixou de existir) não pode impedir o carregamento: o
campo assume o padrão, o programa registra o ocorrido e informa o usuário uma
vez.

RF-029 — Os conjuntos de valores — idiomas, motores de OCR, serviços de tradução
e seus parâmetros — devem ser descritos como **dados**, não como código. Incluir
um item novo em qualquer um deles deve ser uma alteração de dados mais a
implementação do adaptador correspondente, nunca uma alteração no núcleo do
pipeline.

RF-030 — Quando o serviço de tradução é o de API personalizada, o perfil deve
guardar também uma **subchave** que identifica qual preset personalizado está
selecionado.

RF-031 — O programa deve manter um segundo arquivo de **opções avançadas**,
separado do perfil, no mesmo formato de RF-023, capaz de guardar escalares,
listas e objetos aninhados.

RF-032 — As opções avançadas devem ser globais: não mudam quando o usuário troca
de perfil. **Motivo:** são preferências da pessoa, não do jogo.

RF-033 — Se o arquivo de opções avançadas estiver ausente ou vazio, todos os
valores avançados devem assumir seus padrões e o arquivo deve ser criado.

RF-034 — O programa deve manter um terceiro arquivo de **opções do aplicativo**
para: idioma da interface, verificação de atualização ligada/desligada, aba
inicial padrão, e janela de tradução sempre no topo.

RF-035 — Credenciais de serviços de tradução devem ser guardadas em arquivos
separados, um por serviço, contendo apenas os campos daquele serviço, em **texto
puro**. Não há cofre de credenciais, não há criptografia e não há dependência de
serviço do sistema operacional para isso. **Motivo:** o programa roda inteiramente
na máquina do usuário e só usa a rede para traduzir; a credencial pertence ao
usuário, fica no computador dele, e cifrá-la localmente com uma chave também
local não acrescenta proteção real — apenas mais uma capacidade de sistema a
abstrair em cada plataforma (Parte IX).

RF-036 — O arquivo de credenciais de um serviço que aceita várias chaves deve
guardar uma lista de registros, cada um com identificador, segredo e o tipo de
plano, preservando a ordem em que o usuário os cadastrou.

RF-037 — Os atalhos de teclado devem ser guardados em arquivo próprio, como uma
lista de registros com o identificador da ação, a combinação de teclas e o
parâmetro opcional da ação.

RF-038 — Cada arquivo de dados do usuário deve registrar a versão do seu
esquema, e o programa deve conter uma cadeia de migrações que leve qualquer
versão anterior **deste** programa até a atual, executada na leitura e gravada
de volta. Chaves desconhecidas de uma versão mais nova devem ser preservadas
intactas na regravação. **Motivo:** o usuário pode alternar entre uma versão
nova e uma antiga do programa, e não pode perder configuração por isso.

RF-039 — Ao carregar um perfil, o programa deve: aplicar os valores à interface,
depois aplicar a interface à configuração efetiva, depois salvar o perfil
principal. **Motivo:** garante que campos ausentes no arquivo carregado sejam
normalizados.

RF-040 — Ao carregar um perfil, o programa deve recriar as molduras das áreas de
OCR e de exclusão nas posições salvas.

RF-041 — Ao carregar um perfil que define posição e tamanho da janela de
tradução em modo camada, o programa deve validar essas coordenadas contra os
monitores presentes: se o retângulo não intersecta nenhum monitor, deve usar a
posição padrão; se intersecta parcialmente, deve deslocá-lo para dentro dos
limites daquele monitor.

RF-042 — Valores fora de faixa devem ser saturados nos limites, nunca
rejeitados. Especificamente: componentes RGB acima de 255 viram 255; valores de
saturação e brilho acima de 100 viram 100; limiar acima de 255 vira 255; fator
de ampliação acima de 10 volta ao padrão; campos numéricos vazios viram 0.

RF-043 — Se em um grupo de cor o início de uma faixa for maior que o fim, os
dois devem ser trocados automaticamente ao carregar e ao aplicar.

RF-044 — Em um perfil novo, o padrão da opção "dicionário por palavra" deve
seguir o idioma de OCR escolhido: **ligado** para idiomas que separam palavras
por espaço, **desligado** para os que não separam. 🔒 **Motivo:** aplicar
substituição em limite de palavra a um idioma sem separador de palavra não
corrige nada e ainda mascara erros de OCR reais.

RF-045 — A opção "posição e tamanho da janela de tradução" só deve ser salva
quando o usuário aplica ou salva explicitamente — nunca durante a inicialização.

RF-046 — O programa deve oferecer uma função de exportar a configuração atual
para a área de transferência e abrir a página de envio, para o usuário
compartilhar configurações de jogos com a comunidade.

**Comportamento detalhado (aplicação de configuração):**

```
aplicar_configuração():
    trocar_modo_de_janela_se_mudou()
    copiar_todos_os_controles_para_a_configuração()
    resolver_velocidade: índice 1..5 -> intervalo P-05..P-09
    gravar_arquivo_de_atalhos()
    resolver_códigos_de_idioma_dos_tradutores()
    reinicializar_motor_de_ocr_do_sistema_com_o_idioma_escolhido()
    derivar_sinalizadores_de_idioma (inglês / japonês / outro) a partir do
        motor de OCR ativo e do seu código de idioma
    aplicar fonte, cores, alinhamento, remoção de espaço, índice de área
    aplicar ao pré-processamento: grupos de cor, erosão, ampliação, limiar
    aplicar dicionário de correção e banco de dados
    recalcular e reenviar as áreas de captura e exclusão
    inicializar o serviço de tradução escolhido com suas credenciais
    salvar credenciais
```

**Parâmetros usados:** todos os da Parte IV.

**Casos de erro:**
- Arquivo de perfil ausente → cria vazio e usa padrões.
- Arquivo corrompido no meio → exceção capturada, padrões restaurados, log.
- Pasta de dados do usuário ausente → criada automaticamente ao abrir qualquer
  arquivo.

**Critérios de aceite:**
- Salvar um perfil, restaurar padrões e recarregar o perfil devolve exatamente o
  estado anterior, incluindo áreas, grupos de cor e cores de fonte.
- Um perfil ao qual foram removidas linhas aleatórias ainda abre, com os campos
  removidos nos padrões.
- Trocar de perfil não altera nenhuma opção avançada.
- Um perfil gravado por uma versão mais nova do programa, contendo uma chave que
  a versão atual não conhece, abre sem erro e, ao ser regravado, ainda contém
  aquela chave.
- Acrescentar um idioma, um motor de OCR ou um serviço de tradução novo aos
  dados de configuração não invalida nenhum perfil existente.

---

## 11. Seleção e gerenciamento da região de captura

**Responsabilidade:** deixar o usuário definir com precisão o que será lido, e
entregar essa definição ao restante do sistema em coordenadas corretas.

**Entradas / Saídas:** recebe gestos do mouse e a configuração carregada;
devolve listas de retângulos de captura e de exclusão em coordenadas absolutas
de tela, já alinhados.

**Princípio deste módulo.** Definir o retângulo é **etapa obrigatória** para usar
o programa: sem ao menos uma área, não há o que traduzir e a tradução não
inicia. Como é obrigatória, ela não pode ser repetida a cada abertura — as áreas
definidas são **persistidas** e restauradas na abertura seguinte, exatamente onde
estavam. As áreas são **múltiplas** e de dois tipos: as **incrementais**, que
somam região a ser lida, e as **decrementais**, que subtraem região de dentro das
incrementais.

**Requisitos:**

RF-047 — Para criar uma área, o programa deve exibir uma camada sobre **toda a
área de trabalho virtual** (todos os monitores), semitransparente, e permitir
desenhar um retângulo arrastando com o botão esquerdo.

RF-048 — A camada de seleção deve mostrar, em tempo real, o retângulo em
construção: o interior pintado com a cor de destaque configurada e a borda em
verde escuro de 2 px, tudo em desenho com buffer duplo para não piscar.

RF-049 — A cor de fundo e a cor de destaque da camada de seleção devem ser
configuráveis pelo usuário, com uma pré-visualização acionável.

RF-050 — A opacidade da camada de seleção deve ser derivada do canal alfa da cor
de fundo escolhida, saturado num mínimo, pela fórmula P-10.

RF-051 — Clicar com o botão direito durante a seleção deve cancelar sem criar
área.

RF-052 — Um retângulo com largura ou altura de até 4 px deve ser tratado como
clique acidental e descartado sem criar área.

RF-053 — Enquanto a camada de seleção está aberta, todos os atalhos globais do
programa devem ficar inertes.

RF-054 — Cada área criada deve virar uma **moldura** independente: uma janela
sem borda de sistema, sempre no topo, que não aparece na barra de tarefas,
composta de uma barra de título e uma borda dupla desenhada.

RF-055 — A barra de título da moldura deve mostrar o tipo da área, seu índice,
seu tamanho em pixels e sua posição, atualizados em tempo real durante o
arraste.

RF-056 — A moldura deve ser movível arrastando a barra de título e
redimensionável arrastando qualquer borda ou canto, com o cursor mudando para
indicar a direção. A zona sensível de borda é P-11.

RF-057 — A moldura não deve poder ficar menor que P-12 em nenhuma dimensão.

RF-058 — Ao soltar o arraste, a moldura deve ser reposicionada para dentro dos
limites da área de trabalho virtual se tiver saído pela esquerda ou pelo topo.

RF-059 — Durante o arraste ou redimensionamento, a moldura deve notificar o
sistema para recalcular as áreas de captura, com uma taxa máxima de uma
notificação a cada P-13. **Motivo:** permitir ajuste fino com a tradução rodando
sem inundar o pipeline.

RF-060 — Essa notificação só deve ocorrer se o programa já terminou de
inicializar e não está no meio de um carregamento ou aplicação de configuração.

RF-061 — As áreas de captura resultantes de arraste devem ser aplicadas como
**temporárias**: se o usuário cancelar o gerenciamento de áreas sem confirmar,
as áreas voltam ao estado salvo anterior.

RF-062 — Deve existir uma janela de gerenciamento de áreas com: adicionar área,
adicionar área de exclusão, limpar todas, aplicar. Aplicar confirma as áreas
temporárias; fechar sem aplicar reverte.

RF-063 — As áreas de exclusão devem ter aparência distinta (borda vermelha,
opacidade reduzida a 70%) e não devem oferecer os botões de cor.

RF-064 — As áreas devem ser reindexadas quando uma é removida: as de índice
maior que a removida decrementam.

RF-065 — O programa deve exigir **pelo menos uma área incremental** para iniciar
qualquer tradução. Sem ela, a tradução não começa e o programa explica ao
usuário que é preciso definir a área primeiro, oferecendo abrir diretamente a
camada de seleção.

RF-066 — As áreas incrementais e as decrementais devem ser **persistidas no
perfil** e restauradas na abertura seguinte, com a mesma posição, o mesmo tamanho
e a mesma ordem. O usuário define a região uma vez e não repete o trabalho.

RF-067 — O programa deve aceitar **qualquer quantidade** de áreas incrementais e
decrementais, sem limite fixo. A composição é: a união das incrementais, menos a
união das decrementais.

RF-068 — Uma área decremental só tem efeito sobre a parte de si que cai dentro de
alguma área incremental; fora disso ela é inócua, e isso não é erro.

RF-069 — Deve existir uma **área rápida**: uma única área extra, criada por
atalho, aplicada imediatamente e **não persistida** no perfil.

RF-070 — Deve existir uma **área instantânea**: quando presente, ela substitui
todas as demais áreas para os ciclos seguintes, e o retângulo dela é memorizado
como "último instantâneo".

RF-071 — Ao iniciar uma tradução que não é instantânea, a memória de "último
instantâneo" deve ser apagada.

RF-072 — Ao iniciar uma tradução em tempo real, se existir memória de "último
instantâneo", as áreas devem ser recalculadas antes de começar.

RF-073 — A conversão de moldura para retângulo de captura deve descontar a borda
e a barra de título da moldura: origem em (x + borda, y + barra de título),
tamanho em (largura − 2×borda, altura − barra de título − borda), com mínimo de
1 px em cada dimensão.

RF-074 — As espessuras de borda e barra de título usadas nessa conversão devem
ser escaladas pelo fator de DPI, a partir dos valores base P-14, P-15 e P-16.

RF-075 — O fator de escala deve ser o do **monitor que contém a moldura**, obtido
no momento de converter aquela moldura em retângulo — nunca um fator global lido
uma vez na abertura do programa. **Motivo:** com um monitor a 100% e outro a
150%, um fator único erra em um dos dois, e a região capturada sai deslocada
alguns pixels; na sobreposição isso aparece como a tradução desalinhada em
relação ao texto original. Em máquinas com um único monitor, ou com todos na
mesma escala, os dois comportamentos coincidem — é por isso que a falha passa
despercebida na maioria das instalações.

RF-076 — Quando uma moldura é arrastada de um monitor para outro de escala
diferente, o fator deve ser recalculado. A moldura pertence ao monitor onde está
o seu canto superior esquerdo.

RF-077 — A largura de cada retângulo entregue à captura deve ser arredondada
para cima até o próximo múltiplo de 4. **Motivo:** alinhamento de linha da
imagem. 🔒

RF-078 — Cada área de OCR deve ter, individualmente, a lista de grupos de cor
que se aplicam a ela, editável por uma janela dedicada com caixas de seleção e
um botão de marcar todos.

RF-079 — Ao adicionar um grupo de cor, todas as áreas devem passar a incluí-lo
por padrão; ao remover um grupo, ele deve sair da lista de todas as áreas.

RF-080 — Cada área de OCR deve oferecer um **conta-gotas**: captura a região
correspondente da tela e abre uma janela que exibe a imagem ampliável (1× a 4×,
por combo ou roda do mouse) e, ao arrastar o botão esquerdo sobre a imagem,
mostra os valores R, G, B, H, S e V do pixel sob o cursor.

RF-081 — A janela do conta-gotas deve oferecer uma **pré-visualização
binarizada** lado a lado, aplicando exatamente o mesmo critério de filtro que o
pré-processamento usaria (RGB exato, faixas HSV, ou limiar), para que o usuário
veja o que o OCR vai receber.

RF-082 — A pré-visualização binarizada deve pintar de preto os pixels que
**passam** no filtro e de branco os que não passam.

RF-083 — Deve haver um botão global de "ver resultado da imagem" que abre o
conta-gotas da primeira área disponível já com a pré-visualização ligada e com
os valores de filtro atuais.

RF-084 — Se não houver nenhuma área quando o usuário pedir a pré-visualização, o
programa deve informar isso em vez de falhar.

RF-085 — As molduras só devem ser visíveis enquanto o usuário está **definindo**
as áreas — isto é, com a janela de gerenciamento de áreas aberta. Ao concluir,
elas ficam invisíveis (opacidade zero) e assim permanecem durante toda a
tradução. **Motivo:** a moldura sobre a imagem do jogo atrapalha exatamente o que
o usuário está tentando ler.

RF-086 — Se a resolução da tela mudar, um monitor for removido ou a disposição
dos monitores for alterada com o programa aberto, e alguma área ficar total ou
parcialmente fora da área de trabalho, o programa deve **avisar o usuário** e
apontar quais áreas ficaram inválidas, deixando que ele as corrija. O programa
**não** deve reposicionar áreas automaticamente. **Motivo:** o programa não tem
como saber onde o conteúdo do jogo foi parar; mover a área por conta própria
produziria uma região errada silenciosamente, e o usuário só descobriria pelo
resultado da tradução.

RF-087 — Ao emitir esse aviso, o programa deve oferecer abrir diretamente a
janela de gerenciamento de áreas, com as molduras visíveis.

**Comportamento detalhado (montagem da lista final de captura):**

```
montar_áreas():
    lista := vazio
    somente_mouse := modo_segue_mouse_ativo E opção_somente_essa_área
    tem_instantâneo := existe área instantânea

    se tem_instantâneo E NÃO somente_mouse:
        lista += retângulo(área_instantânea)
        memorizar_último_instantâneo(retângulo)

    para cada área normal:
        registrar em áreas_persistidas   # sempre, mesmo se não entrar na lista
        se NÃO tem_instantâneo E NÃO somente_mouse:
            lista += retângulo(área)

    exclusões := retângulos das áreas de exclusão

    se existe área rápida E NÃO tem_instantâneo E NÃO somente_mouse:
        lista += retângulo(área_rápida)

    se modo_segue_mouse_ativo E (NÃO tem_instantâneo OU somente_mouse):
        lista += retângulo(área_do_mouse)

    para cada r em lista:
        r.largura := arredondar_para_múltiplo_de_4_para_cima(r.largura)

    aplicar(lista, exclusões, grupos_de_cor_por_área)
```

Regra de índice para consulta reversa (usada pelo desenho da sobreposição): os
índices 0..N−1 são as áreas normais persistidas; o índice N é a área rápida, se
existir; o índice seguinte é a área que segue o mouse, se existir. Quando há
instantâneo, qualquer índice resolve para o retângulo do instantâneo. Quando o
modo "somente área do mouse" está ativo, apenas o índice 0 é válido e resolve
para a área do mouse.

**Parâmetros usados:** P-10 a P-16.

**Casos de erro:**
- Área totalmente fora dos monitores → a captura não produz imagem e o índice é
  pulado sem erro.
- Área de largura ou altura 0 após ajustes → forçada a 1 px.
- Remoção de uma área durante a tradução → o ciclo seguinte já usa a lista nova.

**Critérios de aceite:**
- Criar cinco áreas, remover a terceira, e as duas seguintes passam a exibir
  índices 3 e 4.
- Arrastar uma moldura por 5 segundos gera no máximo ~17 recálculos (5 s ÷
  P-13), não um por evento de mouse.
- Uma área desenhada sobre um monitor secundário com origem negativa é capturada
  corretamente.
- A pré-visualização binarizada com faixas HSV específicas produz a mesma imagem
  que o OCR recebe.

---

## 12. Captura de tela

**Responsabilidade:** transformar retângulos em imagens de pixels, a partir de
três fontes possíveis.

**Entradas / Saídas:** recebe a lista de retângulos e o modo de fonte; devolve,
por retângulo, uma imagem em memória e, quando pedido, a imagem original.

**Requisitos:**

RF-088 — O programa deve suportar três fontes de imagem:
1. **Tela** — lê o conteúdo atual da área de trabalho nas coordenadas dadas.
2. **Janela ativa** — lê a partir da janela que está em primeiro plano,
   ajustando as coordenadas à origem do cliente dessa janela.
3. **Janela anexada** — lê de uma janela específica escolhida pelo usuário,
   independentemente de ela estar coberta ou não.

RF-089 — No modo janela anexada, o usuário deve escolher a janela por um seletor
do sistema que lista as janelas capturáveis, e o programa deve exibir o estado
("selecionando", "capturando <nome>", "parado") e um botão de parar.

RF-090 — No modo janela anexada, quando a janela escolhida for fechada, a
captura deve parar automaticamente e o modo deve ser desativado.

RF-091 — No modo janela anexada, o programa deve oferecer a opção de exibir ou
não a borda amarela que o sistema desenha em janelas sendo capturadas. Em
sistemas onde desativar não é possível, o programa deve informar isso.

RF-092 — No modo janela anexada, a posição de origem do conteúdo capturado deve
ser obtida a partir dos limites estendidos do quadro da janela — não do
retângulo simples da janela —, com queda para o retângulo simples se os limites
estendidos não estiverem disponíveis. **Motivo:** sombras e bordas invisíveis
deslocariam todo o alinhamento da sobreposição.

RF-093 — No modo janela anexada, a captura deve manter um pequeno buffer de
quadros recentes (P-17 quadros) para que um pedido de captura possa ser
atendido imediatamente com o último quadro válido em vez de esperar o próximo.

RF-094 — No modo janela anexada, mesmo sem pedido explícito, um quadro deve ser
guardado a cada P-18 quadros recebidos, para manter o buffer aquecido.

RF-095 — No modo janela anexada, um quadro guardado deve ser considerado
utilizável se for mais recente que P-19; caso contrário o laço aguarda um novo.

RF-096 — Quando o pedido de captura da janela anexada não é atendido de
imediato, o laço deve tentar novamente em intervalos de P-20 até obter o quadro
ou receber pedido de parada.

RF-097 — Se, ao pedir a imagem da janela anexada, a janela de captura não existir
mais, o laço deve encerrar a si mesmo.

RF-098 — Quando o modo sobreposição com cor automática está ativo, a captura
deve devolver **duas** versões de cada região: a imagem tratada (para o OCR) e a
imagem original sem tratamento (para a análise de cor).

RF-099 — A imagem original deve ser liberada explicitamente assim que a análise
de cor da região terminar, e a imagem tratada assim que o OCR terminar.
**Motivo:** cada região pode ocupar dezenas de megabytes com ampliação.

RF-100 — A captura da tela e da janela ativa deve funcionar com múltiplos
monitores, incluindo coordenadas negativas.

**Comportamento detalhado:**

```
obter_imagens(áreas, fonte, precisa_original):
    se fonte == janela_anexada:
        preparar_captura()
        repetir:
            solicitar_quadro()
            sucesso, bytes, largura, altura, origem := obter_dados()
            se pedido_de_parada: retornar
            se sucesso: sair do repetir
            dormir P-20
        para cada índice de área:
            imagem := recortar_e_tratar(bytes, largura, altura, origem, índice)
            se imagem vazia: continuar
            resultado += imagem
            se precisa_original:
                resultado[último].original := recortar_sem_tratar(...)
    senão:
        para cada índice de área:
            imagem := capturar_e_tratar(índice)   # da tela ou da janela ativa
            se imagem vazia: continuar
            resultado += imagem
            se precisa_original:
                resultado[último].original := capturar_sem_tratar(índice)
```

**Parâmetros usados:** P-17 a P-20.

**Casos de erro:**
- Retângulo fora de qualquer monitor → sem imagem, índice pulado.
- Janela anexada minimizada → o sistema pode continuar entregando quadros do
  conteúdo; se não entregar, o laço espera e o texto anterior permanece.
- Falha ao criar o dispositivo gráfico de captura → o modo janela anexada fica
  indisponível e o usuário é informado.

**Critérios de aceite:**
- Com três áreas em dois monitores diferentes, as três imagens saem com o
  conteúdo correto.
- No modo janela anexada, mover a janela alvo não desloca a sobreposição.
- A memória do processo não cresce indefinidamente ao rodar 30 minutos com
  ampliação 4× e três áreas grandes.

---

## 13. Pré-processamento de imagem

**Responsabilidade:** transformar a imagem capturada em uma imagem que maximize
a taxa de acerto do OCR.

**Entradas / Saídas:** recebe a imagem bruta de uma região, os retângulos de
exclusão, os grupos de cor ativos daquela região, e os sinalizadores de filtro,
erosão e ampliação; devolve a imagem tratada.

**Requisitos:**

RF-101 — O programa deve remover da imagem as porções cobertas por áreas
decrementais antes de qualquer outro tratamento.

RF-102 — A região removida deve ser preenchida com o **valor de fundo do filtro
ativo** — ou seja, com a mesma cor que um pixel reprovado no filtro receberia —,
nunca com preto, branco fixo ou qualquer cor de alto contraste. Quando nenhum
filtro está ativo, deve ser preenchida com a cor dominante da borda da própria
região removida. **Motivo:** o objetivo é que a exclusão fique **invisível para o
OCR**. Um retângulo preto sobre fundo claro cria uma aresta de contraste máximo
que vários motores leem como traço, e a exclusão passaria a inventar caracteres
em vez de eliminar ruído.

RF-103 — A geometria da imagem não muda: a região excluída continua ocupando seu
lugar. **Motivo:** as caixas devolvidas pelo OCR precisam continuar mapeando para
as coordenadas de tela originais.

RF-104 — O programa deve oferecer três modos de filtro de cor, **mutuamente
exclusivos**: extração por RGB exato, extração por faixas HSV, e limiar simples.
Marcar um desmarca os outros dois automaticamente.

RF-105 — No modo RGB, um pixel é considerado texto quando seus três componentes
são exatamente iguais aos valores do grupo de cor. Vários grupos de cor podem
estar ativos; o pixel passa se satisfizer **qualquer** grupo ativo.

RF-106 — No modo HSV, um pixel é considerado texto quando sua saturação está
entre o início e o fim da faixa de saturação **e** seu brilho está entre o
início e o fim da faixa de brilho do grupo. Saturação e brilho são expressos em
0–100.

RF-107 — A conversão para HSV deve usar máximo e mínimo dos componentes: brilho
igual ao máximo; saturação igual a (máximo − mínimo) × 255 ÷ máximo, com
saturação 0 quando o máximo é 0; matiz calculada nos setores de 60 graus, com
normalização para 0–360. Para exibição e comparação, saturação e brilho são
convertidos de 0–255 para 0–100.

RF-108 — No modo limiar, a imagem é convertida para tons de cinza pela matriz de
luminância (0,30 / 0,59 / 0,11) e binarizada no valor de corte P-21.

RF-109 — O resultado do filtro deve ser uma imagem binária: pixels que passam em
uma cor, pixels que não passam na outra. A pré-visualização usa preto para quem
passa e branco para quem não passa.

RF-110 — Quando nenhum filtro está ativo, a imagem deve ser entregue ao OCR sem
binarização.

RF-111 — O programa deve oferecer **erosão** opcional. Erosão é uma operação
morfológica que percorre a imagem binarizada e afina os traços do texto,
removendo uma camada de pixels da borda de cada glifo. Serve para dois casos:
fontes muito grossas, em que letras vizinhas se tocam e o OCR as lê como um
caractere só; e ruído de compressão, que aparece como pontos isolados e some com
uma erosão.

RF-112 — A erosão deve usar elemento estruturante **quadrado de 3 × 3, uma única
iteração**, aplicada sobre a imagem já binarizada e **antes** da ampliação.
**Motivo da ordem:** erodir antes de ampliar afina o traço original, que é o
alvo; erodir depois afinaria o traço já interpolado pela ampliação, o que
destrói detalhe fino em vez de separar glifos. [INFERIDO] — a forma e o número
de iterações são uma escolha conservadora: é o menor elemento que produz efeito
visível. Se na prática 3 × 3 se mostrar agressivo demais para fontes pequenas, o
ajuste correto é expor o tamanho ao usuário, não trocar a ordem das operações.

RF-113 — O programa deve **ampliar** a imagem por um fator configurável P-22
antes de entregá-la ao OCR. A ampliação é o ajuste de maior impacto na taxa de
acerto com fontes pequenas.

RF-114 — O fator de ampliação deve aceitar valores entre P-23 e P-24, em passos
de P-25, com uma casa decimal. Um valor lido do perfil acima de 10 deve ser
substituído pelo padrão.

RF-115 — Deve haver um botão para restaurar o fator de ampliação ao padrão.

RF-116 — As coordenadas devolvidas pelo OCR estão no espaço da imagem ampliada.
Toda conversão de volta para coordenadas de tela deve dividir por P-22, usando
piso para os cantos superior/esquerdo e teto para os cantos inferior/direito.

RF-117 — O programa deve suportar imagens de 1, 3 e 4 canais na conversão para o
formato exigido por cada motor de OCR, replicando o canal único nos três canais
de cor e descartando o canal alfa quando presente.

RF-118 — O pré-processamento deve poder ser desativado por completo, entregando
a imagem colorida original ao motor de OCR. Motores modernos costumam ir melhor
assim; o filtro existe para fundos difíceis.

RF-119 — Deve existir um assistente de configuração rápida que, a partir da
escolha "texto claro" ou "texto escuro", configure automaticamente os grupos de
cor HSV: para texto escuro, dois grupos — P-26 e P-27; para texto claro, um
grupo — P-28. 🔒

**Comportamento detalhado:**

```
pré_processar(imagem, exclusões, grupos, modo, erosão, ampliação):
    para cada e em exclusões que intersecta a região:
        apagar(imagem, e)

    se modo == RGB:
        binária := para cada pixel: passa se ALGUM grupo casa exatamente
    senão se modo == HSV:
        binária := para cada pixel:
            (h, s, v) := rgb_para_hsv(pixel)
            s := s * 100 / 255 ; v := v * 100 / 255
            passa se ALGUM grupo tem s∈[s1,s2] E v∈[v1,v2]
    senão se modo == LIMIAR:
        cinza := 0.30*R + 0.59*G + 0.11*B
        binária := cinza < limiar
    senão:
        binária := imagem            # sem filtro

    se erosão: binária := erodir(binária)
    devolver redimensionar(binária, fator = ampliação)
```

**Parâmetros usados:** P-21 a P-28.

**Casos de erro:**
- Todos os pixels filtrados → imagem em branco → OCR devolve vazio → ciclo segue
  normalmente com texto vazio.
- Fator de ampliação alto com área grande produz uma imagem muito pesada. Não
  deve haver limite artificial: o usuário decide. O programa apenas expõe o
  consumo de memória na interface (RF-558) para que ele perceba antes de haver
  problema.

**Critérios de aceite:**
- Com um grupo HSV configurado para texto branco sobre fundo escuro, a
  pré-visualização mostra as letras em preto e o resto em branco.
- Ativar RGB desmarca HSV e limiar na mesma ação.
- Uma caixa de palavra devolvida pelo OCR com ampliação 2× resulta, ao ser
  convertida, exatamente sobre o texto original na tela.

---

## 14. Reconhecimento de texto (OCR)

**Responsabilidade:** converter a imagem tratada em palavras com posição.

**Entradas / Saídas:** recebe uma imagem em memória e um código de idioma;
devolve o resultado estruturado descrito em 6.4.

**Requisitos:**

RF-120 — O programa deve suportar múltiplos motores de OCR, selecionáveis pelo
usuário, e listar exatamente os que estão disponíveis no sistema atual.

RF-121 — Os motores previstos e suas características são:

| Motor | Rede | Posições | Observação |
|---|---|---|---|
| Motor local clássico | não | por palavra | Requer arquivos de dados de idioma; suporta variantes "rápidas" para inglês e japonês. |
| Motor do sistema operacional | não | por palavra | Depende dos pacotes de idioma instalados no sistema. |
| Motor de reconhecimento moderno embarcado | não | por palavra e por linha | Melhor qualidade local; requer modelo e biblioteca nativa presentes. |
| Motor de nuvem | sim | por palavra | Melhor qualidade absoluta; cota mensal; **só pode ser usado em modo pontual**. |
| Motor baseado em ambiente interpretado | não | por linha | Requer instalação sob demanda de um ambiente e de pacotes; suporta aceleração por GPU. |

RF-122 — O motor de nuvem **não** pode ser usado em tradução em tempo real. Se o
usuário tentar, o programa deve informar isso e não iniciar.

RF-123 — Deve existir uma opção "priorizar o motor de nuvem em modo pontual":
quando ativa, disponível e dentro da cota, os modos pontuais usam o motor de
nuvem mesmo que outro motor esteja selecionado.

RF-124 — O motor de nuvem deve contar as chamadas por credencial e por mês
civil, zerando a contagem quando o mês ou o ano mudam.

RF-125 — Quando a contagem atinge o limite configurado P-29, o motor de nuvem
deve recusar novas chamadas e devolver uma mensagem explicando que a cota
mensal acabou.

RF-126 — Se a credencial do motor de nuvem não estiver configurada, ele deve
devolver uma mensagem pedindo que o usuário selecione o arquivo de credencial.

RF-127 — A contagem de uso do motor de nuvem deve ser persistida por credencial,
junto com a data da última renovação, e exibida ao usuário no formato
"usadas / limite".

RF-128 — O motor de reconhecimento moderno embarcado deve procurar sua
biblioteca e seu modelo na subpasta de bibliotecas do programa. Se não os
encontrar, deve tentar localizá-los em componentes do sistema já instalados e
copiá-los para lá.

RF-129 — A inicialização desse motor deve tentar, em ordem, três formas de
passar o caminho do modelo, e como último recurso copiar o modelo para um
caminho temporário puramente ASCII e tentar de novo. **Motivo:** caminhos com
caracteres não-ASCII quebram a biblioteca nativa. 🔒

RF-130 — Esse motor deve limitar o número máximo de linhas reconhecidas por
imagem a P-30.

RF-131 — Se esse motor não puder ser inicializado, o programa deve, na primeira
tentativa de uso, perguntar ao usuário se quer abrir a página de ajuda, parar a
tradução, e não tentar de novo até que o usuário reinicie a tradução.

RF-132 — O motor baseado em ambiente interpretado deve verificar, antes de
iniciar a tradução, se o ambiente e o pacote de OCR estão instalados. Se não
estiverem, deve perguntar ao usuário se deseja instalar e abrir o instalador.

RF-133 — O instalador desse motor deve oferecer: instalação básica (somente CPU),
instalação com aceleração por GPU escolhendo entre versões pré-definidas de
biblioteca de computação, ou uma linha de comando personalizada; e uma opção de
"forçar reinstalação" que apaga o ambiente antes.

RF-134 — O instalador deve exibir o log de instalação em tempo real e bloquear
o fechamento da janela durante a instalação.

RF-135 — "Forçar reinstalação" deve ser recusada se o ambiente já estiver
carregado na sessão atual; o usuário deve ser informado de que precisa
reiniciar o programa.

RF-136 — O motor do sistema operacional deve listar os idiomas de reconhecimento
disponíveis com nome legível e código, e permitir ao usuário abrir a tela de
idiomas do sistema para instalar mais.

RF-137 — Se o motor do sistema tiver apenas um idioma instalado e esse idioma
não for útil para tradução, o programa deve avisar uma única vez por sessão e
oferecer o link de ajuda.

RF-138 — O motor do sistema opera de forma assíncrona: o programa deve
disponibilizar a imagem, disparar o reconhecimento, e esperar em passos de P-31
até que o motor volte ao estado disponível.

RF-139 — Se o motor do sistema não estiver disponível no início de um ciclo, o
ciclo deve reutilizar o texto do ciclo anterior em vez de produzir vazio.

RF-140 — O motor de reconhecimento moderno deve, quando a opção de orientação
vertical estiver ativa, identificar as linhas verticais (altura maior que a
largura multiplicada por P-32) e reordená-las por coluna: coordenada horizontal
decrescente e, dentro da mesma coluna, coordenada vertical crescente. As linhas
horizontais mantêm sua posição original na lista. 🔒

RF-141 — Para motores que devolvem apenas linhas (sem palavras), cada linha deve
ser convertida em uma única "palavra" com a caixa da própria linha.

RF-142 — Para motores que devolvem quadriláteros (quatro pontos), a caixa
delimitadora deve ser calculada como mínimo e máximo dos quatro pontos em cada
eixo, nunca por diferença direta entre dois pontos. **Motivo:** evita larguras e
alturas negativas em texto rotacionado. 🔒

RF-143 — O texto devolvido por bibliotecas nativas deve ser decodificado como
UTF-8, com decodificação manual byte a byte como alternativa.

RF-144 — Quando o motor de nuvem é usado, as quebras de linha devem ser
detectadas comparando a posição acumulada de caracteres com a posição das
quebras no texto completo devolvido pelo serviço, e a contagem de palavras por
linha derivada disso.

RF-145 — Erros do motor de OCR devem produzir um resultado marcado como vazio,
com a mensagem de erro no campo de texto principal, e o ciclo deve continuar.

RF-146 — O programa deve informar ao pré-processamento se o idioma ativo é
japonês (ou não latino), porque isso muda regras de correção de texto.

RF-147 — A escolha do idioma de OCR deve propagar automaticamente para os
idiomas de origem dos serviços de tradução, quando houver correspondência.

RF-148 — Ao escolher um idioma de OCR, o programa deve ajustar automaticamente
duas opções a partir da propriedade "separa palavras por espaço" declarada para
aquele idioma (RF-311): quando o idioma **não** separa por espaço — o caso do
japonês —, ativar a remoção de espaços e desativar o dicionário por palavra;
quando separa — o caso do inglês —, desativar a remoção de espaços e ativar o
dicionário por palavra. 🔒 **Motivo:** o OCR insere espaços espúrios entre
caracteres de escritas que não os usam, e a substituição em limite de palavra não
tem significado nessas escritas.

RF-149 — Ao trocar de motor de OCR, o programa deve tentar preservar o idioma:
se o motor anterior estava em inglês ou japonês e o novo motor tem esse idioma,
o novo motor deve ser configurado nele.

RF-150 — O motor local clássico deve aceitar um nome de conjunto de dados de
idioma digitado pelo usuário, e uma opção de "modo rápido" que anexa um sufixo
a esse nome quando ele é `eng` ou `jpn`.

RF-151 — A lista de idiomas oferecida por cada motor de OCR deve ser a
interseção entre os idiomas que aquele motor sabe reconhecer e os idiomas de
origem previstos na tabela (RF-309). Nesta versão, portanto: **inglês e
japonês**.

**Parâmetros usados:** P-29 a P-32.

**Casos de erro:**
- Biblioteca nativa ausente → motor indisponível, mensagem uma única vez.
- Arquitetura incompatível → mensagem específica e motor indisponível.
- Nenhum idioma instalado para o motor do sistema → motor marcado indisponível
  com a mensagem de erro guardada.

**Critérios de aceite:**
- Trocar de motor com a tradução rodando não deixa dois motores ativos.
- Uma imagem completamente branca produz resultado vazio, não erro.
- As caixas de palavras de um texto vertical japonês têm largura e altura
  positivas.

---

## 15. Estruturação e pós-processamento do texto reconhecido 🔒

**Responsabilidade:** transformar palavras soltas em blocos de texto que
correspondam a unidades semânticas — frases, parágrafos, itens de lista,
títulos — para que a tradução receba contexto suficiente e a sobreposição saiba
onde desenhar cada tradução.

Este é, junto com a detecção de mudança, o módulo que faz o produto ser
utilizável. Todas as constantes aqui foram calibradas ao longo de anos contra
casos reais.

**Entradas / Saídas:** recebe o resultado do OCR de uma região, o sinalizador de
fusão de linhas e o de remoção de espaços; devolve a lista de blocos de tradução
descrita em 7.4.

### 15.1 Construção de linhas

RF-152 — O texto de uma linha deve ser a concatenação das suas palavras
separadas por um espaço, **incluindo um espaço no final**. 🔒 **Motivo:** o
comportamento a jusante depende dessa forma exata.

RF-153 — A caixa de cada palavra deve ser criada expandindo para fora: piso das
coordenadas de origem, teto das coordenadas de origem somadas às dimensões, com
largura e altura negativas tratadas como zero.

RF-154 — A caixa da linha deve ser a união das caixas das suas palavras.

RF-155 — Uma linha deve ser classificada como **vertical** quando a altura da
sua caixa for maior que a largura multiplicada por P-33; caso contrário,
**horizontal**. 🔒

RF-156 — A caixa de resultado da região deve ser a união das caixas de todas as
linhas.

### 15.2 Agrupamento de linhas em blocos

RF-157 — Quando a fusão de linhas está **desligada**, ou quando o modo de
depuração "uma linha por tradução" está ligado, cada linha vira um bloco
independente.

RF-158 — Quando a fusão de linhas está **ligada**, o agrupamento deve seguir o
algoritmo espacial descrito abaixo.

RF-159 — Primeiro, as linhas devem ser particionadas em **componentes
conectados** por adjacência espacial, usando união-busca: duas linhas ficam no
mesmo componente se forem espacialmente adjacentes (RF-163).

RF-160 — Dentro de cada componente, as linhas devem ser ordenadas: se o
componente é vertical, por coordenada direita decrescente e depois topo
crescente (leitura em colunas da direita para a esquerda); se é horizontal, por
topo crescente e depois esquerda crescente. 🔒

RF-161 — Os componentes devem ser ordenados entre si por topo crescente; em caso
de empate, componentes verticais por direita decrescente e horizontais por
esquerda crescente.

RF-162 — Dentro de cada componente, as linhas são percorridas em ordem e
agrupadas em blocos segundo as regras de item de lista, título e continuação
descritas a seguir.

**Adjacência espacial (o coração do agrupamento):**

RF-163 — Duas linhas são espacialmente adjacentes quando **todas** estas
condições valem:
1. têm a mesma orientação;
2. o tamanho de fonte estimado de ambas é maior que zero e a razão entre o maior
   e o menor não excede P-34; 🔒
3. o intervalo entre elas no eixo de escoamento (vertical para linhas
   horizontais, horizontal para linhas verticais) não excede o tamanho médio de
   fonte multiplicado por P-35; 🔒
4. no eixo transversal, ou a sobreposição relativa é de pelo menos P-36, ou a
   diferença entre os inícios não excede o tamanho médio de fonte multiplicado
   por P-37. 🔒

```
tamanho_médio := (fonte_esquerda + fonte_direita) / 2

intervalo_eixo(a_ini, a_fim, b_ini, b_fim) :=
    max(0, max(a_ini, b_ini) - min(a_fim, b_fim))

sobreposição(a_ini, a_fim, b_ini, b_fim) :=
    max(0, min(a_fim,b_fim) - max(a_ini,b_ini))
    / max(1, min(a_fim-a_ini, b_fim-b_ini))

# horizontal
transversal_ok := sobreposição(a.esq,a.dir,b.esq,b.dir) >= P-36
                  OU |a.esq - b.esq| <= tamanho_médio * P-37
adjacente := intervalo_eixo(a.topo,a.base,b.topo,b.base) <= tamanho_médio*P-35
             E transversal_ok

# vertical: trocar os eixos
```

**Tamanho de fonte estimado:**

RF-164 — O tamanho de fonte de uma linha deve ser a **mediana** de
`min(largura, altura)` sobre todas as caixas de palavra com largura e altura
positivas. Se não houver nenhuma, o valor é P-38. Para número par de amostras, a
mediana é a média das duas centrais, com piso em 1. 🔒 **Motivo:** a mediana
resiste a caixas espúrias de pontuação e ruído; usar `min` da caixa aproxima a
altura x da fonte independentemente da orientação.

**Itens de lista:**

RF-165 — Antes de agrupar, o componente deve ser examinado para saber se está em
**contexto de lista**. Está, se qualquer linha começa com um marcador forte, um
marcador fraco explícito ou um marcador numerado; ou se pelo menos duas linhas
começam com um candidato a marcador fraco. 🔒

RF-166 — **Marcador forte:** o primeiro caractere (após remover espaços à
esquerda) pertence ao conjunto de marcadores de lista tipográficos P-39, e a
linha tem mais de um caractere. 🔒

RF-167 — **Candidato a marcador fraco:** o primeiro caractere é `-`, `*` ou `.`
e a linha tem mais de um caractere.

RF-168 — **Marcador fraco explícito:** é candidato a marcador fraco **e** o
segundo caractere é espaço em branco.

RF-169 — **Marcador numerado:** opcionalmente um parêntese de abertura, seguido
de 1 a 3 caracteres alfanuméricos, seguido do fechamento correspondente — `)` se
havia parêntese de abertura, `.` ou `)` caso contrário —, seguido de espaço em
branco, seguido de pelo menos um caractere não branco. 🔒

RF-170 — Uma linha classificada como item de lista deve virar um bloco próprio e
**quebrar** o bloco em construção. Itens de lista nunca são fundidos com o item
seguinte.

**Títulos:**

RF-171 — Uma linha é **título explícito** quando, após remover espaços nas
pontas, está inteiramente envolvida por colchetes, por colchetes de canto
tipográficos, ou por sinais de menor/maior; ou quando termina com dois-pontos
(versão ASCII ou de largura total). 🔒

RF-172 — A **primeira** linha de um componente é **título por contexto** quando:
existe uma linha seguinte; ambas têm a mesma orientação; a linha é "curta"
segundo RF-173; e a quantidade de caracteres não brancos da linha seguinte é
maior ou igual ao teto de 1,5 vez a da linha atual. 🔒

RF-173 — Uma linha é considerada **curta** quando a soma dos comprimentos das
suas palavras não passa de um limite; o limite é P-40 normalmente, P-41 quando a
remoção de espaços está ativa, e desses valores subtrai-se P-42 quando a linha é
vertical. Adicionalmente, fora do modo de remoção de espaços, uma linha com até
P-43 palavras também é considerada curta. 🔒 **Motivo:** em japonês/chinês sem
espaços a contagem de palavras não significa nada, então só o número de
caracteres vale, e o limiar é menor porque cada caractere carrega mais
informação.

RF-174 — Uma linha classificada como título vira um bloco próprio, marcado como
título, e quebra o bloco em construção. Itens de lista têm precedência sobre
títulos.

**Continuação de bloco:**

RF-175 — Uma linha continua o bloco atual apenas se **todas** valerem: existe um
bloco em construção; a linha anterior não terminava frase (RF-177); e a linha
pode ser anexada ao bloco segundo o teste de tamanho de fonte (RF-176).

RF-176 — Teste de anexação por tamanho de fonte:
1. a linha candidata deve ser espacialmente adjacente à linha anterior;
2. o tamanho de fonte da candidata e a mediana dos tamanhos das linhas já no
   bloco devem existir e ser positivos;
3. a razão entre a candidata e essa mediana não pode exceder P-44; 🔒
4. a razão entre o maior e o menor tamanho considerando todo o bloco mais a
   candidata não pode exceder P-44. 🔒

RF-177 — Uma linha **termina frase** quando, removendo espaços à direita e
depois removendo repetidamente quaisquer caracteres de fechamento do conjunto
P-45, o último caractere restante é ponto final, interrogação, exclamação, ou
suas versões de largura total / ideográficas. Uma linha vazia ou que só contém
caracteres de fechamento não termina frase. 🔒

RF-178 — Quando uma linha termina frase, o bloco em construção é encerrado após
recebê-la.

RF-179 — Após montar todos os blocos, a caixa de cada bloco deve ser a união das
caixas das suas linhas, e as caixas de origem, de visualização e de conteúdo
devem ser inicializadas com esse mesmo valor.

**Pseudocódigo do agrupamento:**

```
agrupar(linhas, remover_espaços):
    componentes := união_busca_por_adjacência(linhas)
    ordenar_dentro_de_cada_componente(componentes)
    ordenar_componentes(componentes)

    blocos := vazio
    para cada componente:
        contexto_lista := tem_contexto_de_lista(componente)
        atual := nulo
        anterior := nulo
        para i, linha em componente:
            próxima := componente[i+1] ou nulo
            é_item := é_item_de_lista(linha, contexto_lista)
            é_título := NÃO é_item E ( título_explícito(linha)
                        OU (i == 0 E título_por_contexto(linha, próxima,
                                                          remover_espaços)) )

            se é_item:
                blocos += novo_bloco(linha)
                atual := nulo ; anterior := linha ; continuar

            se é_título:
                b := novo_bloco(linha) ; b.título := verdadeiro
                blocos += b
                atual := nulo ; anterior := linha ; continuar

            se atual == nulo OU anterior == nulo
               OU anterior.termina_frase()
               OU NÃO pode_anexar(atual, anterior, linha):
                atual := novo_bloco(linha)
                blocos += atual
            senão:
                atual.linhas += linha

            anterior := linha
            se linha.termina_frase(): atual := nulo

    recalcular_caixas(blocos)
    devolver blocos
```

### 15.3 Tratamento textual

RF-180 — Quando a remoção de espaços está ativa, todos os espaços devem ser
removidos do texto reconhecido antes de qualquer outro tratamento.

RF-181 — Quando o dicionário de correção está ativo, ele deve ser aplicado ao
texto reconhecido antes da tradução.

RF-182 — O dicionário de correção deve poder ser aplicado **repetidamente**, de
0 a P-46 passagens adicionais, para permitir correções encadeadas.

RF-183 — O dicionário deve ter um modo "por palavra": quando ativo, a
substituição só ocorre em limites de palavra; quando inativo, em qualquer
posição. **Motivo:** idiomas sem separador de palavra precisam do modo inativo.

RF-184 — O programa deve permitir escolher o arquivo de dicionário por nome e
oferecer um editor rápido: uma janela com o texto reconhecido atual pré-carregado
e um campo para a correção, que acrescenta o par ao arquivo e recarrega o
dicionário imediatamente.

RF-185 — O formato do arquivo de dicionário deve ser: uma linha `/s`, a linha do
texto original, a linha do texto corrigido, e uma linha em branco.

RF-186 — Fora do modo sobreposição, e fora do modo de depuração "uma linha por
tradução", e quando o serviço de tradução não é o banco de dados local, as
quebras de linha do texto reconhecido devem ser removidas antes da tradução —
substituídas por espaço, ou por nada quando a remoção de espaços está ativa. 🔒
**Motivo:** tradutores de máquina traduzem muito pior quando recebem uma frase
quebrada em várias linhas.

RF-187 — No modo sobreposição, as quebras de linha **não** devem ser removidas,
porque cada bloco já é uma unidade e a estrutura de linhas é usada no desenho.

RF-188 — No modo sobreposição, o texto que vai para o tradutor deve ser montado
como: para cada bloco, uma quebra de linha, o token separador do serviço, e o
texto do bloco. Assim uma única requisição carrega todos os blocos.

RF-189 — Quando há mais de uma área de OCR e a numeração de áreas está ativa, o
texto exibido deve ser prefixado com o número da área e " : "; quando está
inativa, com "- ". Quando há uma única área, nenhum prefixo é usado.

RF-190 — Traduções cujo conteúdo seja o marcador de "sem resultado" não devem ser
concatenadas ao texto exibido.

RF-191 — Blocos com texto reconhecido vazio não devem gerar entrada no texto
exibido.

**Parâmetros usados:** P-33 a P-46.

**Casos de erro:**
- Nenhuma linha reconhecida → nenhum bloco → texto vazio → segue o fluxo.
- Todas as linhas com tamanho de fonte 0 → nenhuma anexação é possível, cada
  linha vira um bloco.
- Dicionário ausente → nenhuma correção, sem erro.

**Critérios de aceite:**
- Um diálogo de três linhas de mesma fonte, próximas, sem pontuação final, vira
  **um** bloco.
- Um nome de personagem curto acima de um diálogo longo vira um bloco marcado
  como título, separado do diálogo.
- Uma lista de cinco itens começando com "•" vira cinco blocos.
- Um texto vertical japonês em três colunas é lido da direita para a esquerda.
- Uma frase terminada em "." seguida de outra frase gera dois blocos.
- Ligar e desligar a fusão de linhas altera apenas o agrupamento, nunca o texto
  reconhecido.

---

## 16. Detecção de mudança entre quadros 🔒

**Responsabilidade:** decidir, a cada ciclo, se vale a pena traduzir e redesenhar.
É o que permite rodar um laço de 300 ms sem torrar CPU nem estourar a cota dos
serviços de tradução.

**Entradas / Saídas:** recebe o texto reconhecido do ciclo atual e o do ciclo
anterior; devolve uma decisão: redesenhar tudo, ou apenas repintar.

**Requisitos:**

RF-192 — O programa **não** compara imagens entre quadros. A comparação é feita
sobre o **texto reconhecido concatenado** de todas as áreas. **Motivo:**
comparar pixels sinaliza mudança a cada animação, cursor piscando ou gradiente
de fundo; comparar texto sinaliza apenas quando o conteúdo mudou de verdade. 🔒

RF-193 — A comparação deve ser de igualdade exata de cadeia, sobre o texto
**depois** do tratamento textual (remoção de espaços, dicionário, junção de
linhas) e **antes** da tradução.

RF-194 — Quando o texto difere do anterior, **ou** quando o texto atual é vazio,
o ciclo deve executar o caminho completo: cópia para área de transferência,
memória de exibição, desenho, gravação em arquivo, leitura em voz alta.
**Motivo para tratar vazio como mudança:** quando o diálogo some, a tradução
precisa sumir junto. 🔒

RF-195 — Quando o texto é igual ao anterior, o ciclo **não** deve redesenhar o
conteúdo nem repetir efeitos colaterais.

RF-196 — Mesmo com texto igual, se passaram mais de P-47 desde o último repintar
ocioso, o programa deve forçar um repintar da janela de tradução nos modos
camada e sobreposição. **Motivo:** o texto pode estar igual mas a geometria
mudou — o usuário moveu a área de OCR, ou a janela alvo se moveu. 🔒

RF-197 — O repintar ocioso deve reutilizar os dados já calculados; não deve
disparar OCR, tradução nem análise de cor.

RF-198 — A memória do texto anterior deve ser atualizada apenas quando o caminho
completo é executado.

RF-199 — A memória do texto anterior deve ser local ao laço: ao parar e iniciar
de novo, ela recomeça vazia, garantindo que o primeiro ciclo sempre desenhe.

RF-200 — Assim que o texto difere do anterior, o programa deve retraduzir e
redesenhar **no mesmo ciclo**. É proibido exigir que a mudança se confirme em um
segundo quadro, aplicar média entre quadros, aguardar estabilização ou qualquer
outra forma de amortecimento. 🔒 **Motivo:** cada uma dessas técnicas acrescenta
no mínimo um ciclo inteiro de latência entre o texto aparecer na tela e a
tradução aparecer, e é exatamente essa latência que define se o produto
acompanha ou não um diálogo que passa. A instabilidade eventual do OCR é o preço
aceito por essa velocidade; ela se corrige no pré-processamento, nunca no tempo.

RF-201 — No caminho do motor local clássico, quando o serviço de tradução não é
o banco de dados local, a chamada ao tradutor só deve ocorrer se o texto for
diferente do anterior — uma segunda barreira antes da rede.

RF-202 — Em modo pontual, tanto o caminho completo quanto o caminho de texto
igual devem encerrar o laço e sinalizar parada. **Motivo:** "traduzir uma vez"
tem que parar mesmo que o texto seja idêntico ao da última vez.

RF-203 — Além da comparação de texto, existe uma segunda camada de descarte: a
janela de sobreposição mantém, por área, um registro do último retângulo de
área, posição de cliente, texto reconhecido e texto traduzido. Se todos forem
idênticos ao registro, o objeto de resultado **anterior** é reutilizado no
desenho, preservando os retângulos já calculados. Apenas as cores automáticas
são substituídas pelas novas. 🔒 **Motivo:** evita recalcular todo o layout e
faz a sobreposição parar de tremer entre quadros.

RF-204 — Registros de áreas que não apareceram no ciclo atual devem ser
removidos desse cache.

RF-205 — Quando o laço não consegue detectar mudança porque o motor de OCR não
estava pronto, o texto do ciclo anterior deve ser reutilizado, produzindo
"nenhuma mudança" e portanto nenhum trabalho.

**Comportamento detalhado:**

```
anterior := ""
último_repintar := 0

a cada ciclo:
    atual := texto_reconhecido_tratado()
    se atual != anterior OU atual == "":
        anterior := atual
        copiar_para_área_de_transferência_se_ativo()
        final := aplicar_memória_de_exibição(tradução)
        desenhar(final)
        gravar_arquivo_se_ativo()
        falar_se_ativo()
        se modo_pontual: encerrar_laço()
    senão:
        se agora - último_repintar >= P-47:
            último_repintar := agora
            repintar_sem_recalcular()
        se modo_pontual: encerrar_laço()
```

**Parâmetros usados:** P-47.

**Casos de erro:**
- Texto oscilando entre duas leituras por ruído de OCR → o programa redesenha a
  cada ciclo; é o comportamento correto, e o remédio é ajustar o filtro de cor.
- Motor de OCR devolvendo erro como texto → o erro é tratado como conteúdo e a
  comparação funciona normalmente (o erro é exibido uma vez e depois estabiliza).

**Critérios de aceite:**
- Com uma tela estática e o laço em 300 ms, o número de chamadas ao serviço de
  tradução após o primeiro ciclo é zero.
- Mover uma área de OCR com o texto estático faz a sobreposição reposicionar em
  no máximo P-47.
- Quando o diálogo do jogo desaparece, a tradução desaparece no ciclo seguinte.

---

## 17. Cache de resultados

**Responsabilidade:** evitar traduzir de novo o que já foi traduzido, tanto
dentro da sessão quanto entre sessões.

**Entradas / Saídas:** recebe texto de origem e identidade do serviço; devolve a
tradução conhecida ou nada.

**Requisitos:**

RF-206 — O programa deve manter uma **memória de resultados anteriores**
separada por serviço de tradução: o mesmo texto traduzido por serviços
diferentes gera entradas diferentes.

RF-207 — A memória deve ser consultada antes de qualquer chamada de rede.

RF-208 — A memória deve ser persistida em disco, um arquivo por serviço, e
recarregada na inicialização.

RF-209 — O formato do arquivo de memória deve ser: linha `/s`, texto de origem
(uma ou mais linhas), linha `/t`, texto traduzido (uma ou mais linhas), linha
`/e`, linha em branco. Ao carregar, o texto de origem deve ter espaços à direita
removidos.

RF-210 — A memória deve ter um limite de P-48 entradas por serviço. Ao atingir o
limite, todas as entradas daquele serviço devem ser descartadas e o arquivo
correspondente esvaziado. 🔒 **Motivo:** política simples e barata; não há LRU.

RF-211 — A gravação em disco deve ser assíncrona, acumulando as novas entradas e
gravando em modo anexar quando o laço termina.

RF-212 — Enquanto uma gravação está em andamento, leituras e escritas na memória
devem ser suspensas — a memória se comporta como vazia. **Motivo:** evita
corromper a lista que está sendo serializada. 🔒

RF-213 — O programa deve oferecer um comando para limpar toda a memória de
resultados, apagando também os arquivos.

RF-214 — Os serviços que **não** usam memória de resultados são: banco de dados
local e tradutor local por processo auxiliar. **Motivo:** já são consultas
locais instantâneas; cachear seria só desperdício de memória.

RF-215 — Antes da memória de resultados, o programa deve consultar a **coletânea
de tradução do usuário**: um conjunto de arquivos de pares que o usuário ativa
por caixas de seleção.

RF-216 — Os arquivos da coletânea devem ficar em uma pasta dedicada, com extensão
de texto, e a lista de arquivos ativos deve ser persistida nas opções avançadas.
Apenas arquivos que existem no disco devem ser mantidos na lista ao carregar.

RF-217 — Cada arquivo da coletânea pode conter uma seção de informação, exibida
ao usuário quando ele seleciona o arquivo na lista.

RF-218 — A coletânea deve ter dois modos de busca:
1. **Correspondência exata** — o par só se aplica se o texto for idêntico.
2. **Modo de banco de dados** — os arquivos ativos são concatenados em um
   arquivo temporário e consultados pelo mesmo mecanismo do banco de dados
   local, que permite correspondência parcial.

RF-219 — O modo de banco de dados só deve ser usado quando o idioma de OCR ativo
for inglês ou japonês. Em outros idiomas, cai para correspondência exata. 🔒

RF-220 — O modo de banco de dados deve ter uma opção de ignorar
maiúsculas/minúsculas.

RF-221 — A coletânea não deve ser usada quando o serviço de tradução é o próprio
banco de dados local. **Motivo:** seria consulta duplicada.

RF-222 — Deve existir uma **memória de exibição** independente do cache: mantém
as últimas P-49 traduções e as exibe empilhadas, da mais recente para a mais
antiga, separadas por linha em branco dupla.

RF-223 — Entradas da memória de exibição devem expirar após P-50 segundos. A
expiração é verificada do início da lista para o fim e para no primeiro item
ainda válido.

RF-224 — Quando o texto atual está vazio e a memória de exibição está ativa, o
texto exibido deve ser composto apenas pelas entradas ainda vivas. **Motivo:**
mantém o diálogo anterior legível enquanto não há texto novo na tela.

**Parâmetros usados:** P-48, P-49, P-50.

**Casos de erro:**
- Arquivo de memória corrompido → as linhas inválidas são ignoradas.
- Pasta de coletânea ausente → criada automaticamente.
- Arquivo da coletânea removido do disco → ele sai da lista ativa no próximo
  carregamento.

**Critérios de aceite:**
- Traduzir a mesma frase duas vezes gera uma única chamada de rede.
- Fechar e reabrir o programa preserva as traduções da sessão anterior para o
  mesmo serviço.
- Trocar de serviço de tradução faz a mesma frase ser traduzida de novo.
- Com a memória de exibição em 3 entradas e 10 segundos, três diálogos rápidos
  ficam visíveis simultaneamente e somem um a um.

---

## 18. Tradução

**Responsabilidade:** obter o texto traduzido a partir do serviço escolhido,
com o menor número possível de chamadas e tolerância a falhas.

**Entradas / Saídas:** recebe uma lista de textos de origem e o serviço ativo;
devolve a lista de traduções, na mesma ordem, ou uma mensagem de erro.

### 18.0 Posicionamento dos serviços

RF-225 — O serviço de tradução **padrão** é o tradutor web gratuito. É ele que o
programa oferece a quem nunca configurou nada, e é contra ele que a latência do
ciclo deve ser medida.

RF-226 — A tradução por modelo de linguagem é um recurso **secundário**: melhor
qualidade em troca de latência mais alta, cota e chave de API. Ela nunca deve ser
o padrão, nunca deve ser pré-selecionada, e nenhuma outra parte do programa pode
depender dela para funcionar.

RF-227 — Nenhum serviço de tradução pode executar modelo de inferência na máquina
do usuário. Todos os serviços são ou consulta local a arquivo (banco de dados,
coletânea), ou chamada de rede. **Motivo:** o computador já está rodando um jogo;
o orçamento local de CPU e de memória pertence ao OCR e ao desenho, e é isso que
sustenta o requisito de latência da Parte VII.

### 18.1 Protocolo comum

RF-228 — Um pedido de tradução com texto vazio deve devolver vazio sem chamar
nada.

RF-229 — Cada novo pedido de tradução deve cancelar o pedido anterior ainda em
curso. **Motivo:** se o conteúdo da tela mudou, a tradução antiga já não
interessa e segurá-la atrasa a nova.

RF-230 — Para cada texto de origem, o programa deve primeiro remover espaços à
direita, depois consultar a coletânea do usuário e a memória de resultados. Só
os textos não encontrados entram na requisição.

RF-231 — Quando há mais de um texto, todos os textos não encontrados devem ser
unidos em **uma única requisição**, cada um precedido pelo token separador do
serviço e seguido de quebra de linha.

RF-232 — Cada serviço tem seu próprio token separador: P-51 para os serviços
baseados em busca web e API convencional, P-52 para o serviço de tradução por
navegador embutido. Os tokens devem ser configuráveis remotamente (RF-417).

RF-233 — A resposta deve ser dividida pelo mesmo token e distribuída, em ordem,
aos textos que estavam faltando. Se a resposta tiver menos partes que textos, os
restantes ficam sem tradução.

RF-234 — Deve existir um modo "token avançado" que, quando ligado, envia um token
encurtado (removendo 3 caracteres do início se o token tem 7 ou mais, ou 2 se
tem 6) e, na resposta, remove das pontas de cada parte as repetições do primeiro
caractere do token, descartando partes que ficarem vazias. 🔒 **Motivo:** alguns
tradutores alteram o token; essa heurística tolera a alteração.

RF-235 — Cada tradução obtida por rede deve ser gravada na memória de resultados
imediatamente.

RF-236 — Quando o serviço devolve erro, a mensagem de erro deve ser devolvida
inteira, no lugar de todas as traduções, e o ciclo deve continuar.

RF-237 — O texto final devolvido ao chamador deve ser a concatenação, para cada
texto de origem, do token separador, da tradução e de uma quebra de linha.

RF-238 — Uma tradução em andamento deve poder ser cancelada e o cancelamento não
deve ser tratado como erro.

RF-239 — Deve existir uma opção de **tradução ponte**: traduzir do idioma de
origem para japonês e do japonês para o destino. Ela só se aplica quando o
idioma de origem não é japonês, e apenas a serviços que a declaram suportada.
🔒 **Motivo:** para alguns pares de idiomas, passar pelo japonês melhora o
resultado.

RF-240 — Deve existir uma opção de "ignorar tradução vazia": quando ligada, um
resultado vazio não substitui a tradução anterior na tela.

### 18.2 Serviços previstos

RF-241 — **Banco de dados local.** Consulta um dicionário de pares carregado de
arquivo. Deve primeiro consultar um dicionário em memória carregado no momento
de aplicar as configurações; se não encontrar, delega ao mecanismo nativo de
busca, que suporta correspondência parcial. Resultado igual ao marcador de "sem
resultado" vira vazio.

RF-242 — O banco de dados local deve ter uma opção de **correspondência parcial
em múltiplas linhas** e uma opção de ignorar maiúsculas/minúsculas.

RF-243 — O formato do arquivo de banco de dados deve ser o mesmo do arquivo de
memória de resultados (`/s`, origem, `/t`, destino, `/e`).

RF-244 — **Tradutor web gratuito.** Faz uma requisição HTTP GET a um endpoint
público de tradução, com o texto codificado na URL, e extrai as traduções do
primeiro elemento do vetor JSON devolvido, concatenando as partes separadas por
espaço.

RF-245 — Esse serviço deve usar o parâmetro de cliente de **alta qualidade** por
padrão. Se receber resposta 429 (limite excedido), deve trocar para o parâmetro
de **baixa qualidade** e repetir a requisição uma vez; se já estiver em baixa
qualidade, deve devolver erro explicando que a cota horária acabou.

RF-246 — O modo de baixa qualidade deve permanecer por P-53 e depois voltar
automaticamente ao modo normal.

RF-247 — Enquanto estiver em modo de baixa qualidade, a interface deve indicar
isso e o resultado deve ser prefixado com um marcador visível.

RF-248 — O tempo limite dessa requisição deve ser P-54.

RF-249 — **Tradutor comercial por chave de API (serviço de tradução coreano).**
Faz POST com corpo em formulário e cabeçalhos de identificador e segredo.

RF-250 — Esse serviço deve suportar **múltiplas chaves**: o usuário cadastra até
P-55 pares de credenciais e o programa alterna automaticamente para a próxima
quando a atual devolve erro de cota ou de autenticação.

RF-251 — Ao trocar de chave, o programa deve anexar ao resultado uma nota
informando qual chave passou a ser usada.

RF-252 — O programa deve marcar cada chave com um estado (normal, erro, limite),
exibir esses estados em uma janela de gerenciamento e ordenar a lista colocando
as chaves gratuitas antes das pagas, começando pela primeira em estado normal.

RF-253 — A janela de gerenciamento de chaves deve permitir adicionar, editar e
remover chaves, e marcar cada uma como gratuita ou paga.

RF-254 — **Tradutor web sem chave do mesmo fornecedor.** Faz POST a um endpoint
público com cabeçalhos de navegador. Após cada requisição deve aguardar um
intervalo aleatório entre 0 e P-56 antes de aceitar a próxima. 🔒 **Motivo:**
espaçar as chamadas reduz bloqueio por comportamento automatizado.

RF-255 — **Tradutor por planilha em nuvem.** Escreve o texto em uma linha
aleatória de uma planilha do usuário, com uma fórmula de tradução na coluna ao
lado, e lê o resultado. A planilha deve ter no mínimo P-57 linhas e 2 colunas.

RF-256 — Esse serviço exige autenticação por delegação do usuário, com o token
armazenado localmente. Deve haver um comando para apagar todos os tokens.

RF-257 — Se o token estiver ausente na inicialização, o programa deve perguntar
ao usuário se quer autenticar agora.

RF-258 — Esse serviço deve reportar erros distintos para: planilha inexistente,
falha de inicialização, e fórmula com erro de valor.

RF-259 — Esse serviço é o único que suporta tradução ponte no nível de serviço.

RF-260 — **Tradutor por navegador embutido.** Abre uma página de tradução em um
navegador embutido oculto, navega para uma URL montada com o par de idiomas e o
texto codificado, e extrai o resultado executando um trecho de script na página.

RF-261 — O texto enviado deve ter as barras escapadas e receber um sufixo
marcador P-58 em uma linha nova; a resposta é cortada nesse sufixo. 🔒
**Motivo:** identifica de forma inequívoca o fim da tradução, já que a página
mostra resultado parcial enquanto traduz.

RF-262 — Antes de cada tradução, o campo de resultado da página deve ser limpo
via script, com até 4 tentativas espaçadas de 50 ms.

RF-263 — O programa deve aguardar até que o resultado extraído seja diferente do
resultado anterior e diferente do valor sentinela, consultando a cada 50 ms.

RF-264 — O tempo limite deve ser P-59, ou P-60 quando a opção de alternativa
está ativa; a primeira tradução da sessão recebe P-61 adicionais. 🔒

RF-265 — Se o texto for idêntico ao da requisição anterior, o tempo limite deve
ser reduzido para P-62.

RF-266 — Antes de navegar, deve haver um atraso aleatório de até P-63; após
receber o resultado, um bloqueio aleatório de até P-56. 🔒

RF-267 — Deve existir uma opção "usar tradutor alternativo em caso de erro":
quando ativa e este serviço falha, a requisição é refeita no tradutor web
gratuito.

RF-268 — O usuário deve poder abrir a janela do navegador embutido para
inspecionar o estado (resolver captcha, aceitar cookies).

RF-269 — Fechar essa janela pelo usuário deve apenas ocultá-la, não destruí-la.

RF-270 — A URL base, o formato da URL e o script de extração devem ser
configuráveis remotamente (RF-417), porque a página muda com frequência.

RF-271 — **Tradutor comercial por chave de API (serviço europeu).** Faz POST com
corpo JSON contendo o texto, o idioma de origem e o de destino, e cabeçalho de
autorização com a chave. Deve suportar dois endpoints — gratuito e pago —
escolhidos pelo usuário.

RF-272 — Para esse serviço, os códigos de chinês devem ser normalizados para um
código genérico de chinês antes do envio. 🔒

RF-273 — **Tradutor por modelo de linguagem.** Faz POST a um endpoint de geração
de conteúdo, com: uma instrução de sistema, o texto do usuário, configurações de
segurança e configurações de geração.

RF-274 — A instrução padrão deve pedir tradução para o idioma de destino,
minimizar omissão de palavras, não usar honoríficos, declarar que personagens
têm 22 anos ou mais, preservar todos os símbolos, e devolver **somente** a
tradução. 🔒 **Motivo:** cada cláusula existe para corrigir um comportamento
observado do modelo — omissão, recusa, comentário extra.

RF-275 — O usuário deve poder acrescentar uma instrução própria, e escolher se a
instrução padrão continua sendo enviada junto. Quando as duas são usadas, a
personalizada vem primeiro e a padrão em seguida, separadas por espaço e quebra
de linha.

RF-276 — Todas as categorias de filtro de segurança devem ser configuradas para
não bloquear. 🔒

RF-277 — Quando a resposta indica bloqueio por conteúdo proibido, o programa
deve refazer a requisição no tradutor web gratuito, sem sinalizar erro ao
usuário.

RF-278 — Deve existir uma lista de modelos pré-definidos e uma opção
"personalizado" que aceita o nome do modelo digitado.

RF-279 — A lista de modelos e qual deles é o padrão devem ser **dados de
configuração**, atualizáveis sem recompilar o programa. **Motivo:** modelos de
linguagem são descontinuados e substituídos em prazos de meses; um nome de modelo
embutido no código transforma cada troca do fornecedor em uma nova versão do
programa.

RF-280 — Para modelos personalizados, a família deve ser deduzida do nome: nomes
começando com o prefixo de segunda geração usam o formato de configuração de
raciocínio antigo; os demais usam o novo. O porte "pro" deve ser deduzido pela
presença dessa palavra no nome. [INFERIDO] quanto ao critério ser suficiente.

RF-281 — O usuário deve escolher um **preset** de geração entre "padrão",
"econômico" e "personalizado":
- padrão: temperatura P-64, nível de raciocínio P-65, limite de saída P-66;
- econômico: temperatura P-67, nível de raciocínio P-68, limite de saída P-69;
- personalizado: valores livres dentro das faixas P-70 a P-75.

RF-282 — O nível de raciocínio deve ser traduzido para o formato do modelo: na
família antiga, níveis 0, 2 e 3 omitem o orçamento e níveis 1 usam orçamento
P-76 para modelos "pro" e 0 para os demais; na família nova, o nível vira um
rótulo (0 e 3 = alto, 1 = baixo para "pro" e mínimo para os demais, 2 = baixo
para "pro" e médio para os demais). 🔒

RF-283 — O tempo limite dessa requisição deve ser P-77.

RF-284 — **Tradutor local por processo auxiliar.** O programa deve iniciar um
processo separado que carrega uma biblioteca de tradução instalada no sistema, e
se comunicar com ele por canal nomeado.

RF-285 — Antes de iniciar o processo auxiliar, o programa deve encerrar
instâncias anteriores dele.

RF-286 — O protocolo do canal deve ser: mensagem de identificação do servidor,
depois repetição de "verificação de inicialização" até receber sucesso ou falha;
em seguida, comandos no formato `comando,dados` com resposta em uma mensagem.

RF-287 — Cada mensagem do canal deve ser precedida de dois bytes com o
comprimento (byte alto, byte baixo), com o conteúdo em codificação de 16 bits, e
truncada se exceder 65535 bytes.

RF-288 — A biblioteca de tradução deve ser localizada primeiro em uma subpasta
do programa e, se não estiver lá, pelo registro do sistema.

RF-289 — Se a biblioteca expuser a função de tradução em formato de 16 bits, ela
deve ser preferida; caso contrário, o texto deve ser codificado na página de
código P-78 antes de enviar. 🔒

RF-290 — Se o processo auxiliar não estiver disponível, o serviço deve devolver
uma mensagem informando isso, sem quebrar o laço.

RF-291 — Esse serviço deve consultar a coletânea do usuário antes de traduzir.

RF-292 — **API personalizada.** O usuário informa uma URL e o programa faz POST
com um corpo JSON contendo: um nome, o texto, o código do idioma de destino e o
código do idioma de origem. A resposta deve conter um campo de resultado, um
código de erro e uma mensagem de erro.

RF-293 — Código de erro diferente de "0" deve produzir erro com a mensagem
recebida.

RF-294 — O campo de resultado pode ser texto ou vetor de textos; quando vetor,
as partes devem ser concatenadas.

RF-295 — Além do formato padrão, o usuário deve poder definir **presets** de API
personalizada, cada um com: nome, URL, lista de cabeçalhos adicionais, modelo de
requisição e modelo de resposta.

RF-296 — No modelo de requisição, os marcadores `{OCR_TEXT}`, `{SOURCE_CODE}` e
`{RESULT_CODE}` devem ser substituídos pelos valores correspondentes, com o
texto escapado para JSON.

RF-297 — O modelo de requisição deve aceitar tanto JSON válido quanto uma sintaxe
relaxada `chave = valor` separada por vírgulas, convertida automaticamente para
JSON. A conversão deve preservar textos entre aspas, reconhecer booleanos,
números e nulo, e envolver os demais valores em aspas. Vetores devem ser
convertidos elemento a elemento.

RF-298 — Se o modelo não estiver envolvido por chaves, chaves devem ser
adicionadas automaticamente.

RF-299 — O JSON final deve ser validado antes do envio; se for inválido, o
serviço devolve erro descrevendo a falha de conversão.

RF-300 — No modelo de resposta, o marcador `{RESULT_TEXT}` indica qual chave
contém a tradução. O programa deve descobrir o nome dessa chave no modelo e,
depois, procurar recursivamente por essa chave na resposta real, em qualquer
nível de aninhamento.

RF-301 — Cada cabeçalho adicional deve estar no formato `nome: valor`; linhas
malformadas devem ser registradas e ignoradas.

RF-302 — Presets podem vir de dois lugares: uma lista editável na interface,
persistida em um arquivo próprio; e arquivos individuais colocados pelo usuário
em uma pasta dedicada. Os arquivos individuais têm precedência sobre entradas de
mesmo nome na lista editável.

RF-303 — Presets vindos de arquivo devem ser marcados visualmente, não podem ser
renomeados nem removidos pela interface, e são salvos de volta no arquivo de
origem.

RF-304 — Um arquivo de preset pode conter um único preset ou uma lista; nomes
duplicados dentro do mesmo conjunto devem ser ignorados com registro.

RF-305 — Nomes duplicados criados pela interface devem receber automaticamente um
sufixo numérico entre parênteses.

RF-306 — Cada preset de API personalizada deve aparecer como uma entrada
separada na lista de serviços de tradução, identificada como "Custom – <nome>".

RF-307 — Se o serviço de tradução salvo no perfil não existir mais na lista (por
exemplo, um preset removido), o programa deve cair para o banco de dados local.

### 18.3 Códigos de idioma

RF-308 — O programa deve manter uma tabela de idiomas com, para cada um: uma
chave, um nome exibido (localizável), o código usado pelo OCR, e os códigos
usados por cada serviço de tradução. Um código vazio significa que aquele
serviço não oferece aquele idioma, e o idioma não aparece na lista dele.

RF-309 — **Escopo inicial de idiomas.** Nesta primeira versão o programa traduz
de **japonês** e de **inglês** para **português do Brasil**. Apenas esses três
idiomas precisam existir na tabela, e apenas esses pares precisam ser oferecidos
ao usuário.

RF-310 — Esse escopo é uma decisão de produto, **não** uma limitação de
arquitetura. Nada no programa pode assumir que há dois idiomas de origem ou um
de destino: a tabela é dado (RF-029), o pipeline é agnóstico, e acrescentar um
idioma novo depois deve ser uma entrada nova na tabela e nada mais.

RF-311 — Cada idioma da tabela deve declarar, além dos códigos por serviço, as
propriedades de que o pipeline depende: se separa palavras por espaço, se admite
escrita vertical, e se escreve da direita para a esquerda. Essas propriedades —
e não o identificador do idioma — devem governar os comportamentos automáticos
descritos em RF-148 e RF-324.

RF-312 — O usuário deve poder acrescentar idiomas editando os dados de
configuração, sem recompilar o programa, informando o identificador, o nome
exibido, os códigos por serviço e as propriedades de RF-311.

RF-313 — Nas listas de idioma de destino, o idioma de destino padrão deve
aparecer em primeiro lugar.

RF-314 — O idioma de destino padrão é **português do Brasil**. Quando um serviço
de tradução usa um código diferente do canônico para esse idioma, a tabela
resolve a diferença; nenhum ponto do programa converte código de idioma por
regra especial embutida.

RF-315 — Ao trocar o idioma de OCR, o programa deve procurar o idioma
correspondente nas listas de cada serviço e selecioná-lo automaticamente.

RF-316 — A comparação de códigos de idioma deve tratar `en` e `en-US` como
equivalentes.

**Parâmetros usados:** P-51 a P-78.

**Casos de erro:**
- Rede indisponível → mensagem de erro no lugar da tradução; laço continua.
- Resposta em formato inesperado → mensagem de erro; laço continua.
- Todas as chaves esgotadas → mensagem da última tentativa.
- Cancelamento → sem erro, sem desenho.

**Critérios de aceite:**
- Com três blocos e um deles já em cache, a requisição de rede contém apenas
  dois textos.
- A resposta é redistribuída na ordem correta mesmo quando o cache respondeu
  pelo bloco do meio.
- Desligar a rede durante a tradução não trava nem fecha o programa.
- Um preset de API personalizada com modelo de resposta aninhado extrai a
  tradução corretamente.

---

## 19. Overlay e renderização

**Responsabilidade:** colocar a tradução na tela, em um dos três modos, de forma
legível e sem atrapalhar.

**Entradas / Saídas:** recebe os blocos com tradução, caixas, orientação, título
e cores automáticas, e a configuração de aparência; produz o desenho.

### 19.1 Comum aos três modos

RF-317 — O programa deve oferecer três modos de janela de tradução: **escuro**,
**camada** e **sobreposição**, trocáveis a qualquer momento.

RF-318 — Trocar de modo deve destruir a janela anterior e criar a nova.

RF-319 — Todas as janelas de tradução devem oferecer o estado "sempre no topo",
controlável pelo usuário.

RF-320 — Deve existir a opção "sempre no topo apenas durante a tradução": quando
ativa, a janela só fica no topo enquanto o laço roda, e volta ao normal quando
para.

RF-321 — Deve existir um atalho para ocultar e reexibir a janela de tradução.

RF-322 — Deve existir uma opção que faz o atalho de ocultar também iniciar ou
parar a tradução no mesmo gesto.

RF-323 — Deve existir uma opção de ordenação do texto: alinhamento à esquerda ou
centralizado, alterável pelo menu de contexto da janela em modo camada.

RF-324 — Deve existir suporte a escrita da direita para a esquerda, ativado por
opção e ligado automaticamente quando o idioma de destino declara essa
propriedade (RF-311). Nenhum idioma do escopo inicial a declara; a opção existe
para os idiomas que vierem depois.

RF-325 — Ao aplicar configurações, a janela de tradução ativa deve ser
reconfigurada sem ser destruída (exceto quando o modo mudou).

RF-326 — Fechar a janela de tradução pelo usuário deve apenas ocultá-la; ela
volta pelo menu da bandeja ou do controle remoto.

### 19.2 Modo escuro

RF-327 — O modo escuro deve exibir a tradução em uma caixa de texto rolável, com
fundo escuro, e um indicador visível de "parado" quando o laço não está rodando.

RF-328 — Quando a exibição do texto reconhecido está ativa, o modo escuro deve
mostrar a tradução, duas quebras de linha, o prefixo "OCR : " e o texto
reconhecido.

RF-329 — As quebras de linha recebidas em qualquer formato devem ser normalizadas
para o formato da plataforma antes de exibir.

RF-330 — O modo escuro deve aceitar uma fonte própria, configurável nas opções
avançadas, com queda para a fonte padrão do sistema quando não configurada.

RF-331 — A janela do modo escuro deve poder ser arrastada por qualquer ponto do
seu corpo.

### 19.3 Modo camada

RF-332 — O modo camada deve ser uma janela sem bordas, com transparência por
pixel, desenhada inteira a cada atualização.

RF-333 — Quando a tradução **não** está rodando, o fundo da janela deve ser
semitransparente (alfa P-79) e a janela deve receber cliques normalmente, com
uma borda de destaque desenhada para o usuário poder localizá-la e movê-la.

RF-334 — Quando a tradução está rodando, o fundo deve ficar totalmente
transparente (alfa 0) e a janela deve deixar os cliques passarem através dela.

RF-335 — Deve existir uma opção de **transparência forçada**, acessível pelo
menu de contexto: quando ativa, a janela permanece transparente e atravessável
mesmo depois que a tradução para.

RF-336 — O texto deve ser desenhado como caminho vetorial com contorno duplo: um
contorno externo de espessura P-80 na cor de contorno 2 e um contorno interno de
espessura P-81 na cor de contorno 1, ambos com junção arredondada, e o
preenchimento na cor do texto.

RF-337 — Quando a opção de fundo do texto está ativa e a tradução está rodando, um
retângulo deve ser pintado atrás do texto, medido pela extensão real do texto e
expandido em P-82 à esquerda, P-83 acima, P-84 na largura e P-85 na altura. 🔒

RF-338 — O texto deve ser desenhado dentro de um retângulo com margem de P-86 em
cima e à esquerda, e o mesmo valor descontado da largura e da altura.

RF-339 — O modo camada deve ter tamanho mínimo de P-87 por P-88 e ser
redimensionável arrastando qualquer borda ou canto, com zona sensível de P-89.

RF-340 — A posição e o tamanho do modo camada devem ser persistidos no perfil, e
validados contra os monitores ao carregar (RF-041).

RF-341 — O alinhamento vertical do texto deve ser configurável: no topo ou na
base. O alinhamento horizontal deve ser configurável: esquerda, centro ou
direita.

RF-342 — O modo camada deve poder exibir uma **mensagem de aviso temporária**
prefixada ao texto, com prazo de validade; após o prazo ela desaparece
automaticamente.

RF-343 — O programa deve emitir esse aviso quando, ao iniciar tradução em tempo
real com o modo escuro ou camada, e sem captura de janela, a janela de tradução
**intersecta** alguma área de OCR. **Motivo:** a janela estaria sendo capturada
e traduzida a si mesma. O aviso dura P-90.

### 19.4 Modo sobreposição

RF-344 — O modo sobreposição deve criar uma janela sem bordas, com transparência
por pixel, que cobre a união de todos os monitores.

RF-345 — Essa janela deve ser sempre no topo, incondicionalmente, e não deve
aparecer na barra de tarefas.

RF-346 — Essa janela deve ser marcada para **não aparecer em capturas de tela nem
em gravações** do sistema.

RF-347 — Quando o usuário aciona o atalho de captura de tela do sistema (tecla de
impressão de tela, ou a combinação de recorte de tela), a janela deve se tornar
capturável imediatamente e voltar a ser excluída após P-91. Durante esse
intervalo, atualizações de desenho devem ser suspensas. 🔒

RF-348 — Quando o modo de captura de janela anexada está ativo, a janela de
sobreposição deve ser capturável, porque nesse caso a fonte de imagem não é a
tela e não há risco de realimentação.

RF-349 — A janela deve ser redimensionada e reposicionada a cada desenho para
cobrir a união das áreas de OCR ampliada em P-92 em cada dimensão. 🔒

RF-350 — O retângulo de exibição deve ser **acumulativo enquanto a tradução
roda**: se o novo retângulo cabe no anterior, mantém-se o anterior; senão, usa-se
a união dos dois. Ao parar, o acúmulo é zerado. 🔒 **Motivo:** evita que a
janela encolha e recorte texto que ainda está desenhado.

RF-351 — O modo sobreposição só deve ser permitido com motores de OCR que
devolvem posição de palavra. Se o usuário tentar iniciar com um motor
incompatível, o programa deve informar e oferecer a página de ajuda.

RF-352 — Para cada bloco, o retângulo de origem em coordenadas de tela deve ser
calculado como: origem da área de OCR, menos metade da largura e da altura da
borda da moldura, mais as coordenadas do bloco divididas pelo fator de
ampliação, menos a posição da janela de sobreposição. Os cantos superior e
esquerdo usam piso; os inferior e direito usam teto.

RF-353 — No modo de captura de janela anexada, a origem deve ser limitada por
baixo à posição do cliente da janela capturada.

RF-354 — O retângulo de cada bloco deve ser recortado pelo retângulo da área de
OCR; blocos que ficarem sem área são descartados.

RF-355 — **Resolução de colisões:** enquanto houver dois blocos cujos retângulos
de visualização se sobrepõem, o par com maior área de interseção deve ser
separado. A separação é testada nos dois eixos e escolhe-se a que perde menos
área total.

RF-356 — A fronteira de separação, quando nenhum dos dois é título, deve ser
proporcional às áreas: começa no início da sobreposição e avança pela fração
`área_do_primeiro / (área_do_primeiro + área_do_segundo)` do comprimento da
sobreposição. 🔒

RF-357 — Quando um dos blocos é título e o outro não, o título deve preservar seu
retângulo inteiro e o outro cede. 🔒 **Motivo:** nomes de personagem são curtos
e precisam ficar legíveis.

RF-358 — O número máximo de iterações de resolução de colisão deve ser o
quadrado da quantidade de blocos multiplicado por 4, para garantir término.

RF-359 — O retângulo de **conteúdo** de um bloco deve ser o retângulo de
visualização reduzido em P-93 quando o contorno de fonte está ativo, e não
reduzido quando não está.

RF-360 — **Tamanho automático de fonte.** Quando ativo, o tamanho preferido de
cada bloco deve ser derivado do tamanho do texto original:
1. calcula-se o tamanho mediano das linhas do bloco (altura da caixa para
   blocos horizontais, largura para verticais; se zero, o tamanho de fonte
   estimado por RF-164);
2. calcula-se o tamanho mediano das linhas de todos os blocos **não título** da
   mesma área e mesma orientação — o "tamanho do corpo";
3. usa-se o tamanho próprio do bloco quando ele é título, ou quando é o bloco
   mais acima/à esquerda da área **e** seu tamanho é pelo menos P-94 vezes o
   tamanho do corpo; caso contrário usa-se o tamanho do corpo; 🔒
4. converte-se de pixels de imagem para pontos: dividir pelo fator de
   ampliação, multiplicar por 72, dividir pela resolução vertical em pontos por
   polegada, e multiplicar por P-95. 🔒

**Motivo do passo 3:** blocos pequenos dentro de um parágrafo não devem encolher
em relação ao parágrafo; mas um cabeçalho genuinamente maior deve manter seu
tamanho.

RF-361 — O tamanho preferido deve ser saturado entre o mínimo configurado e o
máximo configurado.

RF-362 — Antes de escolher o tamanho final, o programa deve tentar **expandir o
retângulo do bloco**, em duas etapas:
1. **Na direção de leitura** (só para blocos horizontais): se o texto traduzido,
   no tamanho preferido, quebra em mais linhas do que o original tinha, o
   retângulo pode crescer para a direita até o limite da área ou até o bloco
   vizinho mais próximo que se sobrepõe verticalmente. A largura mínima
   suficiente é encontrada por busca binária.
2. **Para caber a fonte**: se o texto no tamanho mínimo ainda não cabe, o
   retângulo pode crescer para baixo (blocos horizontais) ou para a esquerda
   (blocos verticais), até o limite da área ou o vizinho mais próximo, também
   por busca binária.

RF-363 — O tamanho final da fonte deve ser encontrado por busca binária entre o
mínimo e o preferido:
1. primeiro testa-se diretamente o tamanho preferido; se couber, usa-se ele
   (atalho para o caso comum); 🔒
2. senão, no máximo P-96 iterações de bissecção, parando quando a diferença
   entre os limites for menor ou igual a P-97. 🔒

RF-364 — O teste de "cabe" deve ser feito **posicionando cada linha exatamente
onde ela será desenhada** e verificando se os limites do desenho ultrapassam o
retângulo de conteúdo — não pela soma de alturas de linha. 🔒 **Motivo:** somar
alturas ignora o espaço que a última linha ocupa dentro da sua faixa, e o texto
acaba invadindo o bloco vizinho.

RF-365 — O avanço entre linhas deve ser a altura da fonte multiplicada por P-98.

RF-366 — Quando o contorno de fonte está ativo, os limites medidos devem ser
expandidos em P-99 antes da comparação. 🔒

RF-367 — Linhas compostas apenas de espaços devem ser ignoradas no teste, pois
não desenham nada.

RF-368 — A faixa de cada linha deve ser: para blocos horizontais, a largura
inteira do conteúdo, altura igual ao avanço, deslocada verticalmente pelo
índice multiplicado pelo avanço (com piso); para blocos verticais, a altura
inteira do conteúdo, largura igual ao avanço, posicionada a partir da **direita**
recuando o índice mais um multiplicado pelo avanço (com teto). 🔒

RF-369 — A quebra de linha deve ser feita caractere a caractere, procurando por
busca binária o maior prefixo que cabe na dimensão disponível menos uma folga de
P-100 vezes o tamanho da fonte. 🔒 **Motivo:** medir de 1 em 1 caractere torna a
busca do tamanho de fonte inviavelmente lenta; a monotonicidade do comprimento
garante que a busca binária dá o mesmo resultado.

RF-370 — Se nem um caractere couber, deve-se colocar um caractere na linha
mesmo assim, para garantir progresso.

RF-371 — Após quebrar uma linha, os espaços iniciais do restante devem ser
removidos.

RF-372 — Quebras de linha explícitas no texto traduzido devem ser respeitadas
antes da quebra automática.

RF-373 — A medição de comprimento deve considerar, para blocos horizontais, o
**maior** entre a largura do caminho vetorial e a largura medida pelo motor de
texto; para blocos verticais, a altura do caminho vetorial. 🔒

RF-374 — Durante um mesmo desenho, os resultados de medição e de quebra devem
ser memorizados em cache, com chave composta de texto, família e estilo da
fonte, tamanho em unidades de desenho, orientação, sinalizadores de formato e
alinhamento. O cache deve ser descartado ao fim do desenho. 🔒 **Motivo:** a
busca binária de tamanho de fonte repete as mesmas medições dezenas de vezes; o
cache é o que torna a sobreposição viável.

RF-375 — O modo vertical deve ser usado apenas quando a opção "preservar a
direção do original" está ativa **e** o bloco foi classificado como vertical.

RF-376 — No modo vertical, o formato de texto deve ativar simultaneamente
direção vertical e direção da direita para a esquerda.

RF-377 — O fundo de cada bloco deve ser pintado, quando a opção de fundo está
ativa e a tradução está rodando, cobrindo o retângulo de visualização inteiro.

RF-378 — A opacidade do fundo deve seguir a cor configurada pelo usuário apenas
quando a opção "usar transparência do fundo" está ativa; caso contrário o fundo
é opaco, preservando apenas os componentes de cor. 🔒

RF-379 — A janela de sobreposição deve ser desenhada inteira em um mapa de bits
reutilizado entre quadros, recriado apenas quando as dimensões mudam.
**Motivo:** arrastar uma área de OCR muda o tamanho a cada quadro; recriar o
mapa de bits toda vez esgota os recursos gráficos e a janela fica preta. 🔒

RF-380 — Nenhuma coleta de lixo forçada deve ocorrer durante o desenho.
**Motivo:** uma coleta bloqueante por quadro na thread de interface é
perceptível como travamento durante a tradução. 🔒

RF-381 — O desenho deve ser protegido por um bloqueio de reentrância, e esse
bloqueio deve ser liberado em qualquer caminho de saída, inclusive por exceção.

RF-382 — Se a janela de sobreposição ainda não tem identificador de sistema e o
pedido de desenho vem de outra thread, o desenho deve ser **abandonado**, não
adiado. **Motivo:** ler o identificador a partir de outra thread cria a janela
naquela thread, que não processa mensagens, e todo despacho subsequente para
essa janela trava para sempre. 🔒

RF-383 — Ao preparar a sobreposição para uma nova tradução, o programa deve
limpar os dados, zerar o retângulo acumulado, liberar os bloqueios, desenhar uma
vez e forçar uma sincronização com o compositor da área de trabalho.
**Motivo:** sem a sincronização, o primeiro quadro pode piscar. 🔒

RF-384 — Após um ciclo pontual, se o tempo de permanência configurado for maior
que zero, a sobreposição deve permanecer visível por esse tempo e depois voltar
ao estado normal (fundo visível, cliques ativos). Se for zero, volta
imediatamente.

RF-385 — Esse retorno deve ser cancelado se uma nova tradução começar nesse
intervalo, comparando um contador de tarefa incrementado a cada início.

RF-386 — A qualidade de renderização deve ser configurada para suavização de
texto, suavização de formas de alta qualidade, interpolação bicúbica de alta
qualidade e deslocamento de pixel de alta qualidade.

### 19.5 Fonte, cores e contorno

RF-387 — A família de fonte padrão do texto traduzido deve ser resolvida em tempo
de execução, não fixada por nome: usa-se a fonte de interface do sistema
operacional, e, se ela não estiver disponível, a primeira de uma lista de reserva
declarada nos dados de configuração. **Motivo:** o texto traduzido está sempre no
idioma de destino, e nenhum nome de fonte existe nas três plataformas alvo.
Fixar um nome faz o programa cair silenciosamente para uma fonte substituta
escolhida pelo sistema, que pode ter métricas muito diferentes — e as métricas
governam todo o cálculo de tamanho e de quebra de linha da sobreposição
(RF-357 a RF-372).

RF-388 — O tamanho de fonte padrão é P-127. 🔒

RF-389 — O usuário deve poder configurar quatro cores: cor do texto, cor do
contorno 1, cor do contorno 2, e cor de fundo (esta com canal alfa).

RF-390 — Deve haver um botão de restaurar cores padrão, que define os valores
P-101 a P-104.

RF-391 — A caixa de amostra de cor na interface nunca deve exibir componente
zero: valores 0 devem ser exibidos como 1. [INFERIDO] — a razão é evitar que a
cor de amostra seja interpretada como transparente.

RF-392 — Quando o contorno de fonte está **desativado** no modo sobreposição, o
estilo negrito deve ser removido da fonte usada. 🔒 **Motivo:** sem contorno, o
negrito engrossa demais e reduz a legibilidade sobre fundos claros.

RF-393 — Quando a cor de fonte é automática ou foi corrigida por contraste, as
cores de contorno devem ser derivadas automaticamente da cor de fonte:
- converte-se a cor de fonte para matiz, saturação e brilho;
- se o brilho é maior ou igual a 0,5 (fonte clara): contorno 1 recebe a mesma
  matiz com saturação reduzida em 0,05 e brilho reduzido em 0,1; contorno 2
  recebe preto;
- se o brilho é menor que 0,5 (fonte escura): contorno 1 recebe a mesma matiz
  com saturação aumentada em 0,05 e brilho aumentado em 0,1; contorno 2 recebe
  branco. 🔒

**Parâmetros usados:** P-79 a P-104.

**Casos de erro:**
- Bloco cujo retângulo de conteúdo fica com largura ou altura não positiva após
  a resolução de colisões → o bloco é registrado como recortado e não desenhado.
- Texto que não cabe nem no tamanho mínimo → é desenhado assim mesmo e marcado
  como recortado no registro de depuração.
- Falha de desenho vetorial → cai para desenho simples de texto sem contorno.

**Critérios de aceite:**
- Dois blocos adjacentes com traduções longas não escrevem um por cima do outro.
- Um nome de personagem curto mantém seu retângulo quando colide com o diálogo.
- Uma tela com 20 blocos é desenhada em menos de 50 ms na máquina de referência.
- Arrastar uma área de OCR por 30 segundos com a tradução rodando não deixa a
  janela de sobreposição preta.
- Pressionar a tecla de captura de tela do sistema com a sobreposição ativa
  produz uma imagem contendo a tradução.

---

## 20. Análise automática de cor 🔒

**Responsabilidade:** descobrir, a partir da imagem original, qual a cor do texto
e qual a cor do fundo de cada bloco, para que a tradução sobreposta pareça parte
do software original.

**Entradas / Saídas:** conforme 6.8.

**Requisitos:**

RF-394 — A análise só deve rodar quando o modo é sobreposição **e** a opção de
cor automática está ativa. Só nesse caso a imagem original é capturada.

RF-395 — A análise deve usar a imagem **original**, sem filtro nem binarização,
mas nas dimensões da imagem ampliada — os retângulos vindos do OCR devem ser
convertidos para o espaço da imagem original por escala em cada eixo, com piso
nos cantos superior/esquerdo e teto nos inferior/direito, saturados aos limites
da imagem.

RF-396 — A amostragem de pixels deve ser esparsa: o passo de amostragem é o teto
da raiz quadrada da área do retângulo dividida pelo número máximo de amostras,
com mínimo 1. O máximo de amostras é P-105 para regiões de fundo grandes e P-106
para regiões de palavra.

RF-397 — Pixels com canal alfa menor que P-107 devem ser ignorados.

RF-398 — Cores devem ser quantizadas para agrupamento descartando os 3 bits
menos significativos de cada componente (32 níveis por canal), e o valor final de
cada agrupamento é a **mediana** por componente das cores que caíram nele. 🔒

**Determinação da cor de fundo — três estratégias em cascata:**

RF-399 — **Estratégia 1 — bordas das palavras.** Para cada retângulo de palavra,
sondam-se oito sub-retângulos: as quatro faixas de borda (topo, base, esquerda,
direita) com espessura igual ao teto de P-108 vezes o lado menor, saturada entre
1 e P-109; e os quatro cantos, com largura igual ao mínimo entre a largura da
palavra e o máximo entre a espessura da faixa e o mínimo entre 4 e um terço da
largura (analogamente para a altura). 🔒

RF-400 — Em cada sonda, determina-se a cor dominante. As cores dominantes são
agrupadas; um agrupamento só é elegível se tiver pelo menos P-110 sondas e (pelo
menos 2 cantos **ou** pelo menos 5 sondas no total). Escolhe-se o agrupamento
com mais cantos, depois mais sondas, depois maior população. 🔒 **Motivo:** os
cantos de uma caixa de palavra são quase sempre fundo puro; exigir concordância
entre cantos evita capturar a cor do glifo.

RF-401 — O resultado por palavra é guardado como fundo local daquela palavra.
Para o fundo global do bloco, os fundos locais são agrupados e escolhe-se o que
tem apoio de pelo menos o teto de P-111 vezes o número de palavras; entre os
elegíveis, o de maior apoio, depois maior soma de sondas, depois maior
população. Se nenhum for elegível, a estratégia falha.

RF-402 — **Estratégia 2 — anéis ao redor das palavras.** Para cada palavra,
constrói-se um anel: o retângulo da palavra inflado por um preenchimento igual
ao teto de P-112 vezes o lado menor, saturado entre P-113 e P-114; recortado
pelo retângulo do bloco inflado em P-114; e recortado pela imagem. Amostram-se
os pixels desse anel **excluindo** qualquer pixel que caia dentro de qualquer
retângulo de palavra. As cores são agrupadas e escolhe-se a de maior apoio por
palavra, depois maior população.

RF-403 — **Estratégia 3 — cor dominante do bloco.** Amostra-se o retângulo do
bloco inteiro e escolhe-se a cor mais frequente após quantização; o valor final é
a mediana das cores daquele agrupamento. Em caso de empate na frequência,
desempata-se pela menor chave quantizada.

RF-404 — Se as três estratégias falharem, a análise devolve falha e o desenho usa
as cores configuradas pelo usuário.

**Determinação da cor da fonte:**

RF-405 — Para cada palavra, determina-se o fundo local: o valor da estratégia 1
se existir; senão, o anel daquela palavra; senão, o fundo global.

RF-406 — Amostram-se os pixels do retângulo da palavra. Um pixel só é candidato a
cor de fonte se seu contraste contra o fundo local for de pelo menos P-115. 🔒

RF-407 — Os candidatos são agrupados por cor quantizada, acumulando população e
soma de contraste. Registra-se, por agrupamento, em quantas palavras distintas
ele apareceu.

RF-408 — Escolhe-se o agrupamento com maior número de palavras de apoio, depois
maior população, depois maior contraste médio. 🔒 **Motivo:** a cor do texto
aparece em todas as palavras; um reflexo ou uma borda aparece em uma só.

RF-409 — Se nenhum candidato passar no contraste mínimo, usa-se preto ou branco —
o que der maior contraste contra o fundo — e marca-se o resultado como "recorreu
a alternativa".

RF-410 — A cor escolhida deve passar por uma verificação final de legibilidade:
se o contraste contra o fundo for menor que P-115, ela é substituída por preto
ou branco, o que der maior contraste, e o resultado é marcado como corrigido.

RF-411 — A razão de contraste deve ser calculada pela fórmula de luminância
relativa: `(luminância_maior + 0,05) ÷ (luminância_menor + 0,05)`, com a
luminância dada por `0,2126·R' + 0,7152·G' + 0,0722·B'`, onde cada componente é
linearizado por `c/12,92` se `c ≤ 0,04045` e por `((c+0,055)/1,055)^2,4` caso
contrário.

RF-412 — A correção final de legibilidade só deve ser aplicada quando a cor
automática está em uso, o fundo do texto está ativado, e o alfa efetivo do fundo
é maior que zero. **Motivo:** sem fundo pintado, não faz sentido corrigir contra
uma cor que não será desenhada.

RF-413 — A cor de fonte automática deve ser usada apenas se a opção "cor de fonte
automática" estiver ativa; a cor de fundo automática apenas se a opção "cor de
fundo automática" estiver ativa. As duas são independentes e ficam sob uma opção
mestre.

RF-414 — Quando a cor de fundo automática é usada, o canal alfa vem da cor de
fundo configurada pelo usuário e os componentes de cor vêm da análise.

RF-415 — Se a análise não produziu cor para o índice de bloco pedido, o desenho
deve registrar isso e usar as cores configuradas.

**Parâmetros usados:** P-105 a P-115.

**Casos de erro:**
- Imagem original ausente ou com dimensões incoerentes → análise não roda e as
  cores configuradas são usadas.
- Retângulo do bloco fora da imagem → interseção vazia → falha → cores
  configuradas.

**Critérios de aceite:**
- Texto branco sobre caixa de diálogo azul escura produz fonte branca e fundo
  azul escuro.
- Texto preto sobre fundo bege produz fonte preta e fundo bege.
- Texto com contorno claro sobre fundo claro produz uma cor de fonte com
  contraste de pelo menos P-115 contra o fundo escolhido.
- Um bloco sobre gradiente produz uma cor de fundo estável entre quadros
  consecutivos com o mesmo conteúdo.

---

## 21. Atualização automática e configuração remota

**Responsabilidade:** manter o programa e seus dados auxiliares atualizados, e
permitir corrigir remotamente parâmetros que quebram quando serviços externos
mudam.

**Entradas / Saídas:** recebe arquivos de texto de um endereço fixo na rede;
devolve atualizações aplicadas e parâmetros ajustados.

**Requisitos:**

RF-416 — A verificação de atualização deve ser opcional, controlada por uma
opção do usuário, e ocorrer durante a tela de abertura.

RF-417 — O programa deve baixar, na inicialização, um **arquivo de configuração
padrão remota** e aplicar dele: os tokens separadores de tradução por serviço, o
sinalizador de token avançado, e a URL base, o formato de URL e o script de
extração do tradutor por navegador embutido. **Motivo:** esses valores dependem
de páginas de terceiros que mudam sem aviso; poder corrigi-los remotamente evita
uma atualização do programa a cada mudança. 🔒

RF-418 — Valores ausentes ou vazios no arquivo remoto devem manter os valores
embutidos no programa.

RF-419 — A falha ao baixar a configuração remota não deve impedir a
inicialização.

RF-420 — O programa deve baixar um **arquivo de versão** e comparar a versão
local com a remota. A comparação deve distinguir dois tipos de atualização:
- **menor** — a versão local está dentro da faixa declarada como atualizável em
  linha; pode ser aplicada automaticamente;
- **maior** — fora dessa faixa; exige download manual.

RF-421 — Para atualização maior, o programa deve exibir uma mensagem com as
versões antiga e nova formatadas, e, se o usuário confirmar, abrir a página de
download.

RF-422 — Para atualização menor, antes de qualquer download, o programa deve
verificar se o arquivo de configuração publicado ao lado do executável novo
declara a mesma versão que o arquivo de versão. Se não declarar, o programa deve
concluir que a publicação ainda está em andamento e **abortar silenciosamente**,
sem perguntar nada ao usuário. 🔒 **Motivo:** os dois artefatos são publicados em
lugares diferentes e há uma janela em que um está novo e o outro não.

RF-423 — Para atualização menor, o programa deve baixar o arquivo de soma de
verificação publicado junto do executável. Se o endereço da soma não estiver
declarado, ou se o download falhar, a atualização deve ser **abortada**.
**Motivo:** verificação obrigatória; sem soma não há garantia de integridade.

RF-424 — Todas as URLs recebidas do arquivo de versão devem ser forçadas para
protocolo seguro antes de qualquer download.

RF-425 — A atualização menor deve ser executada por um **programa auxiliar**
separado, iniciado com: a versão nova, a URL do executável, a URL da página de
notas, as cadeias de texto localizadas, e a soma esperada. O programa principal
deve encerrar imediatamente após iniciar o auxiliar.

RF-426 — O auxiliar deve baixar o executável e o arquivo de configuração
correspondente, exibindo o progresso em porcentagem.

RF-427 — Após o download, o auxiliar deve calcular a soma de verificação do
arquivo baixado e compará-la com a esperada, sem diferenciar maiúsculas. Se não
houver soma esperada, ou se houver divergência, o auxiliar deve apagar os
arquivos baixados, registrar um marcador de falha, exibir a mensagem
correspondente e não substituir nada.

RF-428 — O marcador de falha deve conter o instante da falha, e o programa
principal deve, ao iniciar, recusar entrar em fluxo de atualização durante P-116
após esse instante. 🔒 **Motivo:** evita um ciclo infinito de baixar um arquivo
corrompido, falhar e tentar de novo a cada abertura.

RF-429 — Um marcador de falha ausente, malformado, ou com instante futuro, deve
ser tratado como "sem período de espera".

RF-430 — A substituição dos arquivos deve seguir: mover o antigo para um nome de
backup, mover o baixado para o nome definitivo. Cada movimento deve aguardar até
P-117 tentativas espaçadas de P-118 para o arquivo ser liberado, e falhar de
forma limpa se não conseguir.

RF-431 — Ao iniciar, o programa principal deve detectar a presença do arquivo de
backup deixado pelo auxiliar, esperar o processo anterior encerrar, e removê-lo.

RF-432 — O auxiliar deve, ao terminar com sucesso, oferecer abrir as notas da
versão e reiniciar o programa principal.

RF-433 — O programa deve verificar também a versão dos **dicionários de correção**
padrão (um por idioma) e baixá-los quando houver versão nova, gravando-os em
codificação UTF-8 sem marca de ordem de bytes e atualizando o arquivo local de
versões de dados.

RF-434 — A verificação de dicionários não deve ocorrer quando o programa está em
modo de encerramento forçado por atualização.

RF-435 — O programa deve exibir, na tela "sobre", a versão do programa, a data
de compilação e as versões dos dicionários instalados.

**Parâmetros usados:** P-116, P-117, P-118.

**Casos de erro:**
- Sem rede → nenhuma verificação, nenhuma mensagem, inicialização normal.
- Arquivo de versão malformado → exceção capturada, nenhuma atualização.
- Soma divergente → arquivos apagados, mensagem, período de espera registrado.

**Critérios de aceite:**
- Um arquivo baixado corrompido nunca substitui o executável em uso.
- Duas aberturas seguidas após uma falha de soma não tentam baixar de novo.
- Uma mudança apenas no arquivo de configuração remota altera o comportamento do
  tradutor por navegador sem exigir nova versão do programa.

---

## 22. Atalhos de teclado

**Responsabilidade:** permitir acionar as funções principais sem sair da
aplicação que está sendo traduzida.

**Entradas / Saídas:** recebe eventos de teclado do sistema inteiro; devolve
comandos.

**Requisitos:**

RF-436 — O programa deve interceptar o teclado **globalmente**, funcionando
mesmo quando nenhuma janela do programa tem foco.

RF-437 — O interceptador deve normalizar as variantes esquerda e direita dos
modificadores para um único código, de modo que `Shift` esquerdo e direito sejam
equivalentes.

RF-438 — Uma combinação é reconhecida quando o conjunto de teclas pressionadas
tem exatamente o mesmo tamanho e os mesmos elementos que a combinação
configurada, independentemente da ordem.

RF-439 — Combinações duplicadas são permitidas. Se a mesma combinação estiver
configurada para duas ações, vence a primeira encontrada na ordem de verificação
e a segunda nunca dispara — **sem recusar a configuração e sem avisar o
usuário**. A ordem de verificação deve ser estável e documentada, para que o
resultado seja previsível.

RF-440 — Uma tecla já presente no conjunto de pressionadas deve ser ignorada até
que seja solta, para não disparar a ação repetidamente com a repetição
automática do teclado.

RF-441 — Soltar qualquer tecla deve limpar o conjunto de pressionadas.

RF-442 — Cada combinação deve aceitar no máximo três teclas.

RF-443 — Os atalhos devem ficar inertes enquanto: a camada de seleção de área
está aberta; algum campo de captura de atalho está com foco; ou a janela de
opções avançadas está aberta.

RF-444 — Ações com atalho dedicado e seus padrões:

| Ação | Padrão |
|---|---|
| Iniciar/parar tradução em tempo real | `Ctrl+Shift+Z` |
| Traduzir uma vez | `Ctrl+Shift+C` |
| Área instantânea | `Ctrl+Shift+A` |
| Área rápida | `Ctrl+Shift+X` |
| Abrir editor de dicionário | `Ctrl+Shift+S` |
| Ocultar/exibir janela de tradução | `Ctrl+Shift+D` |
| Alternar área que segue o mouse | `Ctrl+Shift+F` |

RF-445 — Cada atalho deve oferecer botões de "restaurar padrão" e "limpar".

RF-446 — Um atalho vazio deve ser válido e nunca disparar.

RF-447 — Devem existir **atalhos avançados** adicionais, configurados em janela
separada:
- abrir perfil de configuração — até P-119 instâncias, cada uma com um arquivo
  de perfil associado escolhido por diálogo;
- alternar transparência forçada da janela de tradução;
- trocar o serviço de tradução para: banco de dados local, planilha em nuvem,
  tradutor web gratuito, tradutor comercial por chave, tradutor local por
  processo auxiliar, tradutor por navegador embutido, tradutor web sem chave.

RF-448 — Ao trocar de serviço por atalho, o programa deve: parar a tradução se
estiver rodando, aplicar o novo serviço, atualizar a interface, salvar o perfil,
exibir uma notificação na janela de tradução e retomar a tradução se estava
rodando.

RF-449 — Se o atalho de abrir perfil apontar para um arquivo inexistente, o
programa deve exibir uma mensagem nomeando o arquivo faltante na janela de
tradução.

RF-450 — Quando o atalho de tradução é acionado enquanto o laço roda, a parada
deve usar o prazo curto P-04 (RF-011).

RF-451 — Quando o atalho de "traduzir uma vez" é acionado enquanto o laço roda,
o programa deve pausar, redefinir as áreas e executar um ciclo pontual, também
com prazo curto.

RF-452 — Quando o atalho de área instantânea é acionado e a captura é da janela
ativa (e não de janela anexada), o programa deve aguardar até P-120 verificações
espaçadas de P-121 para que a janela em primeiro plano deixe de ser uma janela
do próprio programa. Se o prazo esgotar, deve exibir uma mensagem de tempo
esgotado e não traduzir. 🔒 **Motivo:** logo após desenhar a área, a janela em
primeiro plano ainda é a do programa, e a captura pegaria a moldura em vez do
jogo.

RF-453 — Os atalhos devem ser persistidos em arquivo próprio (RF-037) e os
atalhos avançados no arquivo de opções avançadas.

**Parâmetros usados:** P-04, P-119, P-120, P-121.

**Casos de erro:**
- Interceptador removido pelo sistema por lentidão → os atalhos param de
  funcionar até reiniciar; é justamente o que RF-011 e RF-450 previnem.
- Combinação idêntica configurada para duas ações → a primeira encontrada na
  ordem de verificação vence, **silenciosamente**. O programa não recusa a
  duplicata nem avisa (RF-439).

**Critérios de aceite:**
- Pressionar e segurar o atalho de tradução dispara a ação uma única vez.
- Configurar um atalho enquanto o campo tem foco não dispara nenhuma ação.
- Após 50 acionamentos rápidos de iniciar/parar, os atalhos continuam
  funcionando.

---

## 23. Área de OCR que segue o mouse

**Responsabilidade:** oferecer uma área de leitura que acompanha o cursor, para
consultar textos curtos sob demanda.

**Entradas / Saídas:** recebe a posição do cursor e o estado de ativação; devolve
um retângulo de captura reposicionado, que entra na lista de áreas como as
demais. Não captura imagem nem reconhece texto — apenas move a área.

**Requisitos:**

RF-454 — Deve existir uma área de OCR especial que, quando ativada, reposiciona-se
continuamente de modo que seu **centro** fique sob o cursor do mouse.

RF-455 — O reposicionamento deve ocorrer a cada P-122.

RF-456 — A posição deve ser calculada como: `x = cursor.x − borda − largura/2` e
`y = cursor.y − barra_de_título − altura/2`, usando as mesmas espessuras de
moldura de RF-073.

RF-457 — O recálculo das áreas de captura só deve ser disparado quando a posição
efetivamente mudou, e no máximo uma vez a cada P-123.

RF-458 — Ao ativar pela primeira vez, se ainda não existe a área dedicada, o
programa deve abrir a camada de seleção para o usuário desenhá-la, e ativar o
modo assim que ela existir.

RF-459 — Deve existir a opção **"usar somente a área que segue o mouse"**:
quando ativa e o modo está ligado, todas as outras áreas são ignoradas e apenas
esta é capturada. É o padrão.

RF-460 — Deve existir um **modo compatível** que, em vez de criar uma área
dedicada, move a área rápida existente ou, na falta dela, a primeira área
normal. Ao ligar esse modo, a área dedicada existente deve ser destruída.

RF-461 — Ao criar a área que segue o mouse com o gerenciamento de áreas fechado,
ela deve piscar visível por P-124 e depois ficar invisível, para o usuário saber
onde ela está.

RF-462 — Se a área alvo for destruída, o modo deve desligar-se automaticamente.

RF-463 — A área que segue o mouse deve ter borda de cor distinta das demais.

**Parâmetros usados:** P-122, P-123, P-124.

**Critérios de aceite:**
- Com o modo ativo e "somente esta área" ligado, apenas o conteúdo sob o cursor
  é traduzido.
- Mover o mouse rapidamente por 5 segundos não gera mais de ~50 recálculos de
  área.

---

## 24. Tradução da área de transferência

**Responsabilidade:** traduzir texto copiado por outros programas, permitindo uso
em conjunto com extratores de texto.

**Entradas / Saídas:** recebe o conteúdo de texto da área de transferência quando
ele muda; devolve a tradução para a janela de tradução ativa. Não usa captura de
tela nem OCR — entra no pipeline direto na etapa de tradução.

**Requisitos:**

RF-464 — O programa deve poder monitorar a área de transferência e traduzir
automaticamente todo texto novo que aparecer nela.

RF-465 — O monitoramento deve ser ligado e desligado por opção, e o recurso deve
ser inicializado apenas quando ligado.

RF-466 — Só conteúdo do tipo texto deve disparar tradução.

RF-467 — A tradução da área de transferência só deve ocorrer quando: o laço de
tradução está ocioso; o programa não está em meio a um carregamento ou aplicação
de configuração; o modo de janela **não** é sobreposição; e o texto é diferente
do último traduzido por esta via.

RF-468 — Uma tradução da área de transferência em andamento deve bloquear novas
até terminar.

RF-469 — Deve existir a opção de exibir uma mensagem "detectado — traduzindo"
enquanto a tradução ocorre.

RF-470 — Deve existir a opção de anexar o texto original ao final do resultado,
separado por duas quebras de linha.

RF-471 — O resultado deve ser exibido pela janela de tradução ativa e, se a
leitura em voz alta estiver ligada, também lido.

RF-472 — Aplicar configurações deve limpar o estado de "traduzindo pela área de
transferência".

RF-473 — Independentemente do monitoramento, o programa deve poder **copiar para
a área de transferência** o resultado de cada ciclo, em um de três formatos
selecionáveis: só o texto reconhecido, só a tradução, ou os dois separados por
duas quebras de linha.

RF-474 — A cópia deve ocorrer somente quando o texto mudou e quando a área de
transferência está livre; falhas de acesso à área de transferência devem ser
ignoradas silenciosamente.

RF-475 — Ao abrir o editor de dicionário, a cópia automática deve ser suspensa e
restaurada ao fechar. **Motivo:** o usuário vai usar a área de transferência
para editar.

**Critérios de aceite:**
- Copiar um texto em outro programa produz a tradução na janela do programa em
  menos de um ciclo de rede.
- Copiar o mesmo texto duas vezes seguidas produz uma única tradução.
- Com o modo sobreposição ativo, o monitoramento não dispara.

---

## 25. Leitura em voz alta

**Responsabilidade:** ler o resultado de cada ciclo em áudio, para quem prefere
ouvir a tradução em vez de desviar o olhar do jogo.

**Entradas / Saídas:** recebe o texto traduzido do ciclo e o modo de janela
ativo; não devolve nada (o efeito é o áudio). Não altera o texto exibido.

**Requisitos:**

RF-476 — O programa deve poder ler em voz alta o resultado de cada ciclo,
usando o sintetizador de voz do sistema.

RF-477 — Deve existir a opção "aguardar o fim da leitura anterior": quando ativa,
uma nova leitura é descartada se a anterior ainda está tocando; quando inativa, a
nova interrompe a anterior.

RF-478 — No modo sobreposição, os tokens separadores devem ser removidos do texto
antes da leitura.

RF-479 — A leitura deve ocorrer apenas quando o texto mudou.

RF-480 — Se o sintetizador não estiver disponível, a opção deve ficar inerte sem
gerar erro.

**Critérios de aceite:**
- Com "aguardar o fim" ligado e um texto longo, traduções sucessivas não se
  sobrepõem em áudio.

---

## 26. Localização da interface

**Responsabilidade:** fornecer todo texto exibido pelo próprio programa no idioma
da interface, a partir de dados e não de literais no código.

**Entradas / Saídas:** recebe uma chave de texto e o idioma ativo; devolve a
cadeia correspondente, ou a própria chave quando ela não existe na tabela. Não
tem efeito sobre a tradução do conteúdo do usuário — são dois sistemas
independentes.

**Requisitos:**

RF-481 — Toda a interface do programa deve ser traduzível, com os textos
carregados de uma tabela de dados embutida.

RF-482 — A tabela deve ser um arquivo separado por vírgulas, com uma coluna de
chave e uma coluna por idioma, tolerante a campos entre aspas contendo vírgulas
e quebras de linha.

RF-483 — O idioma de interface previsto nesta versão é o **português do
Brasil**. A tabela e o mecanismo de localização devem, ainda assim, ser
completos: acrescentar um idioma de interface é acrescentar uma coluna de dados,
sem tocar em código.

RF-484 — Quando o usuário não escolheu idioma, o programa deve derivar o idioma
do sistema operacional, com queda para o idioma inicial (RF-487) se não houver
correspondência na tabela.

RF-485 — Uma chave ausente na tabela deve resultar no próprio nome da chave sendo
exibido, para tornar a falta visível.

RF-486 — Trocar o idioma da interface deve exigir reinício do programa, e o
usuário deve ser avisado disso na hora da troca, **na língua nova**.

RF-487 — O idioma inicial da interface é o **português do Brasil**. O idioma da
interface é independente do idioma de destino da tradução: mudar um não muda o
outro.

RF-488 — Alguns itens de interface exigem reposicionamento após a tradução,
porque o texto traduzido tem largura diferente; o programa deve reposicioná-los
em relação ao controle anterior, com uma folga e uma posição mínima.

RF-489 — A tabela de localização deve ser um **arquivo de dados externo**,
distribuído junto do programa e editável diretamente, sem recompilar e sem
nenhuma etapa de exportação intermediária. Qualquer função futura de exportar
texto do programa deve igualmente gravar em arquivo externo, nunca embutir texto
no executável.

**Critérios de aceite:**
- Com o sistema em português e sem escolha explícita, a interface abre em
  português.
- Uma chave nova ainda não traduzida aparece como a própria chave, não como texto
  vazio.

---

## 27. Depuração e diagnóstico

**Responsabilidade:** tornar observável o que o programa decidiu em um ciclo, para
que erros de agrupamento, de cor e de layout possam ser investigados com
evidência em vez de impressão.

**Entradas / Saídas:** recebe o resultado completo de um ciclo — dados do OCR,
blocos, traduções — e, no modo sobreposição, também os valores finais do desenho;
devolve um arquivo estruturado por ciclo. Não altera nenhum comportamento do
pipeline quando desligado.

**Requisitos:**

RF-490 — Deve existir um modo de depuração, ativado por um controle escondido na
interface, que revela um painel com opções adicionais.

RF-491 — No modo de depuração devem estar disponíveis:
- **destravar velocidade** — ignora o intervalo entre ciclos e roda o laço o mais
  rápido possível;
- **mostrar resultados de cache** — prefixa as traduções vindas do cache com um
  marcador e a contagem de entradas;
- **traduzir uma linha por vez** — desativa o agrupamento de linhas em blocos;
- **mostrar áreas de palavra** — desenha retângulos semitransparentes sobre as
  caixas de origem em vez do fundo normal, e emite registros detalhados das
  decisões de agrupamento, cor e layout;
- **salvar resultado de análise** — grava um retrato completo do ciclo.

RF-492 — O retrato de análise deve ser um arquivo estruturado por ciclo, gravado
em pasta dedicada, com nome contendo data e hora até milissegundos, contendo:
instante, modo de janela, motor de OCR, serviço de tradução, texto reconhecido,
texto traduzido, e, por área: índice, se é instantâneo, retângulo da área,
retângulo do resultado, textos, cores automáticas, todas as linhas com suas
palavras e caixas, e todos os blocos com seus quatro retângulos.

RF-493 — No modo sobreposição, o retrato só deve ser gravado **depois** que o
desenho terminou, incluindo: retângulo da janela, opções de renderização em
vigor, e por bloco desenhado — texto, se é título, orientação, os quatro
retângulos, família, estilo e tamanho da fonte usada, tamanho preferido, tamanho
mínimo, tamanho estimado do original, cores de fonte, fundo e contornos, se
usou cor automática, se houve correção de contraste, as linhas após a quebra, o
avanço entre linhas, e se o bloco ficou recortado.

RF-494 — O retrato deve incluir também os tempos do desenho: total, cálculo de
tamanho e posição, layout e desenho, apresentação, e as contagens de acerto e
erro do cache de medição de texto.

RF-495 — Se um ciclo seguinte começar antes que o desenho do anterior tenha
completado o retrato, o retrato pendente deve ser gravado sem a parte de
desenho, e não descartado.

RF-496 — Deve existir uma opção de **gravar o resultado em arquivo de texto** a
cada ciclo, no formato do banco de dados (`/s`, reconhecido, `/t`, traduzido,
`/e`), para o usuário construir bancos de tradução a partir do uso real.

RF-497 — Deve existir uma opção de **exibir o texto reconhecido** junto da
tradução, útil para diagnosticar erros de OCR.

RF-498 — Deve existir um contador interno de tentativas de OCR e de traduções,
com um registro de mensagens acessível.

RF-499 — Deve existir um comando para limpar toda a memória de resultados
anteriores, desabilitado enquanto uma gravação está em curso.

RF-500 — O modo de depuração deve poder repassar seus sinalizadores para o
mecanismo nativo de pré-processamento: modo de depuração, mostrar substituições,
salvar captura, salvar resultado da captura.

**Critérios de aceite:**
- Ativar "salvar resultado de análise" e executar um ciclo no modo sobreposição
  produz um arquivo contendo o tamanho de fonte final de cada bloco.
- Ativar "destravar velocidade" faz o laço rodar sem o atraso configurado.
- Desativar o modo de depuração restaura o comportamento normal sem reiniciar.

---

# PARTE IV — PARÂMETROS E CALIBRAGEM 🔒

Todos os valores ajustáveis do programa. A coluna **Exposto** indica se o
usuário pode alterar o valor pela interface (**UI**), se ele vem de um arquivo de
configuração remota (**REMOTO**), ou se é fixo no código (**FIXO**).

As colunas de efeito são inferências ([INFERIDO] por natureza), preenchidas para
transformar cada número em conhecimento utilizável.

Todo valor marcado com 🔒 é **calibragem confirmada**: foi ajustado
empiricamente contra casos reais ao longo de anos de uso e deve ser reproduzido
exatamente. Não são pendências nem sugestões. A política para alterá-los está na
Parte XII.

## IV.1 — Ciclo de vida e temporização

| ID | Parâmetro | Valor | Unidade | O que controla | Efeito se aumentar | Efeito se diminuir | Exposto | 🔒 |
|---|---|---|---|---|---|---|---|---|
| P-01 | Permanência da tela de abertura | 0,7 | s | Tempo em que a tela inicial fica antes de começar a desaparecer | Abertura mais lenta | Verificações da inicialização podem não terminar a tempo | FIXO | 🔒 |
| P-02 | Desvanecimento da tela de abertura | 2,0 | s | Duração do fechamento da tela inicial | Abertura mais lenta | Transição abrupta | FIXO | |
| P-03 | Prazo de espera pela thread do laço | 3000 | ms | Quanto tempo esperar o laço morrer antes de desistir | Interface pode congelar ao parar | Mais falhas de "não parou a tempo", mudanças abortadas | FIXO | |
| P-04 | Prazo de espera vindo do interceptador de teclado | 250 | ms | Idem, quando o pedido vem do gancho global | Acima de ~300 ms o sistema remove o gancho e todos os atalhos morrem | Mais abortos de parada | FIXO | 🔒 |
| P-05 | Intervalo de ciclo — velocidade 1 (mais rápida) | 300 | ms | Tempo mínimo entre ciclos | Menos CPU, resposta mais lenta | Mais responsivo, mais CPU e mais chamadas de rede | UI | 🔒 |
| P-06 | Intervalo de ciclo — velocidade 2 | 1000 | ms | idem | idem | idem | UI | 🔒 |
| P-07 | Intervalo de ciclo — velocidade 3 | 1500 | ms | idem | idem | idem | UI | 🔒 |
| P-08 | Intervalo de ciclo — velocidade 4 | 2000 | ms | idem | idem | idem | UI | 🔒 |
| P-09 | Intervalo de ciclo — velocidade 5 (mais lenta) | 2500 | ms | idem | idem | idem | UI | 🔒 |
| P-125 | Sono quando o intervalo ainda não passou | 100 | ms | Granularidade do laço ocioso | Resposta ao pedido de parada mais lenta | Mais uso de CPU em ociosidade | FIXO | |
| P-126 | Intervalo de verificação de parada durante espera | 50 | ms | Frequência com que uma espera longa checa o pedido de parada | Parada demora mais | Mais despertares desnecessários | FIXO | |
| P-132 | Reinício do contador de tarefas | 100000 | — | Valor em que o contador de identificação de tarefa volta a zero | Nenhum efeito prático | Colisão de identificadores mais frequente | FIXO | |

## IV.2 — Áreas de captura

| ID | Parâmetro | Valor | Unidade | O que controla | Efeito se aumentar | Efeito se diminuir | Exposto | 🔒 |
|---|---|---|---|---|---|---|---|---|
| P-10 | Opacidade da camada de seleção | `max(alfa_do_fundo, 75) ÷ 255 × 0,15` | fração | Transparência da tela escurecida ao selecionar área | Mais escuro, mais difícil ver o jogo por baixo | Mais claro, difícil ver o retângulo em construção | Parcial (a cor é UI) | 🔒 |
| P-11 | Zona sensível de borda da moldura | 31 (= P-14 + P-15 + P-16), escalada por DPI | px | Distância da borda em que o cursor vira redimensionamento | Difícil arrastar molduras pequenas | Difícil acertar a borda | FIXO | 🔒 |
| P-12 | Tamanho mínimo da moldura | 50 × 50 | px | Menor área de OCR possível | Impede áreas muito pequenas úteis | Áreas degeneradas que não produzem imagem | FIXO | |
| P-13 | Intervalo mínimo entre recálculos por arraste | 0,3 | s | Taxa de atualização das áreas durante arraste | Arraste responde com atraso | Pipeline inundado, tradução engasga | FIXO | 🔒 |
| P-14 | Espessura da borda da moldura | 3 | px (base, ×DPI) | Desconto aplicado na conversão moldura→retângulo | Área capturada menor que a desenhada | Bordas entram na captura e viram ruído | FIXO | 🔒 |
| P-15 | Espessura da segunda borda | 8 | px (base, ×DPI) | Borda visual externa | Moldura mais grossa | Moldura menos visível | FIXO | 🔒 |
| P-16 | Altura da barra de título da moldura | 20 | px (base, ×DPI) | Desconto vertical na conversão | Área capturada perde a parte de cima | Barra de título entra na captura | FIXO | 🔒 |
| P-144 | Alinhamento de largura da captura | 4 | px | Múltiplo para o qual a largura é arredondada para cima | Mais margem lateral capturada | Linhas de imagem desalinhadas, OCR falha | FIXO | 🔒 |
| P-140 | Opacidade da área de exclusão | 0,7 | fração | Quanto a moldura vermelha deixa ver | Mais opaca | Menos visível | FIXO | |
| P-141 | Resolução de referência | 96 | pontos por polegada | Base do cálculo de escala de DPI | Elementos menores em telas de alto DPI | Elementos maiores | FIXO | |
| P-145 | Tamanho mínimo do retângulo de seleção | 4 | px | Abaixo disso, o arraste é descartado como clique | Cliques acidentais criam áreas | Difícil criar áreas pequenas | FIXO | |
| P-139 | Faixa de ampliação do conta-gotas | 1 a 4 | × | Zoom da pré-visualização de cor | Mais detalhe, janela maior | Menos precisão ao apontar pixel | UI | |

## IV.3 — Captura de imagem

| ID | Parâmetro | Valor | Unidade | O que controla | Efeito se aumentar | Efeito se diminuir | Exposto | 🔒 |
|---|---|---|---|---|---|---|---|---|
| P-17 | Quadros mantidos em reserva (janela anexada) | 5 | quadros | Quantos quadros ficam disponíveis para atender um pedido imediato | Mais memória, quadro possivelmente mais velho | Mais esperas por quadro novo | FIXO | 🔒 |
| P-18 | Período de captura automática ociosa | 10 | quadros | A cada quantos quadros o buffer é reabastecido sem pedido | Buffer esfria, mais espera | Mais cópias de memória desnecessárias | FIXO | 🔒 |
| P-19 | Idade máxima de um quadro reaproveitável | 0,1 | s | Até quando um quadro guardado ainda serve | Tradução de conteúdo desatualizado | Mais esperas | FIXO | 🔒 |
| P-20 | Intervalo de nova tentativa de captura | 2 | ms | Granularidade da espera por quadro | Latência maior | Consumo de CPU em espera ativa | FIXO | |
| P-31 | Intervalo de espera do motor de OCR do sistema | 2 | ms | Granularidade da espera pelo reconhecimento | Latência maior | Espera ativa mais intensa | FIXO | |

## IV.4 — Pré-processamento de imagem

| ID | Parâmetro | Valor | Unidade | O que controla | Efeito se aumentar | Efeito se diminuir | Exposto | 🔒 |
|---|---|---|---|---|---|---|---|---|
| P-21 | Limiar de binarização | 127 | 0–255 | Ponto de corte do modo limiar | Mais pixels viram fundo; texto claro some | Mais pixels viram texto; fundo vira ruído | UI | |
| P-22 | Fator de ampliação da imagem | 2,0 | × | Redimensionamento antes do OCR | Muito melhor com fonte pequena; muito mais CPU e memória | Mais rápido; fontes pequenas deixam de ser lidas | UI | 🔒 |
| P-23 | Ampliação mínima | 0,1 | × | Limite inferior do controle | — | — | UI | |
| P-24 | Ampliação máxima | 10,0 | × | Limite superior; valores lidos acima disso caem para P-22 | Risco de estouro de memória | — | UI | |
| P-25 | Passo da ampliação | 0,5 | × | Incremento do controle | Ajuste mais grosso | Ajuste mais fino | UI | |
| P-26 | Grupo HSV automático — texto escuro, faixa 1 | S 0–8, V 0–32 | 0–100 | Filtro sugerido pelo assistente para texto escuro | Aceita mais pixels; fundo entra | Perde partes do texto | UI (após aplicar) | 🔒 |
| P-27 | Grupo HSV automático — texto escuro, faixa 2 | S 95–100, V 0–32 | 0–100 | Segunda faixa, para texto escuro saturado | idem | idem | UI (após aplicar) | 🔒 |
| P-28 | Grupo HSV automático — texto claro | S 0–10, V 75–100 | 0–100 | Filtro sugerido para texto claro | idem | idem | UI (após aplicar) | 🔒 |
| P-146 | Matriz de luminância para tons de cinza | 0,30 / 0,59 / 0,11 | coeficientes | Conversão para cinza no modo limiar | — | — | FIXO | |

## IV.5 — OCR

| ID | Parâmetro | Valor | Unidade | O que controla | Efeito se aumentar | Efeito se diminuir | Exposto | 🔒 |
|---|---|---|---|---|---|---|---|---|
| P-29 | Limite mensal do OCR de nuvem | 950 | chamadas/mês | Quando o motor de nuvem para de aceitar chamadas | Risco de ultrapassar a cota gratuita e gerar cobrança | Motor para antes do necessário | UI | 🔒 |
| P-30 | Máximo de linhas do motor moderno | 1000 | linhas | Teto de linhas reconhecidas por imagem | Mais memória por chamada | Texto longo é truncado | FIXO | |
| P-32 | Razão vertical do motor moderno | 1,5 | — | Altura ÷ largura acima da qual a linha é considerada vertical | Menos linhas classificadas como verticais | Linhas horizontais curtas viram verticais | FIXO | 🔒 |

## IV.6 — Estruturação e pós-processamento 🔒

| ID | Parâmetro | Valor | Unidade | O que controla | Efeito se aumentar | Efeito se diminuir | Exposto | 🔒 |
|---|---|---|---|---|---|---|---|---|
| P-33 | Razão vertical de linha | 1,5 | — | Altura > largura × este valor ⇒ linha vertical | Texto vertical curto deixa de ser detectado | Palavras isoladas altas viram verticais | FIXO | 🔒 |
| P-34 | Razão máxima de fonte para adjacência | 1,3 | — | Diferença de tamanho tolerada entre duas linhas vizinhas | Blocos de tamanhos diferentes se fundem; título gruda no corpo | Parágrafos com variação sutil se quebram em vários blocos | FIXO | 🔒 |
| P-35 | Fator de intervalo no eixo de leitura | 1,25 | × tamanho de fonte | Distância máxima entre linhas para fundir | Blocos distantes se fundem; parágrafos separados viram um | Parágrafos com espaçamento generoso se quebram | FIXO | 🔒 |
| P-36 | Sobreposição transversal mínima | 0,25 | fração | Quanto duas linhas precisam se sobrepor lateralmente | Só colunas quase alinhadas se fundem | Colunas vizinhas se fundem erradamente | FIXO | 🔒 |
| P-37 | Fator de alinhamento de início | 2,0 | × tamanho de fonte | Alternativa à sobreposição: quão próximos os inícios precisam estar | Linhas indentadas distantes se fundem | Parágrafos com recuo se quebram | FIXO | 🔒 |
| P-38 | Tamanho de fonte quando não há amostra válida | 10 | px | Valor devolvido quando nenhuma caixa de palavra é válida | Agrupa mais em resultados degenerados | Agrupa menos | FIXO | 🔒 |
| P-39 | Marcadores de lista fortes | `• ● ○ ◦ ▪ ■ ‣ ⁃ · ・ ･` | conjunto | Caracteres que sempre iniciam item de lista | Mais linhas viram itens isolados | Listas viram um bloco só | FIXO | 🔒 |
| P-40 | Limite de caracteres para "linha curta" | 10 | caracteres | Base da detecção de título por contexto | Mais linhas viram título | Títulos deixam de ser detectados | FIXO | 🔒 |
| P-41 | Limite de caracteres com remoção de espaços | 6 | caracteres | Idem para idiomas sem separador de palavra | idem | idem | FIXO | 🔒 |
| P-42 | Desconto do limite para linhas verticais | 3 | caracteres | Subtraído de P-40/P-41 quando a linha é vertical | Menos títulos verticais | Mais títulos verticais | FIXO | 🔒 |
| P-43 | Máximo de palavras para "linha curta" | 3 | palavras | Critério alternativo, só fora do modo sem espaços | Mais títulos | Menos títulos | FIXO | 🔒 |
| P-148 | Razão de comprimento para título por contexto | 1,5 (teto) | — | A linha seguinte precisa ter ao menos esta razão de caracteres | Menos títulos detectados | Falsos títulos | FIXO | 🔒 |
| P-44 | Razão de fonte para anexar ao bloco | 1,2 | — | Tolerância de tamanho ao continuar um bloco já iniciado | Blocos absorvem linhas de tamanho diferente | Parágrafos se fragmentam | FIXO | 🔒 |
| P-45 | Caracteres de fechamento ignorados no fim de frase | `" ' ” ’ 」 』 】 ) 》` | conjunto | Removidos antes de checar pontuação final | Mais frases detectadas como terminadas | Falas entre aspas não terminam frase | FIXO | 🔒 |
| P-149 | Pontuação de fim de frase | `. ? ! 。 ？ ！` | conjunto | Marca o fim de um bloco | Mais quebras | Blocos longos demais | FIXO | 🔒 |
| P-150 | Comprimento máximo do token numerado | 3 | caracteres | Tamanho do "1", "12", "a)" reconhecido como marcador | Falsos itens de lista | Listas com numeração longa não detectadas | FIXO | 🔒 |
| P-46 | Passagens adicionais do dicionário de correção | 0 (faixa 0–3) | vezes | Quantas vezes o dicionário roda de novo sobre o próprio resultado | Correções encadeadas; mais CPU; risco de laço de substituição | Correções que dependem de outra não acontecem | UI | |

**Não há heurística alternativa de agrupamento.** O algoritmo do capítulo 15 é o
único. Não existe modo legado, nem correção de caixa de linha por tipo de glifo,
nem calibragem de reserva a ser recuperada em caso de resultado ruim. Quando o
agrupamento erra, o ajuste se faz nos parâmetros P-33 a P-45, não trocando de
algoritmo.

## IV.7 — Detecção de mudança e cache

| ID | Parâmetro | Valor | Unidade | O que controla | Efeito se aumentar | Efeito se diminuir | Exposto | 🔒 |
|---|---|---|---|---|---|---|---|---|
| P-47 | Intervalo de repintar ocioso | 1000 | ms | Frequência de redesenho quando o texto não mudou | Sobreposição demora a acompanhar o movimento da área | Desenho desnecessário consome CPU e pode piscar | FIXO | 🔒 |
| P-48 | Máximo de entradas na memória de resultados | 10000 | entradas | Quando descartar todo o cache de um serviço | Mais memória e arquivos maiores | Perde cache com mais frequência, mais chamadas de rede | FIXO | 🔒 |
| P-49 | Quantidade da memória de exibição | 5 (faixa 1–10) | traduções | Quantas traduções ficam empilhadas na tela | Mais contexto, mais poluição visual | Menos contexto | UI | |
| P-50 | Tempo de vida da memória de exibição | 10 (faixa até 200) | s | Quanto tempo cada tradução permanece empilhada | Texto antigo persiste demais | Contexto some rápido demais | UI | |

## IV.8 — Tradução

| ID | Parâmetro | Valor | Unidade | O que controla | Efeito se aumentar | Efeito se diminuir | Exposto | 🔒 |
|---|---|---|---|---|---|---|---|---|
| P-51 | Token separador padrão | `//////` | texto | Separa blocos em uma única requisição | Token mais longo, mais tokens gastos | Maior chance de colidir com o texto real | REMOTO | 🔒 |
| P-52 | Token separador do tradutor por navegador | `@@@@@@` | texto | idem | idem | idem | REMOTO | 🔒 |
| P-151 | Sinalizador de token avançado | desligado | booleano | Ativa a heurística de token encurtado e limpeza de repetições | Tolera tradutores que alteram o token; risco de cortar texto legítimo | Divisão falha se o tradutor alterar o token | REMOTO | 🔒 |
| P-53 | Duração do modo de baixa qualidade | 1 | hora | Quanto tempo o tradutor gratuito fica no endpoint alternativo | Qualidade ruim por mais tempo | Volta cedo e leva 429 de novo | FIXO | 🔒 |
| P-54 | Tempo limite do tradutor web gratuito | 2000 | ms | Espera máxima pela resposta | Ciclos travados em rede lenta | Falhas em conexões normais | FIXO | 🔒 |
| P-55 | Máximo de chaves de API alternáveis | 20 | chaves | Quantas credenciais entram no rodízio | Mais cota agregada | Menos alternativas quando uma esgota | FIXO | |
| P-56 | Atraso aleatório após requisição | 0 a 650 | ms | Espaçamento entre chamadas a serviços web sem chave | Menos risco de bloqueio, mais latência | Mais risco de bloqueio por comportamento automatizado | FIXO | 🔒 |
| P-57 | Linhas mínimas da planilha de tradução | 50 | linhas | Espaço de trabalho na planilha do usuário | Menos colisão entre requisições simultâneas | Requisições concorrentes se sobrescrevem | FIXO | 🔒 |
| P-58 | Sufixo marcador do tradutor por navegador | `^^^^` | texto | Marca o fim do texto para saber que a tradução terminou | Mais robusto, mais caracteres enviados | Falso positivo de "terminou" | FIXO | 🔒 |
| P-59 | Tempo limite normal do tradutor por navegador | 5 | s | Espera pela tradução na página | Ciclos longos | Traduções perdidas | FIXO | 🔒 |
| P-60 | Tempo limite com alternativa ativa | 3 | s | Idem, quando há tradutor de reserva | Demora mais para cair na reserva | Cai na reserva cedo demais | FIXO | 🔒 |
| P-61 | Acréscimo na primeira tradução | 5 | s | Compensa o carregamento inicial da página | Primeira tradução demora mais | Primeira tradução falha | FIXO | 🔒 |
| P-62 | Tempo limite quando o texto se repete | 1,5 | s | Espera reduzida quando a requisição é idêntica à anterior | Espera desnecessária | Desiste antes de a página atualizar | FIXO | 🔒 |
| P-63 | Atraso aleatório antes de navegar | 0 a 140 | ms | Espaçamento antes de trocar a URL | Menos detecção, mais latência | Mais risco de bloqueio | FIXO | 🔒 |
| P-136 | Tentativas de limpar o campo de resultado | 4, a cada 50 ms | tentativas | Garante que a página não devolva o resultado anterior | Mais latência | Lê o resultado anterior como se fosse novo | FIXO | 🔒 |
| P-137 | Intervalo de sondagem do resultado | 80 | ms | Frequência com que o resultado é lido | Latência maior | Mais chamadas de script na página | FIXO | |
| P-64 | Temperatura — preset padrão | 20 (=0,20) | 0–100 | Aleatoriedade do modelo de linguagem | Tradução mais criativa e menos literal | Mais literal e repetitiva | UI | 🔒 |
| P-65 | Nível de raciocínio — preset padrão | 0 | 0–3 | Esforço interno do modelo | Melhor qualidade, muito mais lento e caro | Mais rápido, mais erros | UI | 🔒 |
| P-66 | Limite de saída — preset padrão | 4000 | tokens | Tamanho máximo da resposta | Traduções longas cabem; custo maior | Traduções longas são cortadas | UI | 🔒 |
| P-67 | Temperatura — preset econômico | 0 | 0–100 | idem | idem | idem | UI | 🔒 |
| P-68 | Nível de raciocínio — preset econômico | 1 | 0–3 | idem | idem | idem | UI | 🔒 |
| P-69 | Limite de saída — preset econômico | 2000 | tokens | idem | idem | idem | UI | 🔒 |
| P-70 | Temperatura personalizada — mínimo | 0 | 0–100 | Limite do controle | — | — | UI | |
| P-71 | Temperatura personalizada — máximo | 100 | 0–100 | Limite do controle | — | — | UI | |
| P-72 | Raciocínio personalizado — mínimo | 0 | 0–3 | Limite do controle | — | — | UI | |
| P-73 | Raciocínio personalizado — máximo | 3 | 0–3 | Limite do controle | — | — | UI | |
| P-74 | Limite de saída personalizado — mínimo | 500 | tokens | Limite do controle | — | — | UI | |
| P-75 | Limite de saída personalizado — máximo | 10000 | tokens | Limite do controle | — | — | UI | |
| P-152 | Limite de saída personalizado — padrão | 1000 | tokens | Valor inicial do controle | — | — | UI | 🔒 |
| P-153 | Valores iniciais do preset personalizado | iguais aos do preset padrão (P-64, P-65, P-66) | — | O que os controles mostram quando o usuário escolhe "personalizado" pela primeira vez | — | — | FIXO | |
| P-76 | Orçamento de raciocínio para modelos "pro" (família antiga) | 512 | tokens | Substitui o nível mínimo em modelos maiores | Mais lento e caro | Modelos "pro" recusam raciocínio zero | FIXO | 🔒 |
| P-77 | Tempo limite do modelo de linguagem | 300 | s | Espera máxima pela geração | Ciclo pode ficar preso muito tempo | Traduções longas falham | FIXO | 🔒 |
| P-78 | Página de código do tradutor local | 932 | — | Codificação usada quando a biblioteca não expõe interface de 16 bits | — | Texto japonês corrompido | FIXO | 🔒 |
| P-135 | Tamanho máximo de mensagem do canal nomeado | 65535 | bytes | Truncamento da comunicação com o processo auxiliar | — | Textos longos truncados | FIXO | |
| P-138 | Intervalo de sondagem de inicialização do canal | 250 | ms | Frequência da verificação de prontidão do processo auxiliar | Inicialização mais lenta | Mais mensagens no canal | FIXO | |
| P-143 | Intervalo de sondagem da resposta do canal | 50 | ms | Frequência com que a tradução local é verificada | Latência maior | Espera ativa mais intensa | FIXO | |
| P-134 | Capacidade inicial dos acumuladores de texto | 8192 | caracteres | Pré-alocação nas chamadas nativas | Mais memória por chamada | Realocações durante o processamento | FIXO | |

## IV.9 — Janelas de tradução

| ID | Parâmetro | Valor | Unidade | O que controla | Efeito se aumentar | Efeito se diminuir | Exposto | 🔒 |
|---|---|---|---|---|---|---|---|---|
| P-79 | Alfa do fundo da camada quando parada | 190 | 0–255 | Visibilidade da janela em modo camada fora da tradução | Mais visível, mais atrapalha | Difícil localizar e mover a janela | FIXO | 🔒 |
| P-80 | Espessura do contorno externo | 5 | px | Contorno de maior alcance ao redor do texto | Texto mais legível sobre fundo claro; mais "pesado" | Texto se perde no fundo | FIXO | 🔒 |
| P-81 | Espessura do contorno interno | 2 | px | Contorno próximo ao glifo | Mais definição | Menos contraste de borda | FIXO | 🔒 |
| P-82 | Expansão do fundo à esquerda (modo camada) | 8 | px | Margem do retângulo de fundo | Fundo mais folgado | Texto encosta na borda do fundo | FIXO | 🔒 |
| P-83 | Expansão do fundo acima (modo camada) | 4 | px | idem | idem | idem | FIXO | 🔒 |
| P-84 | Expansão da largura do fundo (modo camada) | 16 | px | idem | idem | idem | FIXO | 🔒 |
| P-85 | Expansão da altura do fundo (modo camada) | 8 | px | idem | idem | idem | FIXO | 🔒 |
| P-86 | Margem do texto no modo camada | 15 | px | Recuo do texto em relação à borda da janela | Menos área útil | Texto colado na borda | FIXO | |
| P-87 | Largura mínima do modo camada | 200 | px | Limite de redimensionamento | — | Janela inutilizável | FIXO | |
| P-88 | Altura mínima do modo camada | 100 | px | idem | — | idem | FIXO | |
| P-89 | Zona sensível de redimensionamento das janelas de tradução | 30 | px | Faixa em que o cursor vira redimensionamento | Difícil arrastar a janela | Difícil redimensionar | FIXO | |
| P-133 | Posição e tamanho padrão do modo camada | (20, altura_da_tela − 300), 973 × 192 | px | Valores usados quando não há posição salva ou ela é inválida | — | — | FIXO | 🔒 |
| P-90 | Duração do aviso de sobreposição de janela | 10 | s | Quanto tempo o alerta fica prefixado ao texto | Alerta persistente atrapalha a leitura | Usuário não vê o alerta | FIXO | |
| P-91 | Janela capturável após atalho de captura de tela | 5000 | ms | Tempo em que a sobreposição aparece em prints | Sobreposição aparece em prints indesejados | Usuário não consegue capturar a tradução | FIXO | 🔒 |
| P-92 | Fator de folga do retângulo da sobreposição | 1,3 | × | Ampliação da união das áreas para dimensionar a janela | Janela maior que o necessário | Texto expandido é recortado nas bordas | FIXO | 🔒 |
| P-93 | Redução para o retângulo de conteúdo com contorno | 4 | px por lado | Espaço reservado para o contorno do texto | Menos área útil | Contorno é cortado | FIXO | 🔒 |
| P-154 | Redução inicial do retângulo de conteúdo | 4 | px por lado | Valor inicial antes do ajuste de layout | idem | idem | FIXO | |
| P-94 | Razão para o bloco líder preservar seu tamanho | 1,3 | × | Quando o primeiro bloco é grande o bastante para ser tratado como cabeçalho | Cabeçalhos são reduzidos ao tamanho do corpo | Blocos normais mantêm tamanhos inconsistentes | FIXO | 🔒 |
| P-95 | Escala do tamanho de fonte derivado do original | 1,15 | × | Ajuste fino entre o tamanho medido e o tamanho desenhado | Texto maior que o original | Texto menor e mais difícil de ler | FIXO | 🔒 |
| P-96 | Iterações da busca de tamanho de fonte | 9 | iterações | Precisão da bissecção | Mais preciso, desenho mais lento | Tamanho subótimo | FIXO | 🔒 |
| P-97 | Precisão mínima da busca de fonte | 0,25 | pt | Onde a busca para | Tamanho impreciso | Iterações desnecessárias | FIXO | 🔒 |
| P-98 | Fator de avanço entre linhas | 1,2 | × altura da fonte | Espaçamento vertical do texto desenhado | Mais respiro, cabe menos texto | Linhas coladas | FIXO | 🔒 |
| P-99 | Folga de contorno na medição | 2,5 | px | Margem adicionada aos limites medidos quando há contorno | Fonte fica menor que o necessário | Contorno ultrapassa o bloco | FIXO | 🔒 |
| P-100 | Folga da quebra de linha | 1,2 × tamanho da fonte | px | Reserva descontada da largura disponível ao quebrar | Quebra cedo, linhas curtas | Última palavra estoura a borda | FIXO | 🔒 |
| P-131 | Permanência do resultado após modo pontual | 5 (faixa 0+) | s | Quanto tempo a sobreposição fica visível após um ciclo pontual | Tradução permanece mais tempo na tela | Some antes de o usuário ler | UI | 🔒 |
| P-129 | Tamanho mínimo de fonte automática | 10 (mínimo do controle: 5) | pt | Piso da fonte no modo sobreposição | Texto nunca fica minúsculo, mas é recortado | Texto ilegível de tão pequeno | UI | 🔒 |
| P-130 | Tamanho máximo de fonte automática | 50 (mínimo do controle: 5) | pt | Teto da fonte | Traduções curtas ficam enormes | Traduções não aproveitam o espaço | UI | 🔒 |
| P-127 | Tamanho de fonte padrão | 15 | pt | Tamanho inicial do texto traduzido | — | — | UI | 🔒 |
| P-163 | Família de fonte padrão | fonte de interface do sistema | — | Família inicial do texto traduzido; ver RF-387 | — | — | UI | |
| P-128 | Tamanho mínimo de fonte na interface | 8 | pt | Piso do controle de tamanho | — | — | UI | |
| P-101 | Cor de texto padrão | 255, 255, 255 | RGB | Cor inicial do texto traduzido | — | — | UI | 🔒 |
| P-102 | Cor de contorno 1 padrão | 192, 192, 192 | RGB | Contorno interno | — | — | UI | 🔒 |
| P-103 | Cor de contorno 2 padrão | 0, 0, 0 | RGB | Contorno externo | — | — | UI | 🔒 |
| P-104 | Cor de fundo padrão | alfa 170; 0, 0, 0 | ARGB | Fundo atrás do texto | Mais opaco, mais legível, esconde mais o jogo | Mais transparente, menos legível | UI | 🔒 |
| P-155 | Cor da borda de destaque da janela parada | 40, 134, 249; espessura 3 | RGB, px | Moldura desenhada quando a tradução não está rodando | — | — | FIXO | |
| P-156 | Cor de limpeza do quadro da sobreposição | alfa 0; 240, 248, 255 | ARGB | Fundo transparente do mapa de bits | — | — | FIXO | |
| P-157 | Cor de destaque das áreas de palavra em depuração | alfa 90; 0, 0, 0 | ARGB | Retângulos desenhados no modo de diagnóstico | — | — | FIXO | |

## IV.10 — Análise automática de cor 🔒

| ID | Parâmetro | Valor | Unidade | O que controla | Efeito se aumentar | Efeito se diminuir | Exposto | 🔒 |
|---|---|---|---|---|---|---|---|---|
| P-105 | Máximo de amostras para fundo do bloco | 65536 | pixels | Densidade da amostragem no retângulo inteiro | Mais preciso, mais lento | Cor de fundo instável entre quadros | FIXO | 🔒 |
| P-106 | Máximo de amostras por palavra | 4096 | pixels | Densidade da amostragem em caixas pequenas | Mais preciso, mais lento | Cor de fonte errada em textos finos | FIXO | 🔒 |
| P-107 | Alfa mínimo do pixel considerado | 128 | 0–255 | Ignora pixels quase transparentes | Ignora pixels válidos em janelas translúcidas | Pixels de sombra entram na estatística | FIXO | 🔒 |
| P-108 | Razão da faixa de borda da palavra | 0,15 | fração do lado menor | Espessura das sondas de borda | Sonda invade o glifo | Sonda pega poucos pixels e falha | FIXO | 🔒 |
| P-109 | Espessura máxima da faixa de borda | 4 | px | Teto da espessura | Sonda invade o glifo em fontes grandes | Amostra pequena demais | FIXO | 🔒 |
| P-110 | Sondas mínimas para aceitar um fundo local | 3 | sondas | Rigor da estratégia de bordas | Estratégia falha mais e cai para o anel | Aceita fundo errado | FIXO | 🔒 |
| P-111 | Apoio mínimo entre palavras para o fundo global | 0,4 | fração das palavras | Quantas palavras precisam concordar | Estratégia falha e cai para o anel | Fundo global errado em blocos heterogêneos | FIXO | 🔒 |
| P-112 | Razão do preenchimento do anel | 0,2 | fração do lado menor | Largura do anel ao redor da palavra | Anel invade a palavra vizinha | Anel pega poucos pixels | FIXO | 🔒 |
| P-113 | Preenchimento mínimo do anel | 1 | px | Piso da largura do anel | — | Anel degenerado | FIXO | 🔒 |
| P-114 | Preenchimento máximo do anel | 4 | px | Teto da largura do anel | Anel sai do bloco | Anel estreito demais | FIXO | 🔒 |
| P-115 | Contraste mínimo texto/fundo | 2,5 | razão de luminância | Filtro dos candidatos a cor de fonte e correção final | Só cores muito contrastantes passam; recorre mais a preto/branco | Aceita cores ilegíveis | FIXO | 🔒 |
| P-158 | Quantização de cor | 3 bits descartados por canal (32 níveis) | — | Granularidade do agrupamento de cores | Agrupa cores distintas | Não agrupa variações de compressão | FIXO | 🔒 |
| P-159 | Apoio de cantos exigido | 2 cantos **ou** 5 sondas | — | Critério de aceitação da sonda de borda | Mais rigor, mais quedas para o anel | Aceita cor do glifo como fundo | FIXO | 🔒 |
| P-160 | Coeficientes de luminância relativa | 0,2126 / 0,7152 / 0,0722 | — | Cálculo do contraste | — | — | FIXO | |
| P-161 | Constantes de linearização sRGB | 0,04045 / 12,92 / 1,055 / 0,055 / 2,4 | — | Conversão para luminância linear | — | — | FIXO | |
| P-162 | Constante de contraste | 0,05 | — | Somada às luminâncias na razão de contraste | — | — | FIXO | |

## IV.11 — Atualização, atalhos e recursos auxiliares

| ID | Parâmetro | Valor | Unidade | O que controla | Efeito se aumentar | Efeito se diminuir | Exposto | 🔒 |
|---|---|---|---|---|---|---|---|---|
| P-116 | Período de espera após falha de verificação de integridade | 10 | min | Bloqueio de novas tentativas de atualização | Usuário fica mais tempo sem receber a correção | Volta ao ciclo de baixar arquivo corrompido | FIXO | 🔒 |
| P-117 | Tentativas de mover arquivo bloqueado | 10 | tentativas | Espera pela liberação do executável antigo | Atualização demora mais para falhar | Falha em máquinas lentas | FIXO | |
| P-118 | Intervalo entre tentativas de mover | 500 | ms | idem | idem | idem | FIXO | |
| P-119 | Atalhos de abertura de perfil | 4 | atalhos | Quantos perfis podem ter atalho dedicado | Mais flexibilidade, interface maior | Menos perfis acessíveis por teclado | FIXO | |
| P-120 | Verificações de janela ativa antes do instantâneo | 15 | verificações | Espera pelo retorno do foco ao jogo | Espera mais longa antes de desistir | Captura a moldura em vez do jogo | FIXO | 🔒 |
| P-121 | Intervalo entre essas verificações | 100 | ms | idem | idem | idem | FIXO | 🔒 |
| P-122 | Intervalo do temporizador da área que segue o mouse | 30 | ms | Suavidade do acompanhamento | Movimento aos saltos | Mais CPU | FIXO | 🔒 |
| P-123 | Intervalo mínimo de recálculo da área que segue o mouse | 100 | ms | Taxa de reenvio das áreas ao pipeline | Área capturada fica atrás do cursor | Pipeline inundado | FIXO | 🔒 |
| P-124 | Exibição breve da área que segue o mouse | 500 | ms | Quanto tempo ela pisca ao ser criada | Atrapalha mais | Usuário não vê onde ela ficou | FIXO | |

## IV.12 — Padrões de configuração

Valores iniciais de cada opção, aplicados ao restaurar padrões.

| Opção | Padrão |
|---|---|
| Modo de janela de tradução | sobreposição |
| Serviço de tradução | tradutor web gratuito |
| Motor de OCR | motor de reconhecimento moderno embarcado |
| Idioma de OCR (origem) | inglês |
| Idioma de destino | português do Brasil |
| Idioma da interface | português do Brasil |
| Conjunto de dados do motor clássico | `eng` |
| Modo rápido do motor clássico | desligado |
| Exibir texto reconhecido | ligado |
| Gravar resultado em arquivo | desligado |
| Ignorar maiúsculas/minúsculas no banco de dados | desligado |
| Correspondência parcial no banco de dados | desligado |
| Copiar para a área de transferência | desligado |
| Formato da cópia | só o texto reconhecido |
| Velocidade | 2 (1000 ms) |
| Arquivo de banco de dados | `empty.txt` |
| Arquivo de dicionário | `myDic.txt` |
| Usar dicionário | ligado |
| Dicionário por palavra | ligado |
| Erosão | desligada |
| Grupos de cor | 1 grupo, todos os valores zerados |
| Filtro RGB / HSV / limiar | todos desligados |
| Limiar | 127 |
| Áreas de OCR | nenhuma |
| Áreas de exclusão | nenhuma |
| Ordenação do texto | à esquerda |
| Remoção de espaços | desligada |
| Captura da janela ativa | desligada |
| Fundo do texto | ligado |
| Numeração de áreas | desligada |
| Ampliação | 2,0 |
| Leitura em voz alta | desligada |
| Aguardar fim da leitura | desligada |
| Posição/tamanho da janela camada | não definidos (−1) |
| Fusão de linhas na sobreposição | desligada |
| Preservar direção do original | desligada |
| Cor automática (mestre) | ligada |
| Cor de fundo automática | ligada |
| Cor de fonte automática | ligada |
| Contorno de fonte na sobreposição | desligado |
| Usar transparência do fundo | desligada |
| Tamanho automático de fonte | desligado |
| Alinhamento inferior (camada) | desligado |
| Alinhamento à direita (camada) | desligado |
| Sempre no topo só durante a tradução | desligado |
| Ignorar tradução vazia | desligado |
| Ocultar avançado (atalho de ocultar também traduz) | desligado |
| Memória de exibição | desligada |
| Controle remoto sempre no topo | desligado |
| Modo bandeja | desligado |
| Borda amarela na captura de janela | desligada |
| Direção da direita para a esquerda | desligada (ligada automaticamente para árabe, hebraico, urdu e persa) |
| Modo compatível da área que segue o mouse | desligado |
| Usar somente a área que segue o mouse | ligado |
| Cor de seleção de área | preto |
| Cor de fundo da seleção de área | branco |
| Coletânea em modo banco de dados | ligado |
| Coletânea ignora maiúsculas/minúsculas | ligado |
| Tradução ponte | desligada |
| Tradutor alternativo em caso de erro | ligado |
| Códigos de idioma da API personalizada iguais aos do tradutor web | ligado |
| Idioma de origem da API personalizada | `en` |
| Idioma de destino da API personalizada | `pt-BR` |
| URL da API personalizada | `http://localhost:8080/translator` |
| Instrução personalizada do modelo de linguagem | vazia |
| Modelo de linguagem personalizado | `gemini-2.0-flash` |
| Desativar instrução padrão | desligado |
| Passagens do dicionário | 0 |
| Tradução da área de transferência | desligada |
| Exibir original da área de transferência | desligado |
| Exibir "traduzindo" da área de transferência | desligado |
| Priorizar OCR de nuvem em modo pontual | desligado |
| Limite mensal do OCR de nuvem | 950 |
| Verificação de atualização | ligada |
| Janela de tradução sempre no topo | ligada |

---

# PARTE V — INTERFACE DO USUÁRIO

Descreve função e disposição, não aparência. O programa novo terá visual
diferente; o comportamento e os agrupamentos precisam ser os mesmos.

## V.1 — Janela principal

Uma janela com sete abas, na seguinte ordem fixa:

| Índice | Aba | Conteúdo |
|---|---|---|
| 0 | Configuração básica | OCR, tradução, imagem |
| 1 | Texto | Fonte, cores, alinhamento, pré-visualização |
| 2 | Configuração adicional | Captura, velocidade, perfis, opções avançadas |
| 3 | Tradução | Pares de idiomas e credenciais por serviço |
| 4 | Outros | Atalhos, ajuda, links |
| 5 | Configuração rápida | Assistente |
| 6 | Depuração | Só visível após ativar o modo de depuração |

RF-501 — Ao abrir, o programa deve selecionar a aba de configuração rápida
(índice 5), a menos que o usuário tenha marcado "usar a aba básica como padrão".

RF-502 — A janela principal deve poder ser arrastada por qualquer área vazia do
seu corpo, além da barra de título.

RF-503 — A janela principal deve conter, sempre visíveis: o botão **aplicar**, o
botão de doação, o título com a versão do programa, e o **indicador de memória
em uso** (RF-558 a RF-560), este último clicável para abrir o detalhamento.

RF-504 — O botão **aplicar** deve: limpar o estado de teclas pressionadas,
descartar o backup de áreas temporárias, aplicar todos os valores da interface à
configuração, salvar o perfil principal, e confirmar com uma mensagem. Durante o
processo, a janela de tradução deve sair temporariamente do estado "sempre no
topo" para que os diálogos apareçam.

RF-505 — A janela principal deve ajustar a altura e a largura das abas conforme
a escala de DPI da tela na primeira exibição.

### Aba 0 — Configuração básica

**Bloco OCR:**
- Caixa de seleção do motor de OCR (lista com os cinco motores).
- Três caixas de verificação: exibir resultado do OCR, gravar resultado em
  arquivo, copiar para a área de transferência — cada uma com dica de ajuda.
- Um painel por motor, mutuamente exclusivo, exibido conforme a seleção:
  - **motor clássico**: campo do conjunto de dados de idioma; caixa de seleção
    de idioma (inglês, japonês, outro); caixa "modo rápido".
  - **motor do sistema**: caixa de seleção de idioma, preenchida com os idiomas
    instalados; botão "adicionar idioma" que abre as configurações de idioma do
    sistema.
  - **motor moderno**: caixa de seleção de idioma, preenchida conforme RF-151.
  - **motor de nuvem**: caixa de seleção de idioma (com opção "automático" no
    topo); botão de configuração da credencial; texto explicativo.
  - **motor por ambiente interpretado**: caixa de seleção de idioma; botão de
    instalação.

**Bloco tradução:**
- Caixa de seleção do serviço de tradução, que inclui os presets de API
  personalizada como entradas próprias.
- Botão de ajuda que abre a documentação de tradução.
- Um painel por serviço, mutuamente exclusivo:
  - **banco de dados**: nome do arquivo; ignorar maiúsculas; correspondência
    parcial em múltiplas linhas.
  - **tradutor web gratuito**: texto informativo e indicador de estado
    (qualidade normal ou reduzida).
  - **tradutor web sem chave**: texto informativo.
  - **tradutor comercial por chave**: campos de identificador e segredo; botão
    de gerenciamento de chaves.
  - **planilha em nuvem**: endereço da planilha, identificador e segredo de
    cliente; botão de apagar todos os tokens.
  - **tradutor por navegador embutido**: texto informativo; indicador de estado;
    botão "verificar estado" que abre a janela do navegador.
  - **tradutor comercial europeu**: campo de chave; dois botões de opção
    (endpoint gratuito / pago).
  - **modelo de linguagem**: campo de chave; caixa de seleção do modelo.
  - **tradutor local por processo auxiliar**: texto informativo.
  - **API personalizada**: texto informativo apontando para as opções avançadas.

**Bloco dicionário:** caixa "usar dicionário"; campo do nome do arquivo; caixa
"por palavra".

**Bloco correção de imagem:** três caixas mutuamente exclusivas (RGB, HSV,
limiar), com o campo de limiar ao lado da terceira; caixa "erosão"; botão "ver
resultado da imagem"; caixa de seleção do grupo de cor com os itens especiais
"adicionar" e "remover" nas duas primeiras posições; campos R, G, B e S1, S2,
V1, V2; rótulo com a contagem de grupos.

RF-506 — Os campos numéricos de cor devem aceitar apenas dígitos e a tecla de
retrocesso; ao perder o foco, um campo vazio vira "0" e valores acima do máximo
são saturados.

RF-507 — A caixa de seleção de grupo de cor deve ter dois itens fixos no topo —
adicionar e remover. Selecionar "adicionar" cria um grupo novo, o seleciona, e
adiciona uma entrada ativa em todas as áreas. Selecionar "remover" apaga o grupo
atual, renumera os seguintes e remove a entrada correspondente de todas as
áreas; se houver apenas um grupo, a remoção é ignorada.

### Aba 1 — Texto

- Botão de escolha de fonte (abre o seletor do sistema) e campo numérico de
  tamanho, saturado entre o mínimo e o máximo do controle.
- Quatro amostras clicáveis de cor: texto, contorno 1, contorno 2, fundo. A do
  fundo abre um seletor com canal alfa; as outras abrem o seletor padrão.
- Botão de restaurar cores padrão.
- Quatro caixas de verificação: centralizar, remover espaços do resultado do
  OCR, usar cor de fundo, exibir número da área.
- Uma **pré-visualização ao vivo** que desenha um texto de exemplo com a fonte,
  as cores, o contorno, o preenchimento de fundo e o alinhamento atuais.

RF-508 — A pré-visualização deve refletir imediatamente qualquer mudança nos
controles desta aba, sem exigir "aplicar".

RF-509 — O texto de exemplo deve conter caracteres latinos, japoneses e
numerais, e um trecho que demonstre o formato de múltiplas áreas de OCR — com
prefixo numérico quando a numeração está ativa e com "-" quando não está.

RF-510 — Quando a remoção de espaços está marcada, a pré-visualização deve
mostrar o texto sem espaços.

### Aba 2 — Configuração adicional

- **Captura de imagem:** caixa "capturar da janela ativa"; campo de ampliação
  com botão de restaurar padrão; botão "capturar de janela anexada".
- **Velocidade:** cinco botões de opção, do mais rápido ao mais lento, com texto
  explicativo.
- **Janela de tradução:** três botões de opção (escuro, camada, sobreposição);
  caixa "sempre no topo".
- **Arquivo de configuração:** botões de carregar, salvar e restaurar padrões.
- **Busca de configurações:** botão que abre o navegador de configurações da
  comunidade; botão que exporta a configuração atual.
- **Configuração avançada:** botão que abre a janela de opções avançadas.
- Caixa "verificar a última versão ao iniciar".
- Caixa "abrir na aba básica".

### Aba 3 — Tradução

- **Bloco por serviço**, cada um com sua caixa de origem e sua caixa de destino,
  preenchidas a partir da tabela de idiomas (RF-308) filtrada pelos códigos que
  aquele serviço suporta.
- Texto informativo sobre códigos de idioma.
- Caixas de leitura em voz alta e "aguardar fim da leitura".

RF-511 — As listas de idioma devem conter apenas os idiomas que o serviço
correspondente suporta; um idioma sem código para aquele serviço não aparece na
lista dele.

RF-512 — Trocar o idioma de OCR deve mover automaticamente a seleção de origem
das três listas para o idioma correspondente, quando existir.

### Aba 4 — Outros

- **Atalhos:** sete linhas, uma por ação, cada uma com um campo de captura de
  combinação, um botão "padrão" e um botão "limpar".
- Texto explicativo sobre atalhos.
- **Ajuda:** botões para o manual e para a lista de erros conhecidos.
- **Links:** repositório, página do projeto, servidor de comunidade. Os endereços
  são dados de configuração, não endereços embutidos.

RF-513 — Um campo de captura de combinação deve registrar as teclas na ordem em
que são pressionadas, exibindo-as separadas por "+", limitado a três; a tecla de
escape e a de retrocesso limpam o campo; teclas repetidas são ignoradas.

RF-514 — Enquanto um campo de captura está com foco, os atalhos globais devem
ficar inertes.

### Aba 5 — Configuração rápida

Assistente de quatro etapas, com um único botão de avanço cujo rótulo muda:

1. **Cor do texto:** três opções — escuro, claro, não sei.
2. **Área de OCR:** texto explicativo; o botão abre a camada de seleção.
3. **Confirmação da área:** o programa reabre o gerenciamento de áreas e mostra
   um resumo; o botão avança.
4. **Fim:** links para a documentação de tradução e de uso básico; o botão fecha.

RF-515 — Ao entrar no assistente, as molduras de área devem deixar de ser
"sempre no topo" para que a janela do assistente fique acessível; ao sair, o
estado deve ser restaurado.

RF-516 — Ao concluir, o assistente deve aplicar, de uma só vez:
- parar a tradução;
- escolher o motor de OCR: o motor moderno se estiver disponível; senão o motor
  do sistema, se ele tiver o idioma pedido; senão o motor clássico;
- escolher o serviço de tradução: para inglês, o tradutor web gratuito; para
  japonês, o tradutor local por processo auxiliar se estiver disponível, senão o
  tradutor web gratuito;
- definir os códigos de idioma de origem de cada serviço conforme o idioma
  escolhido, e os de destino conforme o idioma de destino padrão (RF-314);
- aplicar os ajustes automáticos por propriedade de idioma (RF-148);
- ativar o filtro HSV com os grupos correspondentes à cor escolhida, ou desativar
  todos os filtros se o usuário escolheu "não sei";
- desativar a captura da janela ativa;
- forçar o modo de janela **camada**;
- forçar a velocidade mais rápida;
- salvar o perfil principal.

### Aba 6 — Depuração

Visível somente após o modo de depuração ser ativado por um controle escondido.
Contém as caixas descritas em RF-491, mais um botão de limpar a memória de
resultados e caixas de repasse de sinalizadores ao pré-processamento.

## V.2 — Controle remoto

RF-517 — Deve existir uma janela pequena, sem bordas de sistema, sempre
disponível, com: botão de definir área de OCR, botão de instantâneo, botão de
iniciar tradução, botão de parar tradução (que ocupa o mesmo lugar do de
iniciar, alternando visibilidade), botão de abrir configurações, botão de
minimizar.

RF-518 — Essa janela deve ser movível por arraste em qualquer ponto e
redimensionável pelas bordas, **mantendo a proporção original**: ao redimensionar
por uma borda, a outra dimensão é derivada da proporção; ao redimensionar por um
canto, usa-se o maior fator dos dois eixos.

RF-519 — Os controles internos devem ser escalados proporcionalmente ao
redimensionamento, centralizados, exceto o botão de fechar, que permanece
ancorado à direita com sua margem original.

RF-520 — Essa janela deve ter uma opção de "sempre no topo", configurável nas
opções avançadas.

RF-521 — Fechar essa janela deve apenas minimizá-la, nunca encerrar o programa.

RF-522 — Os botões devem trocar de imagem enquanto pressionados.

## V.3 — Janela de opções avançadas

Uma janela com sete abas. Abrir esta janela deve **bloquear os atalhos globais**
até que ela seja fechada.

**Aba "aplicativo":**
- geral: modo bandeja; direção da direita para a esquerda; controle remoto
  sempre no topo;
- área que segue o mouse: modo compatível; usar somente essa área;
- captura de janela: borda amarela;
- cores da seleção de área: amostra de fundo (com alfa), amostra de destaque,
  botão de pré-visualização, botão de restaurar padrões.

**Aba "atalhos avançados":** quatro blocos de "abrir perfil" (cada um com campo
de atalho, campo de arquivo, botão de seleção de arquivo e botão de limpar); um
bloco de transparência forçada; sete blocos de troca de serviço de tradução.

**Aba "janela de tradução":**
- sobreposição: tamanho automático de fonte; fusão automática de blocos;
  preservar direção do original; usar contorno de fonte; usar transparência do
  fundo; cor automática (caixa mestre) com um grupo aninhado contendo cor de
  fonte automática e cor de fundo automática; tamanho mínimo; tamanho máximo;
  tempo de permanência do instantâneo;
- escuro: botão de escolha de fonte;
- camada: alinhamento inferior; alinhamento à direita;
- geral: sempre no topo só durante a tradução; ignorar tradução vazia; atalho de
  ocultar também traduz;
- memória de exibição: ativar; quantidade; tempo; texto explicativo.

RF-523 — O grupo de cor automática aninhado deve ficar desabilitado quando a
caixa mestre está desmarcada.

RF-524 — O campo de tamanho mínimo nunca pode ficar acima do máximo, e
vice-versa: alterar um ajusta o outro automaticamente.

**Aba "coletânea de tradução":** lista de arquivos com caixas de seleção; botões
de marcar todos e desmarcar todos; painel de informação do arquivo selecionado;
caixa "modo banco de dados"; grupo dependente com "ignorar
maiúsculas/minúsculas", habilitado apenas quando o modo banco de dados está
marcado.

**Aba "tradução":**
- tradução ponte;
- tradutor alternativo em caso de erro;
- API personalizada: lista de presets com botões adicionar e remover; campos de
  nome, URL, cabeçalhos, modelo de requisição e modelo de resposta; caixa "usar
  os mesmos códigos de idioma do tradutor web" que desabilita os campos de
  código de origem e destino quando marcada;
- modelo de linguagem: campo de instrução; campo de nome do modelo
  personalizado; caixa "não enviar a instrução padrão"; três botões de opção de
  preset (padrão, econômico, personalizado) e três controles deslizantes
  (temperatura, nível de raciocínio, limite de saída) habilitados apenas no
  preset personalizado;
- área de transferência: usar tradução da área de transferência; exibir
  original; exibir "traduzindo";
- gravação na área de transferência: formato (três opções).

RF-525 — Trocar o preset do modelo de linguagem deve atualizar imediatamente os
três controles deslizantes com os valores do preset e desabilitá-los; escolher
"personalizado" deve habilitá-los mantendo os valores atuais.

RF-526 — O rótulo do controle de temperatura deve exibir o valor dividido por
100 com duas casas decimais; o de nível de raciocínio deve exibir um texto
localizado por nível; o de limite de saída deve exibir o número.

RF-527 — Selecionar um preset de API personalizada deve salvar as alterações do
preset anterior antes de carregar o novo.

RF-528 — Presets vindos de arquivo devem ser exibidos com um prefixo distintivo,
ter o campo de nome somente-leitura e o botão de remover desabilitado.

RF-529 — Nomes duplicados devem receber sufixo "(n)" automaticamente ao salvar.

**Aba "OCR":** priorizar OCR de nuvem em modo pontual; limite mensal; três linhas
de texto explicativo.

**Aba "dicionário":** número de passagens adicionais; texto explicativo.

RF-530 — A janela de opções avançadas deve ter um botão **aplicar** que grava
tudo e reaplica as opções ao programa, e um botão **restaurar padrões** com
confirmação.

RF-531 — Aplicar as opções avançadas deve, sem reiniciar: recarregar a coletânea
de tradução, reconfigurar o modelo de linguagem, ajustar o "sempre no topo" do
controle remoto, aplicar o número de passagens do dicionário, aplicar a direção
do texto, os alinhamentos da camada, a política de "sempre no topo", a fonte do
modo escuro, a fusão de blocos, a pré-visualização de fonte, a memória de
exibição, a lista de serviços de tradução, e o monitoramento da área de
transferência.

RF-532 — Ao restaurar padrões, a direção do texto deve ser derivada da
propriedade de direção do idioma de destino (RF-311), não de uma lista de
idiomas embutida.

## V.4 — Janelas auxiliares

RF-533 — **Gerenciamento de áreas:** botões de adicionar área, adicionar área de
exclusão, limpar tudo e aplicar. Posicionada onde estiver o controle remoto.
Fechar sem aplicar reverte as áreas ao estado salvo. Ao fechar, as molduras
tornam-se invisíveis.

RF-534 — **Grupos de cor por área:** lista com caixas de seleção mostrando, para
cada grupo, seu índice, valores R/G/B e faixas S/V; botão de marcar todos; botões
de aplicar e cancelar.

RF-535 — **Conta-gotas:** imagem ampliável, controle de zoom, rótulos de R, G, B,
H, S, V, amostra de cor, botão de processar. A janela é única (uma instância) e,
enquanto aberta, as molduras deixam de ser "sempre no topo".

RF-536 — **Pré-visualização binarizada:** caixa de seleção de modo (RGB, HSV,
limiar); painéis de parâmetros por modo; controle deslizante e campo numérico de
limiar sincronizados; botão de transformar; botão de reverter. Alterar o limiar
reprocessa automaticamente.

RF-537 — **Editor de dicionário:** campo com o texto reconhecido atual
pré-preenchido, campo com a correção, botões de aceitar e cancelar. Aceitar
acrescenta o par ao arquivo e recarrega o dicionário. Enquanto aberto, a cópia
automática para a área de transferência fica suspensa.

RF-538 — **Gerenciamento de chaves de tradução:** lista das chaves com
identificador, tipo (gratuita/paga) e estado; campos de identificador e segredo;
botões de opção de tipo; botão que alterna entre "adicionar" e "editar" conforme
o identificador digitado já exista ou não; botão de remover.

RF-539 — **Configuração do OCR de nuvem:** caminho do arquivo de credencial;
botão de seleção de arquivo; contador "usadas / limite"; caixa de priorização;
botão de documentação.

RF-540 — **Instalador do motor por ambiente interpretado:** três painéis
alternados — principal (instalação básica, ir para configuração de GPU, guia,
caixa de forçar reinstalação), GPU (opção entre versões pré-definidas de
biblioteca de computação ou linha de comando própria) e log.

RF-541 — **Navegador de configurações da comunidade:** campo de busca com filtro
ao vivo; lista com título original e título traduzido; painel com título, links
de loja e extra, e descrição com links clicáveis; botão de aplicar cujo rótulo
muda para "ir para a página de download" quando não há arquivos para baixar.

RF-542 — **Janela do navegador embutido de tradução:** exibe a página do serviço
para o usuário resolver bloqueios; fechar apenas oculta.

RF-543 — **Sobre:** versão, data de compilação, versões dos dicionários, links do
autor; clicar no logotipo reexibe a tela de abertura.

RF-544 — **Doação:** o botão abre uma página externa de apoio ao projeto. A
página de destino é um dado de configuração, não um endereço embutido.

## V.5 — Menu de contexto da janela de tradução em modo camada

RF-545 — Clicar com o botão direito na janela em modo camada deve abrir um menu
com: ordenação (padrão / centralizado), remover espaços, transparência forçada,
fechar.

RF-546 — As marcações desse menu devem refletir o estado atual e as alterações
devem valer imediatamente, sem exigir "aplicar".

---

# PARTE VI — INTEGRAÇÕES EXTERNAS

Para cada serviço acessado pela rede: o que faz, formato da requisição e da
resposta, autenticação, tratamento de erro, retentativas, tempos limite e
limites de uso.

## VI.1 — Tradutor web gratuito (endpoint público de tradução)

- **Método:** GET.
- **Requisição:** endereço público de tradução com parâmetros de consulta: tipo
  de cliente, idioma de origem, idioma de destino, tipo de dado solicitado
  ("texto"), e o texto codificado para URL. Cabeçalhos: tipo de conteúdo de
  formulário, sem cache, conjunto de caracteres UTF-8.
- **Tipo de cliente:** um identificador de alta qualidade por padrão; um
  identificador de baixa qualidade após receber 429.
- **Resposta:** vetor JSON. O primeiro elemento é um vetor de segmentos; de cada
  segmento que seja um vetor cujo primeiro item é texto, extrai-se esse texto e
  concatena-se com um espaço.
- **Autenticação:** nenhuma.
- **Erros:** 429 → troca para baixa qualidade e repete uma vez; se já estava em
  baixa qualidade, devolve mensagem de cota horária esgotada. Qualquer exceção →
  mensagem de erro de processamento com a descrição.
- **Tempo limite:** P-54.
- **Limites:** cota por hora e por endereço IP, imposta pelo serviço. O programa
  lida com ela degradando a qualidade por P-53.

## VI.2 — Tradutor comercial por chave (serviço de tradução coreano)

- **Método:** POST.
- **Requisição:** corpo em formato de formulário com os campos de idioma de
  origem, idioma de destino e texto codificado. Cabeçalhos: identificador de
  chave e chave secreta, tipo de conteúdo de formulário, sem cache, UTF-8.
- **Resposta:** JSON. Sucesso: objeto `message` → `result` → `translatedText`.
- **Autenticação:** par identificador/segredo, por cabeçalho.
- **Erros:** presença de `errorMessage` (plano gratuito) ou de um objeto `error`
  com `message` (plano pago) indica falha. O código de erro é anexado à
  mensagem. Códigos específicos marcam a chave como "no limite" ou "com erro".
- **Retentativas:** ao detectar erro, se houver mais de uma chave cadastrada, o
  programa passa para a próxima e informa qual passou a valer. O ciclo de chaves
  volta ao início após a última ou após P-55.
- **Limites:** cota mensal por chave, imposta pelo serviço.

## VI.3 — Tradutor web sem chave do mesmo fornecedor

- **Método:** POST.
- **Requisição:** corpo em formato de formulário com: dicionário desativado,
  honoríficos desativados, glossário desativado, idioma de origem, idioma de
  destino, texto. Cabeçalhos de navegador: agente de usuário, referenciador do
  próprio site, aceitação de JSON, idioma de aceitação.
- **Resposta:** JSON com um campo de texto traduzido.
- **Autenticação:** nenhuma.
- **Erros:** resposta ausente ou código diferente de 200 → mensagem com o código.
  JSON inválido → mensagem "JSON inválido".
- **Espaçamento:** após cada requisição, bloqueio aleatório de até P-56 antes de
  aceitar a próxima.
- **Limites:** bloqueio por comportamento automatizado, não documentado.

## VI.4 — Tradutor por planilha em nuvem

- **Método:** API da plataforma de planilhas.
- **Requisição:** escreve, em uma linha aleatória entre 1 e P-57, o texto na
  coluna A (prefixado com apóstrofo para evitar interpretação) e uma fórmula de
  tradução na coluna B, referenciando a célula da coluna A e os códigos de
  idioma. Em seguida lê a célula B.
- **Autenticação:** delegação do usuário, com armazenamento local de token por
  identificador de cliente. Escopo: planilhas.
- **Inicialização:** o programa garante que existe uma aba dedicada com o número
  de linhas exigido e duas colunas, criando-a ou atualizando suas propriedades.
- **Erros:** planilha inexistente → mensagem específica; qualquer outro estado
  HTTP → mensagem com o código; fórmula com erro de valor → erro sem mensagem.
- **Tradução ponte:** quando ativa e a origem não é japonês, traduz para japonês
  e depois para o destino, em duas passagens.
- **Limites:** cota da plataforma de planilhas e da função de tradução embutida.

## VI.5 — Tradutor por navegador embutido

- **Método:** navegação de página + execução de script.
- **Requisição:** navega para uma URL montada a partir de um formato
  configurável remotamente, contendo o par de idiomas e o texto codificado, com
  as barras escapadas e o sufixo marcador P-58 acrescentado.
- **Resposta:** extraída executando um trecho de script configurável remotamente,
  que localiza o elemento de resultado, remove painéis de alternativas e devolve
  o texto visível.
- **Preparação:** antes de navegar, o campo de resultado é limpo por script, com
  até P-136 tentativas.
- **Espera:** consulta a cada P-137 até que o resultado seja diferente do
  anterior e do valor sentinela.
- **Pós-processamento:** remove as aspas externas, converte quebras duplas
  escapadas em quebras reais, remove quebras simples escapadas, corta no sufixo
  marcador, remove aspas nas pontas e barras invertidas restantes.
- **Tempo limite:** P-59 normalmente, P-60 com alternativa ativa, mais P-61 na
  primeira tradução, ou P-62 quando o texto se repete.
- **Erros:** ao esgotar o tempo, devolve vazio e marca erro. Se a opção de
  alternativa estiver ativa, a requisição é refeita no tradutor web gratuito.
- **Limites:** bloqueio por comportamento automatizado e verificação humana; o
  usuário pode abrir a janela para resolver.

## VI.6 — Tradutor comercial europeu por chave

- **Método:** POST.
- **Requisição:** corpo JSON com um vetor de textos, o idioma de destino e o de
  origem. Cabeçalhos: autorização com prefixo de chave, tipo de conteúdo JSON,
  sem cache, UTF-8.
- **Endpoints:** dois, gratuito e pago, escolhidos pelo usuário.
- **Resposta:** JSON com um vetor `translations`, cujo primeiro elemento tem o
  campo `text`. O campo pode ser texto ou vetor; quando vetor, as partes são
  concatenadas.
- **Erros:** resposta sem sucesso → extrai o campo `message` como mensagem de
  erro.
- **Normalização:** códigos de chinês são convertidos para um código genérico
  antes do envio.
- **Limites:** cota mensal de caracteres por chave.

## VI.7 — Tradutor por modelo de linguagem

- **Método:** POST.
- **Endereço:** endpoint de geração de conteúdo do modelo escolhido, com a chave
  como parâmetro de consulta.
- **Requisição:** JSON com: instrução de sistema (as instruções combinadas);
  conteúdo do usuário (o texto a traduzir); configurações de segurança com todas
  as categorias sem bloqueio; e configurações de geração com temperatura, limite
  de tokens de saída e configuração de raciocínio no formato da família do
  modelo.
- **Resposta:** JSON. Se houver retorno de bloqueio por conteúdo proibido, o
  programa refaz no tradutor web gratuito. Caso contrário, extrai o texto do
  primeiro candidato e remove espaços nas pontas.
- **Autenticação:** chave de API.
- **Erros:** tempo esgotado → mensagem de tempo excedido; cancelamento → vazio
  sem erro; erro de requisição → mensagem com a descrição.
- **Tempo limite:** P-77.
- **Limites:** cota por minuto e por dia, imposta pelo serviço.

## VI.8 — Motor de OCR de nuvem

- **Método:** API de reconhecimento de documento em imagem.
- **Requisição:** a imagem da região convertida para um formato de imagem sem
  perdas e enviada como bytes.
- **Autenticação:** arquivo de credencial de conta de serviço, escolhido pelo
  usuário por diálogo.
- **Resposta:** anotação de texto com páginas, blocos, parágrafos, palavras e
  símbolos, cada um com caixa delimitadora de quatro vértices. O programa deriva
  a caixa de cada palavra pelos vértices, e as quebras de linha comparando a
  posição acumulada de caracteres com as quebras no texto completo.
- **Erros:** qualquer exceção produz resultado vazio com a mensagem de erro no
  texto principal.
- **Limites:** cota mensal gratuita; o programa impõe seu próprio limite P-29,
  contado por credencial e por mês civil, e avisa que a contagem local pode
  divergir da contagem real do serviço.

## VI.9 — API de tradução personalizada do usuário

- **Método:** POST.
- **Formato padrão:** corpo JSON com quatro campos — um nome (concatenação dos
  códigos de origem e destino), o texto, o código de destino e o código de
  origem. Resposta esperada: campo de resultado (texto ou vetor de textos),
  campo de código de erro e campo de mensagem de erro. Código de erro diferente
  de "0" significa falha.
- **Formato por preset:** o usuário define o corpo da requisição e o formato da
  resposta como modelos, com marcadores substituídos em tempo de execução.
  Cabeçalhos adicionais são enviados.
- **Autenticação:** o que o usuário configurar nos cabeçalhos.
- **Erros:** resposta sem sucesso → devolve o corpo da resposta como mensagem;
  JSON inválido no modelo → mensagem descrevendo a falha de conversão; chave de
  resultado não encontrada no modelo → mensagem específica.
- **Limites:** os do serviço do usuário.

## VI.10 — Serviços de distribuição e dados

- **Arquivo de versão:** texto simples baixado de um endereço fixo, com chaves
  entre colchetes e chaves entre chaves para valores e URLs. Contém: versão
  atual, faixa de versões atualizáveis em linha, endereço do executável novo,
  endereço da soma de verificação, endereço da página de notas, e versões dos
  dicionários com seus endereços.
- **Arquivo de configuração padrão remota:** texto simples com os tokens
  separadores, o sinalizador de token avançado, e os parâmetros do tradutor por
  navegador embutido.
- **Lista de configurações da comunidade:** um índice em texto com uma linha por
  jogo, campos separados por tabulação (caminho, título, título traduzido); e,
  por jogo, um arquivo de informação com título, links, descrição, e os nomes
  dos arquivos de perfil e de banco de dados a baixar.
- **Tratamento de erro:** qualquer falha nesses downloads é ignorada; o programa
  continua com os valores locais.

---

# PARTE VII — REQUISITOS NÃO FUNCIONAIS

## VII.1 — Latência

RF-547 — O ciclo completo, do início da captura até o desenho, deve caber dentro
do intervalo configurado. Com o intervalo mínimo P-05 e um motor de OCR local,
esse é o alvo: **300 ms para capturar, pré-processar, reconhecer, agrupar,
consultar cache e desenhar**, quando a tradução vem do cache.

RF-548 — Quando a tradução exige rede, o tempo do ciclo é dominado pela rede. Os
tempos limite estabelecidos (P-54, P-59 a P-62, P-77) são os tetos aceitos por
serviço.

RF-549 — O desenho da sobreposição deve caber em uma fração pequena do ciclo. O
programa deve instrumentar, em modo de depuração, quatro tempos separados:
cálculo de tamanho e posição da janela, layout e desenho, apresentação na tela, e
total. **Motivo:** o desenho roda na thread de interface e qualquer excesso é
percebido como travamento.

RF-550 — O programa deve manter as otimizações que tornam esse orçamento
possível, todas obrigatórias:
- descarte de ciclos com texto idêntico (RF-194);
- reuso de resultados por área quando nada mudou (RF-203);
- cache de traduções (RF-206);
- cache de medição e de quebra de texto por desenho (RF-374);
- busca binária na quebra de linha (RF-369) e na escolha de tamanho de fonte
  (RF-363);
- atalho que testa o tamanho preferido antes de iniciar a bissecção (RF-363);
- reuso do mapa de bits da sobreposição entre quadros (RF-379).

RF-551 — Parar a tradução deve levar no máximo P-03; a partir do interceptador
de teclado, no máximo P-04.

## VII.2 — CPU e memória

RF-552 — Em ociosidade (laço parado), o consumo de CPU deve ser
desprezível: nenhum temporizador ativo além do da área que segue o mouse, quando
ela estiver ligada.

RF-553 — Durante o laço, o consumo de CPU deve ser dominado pelo OCR e pelo
pré-processamento; o programa não deve consumir CPU em espera ativa exceto nos
pontos de sondagem especificados (P-20, P-31, P-126, P-137, P-143).

RF-554 — O programa deve liberar explicitamente cada imagem de região assim que
ela não é mais necessária, e não deve manter mais de um conjunto de imagens de
região vivo por vez.

RF-555 — O programa **não** deve forçar coleta de lixo durante o desenho
(RF-380). Deve liberar recursos gráficos nativos deterministicamente:
todo mapa de bits, contexto de desenho, pincel, caneta e caminho criado durante
um desenho deve ser liberado antes do fim do desenho, inclusive em caminhos de
exceção.

RF-556 — O mapa de bits da sobreposição deve ser recriado apenas quando as
dimensões da janela mudam, e o anterior liberado nesse momento.

RF-557 — A memória de resultados anteriores deve ter teto (P-48) para não
crescer sem limite.

RF-558 — O programa deve **exibir na própria interface o quanto de memória está
usando**, atualizado periodicamente, em local visível sem abrir diálogo. Não há
limite imposto ao usuário — nem à ampliação, nem ao número de áreas, nem ao
tamanho do cache. O indicador existe para que o usuário perceba um consumo
crescente antes que ele vire um problema, e relacione o número com o que acabou
de configurar.

RF-559 — O indicador deve mostrar, no mínimo, a memória total do processo. Deve
ser possível abrir um detalhamento com pelo menos: memória ocupada pelas imagens
de região em uso, pelo cache de traduções e pelo mapa de bits da janela de
sobreposição. **Motivo:** só o total diz que há um problema; o detalhamento diz
qual configuração o causou.

RF-560 — A leitura do indicador não pode custar caro: deve ser amostrada em
intervalo fixo e nunca dentro do ciclo de tradução.

## VII.3 — Robustez

RF-561 — Nenhuma falha de rede, de serviço externo, de motor de OCR ou de
arquivo pode encerrar o programa. Todas devem degradar para uma mensagem
visível ao usuário e a continuidade do laço.

RF-562 — O programa deve sobreviver a um arquivo de configuração corrompido, a
uma pasta de dados ausente, a um monitor removido durante a execução, e à
alteração da resolução de tela.

RF-563 — Nenhuma exceção pode escapar da thread do laço para o exterior sem ser
capturada.

## VII.4 — Escalabilidade e evolução

RF-564 — O programa **não** tem compatibilidade com nenhum produto anterior. Não
lê seus arquivos, não importa seus dados e não replica seus formatos. Não deve
haver código de leitura de formato legado em lugar algum.

RF-565 — Em compensação, o programa deve ser compatível **consigo mesmo ao longo
do tempo**: cada arquivo de dados do usuário carrega uma versão de esquema
(RF-038), valores de conjunto fechado são persistidos por identificador textual
(RF-026), e chaves desconhecidas são preservadas na regravação.

RF-566 — Acrescentar um idioma, um motor de OCR ou um serviço de tradução deve
exigir apenas: uma entrada nos dados de configuração e, quando houver, a
implementação do adaptador correspondente aos contratos da Parte II. Nenhuma
alteração no laço de tradução, no agrupamento, no cache ou na renderização pode
ser necessária para isso.

RF-567 — Nenhum ponto do programa pode assumir um conjunto fixo de idiomas,
motores ou serviços — nem por quantidade, nem por ordem, nem por identificador
literal espalhado pelo código. A única exceção admitida é o comportamento
específico por idioma descrito em RF-148, que deve ser expresso como propriedade
do idioma nos dados (por exemplo, "separa palavras por espaço"), e não como uma
comparação com um identificador.

---

# PARTE VIII — COMPORTAMENTO EM SITUAÇÕES EXTREMAS

| Situação | Comportamento exigido |
|---|---|
| **Nenhuma área de OCR definida** | Ao tentar iniciar, exibir mensagem explicando e oferecer abrir o manual. Não iniciar o laço. |
| **Região capturada vazia (0 px)** | A conversão força mínimo de 1 px em cada dimensão. Se ainda assim não houver imagem, o índice é pulado silenciosamente. |
| **Região fora da tela** | A captura não produz imagem; o índice é pulado; o ciclo continua com as demais regiões. |
| **Região sobre a própria janela de tradução** | Nos modos escuro e camada, com captura de tela e tradução em tempo real, exibir aviso temporário (RF-343) por P-90. No modo sobreposição isso não ocorre porque a janela é excluída da captura. |
| **OCR não reconhece nada** | Texto vazio. Vazio é tratado como mudança (RF-194), então a tradução anterior é apagada da tela. |
| **OCR devolve lixo instável** | O texto muda a cada ciclo e o programa retraduz e redesenha a cada ciclo. É o comportamento **exigido**, não uma tolerância: qualquer amortecimento custaria um ciclo de latência. O remédio para o OCR instável é o filtro de cor ou a ampliação, do lado do usuário. |
| **Texto truncado pelo limite do motor** | O motor moderno limita a P-30 linhas; o excedente é perdido silenciosamente. |
| **Rede cai** | O serviço devolve mensagem de erro, que é exibida no lugar da tradução. O laço continua e volta a funcionar quando a rede voltar. Nenhum tempo de espera adicional é imposto. |
| **Serviço devolve erro de autenticação** | A mensagem do serviço é exibida. No serviço com múltiplas chaves, a chave é marcada e a próxima é ativada, com nota anexada ao resultado. |
| **Serviço bloqueia por excesso de uso** | Tradutor web gratuito: cai para o endpoint de baixa qualidade por P-53 e prefixa o resultado com um marcador; se já estava lá, devolve mensagem de cota esgotada. Serviço com chaves: rodízio de chaves. Modelo de linguagem: mensagem do serviço. OCR de nuvem: recusa e explica que a cota mensal acabou. |
| **Serviço devolve resposta em formato inesperado** | Mensagem de erro descrevendo a falha de análise; laço continua. |
| **Resposta com menos partes que blocos** | Os blocos restantes ficam sem tradução; nada é exibido para eles; não é erro. |
| **Texto muito longo** | Vai inteiro na requisição. Se o serviço truncar, a tradução vem truncada. No desenho, o texto que não cabe nem no tamanho mínimo é desenhado assim mesmo e marcado como recortado no registro de depuração. |
| **Tradução vazia** | Se a opção "ignorar tradução vazia" estiver ativa, a tela mantém o conteúdo anterior; senão, a tela é limpa. |
| **Múltiplos monitores com escalas de DPI diferentes** | Cada moldura desconta borda e barra de título usando a escala do **monitor em que ela está**, recalculada quando ela muda de monitor (RF-075, RF-076). Nunca uma escala global. |
| **Coordenadas negativas (monitor à esquerda ou acima do principal)** | Suportadas. A janela de sobreposição cobre a união de todos os monitores e guarda um deslocamento entre a origem da união e a origem do monitor principal. |
| **Resolução muda, monitor é removido ou a disposição muda com o programa aberto** | As áreas guardam coordenadas absolutas e podem ficar fora da tela. O programa **avisa o usuário**, aponta quais áreas ficaram inválidas e oferece abrir o gerenciamento de áreas para ele corrigir. Nenhuma área é reposicionada automaticamente (RF-086, RF-087). |
| **Usuário troca de janela durante a tradução** | Com captura de tela, o programa passa a ler o que estiver naquela posição. Com captura de janela ativa, passa a ler a nova janela. Com captura de janela anexada, continua lendo a janela escolhida. |
| **Jogo em tela cheia exclusiva** | Não funciona: a sobreposição não pode ser desenhada e a captura pode falhar. O programa deve documentar que é preciso usar modo janela ou janela sem borda. |
| **Usuário aciona a captura de tela do sistema** | A janela de sobreposição torna-se capturável por P-91 e as atualizações de desenho ficam suspensas nesse intervalo. |
| **Duas instâncias do programa** | A segunda informa que já há uma em execução e encerra, salvo se o marcador de múltiplas instâncias existir. |
| **Atualização interrompida no meio** | O executável antigo é preservado até que o novo esteja verificado; um arquivo baixado que falhe na verificação é apagado e o período de espera P-116 impede o ciclo de repetição. |
| **Biblioteca gráfica de texto vetorial indisponível** | Desenho vetorial desativado globalmente; o texto passa a ser desenhado sem contorno; o usuário é avisado uma vez com link de ajuda. |
| **Janela de sobreposição sem identificador de sistema** | O desenho vindo de outra thread é abandonado, não adiado (RF-382). |
| **Pedido de parada durante uma tradução lenta** | A espera é interrompida no próximo intervalo de verificação (P-126) e a operação é cancelada. |
| **Thread do laço não morre no prazo** | A mudança de configuração é cancelada; o sinalizador de fim permanece ativo; o próximo pedido tenta de novo. |

---

# PARTE IX — REQUISITOS MULTIPLATAFORMA

Seção nova: o produto de origem só existia em um sistema operacional. Aqui está
o **comportamento exigido** de cada capacidade dependente de sistema e o
**comportamento degradado aceitável** quando ela não existe.

## IX.1 — Capacidades dependentes do sistema

| Capacidade | Comportamento exigido | Onde é usada |
|---|---|---|
| **C1 — Capturar uma região retangular da tela como imagem** | Obter os pixels de um retângulo em coordenadas globais da área de trabalho, incluindo coordenadas negativas, com a janela do próprio programa **excluída** do resultado. | Captura de tela (cap. 12) |
| **C2 — Capturar uma janela específica mesmo quando coberta** | Receber um fluxo de quadros do conteúdo de uma janela escolhida pelo usuário, com a posição de origem do conteúdo em coordenadas globais. | Captura anexada |
| **C3 — Enumerar janelas capturáveis e deixar o usuário escolher uma** | Apresentar uma lista ou seletor do sistema e devolver um identificador de janela. | Captura anexada |
| **C4 — Obter os limites reais do quadro de uma janela** | Retângulo do conteúdo visível, sem sombras nem bordas invisíveis. | Alinhamento da sobreposição |
| **C5 — Janela sempre no topo** | Manter uma janela acima de todas as outras, inclusive de jogos em modo janela sem borda. | Molduras, janelas de tradução, controle remoto |
| **C6 — Janela com transparência por pixel** | Desenhar um quadro RGBA completo por atualização, com canal alfa por pixel respeitado pelo compositor. | Modos camada e sobreposição |
| **C7 — Janela transparente a cliques** | Fazer com que eventos de mouse atravessem a janela e cheguem à janela de baixo, alternável em tempo de execução. | Modos camada e sobreposição durante a tradução |
| **C8 — Excluir uma janela de capturas de tela e gravações** | Marcar a janela para não aparecer em capturas feitas por outros programas, alternável em tempo de execução. | Sobreposição |
| **C9 — Sincronizar com o compositor** | Bloquear até o próximo quadro composto, para evitar cintilação no primeiro desenho. | Sobreposição |
| **C10 — Atalho global de teclado** | Receber eventos de teclado enquanto outro programa tem o foco, sem consumir os eventos. | Atalhos |
| **C11 — Detectar o atalho de captura de tela do sistema** | Saber quando o usuário aciona a captura de tela do sistema. | Sobreposição (RF-347) |
| **C12 — Obter o título e o identificador da janela em primeiro plano** | Necessário para a captura de janela ativa e para a espera antes do instantâneo. | Captura, instantâneo |
| **C13 — Ícone de bandeja com menu** | Manter o programa acessível com a janela principal fechada. | Ciclo de vida |
| **C14 — Área de transferência: ler, escrever e observar mudanças** | Monitoramento contínuo de conteúdo de texto. | Tradução da área de transferência |
| **C15 — Síntese de voz** | Converter texto em áudio e reproduzir. | Leitura em voz alta |
| **C16 — Desenho de texto vetorial com contorno** | Converter texto em caminho e traçar/preencher com espessuras diferentes. | Modos camada e sobreposição |
| **C17 — Medição precisa de texto** | Medir a extensão de um caminho de texto e a largura de uma cadeia. | Layout da sobreposição |
| **C18 — Enumerar monitores e suas escalas** | Obter a união dos monitores e a escala de cada um. | Molduras, sobreposição |
| **C19 — Executar processo auxiliar e comunicar por canal local** | Necessário para o tradutor local que depende de biblioteca nativa de outra arquitetura. | Tradutor local |
| **C20 — Reconhecimento de texto oferecido pelo sistema** | Opcional; quando não existir, o motor correspondente simplesmente não aparece na lista. | OCR |

## IX.2 — Limitações conhecidas por sistema e degradação aceitável

RF-568 — **Sessões gráficas que não permitem que a aplicação posicione a própria
janela nem force "sempre no topo"** (notadamente Wayland). Nesses ambientes:
- C5 não é garantida. **Degradação aceitável:** o programa deve exibir um aviso
  único explicando que a janela de tradução pode ficar atrás do jogo, e oferecer
  a alternativa do modo escuro em uma janela normal que o usuário posiciona
  manualmente.
- O posicionamento absoluto das molduras de área não é possível. **Degradação
  aceitável:** o modo de captura de tela inteira com áreas absolutas deve ser
  substituído por seleção de janela (C2/C3) mais coordenadas relativas ao
  conteúdo dessa janela.
- C1 e C10 exigem portais com consentimento explícito do usuário. **Degradação
  aceitável:** solicitar a permissão na primeira necessidade, explicar por que é
  necessária, e desabilitar as funções correspondentes se negada. O
  consentimento pode ter validade limitada; o programa deve tratar a revogação
  como "captura indisponível" e não como erro fatal.

RF-569 — **macOS** exige permissão de gravação de tela para C1, C2 e C3, e
permissão de acessibilidade para C10 e C12. Nesses casos:
- O programa deve detectar a ausência da permissão, explicar em texto claro qual
  permissão falta e para quê, e oferecer abrir a tela de configuração
  correspondente.
- **Degradação aceitável:** sem permissão de gravação de tela, nenhuma tradução é
  possível; o programa deve dizer isso e não iniciar. Sem permissão de
  acessibilidade, os atalhos globais ficam indisponíveis e o usuário deve usar o
  controle remoto; o programa deve informar isso uma vez.
- C8 pode não existir. **Degradação aceitável:** a sobreposição aparece em
  capturas de tela; o programa deve documentar isso e RF-347 vira inócuo.
- C11 pode não ser detectável. **Degradação aceitável:** RF-347 é omitido.

RF-570 — **Jogos em tela cheia exclusiva** não aceitam sobreposição de terceiros
em nenhum sistema. **Degradação aceitável:** o programa deve documentar que é
preciso usar modo janela ou janela sem borda, e, se detectar que a captura
devolve apenas quadros pretos, deve sugerir isso ao usuário em vez de exibir
tradução vazia repetidamente.

RF-571 — **C9 (sincronização com o compositor)** não existe uniformemente. Sem
ela, o primeiro quadro pode piscar. **Degradação aceitável:** substituir por uma
espera de um intervalo de quadro estimado, ou omitir e aceitar a cintilação
inicial.

RF-572 — **C16 e C17** devem usar a mesma implementação de desenho de texto em
todas as plataformas, para que o layout calculado corresponda ao desenhado. Se a
plataforma não oferecer contorno de texto, a opção correspondente deve ficar
indisponível, não produzir resultado diferente.

RF-573 — **C15 (síntese de voz)** pode não existir. **Degradação aceitável:** a
opção fica desabilitada com uma explicação.

RF-574 — **C19** só é necessário para o tradutor local que depende de biblioteca
proprietária de um sistema específico. Em plataformas onde essa biblioteca não
existe, o serviço correspondente não deve aparecer na lista de tradutores.

RF-575 — **C20** varia por plataforma. O programa deve listar apenas os motores
efetivamente disponíveis e nunca apresentar um motor que falhará ao ser usado.

RF-576 — Toda capacidade indisponível deve ser detectada **na inicialização** e
refletida na interface — controles ocultos ou desabilitados com explicação — e
não descoberta no meio de uma tradução.

RF-577 — O programa deve manter uma camada de abstração explícita para C1 a C12,
com uma implementação por sistema, de modo que os módulos de OCR, tradução,
agrupamento e layout sejam idênticos em todas as plataformas.

RF-578 — Os caminhos de dados do usuário devem seguir a convenção de cada
sistema, mas o **formato** dos arquivos deve ser idêntico, para que uma
configuração criada em um sistema funcione em outro.

---

# PARTE X — ORDEM DE CONSTRUÇÃO

Roteiro incremental. Cada etapa é um entregável funcional e testável
isoladamente. Nenhuma etapa depende de algo que ainda não foi construído.

### Etapa 1 — Esqueleto e configuração
**Requisitos:** RF-001 a RF-003, RF-006, RF-008, RF-015 a RF-019, RF-020 a
RF-046.
**Entregável:** um programa que abre, carrega e salva perfis e opções avançadas,
tem ícone de bandeja e encerra limpo. Nenhuma tradução ainda.
**Como testar:** salvar, restaurar padrões e recarregar um perfil devolve o
mesmo estado; um perfil com linhas removidas ainda abre.

### Etapa 2 — Abstração de plataforma e captura
**Requisitos:** RF-088, RF-100, RF-568 a RF-578.
**Entregável:** uma função que recebe um retângulo em coordenadas globais e
devolve a imagem. Um teste visual que salva a imagem em arquivo.
**Como testar:** capturar uma região em cada monitor, incluindo coordenadas
negativas, e conferir o conteúdo.

### Etapa 3 — Regiões de captura
**Requisitos:** RF-047 a RF-087.
**Entregável:** o usuário desenha, move, redimensiona e remove áreas e áreas de
exclusão; a lista de retângulos alinhados é produzida corretamente.
**Como testar:** os critérios de aceite do capítulo 11.

### Etapa 4 — Pré-processamento
**Requisitos:** RF-101 a RF-119.
**Entregável:** filtro de cor, limiar, erosão e ampliação, com o conta-gotas e a
pré-visualização binarizada funcionando.
**Como testar:** a pré-visualização produz exatamente a imagem que será enviada
ao OCR.

### Etapa 5 — Um motor de OCR
**Requisitos:** RF-120, RF-121 (uma linha da tabela), RF-141 a RF-146.
**Entregável:** o contrato de 6.4 satisfeito por um motor local, devolvendo
palavras com caixas.
**Como testar:** uma imagem de teste conhecida produz as palavras e caixas
esperadas; uma imagem em branco produz resultado vazio.

### Etapa 6 — Estruturação e agrupamento 🔒
**Requisitos:** RF-152 a RF-179.
**Entregável:** o resultado do OCR vira blocos, com títulos e listas
identificados.
**Como testar:** todos os critérios de aceite do capítulo 15. Esta etapa deve ter
uma bateria de testes de unidade com casos reais gravados em arquivo — é a parte
mais fácil de quebrar sem perceber.

### Etapa 7 — Um serviço de tradução e o modo escuro
**Requisitos:** RF-225 a RF-240, RF-244 a RF-248, RF-308 a RF-316, RF-317 a
RF-331.
**Entregável:** **primeiro produto utilizável de ponta a ponta**: captura,
reconhece, traduz e mostra em uma janela.
**Como testar:** traduzir um diálogo de jogo real.

### Etapa 8 — Laço, controle e detecção de mudança 🔒
**Requisitos:** RF-004, RF-005, RF-009 a RF-014, RF-192 a RF-205, RF-547 a
RF-551.
**Entregável:** tradução contínua, com início/parada seguros e descarte de ciclos
idênticos.
**Como testar:** os critérios de aceite dos capítulos 9 e 16, incluindo o teste
de 20 acionamentos rápidos.

### Etapa 9 — Atalhos e controle remoto
**Requisitos:** RF-436 a RF-453, RF-517 a RF-522.
**Entregável:** operação sem sair do jogo.
**Como testar:** iniciar, parar e instantâneo funcionando com o jogo em primeiro
plano.

### Etapa 10 — Cache e fontes locais de tradução
**Requisitos:** RF-206 a RF-224, RF-241 a RF-243, RF-180 a RF-191.
**Entregável:** dicionário de correção, banco de dados local, coletânea, memória
de resultados e memória de exibição.
**Como testar:** os critérios de aceite do capítulo 17.

### Etapa 11 — Modo camada
**Requisitos:** RF-007, RF-332 a RF-343, RF-387 a RF-391.
**Entregável:** janela transparente, atravessável, com contorno duplo; e a
verificação de inicialização que detecta desenho vetorial de texto indisponível e
degrada o programa inteiro para desenho simples (RF-007).
**Como testar:** iniciar a tradução e conseguir clicar através da janela; e
forçar a falha do desenho vetorial e confirmar que o programa continua legível,
sem contorno, com o aviso exibido uma única vez.

### Etapa 12 — Modo sobreposição, layout
**Requisitos:** RF-344 a RF-386, RF-392.
**Entregável:** tradução desenhada sobre o texto original, com resolução de
colisões e tamanho automático de fonte.
**Como testar:** os critérios de aceite do capítulo 19.

### Etapa 13 — Análise automática de cor 🔒
**Requisitos:** RF-098, RF-099, RF-394 a RF-415, RF-393.
**Entregável:** cores derivadas da imagem original.
**Como testar:** os critérios de aceite do capítulo 20, com capturas reais de
pelo menos cinco jogos diferentes.

### Etapa 14 — Demais motores de OCR
**Requisitos:** RF-122 a RF-140, RF-147 a RF-151.
**Entregável:** todos os motores previstos, com instalação sob demanda e cota.

### Etapa 15 — Demais serviços de tradução
**Requisitos:** RF-249 a RF-307.
**Entregável:** todos os serviços previstos, incluindo presets de API
personalizada.

### Etapa 16 — Captura de janela anexada e recursos auxiliares
**Requisitos:** RF-089 a RF-097, RF-454 a RF-480.
**Entregável:** captura de uma janela específica escolhida pelo usuário, mesmo
coberta — com seletor de janela, parada automática quando a janela fecha, buffer
de quadros e origem obtida pelos limites estendidos do quadro; mais área que
segue o mouse, tradução da área de transferência e leitura em voz alta.
**Dependência:** deve vir depois da Etapa 12, porque o cálculo de coordenadas da
sobreposição tem um ramo específico para este modo (RF-347) que fica inerte até
aqui.
**Como testar:** anexar a uma janela, cobri-la com outra e confirmar que a
tradução continua correta e alinhada; mover a janela alvo e confirmar que a
sobreposição acompanha.

### Etapa 17 — Localização e interface completa
**Requisitos:** RF-481 a RF-489, RF-501 a RF-546.
**Entregável:** interface completa e traduzida.

### Etapa 18 — Atualização, comunidade e depuração
**Requisitos:** RF-416 a RF-435, RF-490 a RF-500.
**Entregável:** atualização automática verificada, navegador de configurações da
comunidade, retrato de análise.

### Etapa 19 — Endurecimento
**Requisitos:** RF-552 a RF-567, e toda a Parte VIII.
**Entregável:** o programa sobrevive a todas as situações extremas listadas, o
indicador de memória está na interface (RF-558 a RF-560), e acrescentar um
idioma, um motor de OCR ou um serviço de tradução é uma alteração de dados
(RF-566, RF-567).
**Como testar:** acrescentar um idioma fictício à tabela e verificar que ele
aparece nas listas, é selecionável e traduz, sem nenhuma alteração de código
fora do adaptador do serviço.

---

# PARTE XI — FORA DE ESCOPO

O programa deliberadamente **não** faz o seguinte. Nenhum agente deve
acrescentar estes itens por conta própria.

1. **Não modifica o software traduzido.** Nada de injeção de código, leitura de
   memória de outro processo, interceptação de chamadas gráficas ou alteração de
   arquivos do jogo. A única fonte de dados é a imagem da tela.
2. **Não treina nem ajusta modelos.** Os motores de OCR e os tradutores são
   consumidos como estão.
3. **Não faz correção ortográfica ou gramatical** além das substituições literais
   do dicionário de correção.
4. **Não tem serviço de tradução próprio.** Todo tradutor é externo ou local do
   usuário.
5. **Não sincroniza nada em nuvem.** Perfis, dicionários e memórias ficam no
   disco do usuário. A exceção é o download de configurações da comunidade, que
   é sempre iniciado pelo usuário.
6. **Não coleta telemetria** nem envia qualquer dado a não ser o texto necessário
   ao serviço de tradução escolhido.
7. **Não tem histórico navegável de traduções.** O que existe é a memória de
   resultados (cache técnico) e a memória de exibição (últimas N na tela).
8. **Não faz detecção automática do idioma de origem** — exceto quando o próprio
   serviço externo oferece essa opção como um valor de idioma.
9. **Não reconhece nem traduz áudio.**
10. **Não funciona em jogos em tela cheia exclusiva.** Não deve haver tentativa de
    contornar isso por injeção.
11. **Não tem editor de imagem.** O pré-processamento é o conjunto fixo de
    operações descrito no capítulo 13.
12. **Não faz OCR de documentos ou arquivos.** A entrada é sempre a tela ou uma
    janela.
13. **Não implementa amortecimento, estabilização nem confirmação de resultados
    de OCR entre quadros.** A detecção de mudança é comparação exata de texto
    (RF-192) e o resultado é aceito no primeiro quadro em que aparece (RF-200).
14. **Não reordena blocos por leitura semântica.** A ordem é puramente
    geométrica (RF-160, RF-161).
15. **Não faz layout de texto justificado, hifenização nem quebra por palavra.** A
    quebra é por caractere (RF-369).
16. **Não persiste a área rápida nem a área que segue o mouse** no perfil.
17. **Não implementa política de substituição no cache de traduções.** Ao atingir
    o teto, o cache inteiro é descartado (RF-210).
18. **Não protege criptograficamente as credenciais** guardadas em disco, e não
    usa o cofre de credenciais do sistema operacional (RF-035).

---

# PARTE XII — POLÍTICA DOS VALORES CALIBRADOS 🔒

**Não há questões em aberto nesta especificação.** Toda decisão de projeto está
expressa como requisito no corpo do documento, que é a única fonte de verdade.
Esta parte existe para proteger a informação mais frágil do documento.

## XII.1 — O que significa 🔒

Os 214 pontos marcados com 🔒 são valores que **não podem ser derivados por
raciocínio**. Cada um chegou ao valor atual por tentativa e erro contra casos
reais — telas de jogos concretos, fontes concretas, serviços concretos — ao longo
de mais de dez anos. Eles parecem arbitrários porque são: `1,25`, `0,4`, `2,5`,
`250 ms`. É exatamente essa aparência de arbitrariedade que os põe em risco.

Estão **confirmados**. Devem ser reproduzidos exatamente. Não são sugestões, não
são pendências e não são candidatos a "limpeza".

## XII.2 — O que não fazer

Ao construir, é proibido:

1. **Arredondar.** Trocar `1,25` por `1,5`, `0,15` por `0,2`, `950` por `1000`,
   `2,5` por `3`. Cada arredondamento parece inofensivo e cada um desloca um
   limiar que foi posicionado contra casos reais.
2. **Unificar valores parecidos.** P-34 (1,3), P-44 (1,2) e P-92 (1,3) são três
   razões diferentes, em três decisões diferentes, que por acaso ficaram
   próximas. Não são o mesmo número e não devem virar uma constante compartilhada.
3. **Substituir por "o que a biblioteca recomenda".** Vários destes valores
   existem justamente porque o comportamento padrão da plataforma não servia.
4. **Recalibrar por intuição.** Um valor 🔒 só muda com evidência: um conjunto de
   capturas reais em que o valor atual erra e o novo acerta, e em que nenhum caso
   que funcionava passa a falhar.
5. **Tratar um valor 🔒 como configurável** só porque parece razoável expor.
   A coluna **Exposto** da Parte IV diz quais são de fato ajustáveis pelo
   usuário; os demais são fixos por decisão.

## XII.3 — Como alterar um valor calibrado, quando for necessário

1. Reúna um conjunto de casos reais — capturas de tela de jogos, com o resultado
   esperado — que demonstre o erro do valor atual.
2. Registre o caso que motivou a mudança junto ao parâmetro, na Parte IV.
3. Verifique os casos que já funcionavam. Nenhum pode regredir.
4. Atualize o valor e o texto das colunas de efeito.

O modo de depuração (capítulo 27) existe em boa medida para isso: o retrato de
análise grava, por bloco desenhado, os tamanhos de fonte calculados, os quatro
retângulos, as cores escolhidas e as linhas após a quebra. É esse arquivo que
transforma "ficou ruim" em evidência utilizável.

## XII.4 — Os grupos mais sensíveis

Ordenados por consequência de errar:

| Grupo | Parâmetros | O que quebra |
|---|---|---|
| Agrupamento de linhas | P-33, P-34, P-35, P-36, P-37, P-38, P-44, P-148 | Diálogos se fragmentam em blocos soltos, ou nomes de personagem grudam no texto da fala. É o defeito mais visível do produto. |
| Detecção de título | P-40, P-41, P-42, P-43 | Nomes de personagem passam a ser traduzidos junto com a fala, ou falas curtas viram títulos. |
| Marcadores e pontuação | P-39, P-45, P-149, P-150 | Listas viram um bloco único; frases não terminam onde deveriam. |
| Análise de cor | P-105 a P-115, P-158, P-159 | A sobreposição escolhe cor de fonte igual à do fundo e o texto some. |
| Layout da sobreposição | P-92 a P-100 | Texto recortado, blocos escrevendo um por cima do outro, fonte grande demais ou minúscula. |
| Temporização | P-04, P-05 a P-09, P-13, P-19, P-47, P-120, P-121 | P-04 acima de ~300 ms mata todos os atalhos do programa. Os demais afetam latência e uso de CPU. |
| Rede | P-51 a P-63, P-77 | Bloqueio por comportamento automatizado, respostas divididas erradas, traduções perdidas por tempo esgotado. |
| Modelo de linguagem | P-64 a P-76 e o texto de RF-266 | Cada cláusula da instrução padrão corrige um comportamento observado: omissão de palavras, recusa, comentário extra. Encurtá-la traz o problema de volta. |
| Cores e contorno | P-79 a P-104 | Legibilidade do texto sobre o jogo. |
| Geometria das molduras | P-11 a P-16, P-144 | A região capturada não coincide com a desenhada; a sobreposição sai desalinhada. |
| Grupos HSV do assistente | P-26, P-27, P-28 | O assistente de configuração rápida deixa de funcionar para texto claro ou escuro. |

---

# APÊNDICE A — STACK ALVO

Registrado sem discussão, para que os agentes construtores não escolham
bibliotecas por conta própria.

- **Linguagem e plataforma:** C# / .NET 9.
- **Interface e sobreposição:** Avalonia.
- **Reconhecimento de texto:** ONNX Runtime com RapidOCR.
- **Captura de tela:** camada específica por sistema operacional, atrás da
  abstração descrita em RF-577.
