#!/usr/bin/env bash
# Falha 02 · CPU alta com stress-ng: o monitor alerta (CRITICO) no log, no journal e no painel "Operação".
source "$(dirname "${BASH_SOURCE[0]}")/comum.sh"
inicio_do_teste
passo "stress-ng em todos os núcleos por 80 s"
stress-ng --cpu 0 --cpu-load 100 --timeout 80s --quiet &
carga=$!
alertou() { eventos_do_monitor "$INICIO" | grep '"event":"monitor.alerta"' | grep -q '"recurso":"CPU"'; }
if esperar 100 alertou; then
  passo "o alerta do monitor (journal e /var/log/sombra/sombra-monitor.log):"
  eventos_do_monitor "$INICIO" | grep '"recurso":"CPU"' | jq -c '{timestamp, level, event, message, valor, limite}' | head -n2 | evidencia
  passo "o Core recebeu e empurrou para o painel:"
  journalctl -u sombra-core --since "$INICIO" --no-pager -o cat | grep '"event":"infra.alerta_recebido"' | jq -c '{timestamp, event, message}' | head -n1 | evidencia
  kill "$carga" 2>/dev/null
  fim_do_teste 0 "CPU alta detectada e alertada (log, journal, POST no Core → painel)"
else
  kill "$carga" 2>/dev/null
  fim_do_teste 1 "nenhum alerta de CPU em 100 s"
fi
