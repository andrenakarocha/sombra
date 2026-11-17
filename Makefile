# Sombra · atalhos do monorepo. `make help` lista tudo.
SHELL := bash
.SHELLFLAGS := -eu -o pipefail -c
.DEFAULT_GOAL := help
COMPOSE := docker compose

.PHONY: help env doctor up down ps logs db-reset contratos lint test

help: ## Lista os alvos
	@grep -hE '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | awk 'BEGIN{FS=":.*## "}{printf "  %-10s %s\n", $$1, $$2}'

env: ## Cria ou completa o .env (gera os segredos que faltam; não sobrescreve nada)
	@python3 -I tools/novo-env.py

doctor: ## Confere as ferramentas da máquina
	@tools/doctor.sh

up: .env ## Sobe o ambiente local e espera tudo ficar saudável
	@$(COMPOSE) up -d --wait
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
	@echo "banco recriado"

contratos: ## Confere os contratos: vetores do índice de calor, assinatura MQTT, contraste e AsyncAPI
	@python3 -I tools/indice-de-calor/vetores.py
	@python3 -I tools/mqtt-assinatura/vetores.py
	@python3 -I design/tokens.py conferir | tail -n 1
	@npx -y @asyncapi/cli@6.3.1 validate docs/contratos/mqtt.asyncapi.yaml >/dev/null && echo "AsyncAPI válido"

lint: ## shellcheck em todos os scripts
	@shellcheck tools/*.sh infra/compose/postgres/init/*.sh && echo "shellcheck ok"

test: contratos ## Roda todos os testes (cada componente acrescenta os seus)

.env:
	@python3 -I tools/novo-env.py
