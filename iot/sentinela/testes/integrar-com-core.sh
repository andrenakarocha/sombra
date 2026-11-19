#!/usr/bin/env bash
# Sombra · Sentinela · prova de integração com o Core de verdade (F5.3 e F5.4), sem precisar do Wokwi: as mensagens saem
# do mesmo código C++ do firmware (testes/mensagens.cpp), vão para o broker e o Core decide. Confere: um "estou bem"
# apertado sem rede há 2 min chega depois e é aceito; a telemetria assinada vira leitura; a adulterada (nível trocado no
# caminho) é recusada; a repetida é descartada como replay; e a volta: o perigo abre o alerta, o Core manda o comando
# assinado, o código do firmware confere (e recusa o comando adulterado), e o botão verde resolve o alerta. Um ouvinte
# do SignalR (ouvir-hub.mjs) confere que o painel recebe cada evento com o correlationId da mensagem MQTT que o originou.
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

# o painel: um ouvinte do hub do SignalR, como o painel, para conferir que o ID do MQTT chega igual (F9.5)
HUB=$(mktemp)
SENHA=$(valor Demo__Senha) node iot/sentinela/testes/ouvir-hub.mjs 120 >"$HUB" 2>/dev/null </dev/null &
ouvinte_do_hub=$!
sleep 3

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
trap 'rm -f "$BIN" "$COMANDO" "$HUB"; kill "$ouvinte_do_hub" 2>/dev/null || true' EXIT
# O comando não fica retido no broker: quem assina depois que ele sai não recebe. Por isso o ouvinte assina também um
# tópico de prova (que o Core não usa), e o perigo só sai quando a prova volta, ou seja, com a assinatura já valendo.
# O stdin solto: em segundo plano, o docker compose exec ficaria preso esperando o terminal.
docker compose exec -T mqtt mosquitto_sub -h "$BROKER" -t "sombra/v1/$DEVICE/comando" -t "sombra/v1/$DEVICE/pronto" -v -W 90 \
  >"$COMANDO" 2>/dev/null </dev/null &
for _ in $(seq 1 15); do
  publicar pronto "$base"
  grep -q "/pronto " "$COMANDO" && break
  sleep 1
done
perigo=$("$BIN" telemetria "$DEVICE" "$(date +%s)" $((base + 3)) 35.0 50.0)
publicar telemetria "$perigo"
for _ in $(seq 1 30); do
  grep -q "/comando " "$COMANDO" && break
  sleep 1
done
comando=$(grep -m1 "/comando " "$COMANDO" | cut -d' ' -f2- | jq -c . 2>/dev/null || true)
if [[ -n $comando ]]; then
  campo() { jq -r ".$1" <<<"$comando"; }
  conferencia=$("$BIN" conferir "$DEVICE" "$(campo ts)" "$(campo seq)" "$(campo tipo)" "$(campo mensagem)" "$(campo sig)" || true)
  adulterado=$("$BIN" conferir "$DEVICE" "$(campo ts)" "$(campo seq)" "$(campo tipo)" "$(campo mensagem) Ignore." "$(campo sig)" || true)
fi
# o alerta abriu? (fora da carência de 1 h do D13, tem que abrir) Se abriu, o botão verde resolve, com ou sem comando
alerta_abriu=$(docker compose logs core --since "$inicio" --no-log-prefix 2>/dev/null \
  | jq -rR --arg c "$DEVICE:$((base + 3))" 'fromjson? | select(.correlationId == $c and .event == "alerta.aberto") | .event' | head -1)
if [[ -n $alerta_abriu ]]; then
  verde=$("$BIN" evento "$DEVICE" "$(date +%s)" $((base + 4)) CHECKIN_ESTOU_BEM)
  publicar evento "$verde"
  sleep 3
fi

kill "$ouvinte_do_hub" 2>/dev/null || true
wait "$ouvinte_do_hub" 2>/dev/null || true
no_hub() { jq -r --arg c "$DEVICE:$1" 'select(.correlationId == $c) | .evento' "$HUB" | sort -u | paste -sd, -; }
confere_hub() {  # descrição seq evento-esperado
  local vistos
  vistos=$(no_hub "$2")
  if grep -qw -- "$3" <<<"${vistos//,/ }"; then
    printf '✔ %s  (SignalR %s, correlationId %s:%s)\n' "$1" "$3" "$DEVICE" "$2"
  else
    printf '✘ %s  (esperado %s com o correlationId %s:%s, veio %s)\n' "$1" "$3" "$DEVICE" "$2" "${vistos:-nada}"
  fi
}

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
  if [[ -n $alerta_abriu ]]; then
    confere "35 °C e 50% (sente 40,7 °C) abrem o alerta no Core" "$((base + 3))" alerta.aberto
    confere_hub "o painel recebe a leitura ao vivo com o mesmo ID da mensagem MQTT" "$((base + 3))" LeituraRegistrada
    confere_hub "e o alerta aberto, com o mesmo ID" "$((base + 3))" AlertaAberto
    if [[ -n $comando ]]; then
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
    else
      echo "✘ o alerta abriu, mas o comando para a Sentinela não chegou ao broker em 30 s"
    fi
    confere "o botão verde resolve o alerta" "$((base + 4))" alerta.resolvido
    confere_hub "e o painel recebe o alerta resolvido com o ID do check-in" "$((base + 4))" AlertaResolvido
  else
    echo "⏸ a volta não rodou: nenhum alerta abriu. A casa deve estar na carência de 1 h depois de um alerta resolvido"
    echo "  (D13); rode de novo depois dela."
  fi
  echo
  echo "  o que o Core registrou (log JSON):"
  jq -r '"    \(.event)  \(.correlationId)  \(.message)"' <<<"$logs"
} | tee "$SAIDA/integracao-com-core-$BROKER.txt"

grep -q '^✘' "$SAIDA/integracao-com-core-$BROKER.txt" && exit 1
exit 0
