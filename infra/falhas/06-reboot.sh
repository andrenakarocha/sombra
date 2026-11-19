#!/usr/bin/env bash
# Falha 06 · reboot da VM: todos os serviços voltam sozinhos (enabled no boot). Duas fases:
#   sudo falhas/06-reboot.sh             guarda o boot atual e reinicia a máquina
#   sudo falhas/06-reboot.sh --conferir  depois do boot: outro boot_id, e cada serviço ativo e respondendo
source "$(dirname "${BASH_SOURCE[0]}")/comum.sh"
MARCA=/var/lib/sombra/boot-antes-do-teste
SERVICOS="postgresql mosquitto nginx dnsmasq sombra-clima sombra-core sombra-monitor"
if [[ ${1:-} != --conferir ]]; then
  inicio_do_teste
  cat /proc/sys/kernel/random/boot_id >"$MARCA"
  passo "boot atual $(cut -c1-8 "$MARCA"); reiniciando a VM"
  log_info falha.reboot "Reiniciando a VM para o teste" "teste=$TESTE"
  systemctl reboot
  exit 0
fi
antes=$(cat "$MARCA" 2>/dev/null)
agora=$(cat /proc/sys/kernel/random/boot_id)
passo "boot antes $(cut -c1-8 <<<"$antes") → depois $(cut -c1-8 <<<"$agora"); no ar há $(awk '{print int($1)}' /proc/uptime) s"
todos() { for s in $SERVICOS; do systemctl is-active --quiet "$s" || return 1; done; curl -fsS -o /dev/null http://127.0.0.1:5080/health/ready; }
if [[ -n $antes && $antes != "$agora" ]] && esperar 180 todos; then
  for s in $SERVICOS; do printf '%-16s %s (desde %s)\n' "$s" "$(systemctl is-active "$s")" "$(systemctl show -p ActiveEnterTimestamp --value "$s" | awk '{print $3}')"; done | evidencia
  fim_do_teste 0 "depois do reboot, os ${SERVICOS// /, } voltaram sozinhos"
else
  fim_do_teste 1 "algum serviço não voltou depois do reboot"
fi
