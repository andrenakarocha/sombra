#!/usr/bin/env bash
# Falha 04 · disco quase cheio com fallocate (reserva os blocos sem escrever): o monitor alerta; apagado o arquivo, ele
# registra a volta ao normal.
source "$(dirname "${BASH_SOURCE[0]}")/comum.sh"
inicio_do_teste
ARQUIVO=/var/tmp/sombra-disco-cheio.bin
read -r tamanho usado < <(df --output=size,used -k / | tail -n1)
reservar=$((tamanho * 93 / 100 - usado))
passo "disco em $((100 * usado / tamanho))%; fallocate de $((reservar / 1024 / 1024)) GB em $ARQUIVO"
fallocate -l "${reservar}K" "$ARQUIVO"
df -h / | evidencia
alertou() { eventos_do_monitor "$INICIO" | grep '"event":"monitor.alerta"' | grep -q '"recurso":"DISCO"'; }
ok=1
if esperar 80 alertou; then
  passo "o alerta do monitor:"
  eventos_do_monitor "$INICIO" | grep '"recurso":"DISCO"' | jq -c '{timestamp, level, event, message}' | head -n1 | evidencia
  ok=0
fi
rm -f "$ARQUIVO"
passo "arquivo apagado; esperando o monitor registrar a volta ao normal"
voltou() { eventos_do_monitor "$INICIO" | grep -q '"event":"monitor.normalizado".*"recurso":"DISCO"'; }
if ((ok == 0)) && esperar 40 voltou; then
  eventos_do_monitor "$INICIO" | grep '"event":"monitor.normalizado"' | jq -c '{timestamp, event, message}' | head -n1 | evidencia
  fim_do_teste 0 "disco cheio alertado e a volta ao normal registrada"
else
  fim_do_teste 1 "o alerta de disco ou a volta ao normal não apareceram"
fi
