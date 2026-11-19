#!/usr/bin/env bash
# Etapa 90 · validação: o servidor de ponta a ponta. Host, IP e DNS; só a porta 80 aberta para a rede; todos os
# serviços ativos; os /health pelos nomes; o painel servido; e o Sombra funcionando de verdade: o operador entra, lê as
# casas e o Core busca no Clima os pontos de resfriamento. Qualquer falha para o provisionamento.
source "$(dirname "${BASH_SOURCE[0]}")/../lib/comum.sh"
source "$INFRA_DIR/sombra.conf"
exigir_root
# shellcheck disable=SC1091
source /etc/sombra/segredos.env

falhas=0
checar() { # <descrição> <comando...>
  local descricao=$1
  shift
  if "$@" >/dev/null 2>&1; then
    log_info validacao.ok "$descricao" "verificacao=$descricao"
  else
    log_erro validacao.falhou "$descricao" "verificacao=$descricao"
    falhas=$((falhas + 1))
  fi
}
igual() { [[ $1 == "$2" ]]; }
http200() { [[ $(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$1") == 200 ]]; }
so_local() { ! ss -ltnH | awk '{print $4}' | grep -Eq "^(0\.0\.0\.0|\[::\]|\*|$SOMBRA_IP):$1$"; }
contem() { curl -fsS --max-time 5 "$1" | grep -q "$2"; }

checar "hostname é $SOMBRA_HOSTNAME" igual "$(hostnamectl --static)" "$SOMBRA_HOSTNAME"
checar "IP estático $SOMBRA_IP" ip -4 addr show to "$SOMBRA_IP/$SOMBRA_PREFIXO"
for nome in $SOMBRA_NOMES; do
  checar "DNS: $nome.$SOMBRA_DOMINIO → $SOMBRA_IP" igual "$(dig +short "$nome.$SOMBRA_DOMINIO")" "$SOMBRA_IP"
done
externo() { [[ -n $(dig +short ubuntu.com | head -n1) ]]; }
checar "DNS externo resolve" externo
checar "porta 80 aberta para a rede" bash -c "ss -ltnH | awk '{print \$4}' | grep -q ':80$'"
for porta in 5080 8081 5432; do checar "porta $porta só em localhost" so_local "$porta"; done
for s in postgresql mosquitto nginx dnsmasq sombra-clima sombra-core sombra-monitor auditd; do
  checar "serviço $s ativo e no boot" bash -c "systemctl is-active --quiet $s && systemctl is-enabled --quiet $s"
done
checar "Core pronto (api.$SOMBRA_DOMINIO/health/ready)" http200 "http://api.$SOMBRA_DOMINIO/health/ready"
checar "Clima pronto (clima.$SOMBRA_DOMINIO/actuator/health/readiness)" http200 "http://clima.$SOMBRA_DOMINIO/actuator/health/readiness"
checar "painel no ar (painel.$SOMBRA_DOMINIO)" contem "http://painel.$SOMBRA_DOMINIO/" "Central de cuidado"
checar "escrita no Clima fechada pelo nginx" igual "$(curl -s -o /dev/null -w '%{http_code}' -X POST "http://clima.$SOMBRA_DOMINIO/api/v1/replay")" 403

# o Sombra funcionando: login do operador, as casas e os pontos de resfriamento (o Core pergunta ao Clima)
token=$(curl -fsS --max-time 10 -X POST "http://api.$SOMBRA_DOMINIO/api/v1/auth/login" -H 'Content-Type: application/json' \
  -d "$(jq -cn --arg s "$DEMO_SENHA" '{email: "operador@demo.sombra", senha: $s}')" | jq -r .token) || token=
checar "o operador entra (JWT)" test -n "$token"
casas=$(curl -fsS --max-time 10 "http://api.$SOMBRA_DOMINIO/api/v1/domicilios" -H "Authorization: Bearer $token" | jq -r '.total') || casas=0
checar "o Core lista as casas do seed ($casas)" test "${casas:-0}" -gt 0
casa=$(curl -fsS --max-time 10 "http://api.$SOMBRA_DOMINIO/api/v1/domicilios" -H "Authorization: Bearer $token" | jq -r '.itens[] | select(.monitorado) | .id' | head -n1)
pontos=$(curl -fsS --max-time 10 "http://api.$SOMBRA_DOMINIO/api/v1/domicilios/$casa/pontos-de-resfriamento?raioKm=3" -H "Authorization: Bearer $token" | jq 'length') || pontos=0
checar "o Core traz do Clima os pontos de resfriamento ($pontos)" test "${pontos:-0}" -gt 0

auditar validacao "$([[ $falhas -eq 0 ]] && echo sucesso || echo falha)" "falhas=$falhas"
if ((falhas > 0)); then
  log_erro validacao.reprovada "$falhas verificação(ões) falharam" "falhas=$falhas"
  exit 1
fi
log_info validacao.aprovada "Servidor $SOMBRA_HOSTNAME validado de ponta a ponta"
