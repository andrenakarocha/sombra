# Sombra · atalhos do monorepo. `make help` lista tudo.
SHELL := bash
.SHELLFLAGS := -eu -o pipefail -c
.DEFAULT_GOAL := help
COMPOSE := docker compose

.PHONY: help env doctor up down ps logs db-reset contratos lint semgrep test test-sim test-core test-clima test-mobile test-infra e2e-painel evidencias-painel openapi-clima amostras-clima sim vinculo test-sentinela compilar-sentinela integrar-sentinela integrar-sentinela-wokwi demo-wokwi entrega-cyber test-qa qa-azure

help: ## Lista os alvos
	@grep -hE '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | awk 'BEGIN{FS=":.*## "}{printf "  %-10s %s\n", $$1, $$2}'

env: ## Cria ou completa o .env (gera os segredos que faltam; não sobrescreve nada)
	@python3 -I tools/novo-env.py

doctor: ## Confere as ferramentas da máquina
	@tools/doctor.sh

up: .env ## Sobe o ambiente local e espera tudo ficar saudável (reconstrói o Core quando o código muda)
	@SOMBRA_COMMIT=$$(git rev-parse --short HEAD) SOMBRA_CONSTRUIDO_EM=$$(date -u +%Y-%m-%dT%H:%M:%SZ) $(COMPOSE) up -d --build --wait
	@$(COMPOSE) ps --format 'table {{.Service}}\t{{.Status}}\t{{.Ports}}'

down: ## Para o ambiente (o volume do banco continua)
	@$(COMPOSE) down

ps: ## Estado dos serviços
	@$(COMPOSE) ps

logs: ## Acompanha os logs (SERVICO=core|clima|painel|db|mqtt; vazio = todos)
	@$(COMPOSE) logs -f $(SERVICO)

db-reset: .env ## Apaga o banco local e recria do zero (bancos e papéis por serviço)
	@$(COMPOSE) rm -sf db
	@docker volume rm -f sombra_dados-postgres >/dev/null
	@$(COMPOSE) up -d --wait db
	@$(MAKE) -s -C services/core-api db-migrate
	@set -a && . ./.env && set +a && cd services/clima-service && ./mvnw -q -B flyway:migrate
	@echo "banco recriado, com as migrações e os seeds do Core e do Clima"

contratos: ## Confere os contratos: vetores do índice de calor, assinatura MQTT, código de vínculo, contraste e AsyncAPI
	@python3 -I tools/indice-de-calor/vetores.py
	@python3 -I tools/mqtt-assinatura/vetores.py
	@python3 -I tools/vinculo/codigo.py --conferir
	@python3 -I design/tokens.py conferir | tail -n 1
	@npx -y @asyncapi/cli@6.3.1 validate docs/contratos/mqtt.asyncapi.yaml >/dev/null && echo "AsyncAPI válido"

