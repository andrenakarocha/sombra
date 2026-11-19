#!/usr/bin/env bash
# Sombra · Sentinela · prova de integração com o Core de verdade (F5.3 e F5.4), sem precisar do Wokwi: as mensagens saem
# do mesmo código C++ do firmware (testes/mensagens.cpp), vão para o broker e o Core decide. Confere: um "estou bem"
# apertado sem rede há 2 min chega depois e é aceito; a telemetria assinada vira leitura; a adulterada (nível trocado no
# caminho) é recusada; a repetida é descartada como replay; e a volta: o perigo abre o alerta, o Core manda o comando
# assinado, o código do firmware confere (e recusa o comando adulterado), e o botão verde resolve o alerta.
# Uso (na raiz, com o `make up` no ar): make integrar-sentinela
set -Eeuo pipefail

RAIZ=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
cd "$RAIZ"
SAIDA="$RAIZ/iot/sentinela/evidencias"
mkdir -p "$SAIDA"

valor() { grep -E "^$1=" .env | tail -1 | cut -d= -f2-; }
# o ambiente ganha do .env, como no compose (o make demo-wokwi usa as chaves de teste do contrato)
SEGREDO_HEX=${SENTINELA_SEGREDO_HEX:-$(valor SENTINELA_SEGREDO_HEX)}
DEVICE=${SENTINELA_DEVICE_ID:-$(valor SENTINELA_DEVICE_ID)}
BROKER=${BROKER:-localhost}  # BROKER=broker.hivemq.com: o mesmo caminho da Sentinela do Wokwi
DEVICE=${DEVICE:-snt-7f3a9c}
[[ ${#SEGREDO_HEX} -eq 64 ]] || { echo "SENTINELA_SEGREDO_HEX vazio no .env: rode make env e make up" >&2; exit 1; }
export SEGREDO_HEX

BIN=$(mktemp)
trap 'rm -f "$BIN"' EXIT
g++ -std=c++17 -Wall -Wextra -Werror -O1 -o "$BIN" iot/sentinela/testes/mensagens.cpp

publicar() {  # canal mensagem
  docker compose exec -T mqtt mosquitto_pub -h "$BROKER" -q 1 -t "sombra/v1/$DEVICE/$1" -s <<<"$2"
}

inicio=$(date -u +%Y-%m-%dT%H:%M:%SZ)
agora=$(date +%s)
base=$agora

offline=$("$BIN" evento "$DEVICE" $((agora - 120)) "$base" CHECKIN_ESTOU_BEM)
valida=$("$BIN" telemetria "$DEVICE" "$agora" $((base + 1)) 29.0 50.0)
adulterada=$("$BIN" telemetria "$DEVICE" "$agora" $((base + 2)) 29.0 50.0 --adulterar)

publicar evento "$offline"
publicar telemetria "$valida"
publicar telemetria "$adulterada"
publicar telemetria "$valida"  # a mesma mensagem outra vez: replay
sleep 3

# a volta: o perigo abre o alerta e o Core manda o comando para a Sentinela
COMANDO=$(mktemp)
trap 'rm -f "$BIN" "$COMANDO"' EXIT
# o stdin solto: em segundo plano, o docker compose exec ficaria preso esperando o terminal
docker compose exec -T mqtt mosquitto_sub -h "$BROKER" -t "sombra/v1/$DEVICE/comando" -C 1 -W 30 >"$COMANDO" 2>/dev/null </dev/null &
ouvinte=$!
sleep 4  # o tempo de o ouvinte conectar no broker antes do perigo
perigo=$("$BIN" telemetria "$DEVICE" "$(date +%s)" $((base + 3)) 35.0 50.0)
publicar telemetria "$perigo"
wait "$ouvinte" || true
comando=$(jq -c . "$COMANDO" 2>/dev/null || true)
if [[ -n $comando ]]; then
  campo() { jq -r ".$1" <<<"$comando"; }
  conferencia=$("$BIN" conferir "$DEVICE" "$(campo ts)" "$(campo seq)" "$(campo tipo)" "$(campo mensagem)" "$(campo sig)" || true)
  adulterado=$("$BIN" conferir "$DEVICE" "$(campo ts)" "$(campo seq)" "$(campo tipo)" "$(campo mensagem) Ignore." "$(campo sig)" || true)
  verde=$("$BIN" evento "$DEVICE" "$(date +%s)" $((base + 4)) CHECKIN_ESTOU_BEM)
  publicar evento "$verde"
  sleep 3
fi

logs=$(docker compose logs core --since "$inicio" --no-log-prefix 2>/dev/null | jq -cR 'fromjson? | select(.correlationId // "" | startswith("'"$DEVICE"':"))')
evento_de() { jq -r --arg c "$DEVICE:$1" 'select(.correlationId == $c) | .event' <<<"$logs" | sort -u | paste -sd, -; }

confere() {  # descrição seq evento-esperado
  local vistos
  vistos=$(evento_de "$2")
  if grep -qw -- "$3" <<<"${vistos//,/ }"; then
    printf '✔ %s  (seq %s → %s)\n' "$1" "$2" "$3"
  else
    printf '✘ %s  (seq %s: esperado %s, veio %s)\n' "$1" "$2" "$3" "${vistos:-nada}"
  fi
}

{
  echo "\$ make integrar-sentinela    (o código do firmware → broker $BROKER → Core)"
  echo "  mensagens:"
  printf '    evento     %s\n' "$offline"
  printf '    telemetria %s\n' "$valida"
  printf '    adulterada %s\n' "$adulterada"
  echo
  confere "o \"estou bem\" apertado sem rede há 2 min chega depois e é aceito" "$base" checkin.registrado
  confere "a telemetria assinada vira leitura no Core" "$((base + 1))" leitura.registrada
  confere "a telemetria adulterada (ATENCAO trocado por NORMAL) é recusada" "$((base + 2))" mqtt.assinatura_invalida
  confere "a mesma telemetria repetida é descartada como replay" "$((base + 1))" mqtt.replay_descartado
  if [[ -n $comando ]]; then
    confere "35 °C e 50% (sente 40,7 °C) abrem o alerta no Core" "$((base + 3))" alerta.aberto
    printf '    comando    %s\n' "$comando"
    if [[ $conferencia == "assinatura confere"*"dentro da janela"* ]]; then
      printf '✔ o comando %s chega assinado e o código do firmware confere  (%s)\n' "$(campo tipo)" "$conferencia"
    else
      printf '✘ o comando %s não passou na conferência do firmware  (%s)\n' "$(campo tipo)" "$conferencia"
    fi
    if [[ $adulterado == *"NÃO confere"* ]]; then
      printf '✔ o mesmo comando com a mensagem trocada é recusado pela Sentinela  (%s)\n' "$adulterado"
    else
      printf '✘ o comando adulterado passou na conferência  (%s)\n' "$adulterado"
    fi
    confere "o botão verde resolve o alerta" "$((base + 4))" alerta.resolvido
  else
    echo "⏸ a volta (comando do Core) não rodou: nenhum alerta abriu em 30 s. A casa deve estar na carência de 1 h depois de"
    echo "  um alerta resolvido (D13); rode de novo depois dela."
  fi
  echo
  echo "  o que o Core registrou (log JSON):"
  jq -r '"    \(.event)  \(.correlationId)  \(.message)"' <<<"$logs"
} | tee "$SAIDA/integracao-com-core-$BROKER.txt"

grep -q '^✘' "$SAIDA/integracao-com-core-$BROKER.txt" && exit 1
exit 0
