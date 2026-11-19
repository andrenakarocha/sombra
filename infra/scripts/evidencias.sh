#!/usr/bin/env bash
# Sombra · regenera as evidências da entrega de OS em infra/evidencias/ (D18: prints por script, uma rodada só). Roda no
# host, com a VM no ar (vagrant up): provisiona de novo (prova a idempotência e a validação da etapa 90), fotografa o
# servidor (host, rede, DNS, usuários, serviços, monitor, auditoria) e roda os 7 testes de falha, um print para cada.
# Uso: infra/scripts/evidencias.sh [01 02 ...]   (sem argumento, todos)
set -Eeuo pipefail

INFRA=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
SAIDA="$INFRA/evidencias"
CHROME=${CHROME:-google-chrome-stable}
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT
mkdir -p "$SAIDA"
cd "$INFRA"

vm() { vagrant ssh -c "$1" -- -o LogLevel=ERROR 2>/dev/null | tr -d '\r'; }

# terminal <título> <arquivo de texto> <nome do png>: renderiza a saída como um terminal e fotografa com o Chrome
terminal() {
  local linhas altura
  # cada linha com mais de 150 caracteres quebra em outras na largura de 1280 px
  linhas=$(awk '{n += 1 + int(length($0) / 150)} END {print n}' "$2")
  altura=$((linhas * 19 + 90))
  python3 -I - "$1" "$2" "$TMP/$3.html" <<'PY'
import html, re, sys
titulo, entrada, saida = sys.argv[1:4]
texto = re.sub(r"\x1b\[[0-9;]*m", "", open(entrada, encoding="utf-8").read())
corpo = html.escape(texto)
corpo = re.sub(r"^(\$ .*)$", r'<span class="cmd">\1</span>', corpo, flags=re.M)
corpo = re.sub(r"^(▶ .*)$", r'<span class="passo">\1</span>', corpo, flags=re.M)
corpo = re.sub(r"^(✔ .*)$", r'<span class="ok">\1</span>', corpo, flags=re.M)
corpo = re.sub(r"^(✘ .*)$", r'<span class="erro">\1</span>', corpo, flags=re.M)
corpo = re.sub(r"\b(active|200|sucesso)\b", r'<span class="ok">\1</span>', corpo)
open(saida, "w", encoding="utf-8").write(f"""<!doctype html><meta charset="utf-8">
<style>body{{margin:0;background:#0f1412;color:#d9e4dc;font:13.5px/19px "JetBrains Mono","DejaVu Sans Mono",monospace}}
header{{background:#1c2622;padding:10px 18px;color:#9fb5a8;font-family:sans-serif;font-size:13px}}
pre{{margin:0;padding:14px 18px;white-space:pre-wrap}} .cmd{{color:#f2c36b}} .passo{{color:#8fc7ff}}
.ok{{color:#7ad19b;font-weight:bold}} .erro{{color:#ff8a7a;font-weight:bold}}</style>
<header>{html.escape(titulo)}</header><pre>{corpo}</pre>""")
PY
  "$CHROME" --headless=new --no-sandbox --disable-gpu --hide-scrollbars --force-device-scale-factor=1 \
    --window-size="1280,$altura" --virtual-time-budget=5000 --screenshot="$SAIDA/$3.png" "file://$TMP/$3.html" >/dev/null 2>&1
  cp "$2" "$SAIDA/$3.txt"
  echo "  $3.png"
}

