# Sombra · atalhos do monorepo. `make help` lista tudo.
SHELL := bash
.SHELLFLAGS := -eu -o pipefail -c
.DEFAULT_GOAL := help
COMPOSE := docker compose

.PHONY: help env doctor up down ps logs db-reset contratos lint semgrep test test-sim test-core test-mobile sim vinculo

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
	@echo "banco recriado, com as migrações e o seed do Core"

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

semgrep: ## Taint estático do Core: o canário dispara e o código de produção sai limpo (SARIF em security/semgrep/)
	@security/semgrep/rodar.sh

test: contratos test-sim test-core test-mobile ## Roda todos os testes (cada componente acrescenta os seus)

test-core: ## Build e testes do Core (C#), na imagem do SDK
	@$(MAKE) -s -C services/core-api test

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
