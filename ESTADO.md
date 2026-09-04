# Estado da construção

Rastreia o progresso contra a **PARTE X — ORDEM DE CONSTRUÇÃO** de `instrucoes.md`,
que é a única fonte de verdade. Cada etapa lista os requisitos que ela cobre.

**Stack (Apêndice A):** C# / .NET 9 · Avalonia · ONNX Runtime com RapidOCR ·
captura por camada específica de sistema atrás da abstração de RF-577.

```
Gort.sln
├── data/                    catálogos como DADO, não código (RF-029)
│   ├── languages.toml       tabela de idiomas (RF-308 a RF-316)
│   └── engines.toml         motores de OCR, serviços, modelos, fontes, links
├── src/
│   ├── Gort.Core/           todo o pipeline, sem nenhuma dependência de plataforma
│   └── Gort.Platform/       abstração C1–C20 (RF-577) — a construir
└── tests/
    ├── Gort.Core.Tests/     220 testes
    └── cases/grouping/      casos de agrupamento gravados em arquivo (Etapa 6)
```

## Etapas concluídas

| Etapa | Requisitos | Situação |
|---|---|---|
| **1 — Esqueleto e configuração** | RF-020 a RF-046 | **Persistência completa.** Falta o ciclo de vida da aplicação (RF-001 a RF-019), que depende da interface. |
| **4 — Pré-processamento** | RF-101 a RF-119 | **Completa** no núcleo. Conta-gotas e pré-visualização binarizada existem como função (`Preprocessor.Preview`); falta a janela. |
| **6 — Estruturação e agrupamento 🔒** | RF-152 a RF-179 | **Completa e verificada.** Todos os seis critérios de aceite do cap. 15 passam, mais 8 casos gravados em arquivo. |
| **— Tratamento textual** | RF-180 a RF-191 | **Completa.** |
| **— Detecção de mudança 🔒** | RF-192 a RF-205 | **Completa.** |
| **— Cache e fontes locais** | RF-206 a RF-224, RF-241 a RF-243 | **Completa.** |
| **13 — Análise automática de cor 🔒** | RF-393 a RF-415 | **Completa e verificada.** Os quatro critérios de aceite do cap. 20 passam. |

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
| 2 — Abstração de plataforma e captura | RF-088, RF-100, RF-568 a RF-578 | C1–C12 com uma implementação por sistema. |
| 3 — Regiões de captura | RF-047 a RF-087 | Molduras, camada de seleção, escala por monitor. |
| 5 — Um motor de OCR | RF-120, RF-121, RF-141 a RF-146 | RapidOCR sobre ONNX Runtime. |
| 7 — Um serviço de tradução e o modo escuro | RF-225 a RF-248, RF-317 a RF-331 | **Primeiro produto utilizável de ponta a ponta.** |
| 8 — Laço, controle e detecção de mudança | RF-004, RF-005, RF-009 a RF-014, RF-547 a RF-551 | A detecção já existe; falta o laço e o protocolo de pausa. |
| 9 — Atalhos e controle remoto | RF-436 a RF-453, RF-517 a RF-522 | |
| 11 — Modo camada | RF-007, RF-332 a RF-343, RF-387 a RF-391 | |
| 12 — Modo sobreposição, layout | RF-344 a RF-386, RF-392 | Colisões, tamanho automático de fonte, quebra por caractere. |
| 14 — Demais motores de OCR | RF-122 a RF-140, RF-147 a RF-151 | |
| 15 — Demais serviços de tradução | RF-249 a RF-307 | |
| 16 — Captura de janela anexada e auxiliares | RF-089 a RF-097, RF-454 a RF-480 | Depende da Etapa 12. |
| 17 — Localização e interface completa | RF-481 a RF-489, RF-501 a RF-546 | |
| 18 — Atualização, comunidade e depuração | RF-416 a RF-435, RF-490 a RF-500 | |
| 19 — Endurecimento | RF-552 a RF-567 e toda a PARTE VIII | |

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

## Como rodar os testes

```
dotnet test tests/Gort.Core.Tests/Gort.Core.Tests.csproj
```

Para acrescentar um caso de agrupamento, basta criar um arquivo em
`tests/cases/grouping/` — nenhum código muda.
