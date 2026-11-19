#!/usr/bin/env bash
# Falha 05 · kill -9 na API: o systemd reinicia o Core (Restart=always); o /health volta a 200 e o journal registra.
source "$(dirname "${BASH_SOURCE[0]}")/comum.sh"
inicio_do_teste
antes=$(systemctl show -p MainPID --value sombra-core)
passo "Core no ar com o PID $antes; kill -9 nele"
t0=$(date +%s%3N)
kill -9 "$antes"
pronto() { [[ $(systemctl show -p MainPID --value sombra-core) != "$antes" ]] && curl -fsS -o /dev/null http://127.0.0.1:5080/health/ready; }
if esperar 90 pronto; then
  t1=$(date +%s%3N)
  passo "o journal do systemd:"
  journalctl -u sombra-core --since "$INICIO" --no-pager -o short-iso | grep -E "Main process exited|Scheduled restart|Started" | evidencia
  fim_do_teste 0 "API de volta em $(((t1 - t0) / 1000)) s, com o /health/ready respondendo 200"
else
  fim_do_teste 1 "a API não voltou em 90 s"
fi
