#!/usr/bin/env bash
# Sombra · testes das bibliotecas de log e auditoria e do orquestrador, num Ubuntu 24.04 descartável (make test-infra).
# Confere: a linha de log é um JSON com os oito campos (mesmo com aspas e quebra de linha na mensagem), a auditoria
# registra o quê, quando, quem e o resultado, o trap ERR pega linha, comando e código, e o provision.sh para na
# primeira etapa que falha, sem rodar as seguintes.
# shellcheck source-path=SCRIPTDIR/..
# shellcheck disable=SC2030,SC2031  # cada bloco roda num subshell de propósito, com o próprio ambiente
set -Eeuo pipefail

INFRA=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
falhas=0
checa() {
  local descricao=$1
  shift
  if "$@"; then
    printf '  ok  %s\n' "$descricao"
  else
    printf '  FALHOU  %s\n' "$descricao"
    falhas=$((falhas + 1))
  fi
}
jqe() { jq -e "$@" >/dev/null; }
nao() { ! "$@"; }

TMP=$(mktemp -d)
export SOMBRA_DIR_LOG=$TMP/log SOMBRA_AMBIENTE=Test SOMBRA_VERSAO=teste01
mkdir -p "$SOMBRA_DIR_LOG"

echo "log.sh"
(
  export SOMBRA_SERVICO=provision SOMBRA_CORRELACAO=provision:teste
  source "$INFRA/lib/log.sh"
  log_info config.alterada $'Mensagem com "aspas" e\nquebra de linha' "arquivo=/etc/x" "duracaoMs=12"
) >"$TMP/linha.json"
checa "uma linha só" test "$(wc -l <"$TMP/linha.json")" -eq 1
checa "os oito campos" jqe 'has("timestamp") and has("level") and has("service") and has("version")
  and has("environment") and has("correlationId") and has("event") and has("message")' "$TMP/linha.json"
checa "timestamp em UTC com milissegundos" jqe '.timestamp | test("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:]{8}\\.[0-9]{3}Z$")' "$TMP/linha.json"
checa "a mensagem volta inteira" jqe '.message == "Mensagem com \"aspas\" e\nquebra de linha"' "$TMP/linha.json"
checa "número vira número e texto vira texto" jqe '.duracaoMs == 12 and .arquivo == "/etc/x" and .level == "Information"' "$TMP/linha.json"
checa "também vai para o arquivo do serviço" test -s "$SOMBRA_DIR_LOG/provision.log"

echo "auditoria.sh"
(
  export SOMBRA_SERVICO=provision SOMBRA_CORRELACAO=provision:teste
  source "$INFRA/lib/log.sh"
  source "$INFRA/lib/auditoria.sh"
  auditar "arquivo:/etc/sombra/core.env" sucesso "modo=0640"
) >/dev/null
checa "o quê, quando, quem e o resultado" jqe 'select(.acao == "arquivo:/etc/sombra/core.env")
  | .quando and .quem and (.uid | type == "number") and .host and .resultado == "sucesso" and .modo == "0640"' \
  "$SOMBRA_DIR_LOG/auditoria.jsonl"

echo "trap ERR numa etapa"
cat >"$TMP/etapa-quebrada.sh" <<ETAPA
source "$INFRA/lib/comum.sh"
echo preparando
ls /caminho/que/nao/existe
echo nunca chega aqui
ETAPA
(
  export SOMBRA_SERVICO=provision SOMBRA_CORRELACAO=provision:teste
  source "$INFRA/lib/log.sh"
  source "$INFRA/lib/auditoria.sh"
  codigo=0
  executar_etapa 99-quebrada "$TMP/etapa-quebrada.sh" || codigo=$?
  echo "$codigo" >"$TMP/codigo"
) >"$TMP/etapa.log" 2>/dev/null
grep '^{' "$TMP/etapa.log" >"$TMP/etapa.json"
checa "a etapa devolve o código da falha" test "$(cat "$TMP/codigo")" -eq 2
checa "o trap registra linha e comando" jqe 'select(.event == "provision.comando_falhou")
  | .linha == 3 and (.comando | test("ls /caminho")) and .codigo == 2' "$TMP/etapa.json"
checa "a auditoria registra a falha da etapa" jqe 'select(.acao == "etapa:99-quebrada" and .resultado == "falha")
  | .codigo == 2' "$SOMBRA_DIR_LOG/auditoria.jsonl"
checa "o que vem depois da falha não roda" nao grep -q "nunca chega aqui" "$TMP/etapa.log"

echo "provision.sh"
mkdir -p "$TMP/etapas"
printf 'source "%s/lib/comum.sh"\nlog_info teste.ok "primeira"\n' "$INFRA" >"$TMP/etapas/10-primeira.sh"
printf 'source "%s/lib/comum.sh"\nfalse\n' "$INFRA" >"$TMP/etapas/20-falha.sh"
printf 'source "%s/lib/comum.sh"\ntouch "%s/terceira-rodou"\n' "$INFRA" "$TMP" >"$TMP/etapas/30-terceira.sh"
lista=$(SOMBRA_DIR_ETAPAS=$TMP/etapas "$INFRA/provision.sh" --listar | tr '\n' ' ')
checa "lista as etapas" test "$lista" = "10-primeira 20-falha 30-terceira "
codigo=0
SOMBRA_DIR_ETAPAS=$TMP/etapas "$INFRA/provision.sh" >"$TMP/provision.log" 2>&1 || codigo=$?
checa "para na primeira etapa que falha" test "$codigo" -ne 0
checa "a etapa seguinte não roda" test ! -e "$TMP/terceira-rodou"
checa "registra a interrupção" jqe 'select(.event == "provision.interrompido") | .etapa == "20-falha"' "$TMP/provision.log"
checa "a correlação é a mesma em toda a execução" test "$(jq -r '.correlationId' "$TMP/provision.log" | sort -u | wc -l)" -eq 1
codigo=0
SOMBRA_DIR_ETAPAS=$TMP/etapas "$INFRA/provision.sh" 10 30 >"$TMP/parcial.log" 2>&1 || codigo=$?
checa "roda só as etapas pedidas" test "$codigo" -eq 0
checa "a etapa 30 rodou" test -e "$TMP/terceira-rodou"
checa "e registra a conclusão com 2 etapas" jqe 'select(.event == "provision.concluido") | .etapas == 2' "$TMP/parcial.log"

rm -rf "$TMP"
if ((falhas > 0)); then
  echo "$falhas falha(s)"
  exit 1
fi
echo "tudo certo"
