#!/usr/bin/env bash
# Falha 07 · uma etapa de provisionamento com erro proposital: o trap ERR registra a linha, o comando e o código no log e
# na trilha de auditoria, e o orquestrador para sem rodar as etapas seguintes.
source "$(dirname "${BASH_SOURCE[0]}")/comum.sh"
inicio_do_teste
ETAPAS=$(mktemp -d)
cat >"$ETAPAS/10-configura.sh" <<'ETAPA'
source "$INFRA_DIR/lib/comum.sh"
log_info teste.ok "configuração de exemplo"
ETAPA
cat >"$ETAPAS/20-com-erro.sh" <<'ETAPA'
source "$INFRA_DIR/lib/comum.sh"
log_info teste.ok "copiando a configuração"
cp /etc/sombra/arquivo-que-nao-existe.conf /tmp/destino.conf
log_info teste.ok "nunca chega aqui"
ETAPA
cat >"$ETAPAS/30-nunca-roda.sh" <<'ETAPA'
source "$INFRA_DIR/lib/comum.sh"
touch /tmp/sombra-etapa-30-rodou
ETAPA
rm -f /tmp/sombra-etapa-30-rodou
passo "provision.sh com uma etapa que copia um arquivo que não existe (linha 3)"
codigo=0
SOMBRA_DIR_ETAPAS=$ETAPAS "$INFRA_DIR/provision.sh" >/dev/null 2>&1 || codigo=$?
passo "o que o trap gravou na trilha de auditoria (/var/log/sombra/auditoria.jsonl):"
grep '"acao":"etapa:20-com-erro"' "$SOMBRA_AUDITORIA" | tail -n2 | jq -c '{quando, quem, acao, resultado, linha, comando, codigo}' | evidencia
registrou() { grep '"acao":"etapa:20-com-erro"' "$SOMBRA_AUDITORIA" | tail -n2 | grep -q '"linha":3'; }
rm -rf "$ETAPAS"
if ((codigo != 0)) && registrou && [[ ! -e /tmp/sombra-etapa-30-rodou ]]; then
  fim_do_teste 0 "erro registrado com linha, comando e código; o provisionamento parou (código $codigo) e a etapa 30 não rodou"
else
  fim_do_teste 1 "o erro não foi registrado como esperado"
fi