lint: ## shellcheck nos scripts, ruff no Python e ESLint + tsc no app
	@shellcheck tools/*.sh infra/compose/postgres/init/*.sh && echo "shellcheck ok"
	@cd tools/sentinela-sim && uv run -q ruff check . && uv run -q ruff format --check . >/dev/null && echo "ruff ok"
	@cd mobile && npm run -s lint && npm run -s typecheck && echo "app ok"

test-qa: ## QA: confere a especificação (Features e Test Cases) e testa a sincronização contra um Azure falso
	@uv run -q qa/ferramentas/conferir_especificacao.py
	@uv run -q qa/ferramentas/testar_sincronizacao.py

qa-azure: ## Sobe as Features e os Test Cases para o Azure Boards (AZURE_DEVOPS_ORG, AZURE_DEVOPS_PROJETO e AZURE_DEVOPS_PAT)
	@uv run -q qa/ferramentas/sincronizar_azure.py

entrega-cyber: ## Gera a entrega de Cyber: security/entrega/cyber.pdf e, à parte, o sombra-codigo.zip (com o make up no ar)
	@python3 -I security/entrega/gerar.py

semgrep: ## Taint estático do Core: o canário dispara e o código de produção sai limpo (SARIF em security/semgrep/)
	@security/semgrep/rodar.sh

test: contratos test-sim test-core test-clima test-mobile test-infra test-sentinela ## Roda todos os testes (cada componente acrescenta os seus)

test-core: ## Build e testes do Core (C#), na imagem do SDK
	@$(MAKE) -s -C services/core-api test

test-clima: ## Build, testes de domínio, API (Testcontainers) e arquitetura do Clima (Java)
	@cd services/clima-service && ./mvnw -q -B verify

openapi-clima: ## Regenera docs/contratos/clima-service.openapi.json a partir do código (revise o diff)
	@cd services/clima-service && SOMBRA_ATUALIZAR_OPENAPI=1 ./mvnw -q -B test -Dtest=ContratoOpenApiTest -Dsurefire.failIfNoSpecifiedTests=false
	@git --no-pager diff --stat -- docs/contratos/clima-service.openapi.json

amostras-clima: ## Regrava docs/contratos/amostras-clima/ (respostas reais do Clima que o Core lê nos testes do ClimaClient)
	@cd services/clima-service && SOMBRA_ATUALIZAR_AMOSTRAS=1 ./mvnw -q -B test -Dtest=AmostrasDoContratoTest -Dsurefire.failIfNoSpecifiedTests=false
	@git --no-pager diff --stat -- docs/contratos/amostras-clima

test-infra: ## shellcheck e testes das bibliotecas da VM (log, auditoria, trap, orquestrador) num Ubuntu 24.04 descartável
	@find infra -name '*.sh' -not -path 'infra/compose/*' -print0 | xargs -0 shellcheck && echo "shellcheck ok"
	@docker run --rm -v "$$PWD/infra":/infra:ro ubuntu:24.04 bash -c \
	  'apt-get update -qq >/dev/null && apt-get install -y -qq jq >/dev/null 2>&1 && /infra/testes/testar-bibliotecas.sh'

# As chaves de teste do contrato (públicas de propósito, as mesmas do iot/sentinela/secrets.h): só para a demo no Wokwi.
SEGREDO_DE_TESTE := 000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f
CHAVE_DE_VINCULO_DE_TESTE := 202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f
TESTAR_SENTINELA := $(or $(TMPDIR),/tmp)/sombra-testar-sentinela

test-sentinela: ## Firmware da Sentinela no host (g++): índice de calor, HMAC, estados e fila, com os vetores do contrato
	@python3 -I iot/sentinela/testes/gerar_vetores.py iot/sentinela/testes/vetores.h
	@g++ -std=c++17 -Wall -Wextra -Werror -O1 -o $(TESTAR_SENTINELA) iot/sentinela/testes/testar.cpp && $(TESTAR_SENTINELA)

COMPILAR_SENTINELA := $(or $(TMPDIR),/tmp)/sombra-sentinela/sketch
compilar-sentinela: ## Compila o firmware para o ESP32 DevKit com aviso ligado (arduino-cli, core esp32 e o libraries.txt)
	@mkdir -p $(COMPILAR_SENTINELA) && cp iot/sentinela/sketch.ino iot/sentinela/*.h $(COMPILAR_SENTINELA)/
	@$(or $(ARDUINO_CLI),arduino-cli) compile --fqbn esp32:esp32:esp32 --warnings all $(COMPILAR_SENTINELA)

integrar-sentinela: ## Prova a Sentinela com o Core de verdade (o código do firmware → broker do compose → Core), com o make up no ar
	@iot/sentinela/testes/integrar-com-core.sh

integrar-sentinela-wokwi: ## A mesma prova pelo broker público, o caminho da Sentinela do Wokwi (com o make demo-wokwi no ar)
	@BROKER=broker.hivemq.com SENTINELA_SEGREDO_HEX=$(SEGREDO_DE_TESTE) iot/sentinela/testes/integrar-com-core.sh

demo-wokwi: .env ## Sobe o ambiente para a Sentinela do Wokwi: o Core no broker público, com as chaves de teste do contrato
	@MQTT_HOST_DO_CORE=broker.hivemq.com SENTINELA_SEGREDO_HEX=$(SEGREDO_DE_TESTE) SENTINELA_CHAVE_DE_VINCULO=$(CHAVE_DE_VINCULO_DE_TESTE) \
	  $(MAKE) --no-print-directory up

e2e-painel: ## E2E do painel (Playwright) contra o make up: login, roteiro com o simulador, Risco Cidadão e operação ao vivo
	@cd web/painel && npx playwright test

evidencias-painel: ## Regrava os prints das cinco páginas do painel em 1440, 768 e 390 px (web/painel/docs/evidencias)
	@cd web/painel && EVIDENCIAS=1 npx playwright test evidencias

test-sim: ## Testes do simulador da Sentinela (a integração usa o Mosquitto do make up)
	@cd tools/sentinela-sim && uv run -q pytest -q

test-mobile: ## Testes Jest do app
	@cd mobile && npm test -s -- --ci

vinculo: .env ## Imprime o QR de vínculo de uma Sentinela para o app ler (DEVICE=snt-abc123)
	@python3 -I tools/vinculo/codigo.py $(or $(DEVICE),snt-abc123)

sim: .env ## Roda um cenário da Sentinela simulada (CENARIO=onda-de-calor INTERVALO=1 OUVIR=0)
	@set -a && . ./.env && set +a && cd tools/sentinela-sim && uv run -q sentinela-sim rodar \
	  --cenario $(or $(CENARIO),onda-de-calor) --intervalo $(or $(INTERVALO),1) --ouvir $(or $(OUVIR),0)

.env:
	@python3 -I tools/novo-env.py
