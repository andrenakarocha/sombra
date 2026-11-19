# shellcheck shell=bash
# Sombra · log estruturado da VM (docs/observabilidade.md §1 e §5): uma linha JSON por evento, com os oito campos
# obrigatórios, no stdout (vira journald quando roda sob o systemd) e no arquivo de log do serviço.
#
# Uso: log_info <evento> <mensagem> [chave=valor ...]   (também log_aviso, log_erro e log_critico)
# Variáveis: SOMBRA_SERVICO (provision, sombra-monitor), SOMBRA_LOG_ARQUIVO, SOMBRA_AMBIENTE, SOMBRA_VERSAO,
# SOMBRA_CORRELACAO. O jq monta o JSON, então aspas, quebras de linha e acentos nunca quebram a linha.

: "${SOMBRA_SERVICO:=provision}"
: "${SOMBRA_AMBIENTE:=Production}"
: "${SOMBRA_VERSAO:=dev}"
: "${SOMBRA_DIR_LOG:=/var/log/sombra}"
: "${SOMBRA_LOG_ARQUIVO:=$SOMBRA_DIR_LOG/$SOMBRA_SERVICO.log}"

# Uma correlação por execução: tudo o que um provisionamento ou uma volta do monitor faz sai com o mesmo ID.
if [[ -z "${SOMBRA_CORRELACAO:-}" ]]; then
  SOMBRA_CORRELACAO="$SOMBRA_SERVICO:$(od -An -N4 -tx1 /dev/urandom | tr -d ' \n')"
fi
export SOMBRA_SERVICO SOMBRA_AMBIENTE SOMBRA_VERSAO SOMBRA_DIR_LOG SOMBRA_LOG_ARQUIVO SOMBRA_CORRELACAO

# Instante em UTC com milissegundos (§1).
agora_utc() {
  date -u +%Y-%m-%dT%H:%M:%S.%3NZ
}

# _log <nível> <evento> <mensagem> [chave=valor ...]
# Os valores que são número inteiro (sem zero à esquerda, como um modo 0640) saem como número; o resto, como texto.
_log() {
  local nivel=$1 evento=$2 mensagem=$3
  shift 3
  local extras='{}' par chave valor
  for par in "$@"; do
    chave=${par%%=*}
    valor=${par#*=}
    if [[ $valor =~ ^-?(0|[1-9][0-9]*)$ ]]; then
      extras=$(jq -c --arg k "$chave" --argjson v "$valor" '. + {($k): $v}' <<<"$extras")
    else
      extras=$(jq -c --arg k "$chave" --arg v "$valor" '. + {($k): $v}' <<<"$extras")
    fi
  done
  local linha
  linha=$(jq -cn \
    --arg timestamp "$(agora_utc)" --arg level "$nivel" --arg service "$SOMBRA_SERVICO" \
    --arg version "$SOMBRA_VERSAO" --arg environment "$SOMBRA_AMBIENTE" --arg correlationId "$SOMBRA_CORRELACAO" \
    --arg event "$evento" --arg message "$mensagem" --argjson extras "$extras" \
    '{timestamp: $timestamp, level: $level, service: $service, version: $version, environment: $environment,
      correlationId: $correlationId, event: $event, message: $message} + $extras')
  printf '%s\n' "$linha"
  if [[ -d $(dirname "$SOMBRA_LOG_ARQUIVO") && -w $(dirname "$SOMBRA_LOG_ARQUIVO") ]]; then
    printf '%s\n' "$linha" >>"$SOMBRA_LOG_ARQUIVO"
  fi
}

log_info() { _log Information "$@"; }
log_aviso() { _log Warning "$@"; }
log_erro() { _log Error "$@"; }
log_critico() { _log Critical "$@"; }
