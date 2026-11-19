#!/usr/bin/env bash
# Etapa 50 · dependências: banco, broker MQTT, JRE 21 do Clima, nginx e as ferramentas do monitor e dos testes de falha.
# O Core vai self-contained no release (traz o próprio runtime .NET 10); aqui só a ICU de que ele precisa.
source "$(dirname "${BASH_SOURCE[0]}")/../lib/comum.sh"
source "$INFRA_DIR/sombra.conf"
exigir_root

instalar_pacotes \
  postgresql postgresql-contrib \
  mosquitto mosquitto-clients \
  openjdk-21-jre-headless \
  nginx \
  libicu74 ca-certificates curl jq \
  stress-ng logrotate auditd

versoes=$(jq -cn \
  --arg postgresql "$(psql --version | awk '{print $3}')" \
  --arg java "$(java -version 2>&1 | awk -F'"' '/version/ {print $2}')" \
  --arg nginx "$(nginx -v 2>&1 | cut -d/ -f2)" \
  --arg mosquitto "$(dpkg-query -W -f='${Version}' mosquitto)" \
  '{postgresql: $postgresql, java: $java, nginx: $nginx, mosquitto: $mosquitto}')
auditar dependencias sucesso "versoes=$versoes"
log_info dependencias.instaladas "Dependências prontas" "versoes=$versoes"
