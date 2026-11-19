#!/usr/bin/env bash
# Sombra · a prova de reprodutibilidade (F8.7): apaga a VM, cria de novo do zero só com o Vagrantfile e o provision.sh,
# provisiona uma segunda vez e confere que nada mudou. Grava as duas execuções e o resumo em infra/evidencias/.
# Uso (no host): infra/scripts/reproduzir.sh
set -Eeuo pipefail

INFRA=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
SAIDA="$INFRA/evidencias"
cd "$INFRA"
mkdir -p "$SAIDA"

eventos() { sed 's/^ *default: //' "$1" | jq -rR 'fromjson? | select(.service == "provision") | .event'; }

inicio=$(date +%s)
vagrant destroy -f >/dev/null 2>&1 || true
vagrant up >"$SAIDA/reproducao-1-vagrant-up.log" 2>&1
fim_up=$(date +%s)
vagrant provision >"$SAIDA/reproducao-2-segunda-execucao.log" 2>&1
fim=$(date +%s)

{
  echo '$ vagrant destroy -f && vagrant up      (do zero, sem nenhum passo manual)'
  printf '  duração: %d min %02d s\n' $(((fim_up - inicio) / 60)) $(((fim_up - inicio) % 60))
  printf '  etapas concluídas: %s\n' "$(eventos "$SAIDA/reproducao-1-vagrant-up.log" | grep -c '^provision.etapa_concluida$')"
  printf '  arquivos gravados: %s\n' "$(eventos "$SAIDA/reproducao-1-vagrant-up.log" | grep -c '^config.alterada$')"
  printf '  validação: %s\n' "$(eventos "$SAIDA/reproducao-1-vagrant-up.log" | grep -qx 'validacao.aprovada' && echo aprovada || echo REPROVADA)"
  echo
  echo '$ vagrant provision                      (segunda execução, sobre a máquina já pronta)'
  printf '  duração: %d s\n' $((fim - fim_up))
  printf '  etapas concluídas: %s\n' "$(eventos "$SAIDA/reproducao-2-segunda-execucao.log" | grep -c '^provision.etapa_concluida$')"
  printf '  arquivos gravados: %s   (idempotente: nada muda)\n' "$(eventos "$SAIDA/reproducao-2-segunda-execucao.log" | grep -c '^config.alterada$')"
  printf '  validação: %s\n' "$(eventos "$SAIDA/reproducao-2-segunda-execucao.log" | grep -qx 'validacao.aprovada' && echo aprovada || echo REPROVADA)"
} | tee "$SAIDA/reproducao.txt"

eventos "$SAIDA/reproducao-1-vagrant-up.log" | grep -qx validacao.aprovada
eventos "$SAIDA/reproducao-2-segunda-execucao.log" | grep -qx validacao.aprovada
[[ $(eventos "$SAIDA/reproducao-2-segunda-execucao.log" | grep -c '^config.alterada$') -eq 0 ]]