pedidos=("$@")
quer() { ((${#pedidos[@]} == 0)) || [[ " ${pedidos[*]} " == *" $1 "* ]]; }

echo "evidências em $SAIDA:"

if quer provision; then
  {
    echo '$ sudo ./provision.sh        (segunda execução: idempotente, com a validação da etapa 90)'
    vm 'sudo /vagrant/provision.sh' | jq -rR 'fromjson? | select(.event | test("provision\\.(iniciado|etapa_concluida|concluido)|validacao\\.|config\\.alterada")) | "\(.timestamp[11:19])  \(.event | . + (" " * (28 - length)))  \(.message)"'
  } >"$TMP/provision.txt"
  terminal "Provisionamento idempotente: as 9 etapas, nenhuma mudança e a validação de ponta a ponta" "$TMP/provision.txt" 00-provisionamento
fi

if quer servidor; then
  vm 'bash -s' >"$TMP/servidor.txt" <<'VM'
echo '$ hostnamectl --static && hostname -f && timedatectl show -p Timezone --value'
hostnamectl --static; hostname -f; timedatectl show -p Timezone --value
echo '$ ip -4 -br addr'
ip -4 -br addr | grep -v '^lo'
echo '$ dig +short {api,clima,painel}.sombra.local'
for n in api clima painel; do printf '%-22s %s\n' "$n.sombra.local" "$(dig +short "$n.sombra.local")"; done
echo '$ id sombra && getent passwd sombra'
id sombra; getent passwd sombra | cut -d: -f1,6,7
echo '$ stat -c "%n %a %U:%G" /opt/sombra /etc/sombra /etc/sombra/*.env /var/log/sombra'
sudo stat -c '%n %a %U:%G' /opt/sombra /etc/sombra /etc/sombra/core.env /etc/sombra/clima.env /etc/sombra/segredos.env /var/log/sombra
echo '$ sudo ss -ltn   (só a 80 para a rede; o resto em localhost)'
sudo ss -ltnH | awk '{print $4}' | grep -E ':(80|5080|8081|5432|1883)$' | sort -u
echo '$ systemctl is-active / is-enabled'
for s in postgresql mosquitto nginx dnsmasq sombra-clima sombra-core sombra-monitor auditd; do printf '%-16s %s  %s\n' "$s" "$(systemctl is-active "$s")" "$(systemctl is-enabled "$s")"; done
echo '$ curl dos /health pelos nomes'
for u in http://api.sombra.local/health/ready http://clima.sombra.local/actuator/health/readiness http://painel.sombra.local/healthz; do printf '%s  %s\n' "$(curl -s -o /dev/null -w '%{http_code}' "$u")" "$u"; done
VM
  terminal "O servidor: hostname, IP estático, DNS sombra.local, usuário de serviço, permissões, portas e serviços" "$TMP/servidor.txt" 01-servidor

  vm 'bash -s' >"$TMP/systemd.txt" <<'VM'
echo '$ systemctl status sombra-monitor --no-pager'
systemctl status sombra-monitor --no-pager -n 0 | head -n 12
echo
echo '$ systemctl show sombra-core -p User -p Restart -p NoNewPrivileges -p ProtectSystem -p PrivateTmp -p CapabilityBoundingSet'
systemctl show sombra-core -p User -p Restart -p NoNewPrivileges -p ProtectSystem -p PrivateTmp -p CapabilityBoundingSet
echo
echo '$ sudo tail -n 3 /var/log/sombra/sombra-monitor.log | jq -c'
sudo tail -n 3 /var/log/sombra/sombra-monitor.log | jq -c '{timestamp, event, message, cpu, ram, disco}'
echo
echo '$ sudo auditctl -l | grep sombra'
sudo auditctl -l | grep sombra
echo
echo '$ sudo tail -n 4 /var/log/sombra/auditoria.jsonl | jq -c'
sudo tail -n 4 /var/log/sombra/auditoria.jsonl | jq -c '{quando, quem, acao, resultado}'
VM
  terminal "Serviços systemd: o monitor com notify + watchdog, o Core endurecido, o log do monitor e a trilha de auditoria" "$TMP/systemd.txt" 02-systemd-monitor-auditoria
fi

for teste in 01-matar-monitor 02-cpu-alta 03-memoria-alta 04-disco-cheio 05-derrubar-api 07-falha-de-script; do
  quer "${teste%%-*}" || continue
  { echo "\$ sudo falhas/$teste.sh"; vm "sudo /vagrant/falhas/$teste.sh"; } >"$TMP/$teste.txt"
  terminal "Teste de falha $teste" "$TMP/$teste.txt" "falha-$teste"
done

if quer 06; then
  { echo '$ sudo falhas/06-reboot.sh'; vm 'sudo /vagrant/falhas/06-reboot.sh' || true; } >"$TMP/06.txt"
  for _ in $(seq 1 60); do sleep 3; vagrant ssh -c true -- -o ConnectTimeout=3 -o LogLevel=ERROR >/dev/null 2>&1 && break; done
  { echo '$ sudo falhas/06-reboot.sh --conferir   (depois que a VM voltou)'; vm 'sudo /vagrant/falhas/06-reboot.sh --conferir'; } >>"$TMP/06.txt"
  terminal "Teste de falha 06-reboot" "$TMP/06.txt" "falha-06-reboot"
fi
