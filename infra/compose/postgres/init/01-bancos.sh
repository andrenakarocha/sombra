#!/usr/bin/env bash
# Cria um papel e um banco por serviço (ADR-003): cada serviço só consegue se conectar ao próprio banco.
# Roda uma única vez, na criação do volume. As senhas chegam pelo ambiente e entram no SQL como variáveis do psql.
set -Eeuo pipefail

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
  -v core_senha="$CORE_DB_PASSWORD" -v clima_senha="$CLIMA_DB_PASSWORD" <<'SQL'
CREATE ROLE sombra_core LOGIN PASSWORD :'core_senha';
CREATE ROLE sombra_clima LOGIN PASSWORD :'clima_senha';

CREATE DATABASE sombra_core OWNER sombra_core;
CREATE DATABASE sombra_clima OWNER sombra_clima;

REVOKE CONNECT ON DATABASE sombra_core FROM PUBLIC;
REVOKE CONNECT ON DATABASE sombra_clima FROM PUBLIC;
SQL

echo "bancos sombra_core e sombra_clima criados, cada um com o seu papel"
