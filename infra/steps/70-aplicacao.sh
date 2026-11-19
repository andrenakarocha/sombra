#!/usr/bin/env bash
# Etapa 70 · aplicação: baixa os artefatos do release (ou usa os locais), confere cada um pelo SHA256SUMS, instala em
# /opt/sombra/releases/<versão> com o link /opt/sombra/atual, escreve o ambiente em /etc/sombra (0640, sem segredo no
# Git), configura o broker e o nginx, instala as units endurecidas e sobe tudo, esperando cada /health responder.
source "$(dirname "${BASH_SOURCE[0]}")/../lib/comum.sh"
source "$INFRA_DIR/sombra.conf"
exigir_root
# shellcheck disable=SC1091
source /etc/sombra/segredos.env

ARQUIVOS=(sombra-core-linux-x64.tar.gz sombra-clima.jar sombra-painel.tar.gz)
BAIXADOS=$(mktemp -d)
trap 'rm -rf "$BAIXADOS"' EXIT

# 1 · os artefatos: um release do GitHub (o pedido, ou o último) ou os locais de infra/artefatos/.
if [[ -z $SOMBRA_RELEASE && -s $INFRA_DIR/artefatos/SHA256SUMS ]]; then
  origem=local
  versao=$(cat "$INFRA_DIR/artefatos/VERSAO")
  cp "$INFRA_DIR/artefatos/SHA256SUMS" "${ARQUIVOS[@]/#/$INFRA_DIR/artefatos/}" "$BAIXADOS/"
else
  origem=github
  versao=${SOMBRA_RELEASE:-$(curl -fsSL "https://api.github.com/repos/$SOMBRA_REPOSITORIO/releases/latest" | jq -r .tag_name)}
  base="https://github.com/$SOMBRA_REPOSITORIO/releases/download/$versao"
  for arquivo in SHA256SUMS "${ARQUIVOS[@]}"; do
    curl -fsSL --retry 3 -o "$BAIXADOS/$arquivo" "$base/$arquivo"
  done
fi
log_info release.obtido "Artefatos da versão $versao ($origem)" "versao=$versao" "origem=$origem"

# 2 · a integridade: nada é instalado se um só byte não bater com o SHA256SUMS.
if ! (cd "$BAIXADOS" && sha256sum --quiet -c SHA256SUMS); then
  log_critico release.checksum_invalido "O SHA256 dos artefatos da versão $versao não confere" "versao=$versao"
  auditar "release:$versao" falha "motivo=checksum"
  exit 1
fi
auditar "release:$versao" sucesso "origem=$origem" "sha256sums=$(sha256sum "$BAIXADOS/SHA256SUMS" | cut -c1-16)"

# 3 · a instalação, uma pasta por versão; trocar de versão é trocar o link (e voltar também).
destino=/opt/sombra/releases/$versao
nova_versao=0
if [[ ! -f $destino/SHA256SUMS ]] || ! cmp -s "$destino/SHA256SUMS" "$BAIXADOS/SHA256SUMS"; then
  rm -rf "$destino"
  install -d -m 0755 "$destino/core" "$destino/clima" "$destino/painel"
  tar -C "$destino/core" -xzf "$BAIXADOS/sombra-core-linux-x64.tar.gz"
  install -m 0644 "$BAIXADOS/sombra-clima.jar" "$destino/clima/sombra-clima.jar"
  tar -C "$destino/painel" -xzf "$BAIXADOS/sombra-painel.tar.gz"
  install -m 0644 "$BAIXADOS/SHA256SUMS" "$destino/SHA256SUMS"
  chown -R root:root "$destino"
  chmod 0755 "$destino/core/Sombra.Api"
  nova_versao=1
  auditar "instalacao:$destino" sucesso "versao=$versao"
fi
if [[ $(readlink -f /opt/sombra/atual 2>/dev/null) != "$destino" ]]; then
  ln -sfn "$destino" /opt/sombra/atual
  nova_versao=1
  auditar "link:/opt/sombra/atual" sucesso "versao=$versao"
fi

