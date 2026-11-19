#!/usr/bin/env bash
# Sombra · provisiona o servidor sombra-srv01 do zero: host, rede, DNS, usuários, dependências, banco, aplicação,
# monitor e validação, uma etapa por script em steps/, na ordem. Idempotente (rodar de novo não muda o que já está
# certo), com log JSON em /var/log/sombra/provision.log e a trilha em /var/log/sombra/auditoria.jsonl. Para na primeira
# etapa que falhar, com a etapa, a linha, o comando e o código registrados.
#
# Uso: sudo ./provision.sh              todas as etapas
#      sudo ./provision.sh 10 30        só as etapas 10 e 30
#      ./provision.sh --listar          lista as etapas
set -Eeuo pipefail

INFRA_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
SOMBRA_DIR_ETAPAS=${SOMBRA_DIR_ETAPAS:-$INFRA_DIR/steps}
SOMBRA_SERVICO=provision
if [[ -z "${SOMBRA_VERSAO:-}" ]]; then
  SOMBRA_VERSAO=$(git -C "$INFRA_DIR" rev-parse --short HEAD 2>/dev/null || echo dev)
fi
export INFRA_DIR SOMBRA_DIR_ETAPAS SOMBRA_SERVICO SOMBRA_VERSAO
# shellcheck source=lib/log.sh
source "$INFRA_DIR/lib/log.sh"
# shellcheck source=lib/auditoria.sh
source "$INFRA_DIR/lib/auditoria.sh"

mapfile -t ETAPAS < <(find "$SOMBRA_DIR_ETAPAS" -maxdepth 1 -name '[0-9][0-9]-*.sh' -printf '%f\n' | sort)

if [[ ${1:-} == --listar ]]; then
  printf '%s\n' "${ETAPAS[@]%.sh}"
  exit 0
fi

if [[ $EUID -ne 0 ]]; then
  echo "rode como root: sudo ./provision.sh" >&2
  exit 1
fi

# A pasta de logs é a primeira coisa: tudo o que vem depois deixa rastro. Só cria se não existir; o dono e o grupo
# definitivos (sombra-ops) são da etapa 40, e uma segunda execução não os desfaz.
if [[ ! -d $SOMBRA_DIR_LOG ]]; then
  install -d -m 0750 -o root -g adm "$SOMBRA_DIR_LOG"
fi
if [[ ! -e $SOMBRA_AUDITORIA ]]; then
  install -m 0640 -o root -g "$(stat -c %G "$SOMBRA_DIR_LOG")" /dev/null "$SOMBRA_AUDITORIA"
fi
instalar_trap_de_erro

# Filtro opcional: os números das etapas pedidas.
selecionadas=()
for etapa in "${ETAPAS[@]}"; do
  if (($# == 0)) || [[ " $* " == *" ${etapa%%-*} "* ]]; then
    selecionadas+=("$etapa")
  fi
done

inicio=$(date +%s%3N)
log_info provision.iniciado "Provisionamento iniciado com ${#selecionadas[@]} etapa(s)" \
  "etapas=${selecionadas[*]%.sh}" "host=$(hostname)"
auditar provision sucesso "etapas=${selecionadas[*]%.sh}" "inicio=1"

for etapa in "${selecionadas[@]}"; do
  nome=${etapa%.sh}
  if ! executar_etapa "$nome" "$SOMBRA_DIR_ETAPAS/$etapa"; then
    log_critico provision.interrompido "Provisionamento interrompido na etapa $nome" "etapa=$nome" \
      "duracaoMs=$(($(date +%s%3N) - inicio))"
    auditar provision falha "etapa=$nome" "duracaoMs=$(($(date +%s%3N) - inicio))"
    exit 1
  fi
done

log_info provision.concluido "Provisionamento concluído" "etapas=${#selecionadas[@]}" \
  "duracaoMs=$(($(date +%s%3N) - inicio))"
auditar provision sucesso "etapas=${#selecionadas[@]}" "duracaoMs=$(($(date +%s%3N) - inicio))"
