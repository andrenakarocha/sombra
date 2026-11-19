#!/usr/bin/env bash
# Falha 01 · kill -9 no monitor: o systemd (Restart=always) o sobe de novo em ~5 s e registra no journal.
source "$(dirname "${BASH_SOURCE[0]}")/comum.sh"
inicio_do_teste
antes=$(systemctl show -p MainPID --value sombra-monitor)
passo "monitor ativo com o PID $antes; kill -9 nele"
kill -9 "$antes"
novo() { local p; p=$(systemctl show -p MainPID --value sombra-monitor); [[ $p != 0 && $p != "$antes" ]] && systemctl is-active --quiet sombra-monitor; }
if esperar 20 novo; then
  depois=$(systemctl show -p MainPID --value sombra-monitor)
  passo "o journal do systemd:"
  journalctl -u sombra-monitor --since "$INICIO" --no-pager -o short-iso | grep -E "Main process exited|Scheduled restart|Started|signal=KILL|status=9" | evidencia
  fim_do_teste 0 "monitor de volta sozinho: PID $antes → $depois ($(systemctl show -p NRestarts --value sombra-monitor) reinício(s) no total)"
else
  fim_do_teste 1 "o monitor não voltou em 20 s"
fi
