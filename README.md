# GORT-NG

**G**ame & **O**verall **R**eal-**T**ime **T**ranslator — *next generation*.

Traduz o que está na tela, enquanto está na tela. O programa captura uma região do
monitor, reconhece o texto, traduz e **desenha a tradução por cima do original**, no
lugar dele, com a cor e o tamanho de fonte certos. Feito para jogos que nunca foram
traduzidos, mas serve para qualquer coisa que mostre texto e não deixe copiá-lo.

> Sucessor do Gort original. O `-ng` marca a ruptura: reescrita completa,
> multiplataforma, com o pipeline inteiro especificado antes de existir.

---

## O que ele faz

Um ciclo, repetido continuamente enquanto a tradução está ligada:

```
captura → pré-processa → detecta → reconhece → estrutura → agrupa
        → trata o texto → traduz → analisa a cor → desenha
```

- **Três modos de janela.** *Escuro* (caixa de texto rolável ao lado), *camada*
  (texto flutuando sobre a tela, com contorno duplo) e **sobreposição** — a tradução
  ocupa exatamente o retângulo do texto original, com o fundo e a cor de fonte
  deduzidos da própria imagem.
- **Tamanho de fonte automático.** Cada bloco traduzido é ajustado por busca binária
  para caber no espaço do original, com quebra de linha por caractere.
- **Análise automática de cor.** Três estratégias em cascata para achar a cor de
  fundo, e agrupamento por contraste para a cor da fonte.
- **Detecção de mudança.** O texto só é retraduzido quando muda de verdade, e um
  cache de traduções mantém o diálogo repetido instantâneo.
- **Memória de tradução.** Bancos em texto puro que você pode construir a partir do
  próprio uso e recarregar depois como fonte local — tradução offline e instantânea.
- **Recursos auxiliares.** Área que segue o mouse, área de transferência, leitura em
  voz alta, atalhos globais e um controle remoto sempre acessível.

Nesta versão traduz **de japonês e de inglês para português do Brasil** (RF-309) — é
decisão de produto, não limitação de arquitetura: acrescentar um idioma é acrescentar
uma entrada em `data/languages.toml`, sem tocar em código.

## Como é construído

Todo o projeto é a implementação de **`instrucoes.md`**, uma especificação de 4.841
linhas com 578 requisitos funcionais (`RF-001`–`RF-578`) e 163 parâmetros
(`P-01`–`P-163`). Ela é a única fonte de verdade, e o código aponta para ela: cada
decisão não óbvia cita o requisito que a exige.

**214 desses pontos estão marcados 🔒** — valores calibrados empiricamente ao longo
de mais de dez anos de uso real, que não podem ser deduzidos por raciocínio. Eles
vivem todos em [`src/Gort.Core/Calibration/P.cs`](src/Gort.Core/Calibration/P.cs),
cada um com o seu identificador, o valor exato e o motivo transcrito. A PARTE XII da
especificação proíbe arredondá-los, unificar valores parecidos (`P-34 = 1,3`,
`P-44 = 1,2` e `P-92 = 1,3` são três razões distintas) ou substituí-los por padrões
de biblioteca.

**Catálogos são dado, não código** (RF-029, RF-566, RF-567). Idiomas, motores de OCR,
serviços de tradução, modelos, links e todo o texto da interface vivem em `data/`.
Vinte e nove testes leem os arquivos reais: se alguém mover uma dessas decisões para
dentro do código, eles quebram.

## Pilha

C# / .NET 9 · [Avalonia](https://avaloniaui.net) 11.3.7 · ONNX Runtime com RapidOCR
(detecção DBNet + reconhecimento CRNN com decodificação CTC) · captura por camada
específica de sistema atrás da abstração de RF-577.

```
src/
  Gort.Core/        o pipeline inteiro, sem nenhuma dependência de plataforma
  Gort.Platform/    a abstração de capacidades C1–C20, uma implementação por sistema
  Gort.Ocr.Rapid/   o motor de OCR do Apêndice A
  Gort.Engine/      o ciclo de tradução
  Gort.App/         a interface
tools/              sondas visuais: captura, OCR, ciclo completo, desenho de camada
tests/              618 testes
```

## Construir e rodar

```bash
dotnet build
dotnet test
dotnet run --project src/Gort.App
```

Os modelos de OCR (dezenas de MB) não são versionados; veja
[`modelos/LEIAME.md`](modelos/LEIAME.md).

## Estado

[`ESTADO.md`](ESTADO.md) rastreia o progresso contra a ordem de construção de 19
etapas da PARTE X, etapa por etapa, com o que está verificado em máquina real, o
orçamento de latência medido e as decisões de projeto registradas.

Verificado de ponta a ponta no macOS: **99 ms** para uma caixa de diálogo de duas
linhas com tradução em cache — 33% do orçamento de 300 ms de P-05.

## Autoria

**Lamartine Barbosa** — autor e único contribuidor.
