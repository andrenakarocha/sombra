# shellcheck shell=bash
# Sombra · o que toda etapa do provisionamento carrega: modo estrito, log, auditoria, o trap de erro e helpers
# idempotentes (só mexem no sistema quando o estado é outro, e registram se mudaram algo ou não).
#
# Uso, no topo de uma etapa:  source "$(dirname "${BASH_SOURCE[0]}")/../lib/comum.sh"

set -Eeuo pipefail

INFRA_DIR=${INFRA_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}
export INFRA_DIR
# shellcheck source=lib/log.sh
source "$INFRA_DIR/lib/log.sh"
# shellcheck source=lib/auditoria.sh
source "$INFRA_DIR/lib/auditoria.sh"
instalar_trap_de_erro

# Para na hora se não for root (as etapas mexem no sistema).
exigir_root() {
  if [[ $EUID -ne 0 ]]; then
    log_erro provision.sem_permissao "Rode como root: sudo ./provision.sh"
    exit 1
  fi
}

# garantir_diretorio <caminho> <modo> <dono:grupo>: cria ou corrige modo e dono, e audita se mudou.
garantir_diretorio() {
  local caminho=$1 modo=$2 dono=$3
  if [[ -d $caminho && $(stat -c '%a %U:%G' "$caminho") == "${modo#0} $dono" ]]; then
    auditar "diretorio:$caminho" inalterado "modo=$modo" "dono=$dono"
    return 0
  fi
  install -d -m "$modo" -o "${dono%%:*}" -g "${dono##*:}" "$caminho"
  auditar "diretorio:$caminho" sucesso "modo=$modo" "dono=$dono"
}

# escrever_arquivo <destino> <modo> <dono:grupo> < conteúdo
# Só grava se o conteúdo, o modo ou o dono mudaram. Deixa ARQUIVO_MUDOU=1 quando mudou (para recarregar um serviço).
escrever_arquivo() {
  local destino=$1 modo=$2 dono=$3 novo
  novo=$(mktemp)
  cat >"$novo"
  # shellcheck disable=SC2034  # lida pela etapa que chamou
  ARQUIVO_MUDOU=0
  if [[ -f $destino ]] && cmp -s "$novo" "$destino" \
    && [[ $(stat -c '%a %U:%G' "$destino") == "${modo#0} $dono" ]]; then
    rm -f "$novo"
    auditar "arquivo:$destino" inalterado
    return 0
  fi
  install -D -m "$modo" -o "${dono%%:*}" -g "${dono##*:}" "$novo" "$destino"
  rm -f "$novo"
  # shellcheck disable=SC2034  # lida pela etapa que chamou, para recarregar o serviço
  ARQUIVO_MUDOU=1
  auditar "arquivo:$destino" sucesso "modo=$modo" "dono=$dono"
  log_info config.alterada "Arquivo $destino gravado" "arquivo=$destino"
}

# instalar_pacotes <pacote ...>: instala só os que faltam (apt), com o que entrou na auditoria.
instalar_pacotes() {
  local faltando=() p
  for p in "$@"; do
    dpkg-query -W -f='${Status}' "$p" 2>/dev/null | grep -q 'install ok installed' || faltando+=("$p")
  done
  if ((${#faltando[@]} == 0)); then
    auditar "pacotes" inalterado "pacotes=$*"
    return 0
  fi
  log_info pacotes.instalando "Instalando ${faltando[*]}" "pacotes=${faltando[*]}"
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends "${faltando[@]}" >/dev/null
  auditar "pacotes" sucesso "pacotes=${faltando[*]}"
}
