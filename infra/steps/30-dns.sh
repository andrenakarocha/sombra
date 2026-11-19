#!/usr/bin/env bash
# Etapa 30 · DNS: dnsmasq com a zona sombra.local (api, clima, painel e o próprio host) e o systemd-resolved
# perguntando a ele. O que não é da zona vai para os servidores que a máquina já usava.
source "$(dirname "${BASH_SOURCE[0]}")/../lib/comum.sh"
source "$INFRA_DIR/sombra.conf"
exigir_root

instalar_pacotes dnsmasq bind9-dnsutils

# Os servidores externos: os configurados, ou os que o systemd-resolved já usava (lidos antes de apontá-lo para o dnsmasq).
externos=$SOMBRA_DNS_EXTERNO
if [[ -z $externos ]]; then
  externos=$(resolvectl dns 2>/dev/null | awk -F': ' '{print $2}' | tr ' ' '\n' | grep -vE '^(127\.|::1|$)' | sort -u | tr '\n' ' ')
fi
externos=${externos:-1.1.1.1 9.9.9.9}

{
  echo "# Gerado por infra/steps/30-dns.sh: a zona $SOMBRA_DOMINIO do $SOMBRA_HOSTNAME."
  echo "listen-address=127.0.0.1,$SOMBRA_IP"
  echo "bind-interfaces"
  echo "no-resolv"
  echo "domain-needed"
  echo "bogus-priv"
  echo "local=/$SOMBRA_DOMINIO/"
  echo "domain=$SOMBRA_DOMINIO"
  for s in $externos; do echo "server=$s"; done
  echo "host-record=$SOMBRA_HOSTNAME.$SOMBRA_DOMINIO,$SOMBRA_HOSTNAME,$SOMBRA_IP"
  for nome in $SOMBRA_NOMES; do echo "address=/$nome.$SOMBRA_DOMINIO/$SOMBRA_IP"; done
} | escrever_arquivo /etc/dnsmasq.d/sombra.conf 0644 root:root
mudou_dnsmasq=$ARQUIVO_MUDOU

escrever_arquivo /etc/systemd/resolved.conf.d/sombra.conf 0644 root:root <<RESOLVED
# Gerado por infra/steps/30-dns.sh: o resolved pergunta ao dnsmasq, que responde a zona $SOMBRA_DOMINIO.
[Resolve]
DNS=127.0.0.1
Domains=$SOMBRA_DOMINIO
RESOLVED
mudou_resolved=$ARQUIVO_MUDOU

systemctl enable --now dnsmasq >/dev/null 2>&1
if ((mudou_dnsmasq)); then systemctl restart dnsmasq; fi
if ((mudou_resolved)); then systemctl restart systemd-resolved; fi

for nome in $SOMBRA_NOMES; do
  resposta=$(dig +short "$nome.$SOMBRA_DOMINIO" @127.0.0.1)
  if [[ $resposta != "$SOMBRA_IP" ]]; then
    log_erro dns.falhou "$nome.$SOMBRA_DOMINIO respondeu '$resposta', esperado $SOMBRA_IP" "nome=$nome.$SOMBRA_DOMINIO"
    exit 1
  fi
done
auditar "dns:$SOMBRA_DOMINIO" sucesso "nomes=$SOMBRA_NOMES" "externos=$externos"
log_info dns.configurado "Zona $SOMBRA_DOMINIO respondendo pelo dnsmasq" "nomes=$SOMBRA_NOMES" "externos=$externos"
