#!/usr/bin/env bash
# Etapa 10 · host: hostname, nome completo no /etc/hosts, fuso horário de São Paulo, NTP e o locale pt-BR.
source "$(dirname "${BASH_SOURCE[0]}")/../lib/comum.sh"
source "$INFRA_DIR/sombra.conf"
exigir_root

fqdn="$SOMBRA_HOSTNAME.$SOMBRA_DOMINIO"
if [[ $(hostnamectl --static) != "$SOMBRA_HOSTNAME" ]]; then
  hostnamectl set-hostname "$SOMBRA_HOSTNAME"
  auditar hostname sucesso "hostname=$SOMBRA_HOSTNAME"
  log_info config.alterada "Hostname definido como $SOMBRA_HOSTNAME" "hostname=$SOMBRA_HOSTNAME"
else
  auditar hostname inalterado "hostname=$SOMBRA_HOSTNAME"
fi

# 127.0.1.1 com o nome completo e o curto (convenção do Debian/Ubuntu para o próprio host).
linha="127.0.1.1 $fqdn $SOMBRA_HOSTNAME"
{ grep -v '^127\.0\.1\.1[[:space:]]' /etc/hosts; echo "$linha"; } | escrever_arquivo /etc/hosts 0644 root:root

if [[ $(timedatectl show -p Timezone --value) != "$SOMBRA_FUSO" ]]; then
  timedatectl set-timezone "$SOMBRA_FUSO"
  auditar fuso sucesso "fuso=$SOMBRA_FUSO"
else
  auditar fuso inalterado "fuso=$SOMBRA_FUSO"
fi
timedatectl set-ntp true

instalar_pacotes locales
if ! locale -a | grep -qi "^${SOMBRA_LOCALE%%.*}\.utf-\?8$"; then
  locale-gen "$SOMBRA_LOCALE" >/dev/null
  auditar locale sucesso "locale=$SOMBRA_LOCALE"
else
  auditar locale inalterado "locale=$SOMBRA_LOCALE"
fi

log_info host.configurado "Host $fqdn no fuso $SOMBRA_FUSO" "hostname=$SOMBRA_HOSTNAME" "fuso=$SOMBRA_FUSO"
