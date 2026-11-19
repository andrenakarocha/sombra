#!/usr/bin/env bash
# Sombra · Clima Service: regenera os prints de evidência da rubrica em docs/evidencias/ (D18: prints por script).
# Pré-requisito: o ambiente local no ar (make up). Usa o Chrome headless para fotografar o Swagger e as saídas de terminal.
# Uso: services/clima-service/scripts/evidencias.sh
set -Eeuo pipefail

cd "$(dirname "$0")/.."
RAIZ=$(cd ../.. && pwd)
SAIDA="$PWD/docs/evidencias"
CHROME=${CHROME:-google-chrome-stable}
URL=${CLIMA_URL:-http://127.0.0.1:8081}
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT
mkdir -p "$SAIDA"
json() { python3 -I -c 'import json, sys; print(json.dumps(json.load(sys.stdin), ensure_ascii=False, indent=2))'; }

set -a
# shellcheck disable=SC1091
. "$RAIZ/.env"
set +a

fotografar() { # url arquivo largura altura
  "$CHROME" --headless=new --no-sandbox --disable-gpu --hide-scrollbars --force-device-scale-factor=1 \
    --window-size="$3,$4" --virtual-time-budget=10000 --screenshot="$SAIDA/$2" "$1" >/dev/null 2>&1
  echo "  $2"
}

terminal() { # título arquivo-de-texto print
  local linhas altura
  linhas=$(wc -l <"$2")
  altura=$(( linhas * 19 + 90 ))
  python3 -I - "$1" "$2" "$TMP/$3.html" <<'PY'
import html, re, sys
titulo, entrada, saida = sys.argv[1:4]
texto = re.sub(r"\x1b\[[0-9;]*m", "", open(entrada, encoding="utf-8").read())
corpo = html.escape(texto)
corpo = re.sub(r"^(\$ .*)$", r'<span class="cmd">\1</span>', corpo, flags=re.M)
corpo = re.sub(r"(BUILD SUCCESS|Failures: 0, Errors: 0|All coverage checks have been met\.|\bt\b$|UP)", r'<span class="ok">\1</span>', corpo, flags=re.M)
open(saida, "w", encoding="utf-8").write(f"""<!doctype html><meta charset="utf-8">
<style>body{{margin:0;background:#0f1412;color:#d9e4dc;font:13.5px/19px "JetBrains Mono","DejaVu Sans Mono",monospace}}
header{{background:#1c2622;padding:10px 18px;color:#9fb5a8;font-family:sans-serif;font-size:13px}}
pre{{margin:0;padding:14px 18px;white-space:pre-wrap}} .cmd{{color:#f2c36b}} .ok{{color:#7ad19b;font-weight:bold}}</style>
<header>{html.escape(titulo)}</header><pre>{corpo}</pre>""")
PY
  fotografar "file://$TMP/$3.html" "$3.png" 1280 "$altura"
}

echo "prints em $SAIDA:"

# 1 · documentação gerada do código, aberta numa URL
fotografar "$URL/swagger-ui/index.html" swagger.png 1440 1850
# 2 · um endpoint aberto, com os erros 400/404/409/422 documentados e o corpo Problema
fotografar "$URL/swagger-ui/index.html#/Observa%C3%A7%C3%B5es/registrarObservacao" swagger-erros.png 1440 3400

# 3 · banco relacional de verdade, migrações aplicadas, chave estrangeira e o seed
{
  echo '$ docker compose exec db psql -U sombra_clima -d sombra_clima'
  docker compose -f "$RAIZ/compose.yaml" exec -T -e PGPASSWORD="$CLIMA_DB_PASSWORD" db \
    psql -h 127.0.0.1 -U sombra_clima -d sombra_clima -P pager=off \
    -c "select installed_rank, version, description, success from flyway_schema_history order by installed_rank" \
    -c "\dt" \
    -c "select conname, pg_get_constraintdef(oid) from pg_constraint where contype = 'f' order by conname" \
    -c "select fonte, count(*) as horas, min(observada_em), max(observada_em), max(indice_de_calor_c) as pico_c from observacao group by fonte order by fonte" \
    -c "select count(*) as pontos_de_resfriamento from ponto_de_resfriamento"
} >"$TMP/migracoes.txt" 2>&1
terminal "PostgreSQL 16 · banco sombra_clima criado pelas migrações do Flyway (V1–V4)" "$TMP/migracoes.txt" migracoes

# 4 · um comando roda todos os testes
{
  echo '$ ./mvnw verify'
  ./mvnw -B -ntp verify 2>&1 | grep -E "Tests run:|BUILD|coverage checks|Total time" | sed -E 's/^\[(INFO|WARNING)\] //'
} >"$TMP/testes.txt"
terminal "Clima Service · domínio, API (MockMvc + Testcontainers), arquitetura (ArchUnit) e cobertura do domínio (JaCoCo)" "$TMP/testes.txt" testes

# 5 · a API respondendo: o replay real de nov/2023, o risco e os pontos de resfriamento perto de casa
{
  echo "\$ curl -X POST $URL/api/v1/replay -d '{\"regiao\":\"SP-MIRANTE\",\"inicio\":\"2023-11-15T15:00:00-03:00\",\"velocidade\":60}'"
  curl -fsS -X POST "$URL/api/v1/replay" -H 'Content-Type: application/json' \
    -d '{"regiao":"SP-MIRANTE","inicio":"2023-11-15T15:00:00-03:00","velocidade":60}' | json
  echo "\$ curl $URL/api/v1/regioes/SP-MIRANTE/risco-calor"
  curl -fsS -H 'X-Correlation-Id: evidencia-f38' "$URL/api/v1/regioes/SP-MIRANTE/risco-calor" | json
  echo "\$ curl '$URL/api/v1/pontos-de-resfriamento/proximos?lat=-23.4964&lon=-46.62&raioKm=1'"
  curl -fsS "$URL/api/v1/pontos-de-resfriamento/proximos?lat=-23.4964&lon=-46.62&raioKm=1" | json
  echo "\$ curl $URL/api/v1/estacoes/A701/observacoes -d '{\"observadaEm\":\"2026-11-18T18:00:00Z\",\"temperaturaC\":-9999,\"umidadePct\":50}'"
  curl -sS -X POST "$URL/api/v1/estacoes/A701/observacoes" -H 'Content-Type: application/json' \
    -d '{"observadaEm":"2026-11-18T18:00:00Z","temperaturaC":-9999,"umidadePct":50}' | json
  curl -fsS -X DELETE "$URL/api/v1/replay"
} >"$TMP/api.txt" 2>&1
terminal "A API respondendo: replay da onda de calor real, risco de calor, pontos de resfriamento e o -9999 recusado" "$TMP/api.txt" api
