#!/usr/bin/env bash
# Etapa 40 · usuários e permissões: o usuário de serviço sem shell (roda o Sombra), o grupo sombra-ops dos operadores,
# um sudoers mínimo (só os serviços do Sombra) e as pastas com dono e modo certos.
source "$(dirname "${BASH_SOURCE[0]}")/../lib/comum.sh"
source "$INFRA_DIR/sombra.conf"
exigir_root

if ! getent group "$SOMBRA_GRUPO_OPS" >/dev/null; then
  groupadd --system "$SOMBRA_GRUPO_OPS"
  auditar "grupo:$SOMBRA_GRUPO_OPS" sucesso
else
  auditar "grupo:$SOMBRA_GRUPO_OPS" inalterado
fi

# O usuário de serviço não tem shell nem senha: ninguém entra como ele, só o systemd o usa.
if ! id "$SOMBRA_USUARIO_SERVICO" >/dev/null 2>&1; then
  useradd --system --home-dir /opt/sombra --no-create-home --shell /usr/sbin/nologin \
    --gid "$SOMBRA_GRUPO_OPS" "$SOMBRA_USUARIO_SERVICO"
  auditar "usuario:$SOMBRA_USUARIO_SERVICO" sucesso "shell=/usr/sbin/nologin"
else
  usermod --shell /usr/sbin/nologin --gid "$SOMBRA_GRUPO_OPS" "$SOMBRA_USUARIO_SERVICO"
  auditar "usuario:$SOMBRA_USUARIO_SERVICO" inalterado "shell=/usr/sbin/nologin"
fi
passwd -l "$SOMBRA_USUARIO_SERVICO" >/dev/null 2>&1 || true

for operador in $SOMBRA_OPERADORES; do
  if id "$operador" >/dev/null 2>&1 && ! id -nG "$operador" | tr ' ' '\n' | grep -qx "$SOMBRA_GRUPO_OPS"; then
    usermod -aG "$SOMBRA_GRUPO_OPS" "$operador"
    auditar "grupo:$SOMBRA_GRUPO_OPS" sucesso "membro=$operador"
  fi
done

# Os operadores mexem nos serviços do Sombra e leem o journal deles, e mais nada como root.
sudoers=$(mktemp)
cat >"$sudoers" <<SUDO
# Gerado por infra/steps/40-usuarios.sh: o mínimo para operar o Sombra.
Cmnd_Alias SOMBRA_SERVICOS = /usr/bin/systemctl start sombra-*, /usr/bin/systemctl stop sombra-*, \\
  /usr/bin/systemctl restart sombra-*, /usr/bin/systemctl status sombra-*, /usr/bin/journalctl -u sombra-*
%$SOMBRA_GRUPO_OPS ALL=(root) NOPASSWD: SOMBRA_SERVICOS
SUDO
visudo -cqf "$sudoers"
escrever_arquivo /etc/sudoers.d/sombra-ops 0440 root:root <"$sudoers"
rm -f "$sudoers"

# /opt/sombra (binários, do root), /etc/sombra (configuração e segredos, só o grupo lê), /var/log/sombra (logs).
garantir_diretorio /opt/sombra 0755 root:root
garantir_diretorio /etc/sombra 0750 "root:$SOMBRA_GRUPO_OPS"
garantir_diretorio /var/lib/sombra 0750 "$SOMBRA_USUARIO_SERVICO:$SOMBRA_GRUPO_OPS"
garantir_diretorio "$SOMBRA_DIR_LOG" 2770 "root:$SOMBRA_GRUPO_OPS"
chgrp "$SOMBRA_GRUPO_OPS" "$SOMBRA_DIR_LOG"/*.log "$SOMBRA_AUDITORIA" 2>/dev/null || true

log_info usuarios.configurados "Usuário $SOMBRA_USUARIO_SERVICO e grupo $SOMBRA_GRUPO_OPS prontos" \
  "usuario=$SOMBRA_USUARIO_SERVICO" "grupo=$SOMBRA_GRUPO_OPS" "operadores=$SOMBRA_OPERADORES"
