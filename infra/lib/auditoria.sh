# shellcheck shell=bash
# Sombra · trilha de auditoria da VM (docs/observabilidade.md §6): cada ação registra o quê, quando, quem, onde e o
# resultado (com o código de saída) numa linha JSON de /var/log/sombra/auditoria.jsonl. Ninguém apaga: a rotação é só
# por tempo. Depende de lib/log.sh.
#
# Uso:
#   auditar <ação> <resultado> [chave=valor ...]       registra uma linha (resultado: sucesso | falha | inalterado)
#   executar_etapa <nome> <script>                     roda uma etapa com log de início e fim, duração e auditoria
#   instalar_trap_de_erro                              registra linha, comando e código de qualquer falha (trap ERR)

: "${SOMBRA_AUDITORIA:=$SOMBRA_DIR_LOG/auditoria.jsonl}"
export SOMBRA_AUDITORIA

# Quem executou: o usuário real por trás do sudo, ou o próprio usuário.
quem_executa() {
  printf '%s' "${SUDO_USER:-$(id -un)}"
}

# auditar <ação> <resultado> [chave=valor ...]
auditar() {
  local acao=$1 resultado=$2
  shift 2
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
    --arg quando "$(agora_utc)" --arg quem "$(quem_executa)" --argjson uid "$(id -u)" --arg host "$(hostname)" \
    --arg servico "$SOMBRA_SERVICO" --arg correlationId "$SOMBRA_CORRELACAO" --arg acao "$acao" \
    --arg resultado "$resultado" --argjson extras "$extras" \
    '{quando: $quando, quem: $quem, uid: $uid, host: $host, servico: $servico, correlationId: $correlationId,
      acao: $acao, resultado: $resultado} + $extras')
  if [[ -d $(dirname "$SOMBRA_AUDITORIA") && -w $(dirname "$SOMBRA_AUDITORIA") ]]; then
    printf '%s\n' "$linha" >>"$SOMBRA_AUDITORIA"
  fi
}

# _ao_falhar <linha> <comando> <código>: chamado pelo trap ERR.
_ao_falhar() {
  local linha=$1 comando=$2 codigo=$3
  log_erro "${SOMBRA_EVENTO_DE_FALHA:-script.falhou}" "Falha na linha $linha: $comando (código $codigo)" \
    "script=${SOMBRA_SCRIPT_ATUAL:-${BASH_SOURCE[1]:-?}}" "linha=$linha" "comando=$comando" "codigo=$codigo"
  auditar "${SOMBRA_ACAO_ATUAL:-script}" falha "script=${SOMBRA_SCRIPT_ATUAL:-${BASH_SOURCE[1]:-?}}" \
    "linha=$linha" "comando=$comando" "codigo=$codigo"
}

# Toda falha (comando com código ≠ 0 fora de um if/||) vira uma linha de log e de auditoria com linha, comando e código.
instalar_trap_de_erro() {
  set -o errtrace
  trap '_ao_falhar "$LINENO" "$BASH_COMMAND" "$?"' ERR
}

# executar_etapa <nome> <script>: roda a etapa num processo próprio (ela não herda nem quebra o estado do orquestrador),
# com provision.etapa_iniciada, provision.etapa_concluida ou provision.etapa_falhou, a duração e a auditoria.
executar_etapa() {
  local nome=$1 script=$2 inicio fim codigo=0
  log_info provision.etapa_iniciada "Etapa $nome iniciada" "etapa=$nome"
  inicio=$(date +%s%3N)
  SOMBRA_ACAO_ATUAL="etapa:$nome" SOMBRA_SCRIPT_ATUAL="$script" SOMBRA_EVENTO_DE_FALHA=provision.comando_falhou \
    bash "$script" || codigo=$?
  fim=$(date +%s%3N)
  if ((codigo == 0)); then
    log_info provision.etapa_concluida "Etapa $nome concluída" "etapa=$nome" "duracaoMs=$((fim - inicio))"
    auditar "etapa:$nome" sucesso "codigo=0" "duracaoMs=$((fim - inicio))"
  else
    log_erro provision.etapa_falhou "Etapa $nome falhou com o código $codigo" "etapa=$nome" "codigo=$codigo" \
      "duracaoMs=$((fim - inicio))"
    auditar "etapa:$nome" falha "codigo=$codigo" "duracaoMs=$((fim - inicio))"
  fi
  return "$codigo"
}