# 4 · o ambiente de cada serviço: segredos só aqui, legíveis pelo root e pelo grupo sombra-ops.
escrever_arquivo /etc/sombra/core.env 0640 "root:$SOMBRA_GRUPO_OPS" <<ENV
# Gerado por infra/steps/70-aplicacao.sh. Contém segredos: 0640 root:$SOMBRA_GRUPO_OPS.
ASPNETCORE_ENVIRONMENT=$SOMBRA_AMBIENTE_APP
ASPNETCORE_URLS=http://127.0.0.1:5080
HOME=/var/lib/sombra
ConnectionStrings__Core=Host=127.0.0.1;Port=5432;Database=sombra_core;Username=sombra_core;Password=$CORE_DB_PASSWORD
Banco__MigrarAoIniciar=true
Jwt__Emissor=sombra-core
Jwt__Segredo=$JWT_SEGREDO
Privacidade__ChaveAes=$PRIVACIDADE_CHAVE_AES
Privacidade__ChaveIndiceCego=$PRIVACIDADE_CHAVE_INDICE_CEGO
Privacidade__Aplicacao=Ligada
Sentinela__ChaveDeVinculo=$SENTINELA_CHAVE_DE_VINCULO
Demo__Senha=$DEMO_SENHA
Demo__SentinelaDeviceId=$SOMBRA_DEVICE_DEMO
Demo__SentinelaSegredoHex=$SENTINELA_SEGREDO_HEX
Ops__TokenDoMonitor=$OPS_TOKEN_DO_MONITOR
Mqtt__Host=127.0.0.1
Mqtt__Porta=1883
Clima__UrlBase=http://127.0.0.1:8081
Versao__Commit=$versao
ENV
mudou_core=$ARQUIVO_MUDOU
escrever_arquivo /etc/sombra/clima.env 0640 "root:$SOMBRA_GRUPO_OPS" <<ENV
# Gerado por infra/steps/70-aplicacao.sh. Contém segredos: 0640 root:$SOMBRA_GRUPO_OPS.
SERVER_ADDRESS=127.0.0.1
SPRING_DATASOURCE_URL=jdbc:postgresql://127.0.0.1:5432/sombra_clima
SPRING_DATASOURCE_USERNAME=sombra_clima
SPRING_DATASOURCE_PASSWORD=$CLIMA_DB_PASSWORD
SOMBRA_AMBIENTE=$SOMBRA_AMBIENTE_APP
SOMBRA_COMMIT=$versao
CLIMA_OPEN_METEO_HABILITADO=true
ENV
mudou_clima=$ARQUIVO_MUDOU

# 5 · o broker: anônimo por desenho (a segurança vem da assinatura HMAC das mensagens, ADR-004), na rede interna.
escrever_arquivo /etc/mosquitto/conf.d/sombra.conf 0644 root:root <<MQTT
# Gerado por infra/steps/70-aplicacao.sh: localhost (o Core) e a rede interna (a Sentinela e o simulador).
listener 1883 127.0.0.1
listener 1883 $SOMBRA_IP
allow_anonymous true
MQTT
if ((ARQUIVO_MUDOU)) || ! systemctl is-active --quiet mosquitto; then
  systemctl enable mosquitto >/dev/null 2>&1
  systemctl restart mosquitto
fi

# 6 · o nginx: os três nomes da zona; o site padrão do pacote sai.
escrever_arquivo /etc/nginx/sites-available/sombra.conf 0644 root:root <"$INFRA_DIR/nginx/sombra.conf"
mudou_nginx=$ARQUIVO_MUDOU
ln -sfn /etc/nginx/sites-available/sombra.conf /etc/nginx/sites-enabled/sombra.conf
rm -f /etc/nginx/sites-enabled/default
nginx -t -q
if ((mudou_nginx)) || ! systemctl is-active --quiet nginx; then
  systemctl enable nginx >/dev/null 2>&1
  systemctl reload-or-restart nginx
fi

# 7 · as units endurecidas; sobem no boot e reiniciam sozinhas.
mudou_units=0
for unit in sombra-clima sombra-core; do
  escrever_arquivo "/etc/systemd/system/$unit.service" 0644 root:root <"$INFRA_DIR/systemd/$unit.service"
  ((ARQUIVO_MUDOU)) && mudou_units=1
done
((mudou_units)) && systemctl daemon-reload
systemctl enable sombra-clima sombra-core >/dev/null 2>&1
if ((nova_versao || mudou_clima || mudou_units)) || ! systemctl is-active --quiet sombra-clima; then
  systemctl restart sombra-clima
  auditar "servico:sombra-clima" sucesso "acao=restart" "versao=$versao"
fi
if ((nova_versao || mudou_core || mudou_units)) || ! systemctl is-active --quiet sombra-core; then
  systemctl restart sombra-core
  auditar "servico:sombra-core" sucesso "acao=restart" "versao=$versao"
fi

# 8 · espera cada serviço ficar pronto (as migrações rodam na subida: Flyway no Clima, EF Core no Core).
esperar() { # <nome> <url> <segundos>
  local nome=$1 url=$2 limite=$3
  for _ in $(seq 1 "$limite"); do
    curl -fsS -o /dev/null "$url" && { log_info servico.pronto "$nome pronto" "servico=$nome"; return 0; }
    sleep 1
  done
  log_erro servico.nao_ficou_pronto "$nome não respondeu em $limite s ($url)" "servico=$nome"
  journalctl -u "$nome" -n 30 --no-pager || true
  return 1
}
esperar sombra-clima http://127.0.0.1:8081/actuator/health/readiness 180
esperar sombra-core http://127.0.0.1:5080/health/ready 120
log_info aplicacao.instalada "Sombra $versao no ar" "versao=$versao" "origem=$origem"
