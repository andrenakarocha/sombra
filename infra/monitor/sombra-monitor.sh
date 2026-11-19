#!/usr/bin/env bash
# Sombra · monitor do servidor (sombra-monitor.service). A cada ciclo mede CPU (delta do /proc/stat), RAM (MemAvailable),
# disco (df) e os serviços (systemctl is-active e os /health), registra a verificação e, quando um limite fica acima por
# CICLOS_PARA_ALERTAR ciclos seguidos, alerta no log, no journald e com um POST no Core (aparece no painel "Operação").
# Avisa o watchdog do systemd a cada ciclo: se travar, o systemd o reinicia.
set -uo pipefail

DIR=$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)
CONF=${SOMBRA_MONITOR_CONF:-/etc/sombra/monitor.conf}
export SOMBRA_SERVICO=sombra-monitor
# shellcheck source=../lib/log.sh
source "$DIR/lib/log.sh"
# shellcheck disable=SC1090
source "$CONF"
TOKEN=$(grep -m1 '^OPS_TOKEN_DO_MONITOR=' /etc/sombra/segredos.env 2>/dev/null | cut -d= -f2-)
HOST=$(hostname)

notificar() { [[ -n ${NOTIFY_SOCKET:-} ]] && systemd-notify "$@" 2>/dev/null; return 0; }

declare -A acima=() ultimo_alerta=()

# alertar <recurso> <severidade AVISO|CRITICO> <valor> <limite> <mensagem>
alertar() {
  local recurso=$1 severidade=$2 valor=$3 limite=$4 mensagem=$5 agora
  agora=$(date +%s)
  if ((agora - ${ultimo_alerta[$recurso]:-0} < REPETIR_ALERTA_EM)); then return 0; fi
  ultimo_alerta[$recurso]=$agora
  if [[ $severidade == CRITICO ]]; then nivel=log_erro; else nivel=log_aviso; fi
  $nivel monitor.alerta "$mensagem" "recurso=$recurso" "severidade=$severidade" "valor=$valor" "limite=$limite"
  local corpo codigo
  corpo=$(jq -cn --arg host "$HOST" --arg recurso "$recurso" --arg severidade "$severidade" --arg mensagem "$mensagem" \
    --arg momento "$(agora_utc)" --argjson valor "$valor" --argjson limite "$limite" \
    '{host: $host, recurso: $recurso, severidade: $severidade, valor: $valor, limite: $limite, mensagem: $mensagem, momento: $momento}')
  codigo=$(curl -s -o /dev/null -w '%{http_code}' --max-time 3 -X POST "$CORE_ALERTAS_URL" \
    -H 'Content-Type: application/json' -H "Authorization: Bearer $TOKEN" -H "X-Correlation-Id: $SOMBRA_CORRELACAO" \
    -d "$corpo") || codigo=000
  if [[ $codigo != 202 ]]; then
    log_aviso monitor.envio_falhou "O Core não recebeu o alerta (HTTP $codigo); fica só no log" "recurso=$recurso" "codigo=$codigo"
  fi
}

# conferir <recurso> <valor inteiro> <aviso> <crítico> <rótulo>: conta os ciclos acima e alerta (ou avisa a volta ao normal)
conferir() {
  local recurso=$1 valor=$2 aviso=$3 critico=$4 rotulo=$5
  if ((valor >= aviso)); then
    acima[$recurso]=$((${acima[$recurso]:-0} + 1))
    if ((acima[$recurso] >= CICLOS_PARA_ALERTAR)); then
      if ((valor >= critico)); then
        alertar "$recurso" CRITICO "$valor" "$critico" "$rotulo em $valor% (limite crítico $critico%) por ${acima[$recurso]} verificações seguidas"
      else
        alertar "$recurso" AVISO "$valor" "$aviso" "$rotulo em $valor% (limite de aviso $aviso%) por ${acima[$recurso]} verificações seguidas"
      fi
    fi
  else
    if ((${acima[$recurso]:-0} >= CICLOS_PARA_ALERTAR)); then
      log_info monitor.normalizado "$rotulo voltou ao normal ($valor%)" "recurso=$recurso" "valor=$valor"
    fi
    acima[$recurso]=0
    ultimo_alerta[$recurso]=0
  fi
}

