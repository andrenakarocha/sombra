#!/usr/bin/env bash
# Etapa 60 · banco: os segredos do servidor (gerados uma vez, nunca no log), um papel e um banco por serviço (o Core não
# entra no banco do Clima, ADR-003) e o pg_hba fechado: só conexões locais, cada papel só no próprio banco.
source "$(dirname "${BASH_SOURCE[0]}")/../lib/comum.sh"
source "$INFRA_DIR/sombra.conf"
exigir_root

SEGREDOS=/etc/sombra/segredos.env

# Os segredos nascem aqui, com o gerador do sistema, e não mudam nas próximas execuções (mudar a chave AES deixaria a PII
# cifrada ilegível). Só o root e o grupo sombra-ops leem.
gerar() { head -c "$1" /dev/urandom | base64 | tr -d '\n'; }
hex() { od -An -N"$1" -tx1 /dev/urandom | tr -d ' \n'; }
if [[ ! -s $SEGREDOS ]]; then
  umask 0027
  cat >"$SEGREDOS" <<SEG
# Gerado por infra/steps/60-banco.sh em $(agora_utc). Nunca vai para o Git nem para o log.
CORE_DB_PASSWORD=$(hex 24)
CLIMA_DB_PASSWORD=$(hex 24)
JWT_SEGREDO=$(gerar 48)
PRIVACIDADE_CHAVE_AES=$(gerar 32)
PRIVACIDADE_CHAVE_INDICE_CEGO=$(gerar 32)
SENTINELA_CHAVE_DE_VINCULO=$(hex 32)
SENTINELA_SEGREDO_HEX=$(hex 32)
OPS_TOKEN_DO_MONITOR=$(hex 24)
DEMO_SENHA=demo-$(hex 6)
SEG
  chown "root:$SOMBRA_GRUPO_OPS" "$SEGREDOS"
  chmod 0640 "$SEGREDOS"
  auditar "segredos:$SEGREDOS" sucesso "modo=0640"
  log_info config.alterada "Segredos do servidor gerados (o conteúdo não sai daqui)" "arquivo=$SEGREDOS"
else
  auditar "segredos:$SEGREDOS" inalterado
fi
# shellcheck disable=SC1090
source "$SEGREDOS"

systemctl enable --now postgresql >/dev/null 2>&1
versao=$(psql --version | awk '{print $3}' | cut -d. -f1)
CONF_DIR=/etc/postgresql/$versao/main

# Só escuta localmente, e cada papel entra só no próprio banco, com senha (scram); o postgres entra pelo socket (peer).
escrever_arquivo "$CONF_DIR/conf.d/60-sombra.conf" 0644 postgres:postgres <<'PGCONF'
# Gerado por infra/steps/60-banco.sh
listen_addresses = 'localhost'
password_encryption = 'scram-sha-256'
log_connections = on
PGCONF
mudou_conf=$ARQUIVO_MUDOU
escrever_arquivo "$CONF_DIR/pg_hba.conf" 0640 postgres:postgres <<'HBA'
# Gerado por infra/steps/60-banco.sh: cada serviço só no próprio banco, e só pela máquina.
# TYPE  DATABASE       USER           ADDRESS        METHOD
local   all            postgres                      peer
host    sombra_core    sombra_core    127.0.0.1/32   scram-sha-256
host    sombra_core    sombra_core    ::1/128        scram-sha-256
host    sombra_clima   sombra_clima   127.0.0.1/32   scram-sha-256
host    sombra_clima   sombra_clima   ::1/128        scram-sha-256
host    all            all            0.0.0.0/0      reject
host    all            all            ::/0           reject
HBA
mudou_hba=$ARQUIVO_MUDOU
if ((mudou_conf)); then
  systemctl restart postgresql
elif ((mudou_hba)); then
  systemctl reload postgresql
fi

psql_admin() { runuser -u postgres -- psql -v ON_ERROR_STOP=1 -qtA "$@"; }
for _ in $(seq 1 30); do psql_admin -c 'select 1' >/dev/null 2>&1 && break; sleep 1; done

garantir_banco() { # <papel> <senha>
  local papel=$1 senha=$2
  if [[ $(psql_admin -c "select 1 from pg_roles where rolname = '$papel'") != 1 ]]; then
    psql_admin -c "create role $papel login" >/dev/null
    auditar "banco:papel:$papel" sucesso
  fi
  # a senha é sempre a dos segredos (passa por variável do psql, nunca pela linha de comando)
  psql_admin -v senha="$senha" <<<"alter role $papel with password :'senha';" >/dev/null
  if [[ $(psql_admin -c "select 1 from pg_database where datname = '$papel'") != 1 ]]; then
    psql_admin -c "create database $papel owner $papel" >/dev/null
    psql_admin -c "revoke connect on database $papel from public" >/dev/null
    auditar "banco:$papel" sucesso "dono=$papel"
  else
    auditar "banco:$papel" inalterado
  fi
}
garantir_banco sombra_core "$CORE_DB_PASSWORD"
garantir_banco sombra_clima "$CLIMA_DB_PASSWORD"

# Prova do isolamento: o papel do Core não entra no banco do Clima.
if PGPASSWORD=$CORE_DB_PASSWORD psql -h 127.0.0.1 -U sombra_core -d sombra_clima -c 'select 1' >/dev/null 2>&1; then
  log_erro banco.isolamento_falhou "O papel do Core entrou no banco do Clima"
  exit 1
fi
log_info banco.configurado "PostgreSQL $versao com um banco por serviço, só local" "versao=$versao"
