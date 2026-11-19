#!/usr/bin/env bash
# Etapa 80 · monitor e auditoria: o sombra-monitor como serviço tolerante a falhas (notify + watchdog + Restart=always),
# a rotação dos logs (diária, 7 arquivos, comprimidos; a trilha de auditoria só por tempo) e as regras do auditd sobre a
# configuração, os binários e as units do Sombra.
source "$(dirname "${BASH_SOURCE[0]}")/../lib/comum.sh"
source "$INFRA_DIR/sombra.conf"
exigir_root

garantir_diretorio /opt/sombra/monitor 0755 root:root
garantir_diretorio /opt/sombra/monitor/lib 0755 root:root
escrever_arquivo /opt/sombra/monitor/sombra-monitor.sh 0755 root:root <"$INFRA_DIR/monitor/sombra-monitor.sh"
mudou=$ARQUIVO_MUDOU
escrever_arquivo /opt/sombra/monitor/lib/log.sh 0644 root:root <"$INFRA_DIR/lib/log.sh"
((ARQUIVO_MUDOU)) && mudou=1
# a configuração só é escrita na primeira vez: depois ela é do operador (os limites podem ser ajustados na VM)
if [[ ! -f /etc/sombra/monitor.conf ]]; then
  escrever_arquivo /etc/sombra/monitor.conf 0640 "root:$SOMBRA_GRUPO_OPS" <"$INFRA_DIR/monitor/monitor.conf"
  mudou=1
else
  auditar "arquivo:/etc/sombra/monitor.conf" inalterado "dono=operador"
fi
escrever_arquivo /etc/systemd/system/sombra-monitor.service 0644 root:root <"$INFRA_DIR/systemd/sombra-monitor.service"
if ((ARQUIVO_MUDOU)); then systemctl daemon-reload; mudou=1; fi

escrever_arquivo /etc/logrotate.d/sombra 0644 root:root <<LOGROTATE
# Gerado por infra/steps/80-monitor.sh (observabilidade §6)
/var/log/sombra/*.log {
  daily
  rotate 7
  compress
  delaycompress
  missingok
  notifempty
  copytruncate
  su root $SOMBRA_GRUPO_OPS
  create 0640 root $SOMBRA_GRUPO_OPS
}
# a trilha de auditoria ninguém apaga: só gira por tempo, e guarda um ano
/var/log/sombra/auditoria.jsonl {
  monthly
  rotate 12
  compress
  missingok
  notifempty
  copytruncate
  su root $SOMBRA_GRUPO_OPS
  create 0640 root $SOMBRA_GRUPO_OPS
}
LOGROTATE
logrotate --debug /etc/logrotate.d/sombra >/dev/null 2>&1

escrever_arquivo /etc/audit/rules.d/60-sombra.rules 0640 root:root <<'AUDIT'
## Gerado por infra/steps/80-monitor.sh: quem mexeu na configuração, nos binários e nas units do Sombra
-w /etc/sombra -p wa -k sombra-config
-w /opt/sombra -p wa -k sombra-binarios
-w /etc/systemd/system/sombra-core.service -p wa -k sombra-units
-w /etc/systemd/system/sombra-clima.service -p wa -k sombra-units
-w /etc/systemd/system/sombra-monitor.service -p wa -k sombra-units
-w /etc/sudoers.d/sombra-ops -p wa -k sombra-acesso
AUDIT
systemctl enable --now auditd >/dev/null 2>&1
if ((ARQUIVO_MUDOU)); then augenrules --load >/dev/null; fi

systemctl enable sombra-monitor >/dev/null 2>&1
if ((mudou)) || ! systemctl is-active --quiet sombra-monitor; then
  systemctl restart sombra-monitor
  auditar "servico:sombra-monitor" sucesso "acao=restart"
fi
for _ in $(seq 1 20); do systemctl is-active --quiet sombra-monitor && break; sleep 1; done
systemctl is-active --quiet sombra-monitor
log_info monitor.instalado "Monitor, logrotate e auditd prontos" "regrasAuditd=$(auditctl -l 2>/dev/null | grep -c sombra)"
