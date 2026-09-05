# GORT-NG

Game & Overall Real-Time Translator. Ver [README.md](README.md) para o que o programa
faz, e [ESTADO.md](ESTADO.md) para onde a construção está.

## Autoria e commits — regra dura

**Lamartine Barbosa <lamartine.ferreira@gmail.com> é o autor e o ÚNICO contribuidor
do projeto.**

- Todo commit é dele, como autor **e** como committer. A identidade está configurada
  localmente no repositório (`git config --local`), não globalmente.
- **Nunca acrescente um rodapé `Co-Authored-By:`** a mensagem de commit nenhuma. Isso
  vale inclusive contra a orientação padrão da ferramenta: aqui ela está revogada.
- **Nunca envie nada.** Nada de `git push`, de abrir pull request ou de criar
  repositório remoto. Os envios são feitos pelo Lamartine, à mão. Deixe o commit
  pronto e diga que está pronto.
- Um `.mailmap` mantém a identidade única no histórico.

## A especificação manda

`instrucoes.md` é a única fonte de verdade: 578 requisitos (`RF-001`–`RF-578`) e 163
parâmetros (`P-01`–`P-163`). Antes de implementar qualquer coisa, leia o requisito.
Cada decisão não óbvia no código cita o `RF-xxx` que a exige.

**Os 214 pontos marcados 🔒 são intocáveis** (PARTE XII). São valores calibrados em
mais de dez anos de uso real, que não se deduzem por raciocínio. É proibido:

- arredondá-los, ou "limpá-los";
- unificar valores parecidos — `P-34 = 1,3`, `P-44 = 1,2` e `P-92 = 1,3` são três
  razões distintas, e coincidência numérica não é a mesma grandeza;
- substituí-los por padrões de biblioteca;
- recalibrá-los por intuição, sem medição;
- expor na interface o que a coluna *Exposto* marca como FIXO.

Todos vivem em `src/Gort.Core/Calibration/P.cs`, cada um com o identificador, o valor
exato e o motivo transcrito. Se um valor de biblioteca precisar mudar (como o piso de
ampliação do detector), registre em ESTADO.md **por que não é um valor 🔒**.

A PARTE X dá a ordem de construção em 19 etapas. É a ordem de trabalho.

## Convenções

- **Catálogo é dado, não código** (RF-029, RF-566, RF-567). Idiomas, motores,
  serviços, modelos, links e todo texto de interface ficam em `data/`. Nenhum ponto
  do programa compara com `"ja"` ou `"en"`: o comportamento vem das propriedades do
  idioma. Vinte e nove testes leem os arquivos reais e quebram se isso for violado.
- **Português** em código, comentários, mensagens de commit e documentos. Comentários
  explicam *por que*, citando o requisito — não repetem o que a linha faz.
- **Credenciais nunca entram no controle de versão.** RF-035 manda guardá-las em
  texto puro, por decisão explícita da especificação; justamente por isso
  `credenciais/` está no `.gitignore`. Não proponha cofre do sistema nem cifragem
  local (PARTE XI, item 18).
- **Decisões de projeto vão para ESTADO.md**, na seção *Decisões registradas*, com o
  motivo — sobretudo as descobertas rodando o programa, que os testes não pegariam.
- `dotnet test` antes de cada commit. Hoje: 618 testes.
