#!/usr/bin/env bash
# Sombra · taint estático (F2.7). Duas provas, na imagem oficial do Semgrep com versão fixa:
#   1. o canário dispara exatamente as linhas marcadas com "ruleid" (e nenhuma "ok");
#   2. o código de produção do Core sai sem nenhum achado e sem erro de parse.
# Grava o SARIF em security/semgrep/semgrep.sarif (o CI manda para o GitHub Code Scanning).
set -euo pipefail

RAIZ=$(cd "$(dirname "$0")/../.." && pwd)
cd "$RAIZ"
IMAGEM=semgrep/semgrep:1.177.0
REGRAS=security/semgrep/sombra-taint.yaml
CANARIO=security/semgrep/canario/Canario.cs
SARIF=security/semgrep/semgrep.sarif

semgrep() {
  docker run --rm --user "$(id -u):$(id -g)" -e HOME=/tmp -v "$RAIZ:/src" -w /src "$IMAGEM" \
    semgrep scan --metrics=off --disable-version-check --config "$REGRAS" "$@"
}

linhas() { python3 -I -c 'import json,sys; print(" ".join(str(n) for n in sorted({r["start"]["line"] for r in json.load(sys.stdin)["results"]})))'; }

esperadas=$(grep -n '// ruleid:' "$CANARIO" | cut -d: -f1 | awk '{printf "%s%d", (NR > 1 ? " " : ""), $1 + 1}')
achadas=$(semgrep --json "$CANARIO" 2>/dev/null | linhas)
if [ "$esperadas" != "$achadas" ]; then
  echo "canário: esperava as linhas [$esperadas] e o Semgrep achou [$achadas]"
  exit 1
fi
echo "canário ok: $(wc -w <<<"$esperadas" | tr -d ' ') vazamentos achados, nenhum falso positivo"

semgrep --json --sarif-output "$SARIF" --exclude '*/bin/*' --exclude '*/obj/*' --exclude '*Tests*' services/core-api/sources 2>/dev/null \
  | python3 -I -c '
import json, sys
d = json.load(sys.stdin)
arquivos, achados, erros = len(d["paths"]["scanned"]), d["results"], d.get("errors", [])
for r in achados:
    print(f"  {r["path"]}:{r["start"]["line"]} {r["check_id"].split(".")[-1]}")
for e in erros:
    print(f"  erro de parse: {e.get("path", "?")}")
print(f"código do Core: {arquivos} arquivos, {len(achados)} achados, {len(erros)} erros de parse")
sys.exit(1 if achados or erros else 0)'

# O Code Scanning do GitHub não respeita as supressões do SARIF: um "nosemgrep" justificado (os endpoints
# propositalmente vulneráveis da auditoria) viraria alerta aberto. O SARIF enviado leva só os achados ativos; os
# suprimidos ficam no código, com a justificativa, e aparecem aqui.
python3 -I - "$SARIF" <<'PY'
import json, sys
caminho = sys.argv[1]
sarif = json.load(open(caminho, encoding="utf-8"))
suprimidos = 0
for execucao in sarif["runs"]:
    ativos = [r for r in execucao["results"] if not r.get("suppressions")]
    suprimidos += len(execucao["results"]) - len(ativos)
    execucao["results"] = ativos
json.dump(sarif, open(caminho, "w", encoding="utf-8"))
print(f"SARIF para o Code Scanning: {suprimidos} achados com nosemgrep justificado ficam de fora")
PY
