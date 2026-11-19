#!/usr/bin/env bash
# Etapa 20 · rede: IP estático na interface da rede interna (netplan) e parâmetros de rede do kernel (sysctl).
source "$(dirname "${BASH_SOURCE[0]}")/../lib/comum.sh"
source "$INFRA_DIR/sombra.conf"
exigir_root

# A interface: a configurada, ou a primeira que não é a da rota padrão (no Vagrant, a host-only).
interface=$SOMBRA_INTERFACE
if [[ -z $interface ]]; then
  padrao=$(ip -o route show default | awk '{print $5; exit}')
  interface=$(ip -o link show | awk -F': ' '{print $2}' | grep -vE "^(lo|$padrao|docker|veth|br-)" | head -n1)
fi
if [[ -z $interface ]] || ! ip link show "$interface" >/dev/null 2>&1; then
  log_erro rede.sem_interface "Nenhuma interface para o IP estático (defina SOMBRA_INTERFACE em sombra.conf)"
  exit 1
fi

escrever_arquivo /etc/netplan/60-sombra.yaml 0600 root:root <<YAML
# Gerado por infra/steps/20-rede.sh: IP estático do $SOMBRA_HOSTNAME na rede interna.
network:
  version: 2
  ethernets:
    $interface:
      dhcp4: false
      dhcp6: false
      addresses: [$SOMBRA_IP/$SOMBRA_PREFIXO]
YAML
if ((ARQUIVO_MUDOU)) || ! ip -4 addr show "$interface" | grep -q "inet $SOMBRA_IP/"; then
  netplan apply
  auditar "rede:$interface" sucesso "ip=$SOMBRA_IP/$SOMBRA_PREFIXO"
fi

# Parâmetros de rede: sem redirecionamento de ICMP, filtro de origem, SYN cookies e fila de conexões maior para o nginx.
escrever_arquivo /etc/sysctl.d/60-sombra.conf 0644 root:root <<'SYSCTL'
# Gerado por infra/steps/20-rede.sh
net.ipv4.conf.all.rp_filter = 1
net.ipv4.conf.default.rp_filter = 1
net.ipv4.conf.all.accept_redirects = 0
net.ipv4.conf.all.send_redirects = 0
net.ipv6.conf.all.accept_redirects = 0
net.ipv4.tcp_syncookies = 1
net.core.somaxconn = 1024
SYSCTL
if ((ARQUIVO_MUDOU)); then
  sysctl --system >/dev/null
fi

for _ in $(seq 1 20); do
  ip -4 addr show "$interface" | grep -q "inet $SOMBRA_IP/" && break
  sleep 0.5
done
ip -4 addr show "$interface" | grep -q "inet $SOMBRA_IP/"
log_info rede.configurada "Interface $interface com $SOMBRA_IP/$SOMBRA_PREFIXO" "interface=$interface" "ip=$SOMBRA_IP"
