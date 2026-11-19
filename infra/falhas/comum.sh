# shellcheck shell=bash
# Sombra · o que os testes de falha compartilham: log e auditoria (serviço "falhas"), espera com prazo e a narração que
# vira evidência (infra/evidencias/). Cada teste sai com 0 só se a recuperação ou o registro esperado aconteceu.
export SOMBRA_SERVICO=falhas
INFRA_DIR=${INFRA_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}
# shellcheck source=../lib/log.sh
source "$INFRA_DIR/lib/log.sh"
# shellcheck source=../lib/auditoria.sh
source "$INFRA_DIR/lib/auditoria.sh"
set -uo pipefail

# o log JSON vai para /var/log/sombra/falhas.log; na tela fica só a narração do teste (que vira o print da evidência)
log_info() { _log Information "$@" >/dev/null; }
log_aviso() { _log Warning "$@" >/dev/null; }
log_erro() { _log Error "$@" >/dev/null; }

TESTE=$(basename "$0" .sh)
passo() { printf '▶ %s\n' "$*"; }
evidencia() { sed 's/^/  │ /'; }

# esperar <segundos> <comando...>: tenta a cada segundo até o prazo
esperar() {
  local prazo=$1
  shift
  for _ in $(seq 1 "$prazo"); do
    "$@" >/dev/null 2>&1 && return 0
    sleep 1
  done
  return 1
}

# o journal do monitor (JSON por linha) a partir de um instante
eventos_do_monitor() { journalctl -u sombra-monitor --since "$1" --no-pager -o cat 2>/dev/null; }

inicio_do_teste() {
  # shellcheck disable=SC2034  # lido pelos testes
  INICIO=$(date '+%Y-%m-%d %H:%M:%S')
  log_info falha.iniciada "Teste de falha $TESTE" "teste=$TESTE"
  auditar "falha:$TESTE" sucesso "fase=inicio"
}

fim_do_teste() { # <ok 0|1> <mensagem>
  if (($1 == 0)); then
    log_info falha.recuperada "$2" "teste=$TESTE"
    auditar "falha:$TESTE" sucesso "fase=fim" "resultado=$2"
    printf '✔ %s\n' "$2"
  else
    log_erro falha.nao_recuperada "$2" "teste=$TESTE"
    auditar "falha:$TESTE" falha "fase=fim" "resultado=$2"
    printf '✘ %s\n' "$2"
  fi
  return "$1"
}