ler_cpu() { awk '/^cpu / {ocioso = $5 + $6; total = 0; for (i = 2; i <= NF; i++) total += $i; print total, ocioso}' /proc/stat; }

encerrar() {
  log_info servico.encerrado "Monitor encerrado"
  exit 0
}
trap encerrar TERM INT

log_info servico.iniciado "Monitor iniciado" "intervalo=$INTERVALO" "cicloParaAlertar=$CICLOS_PARA_ALERTAR" \
  "cpu=$CPU_AVISO/$CPU_CRITICO" "ram=$RAM_AVISO/$RAM_CRITICO" "disco=$DISCO_AVISO/$DISCO_CRITICO"
notificar --ready --status="monitorando a cada ${INTERVALO}s"

read -r total_antes ocioso_antes < <(ler_cpu)
while true; do
  sleep "$INTERVALO" &
  wait $!
  SOMBRA_CORRELACAO="sombra-monitor:$(od -An -N4 -tx1 /dev/urandom | tr -d ' \n')"

  read -r total ocioso < <(ler_cpu)
  delta=$((total - total_antes))
  cpu=$((delta > 0 ? 100 * (delta - (ocioso - ocioso_antes)) / delta : 0))
  total_antes=$total
  ocioso_antes=$ocioso

  ram=$(awk '/^MemTotal:/ {t = $2} /^MemAvailable:/ {a = $2} END {printf "%d", 100 * (t - a) / t}' /proc/meminfo)

  disco_max=0
  disco_ponto=/
  for ponto in $DISCO_PONTOS; do
    uso=$(df --output=pcent "$ponto" 2>/dev/null | tail -n1 | tr -dc '0-9')
    if ((${uso:-0} > disco_max)); then disco_max=$uso; disco_ponto=$ponto; fi
  done

  inativos=()
  for s in $SERVICOS; do
    systemctl is-active --quiet "$s" || inativos+=("$s")
  done
  sem_saude=()
  for par in $SAUDE; do
    curl -fsS -o /dev/null --max-time 3 "${par#*=}" 2>/dev/null || sem_saude+=("${par%%=*}")
  done

  log_info monitor.verificacao "CPU $cpu%, RAM $ram%, disco $disco_max% ($disco_ponto)" \
    "cpu=$cpu" "ram=$ram" "disco=$disco_max" "discoPonto=$disco_ponto" \
    "servicosInativos=${inativos[*]:-}" "semSaude=${sem_saude[*]:-}"

  conferir CPU "$cpu" "$CPU_AVISO" "$CPU_CRITICO" "CPU"
  conferir RAM "$ram" "$RAM_AVISO" "$RAM_CRITICO" "Memória"
  conferir DISCO "$disco_max" "$DISCO_AVISO" "$DISCO_CRITICO" "Disco ($disco_ponto)"

  for s in "${inativos[@]}"; do
    log_erro monitor.servico_inativo "O serviço $s está parado" "servico=$s"
    alertar "SERVICO:$s" CRITICO 0 1 "O serviço $s está parado; o systemd tenta reiniciá-lo"
  done
  for s in "${sem_saude[@]}"; do
    [[ " ${inativos[*]:-} " == *" $s "* ]] && continue
    alertar "SAUDE:$s" CRITICO 0 1 "O /health de $s não respondeu 200"
  done
  [[ ${#inativos[@]} -eq 0 ]] && for s in $SERVICOS; do ultimo_alerta["SERVICO:$s"]=0; done

  notificar WATCHDOG=1 --status="CPU $cpu% · RAM $ram% · disco $disco_max%"
done
