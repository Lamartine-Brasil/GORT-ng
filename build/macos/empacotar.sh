#!/bin/bash
# Monta GORT.app a partir de uma publicação do .NET.
#
# Um pacote .app não é enfeite no macOS: é o que dá ao programa um identificador estável na
# base de permissões do sistema. Rodando solto, a permissão de gravação de tela fica presa
# ao processo que lançou o programa — o terminal, o depurador — e some quando ele muda.
#
# O pacote é AUTOSSUFICIENTE: leva o runtime do .NET dentro dele. Um pacote dependente do
# runtime não abre por duplo clique quando o .NET está instalado fora do caminho padrão —
# pelo Homebrew, por exemplo —, porque o Finder não passa PATH nem DOTNET_ROOT. Custa uns
# 80 MB e resolve o problema para sempre.
#
# Uso:  build/macos/empacotar.sh [arm64|x64] [pasta-de-saida] [--dependente]

set -euo pipefail

RAIZ="$(cd "$(dirname "$0")/../.." && pwd)"
ARQ="${1:-$( [ "$(uname -m)" = "arm64" ] && echo arm64 || echo x64 )}"
SAIDA="${2:-$RAIZ/artefatos}"
RID="osx-$ARQ"

AUTOSSUFICIENTE=true
for arg in "$@"; do
  [ "$arg" = "--dependente" ] && AUTOSSUFICIENTE=false
done

APP="$SAIDA/GORT.app"
PUB="$SAIDA/publicacao-$RID"

echo "Publicando para ${RID}…"
dotnet publish "$RAIZ/src/Gort.App/Gort.App.csproj" \
  -c Release -r "$RID" --self-contained "$AUTOSSUFICIENTE" \
  -o "$PUB" --nologo -v quiet

# A versão do pacote acompanha a do executável, para que a tela de abertura (RF-004) e o
# "sobre" (RF-543) não divirjam do que o sistema mostra.
VERSAO="$(defaults read "$PUB/Gort.App.dll" CFBundleShortVersionString 2>/dev/null || true)"
if [ -z "$VERSAO" ]; then
  VERSAO="$(grep -m1 '<Version>' "$RAIZ/src/Gort.App/Gort.App.csproj" 2>/dev/null \
            | sed 's/.*<Version>\(.*\)<\/Version>.*/\1/' || true)"
fi
VERSAO="${VERSAO:-1.0.0}"

echo "Montando $APP (versão $VERSAO)…"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

sed "s/__VERSAO__/$VERSAO/g" "$RAIZ/build/macos/Info.plist" > "$APP/Contents/Info.plist"

cp -R "$PUB"/* "$APP/Contents/MacOS/"

# RF-029 — os catálogos são DADOS e vivem ao lado do executável; RF-003 faz o programa
# tornar essa pasta o diretório de trabalho corrente.
if [ -d "$RAIZ/data" ]; then
  rm -rf "$APP/Contents/MacOS/data"
  cp -R "$RAIZ/data" "$APP/Contents/MacOS/data"
fi

chmod +x "$APP/Contents/MacOS/Gort.App"

# Assinatura ad-hoc: sem ela o sistema trata cada execução como um binário novo e volta a
# pedir as permissões toda vez. Não substitui uma assinatura de desenvolvedor para
# distribuição, mas resolve o uso local, que é o caso deste programa.
codesign --force --deep --sign - "$APP" 2>/dev/null \
  && echo "Assinado (ad-hoc)." \
  || echo "Aviso: não foi possível assinar; as permissões podem ser pedidas a cada execução."

TAMANHO="$(du -sh "$APP" | cut -f1)"

echo
echo "Pronto: $APP  ($TAMANHO)"
echo "Abrir com:  open '$APP'"
