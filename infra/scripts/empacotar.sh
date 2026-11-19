#!/usr/bin/env bash
# Sombra · gera localmente os mesmos artefatos do .github/workflows/release.yml, em infra/artefatos/ (fora do Git), com
# o SHA256SUMS. Serve para provisionar a VM antes de existir um release no GitHub: a etapa 70 confere os dois do mesmo
# jeito. Uso: infra/scripts/empacotar.sh
set -Eeuo pipefail

RAIZ=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
SAIDA="$RAIZ/infra/artefatos"
COMMIT=$(git -C "$RAIZ" rev-parse --short HEAD)
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT
rm -rf "$SAIDA" && mkdir -p "$SAIDA"

echo "Core (.NET 10, linux-x64, self-contained)"
docker run --rm --user "$(id -u):$(id -g)" -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp -e DOTNET_NOLOGO=1 \
  -e NUGET_PACKAGES=/nuget -v "$HOME/.nuget/packages:/nuget" \
  -v "$RAIZ":/repo -v "$TMP":/saida -w /repo/services/core-api mcr.microsoft.com/dotnet/sdk:10.0.404 \
  dotnet publish sources/Sombra.Api/Sombra.Api.csproj -c Release -r linux-x64 --self-contained true \
  -p:UseAppHost=true -o /saida/core --nologo -v quiet
tar -C "$TMP/core" -czf "$SAIDA/sombra-core-linux-x64.tar.gz" .

echo "Clima (jar do Spring Boot)"
(cd "$RAIZ/services/clima-service" && ./mvnw -q -B -DskipTests package)
cp "$RAIZ/services/clima-service/target/clima-service.jar" "$SAIDA/sombra-clima.jar"

echo "Painel (build estático)"
(cd "$RAIZ/web/painel" && npm ci --no-audit --no-fund >/dev/null && SOMBRA_COMMIT=$COMMIT npm run -s build >/dev/null)
tar -C "$RAIZ/web/painel/dist" -czf "$SAIDA/sombra-painel.tar.gz" .

(cd "$SAIDA" && sha256sum sombra-core-linux-x64.tar.gz sombra-clima.jar sombra-painel.tar.gz >SHA256SUMS)
echo "$COMMIT" >"$SAIDA/VERSAO"
echo "artefatos de $COMMIT em $SAIDA:"
cat "$SAIDA/SHA256SUMS"
