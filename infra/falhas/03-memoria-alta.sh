#!/usr/bin/env bash
# Falha 03 · memória alta com stress-ng: o monitor alerta. Ocupa até ~86% da RAM (acima do aviso de 80%, sem levar a
# máquina ao OOM: os serviços seguem de pé).
source "$(dirname "${BASH_SOURCE[0]}")/comum.sh"
inicio_do_teste
total=$(awk '/^MemTotal:/ {print $2}' /proc/meminfo)
livre=$(awk '/^MemAvailable:/ {print $2}' /proc/meminfo)
alocar=$((total * 86 / 100 - (total - livre)))
passo "stress-ng --vm alocando $((alocar / 1024)) MB por 80 s (RAM em uso hoje: $((100 * (total - livre) / total))%)"
stress-ng --vm 1 --vm-bytes "${alocar}k" --vm-keep --timeout 80s --quiet &
carga=$!
alertou() { eventos_do_monitor "$INICIO" | grep '"event":"monitor.alerta"' | grep -q '"recurso":"RAM"'; }
if esperar 100 alertou; then
  passo "o alerta do monitor:"
  eventos_do_monitor "$INICIO" | grep '"recurso":"RAM"' | jq -c '{timestamp, level, event, message}' | head -n1 | evidencia
  kill "$carga" 2>/dev/null
  passo "os serviços continuam de pé: $(systemctl is-active sombra-core sombra-clima postgresql | paste -sd ' ')"
  fim_do_teste 0 "memória alta detectada e alertada, sem derrubar nenhum serviço"
else
  kill "$carga" 2>/dev/null
  fim_do_teste 1 "nenhum alerta de memória em 100 s"
fi
