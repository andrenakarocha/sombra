# Sombra · Core API (.NET 10). Todo comando dotnet roda na imagem oficial do SDK: reprodutível e igual ao CI.
SHELL := bash
.SHELLFLAGS := -eu -o pipefail -c
.DEFAULT_GOAL := help
SDK_IMAGE ?= mcr.microsoft.com/dotnet/sdk:10.0.404
RAIZ := $(abspath ../..)
GITLEAKS := $(shell command -v gitleaks 2>/dev/null)
DOCKER_SOCK ?= /var/run/docker.sock
SDK_RUN := docker run --rm --user $$(id -u):$$(id -g) -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp -e DOTNET_NOLOGO=1 \
	-e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e NUGET_PACKAGES=/nuget \
	-e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=* \
	-v $(RAIZ):/repo -v $(HOME)/.nuget/packages:/nuget $(if $(GITLEAKS),-v $(GITLEAKS):/usr/local/bin/gitleaks:ro) \
	-w /repo/services/core-api
DOTNET := $(SDK_RUN) $(SDK_IMAGE)
# Os testes de integração sobem um PostgreSQL com Testcontainers: o SDK usa o Docker e a rede do host.
DOTNET_COM_DOCKER := $(SDK_RUN) -v $(DOCKER_SOCK):/var/run/docker.sock --group-add $$(stat -c %g $(DOCKER_SOCK)) \
	--network host -e TESTCONTAINERS_HOST_OVERRIDE=127.0.0.1 $(SDK_IMAGE)
EF := dotnet tool restore >/dev/null && dotnet ef
# O banco local do make up (porta e senha vêm do .env da raiz).
DB_PORTA := $(or $(shell grep -m1 '^DB_PORTA_LOCAL=' $(RAIZ)/.env 2>/dev/null | cut -d= -f2-),55432)
CORE_DB_PASSWORD := $(shell grep -m1 '^CORE_DB_PASSWORD=' $(RAIZ)/.env 2>/dev/null | cut -d= -f2-)

.PHONY: help restore build test coverage coverage-report format format-fix quality migration migration-check db-migrate openapi

help: ## Lista os alvos
	@grep -hE '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | awk 'BEGIN{FS=":.*## "}{printf "  %-11s %s\n", $$1, $$2}'

restore: ## Restaura os pacotes
	@mkdir -p $(HOME)/.nuget/packages
	@$(DOTNET) dotnet restore Sombra.Core.slnx --nologo

build: restore ## Compila com warnings tratados como erro
	@$(DOTNET) dotnet build Sombra.Core.slnx --no-restore --nologo -warnaserror -v minimal

test: build ## Roda os testes (MSTest); os de integração sobem um PostgreSQL com Testcontainers
	@$(DOTNET_COM_DOCKER) dotnet test Sombra.Core.slnx --no-build --nologo -v quiet

coverage: build ## Cobertura do domínio (mínimo de 90% de linhas)
	@rm -rf artifacts/coverage
	@$(DOTNET) dotnet test sources/Sombra.Dominio.Tests --no-build --nologo -v quiet --collect:"XPlat Code Coverage" --results-directory artifacts/coverage >/dev/null
	@python3 -I scripts/cobertura.py artifacts/coverage Sombra.Dominio 90

coverage-report: build ## Relatório de cobertura de toda a suíte (HTML em artifacts/coverage/relatorio)
	@rm -rf artifacts/coverage
	@$(DOTNET_COM_DOCKER) sh -c 'dotnet test Sombra.Core.slnx --no-build --nologo -v quiet --collect:"XPlat Code Coverage" --results-directory artifacts/coverage >/dev/null \
		&& dotnet tool restore >/dev/null \
		&& dotnet reportgenerator -reports:"artifacts/coverage/*/coverage.cobertura.xml" -targetdir:artifacts/coverage/relatorio \
			-reporttypes:"Html;TextSummary" -assemblyfilters:"+Sombra.*;-*Tests" -classfilters:"-*.Migrations.*" -title:"Sombra · Core API" >/dev/null \
		&& sed -n "1,20p" artifacts/coverage/relatorio/Summary.txt'

format: restore ## Confere formatação e estilo sem mudar nada
	@$(DOTNET) dotnet format Sombra.Core.slnx --verify-no-changes --no-restore --severity warn

format-fix: restore ## Aplica formatação e estilo
	@$(DOTNET) dotnet format Sombra.Core.slnx --no-restore --severity warn

quality: restore ## Pipeline de qualidade do professor (scripts/Invoke-CodeQuality.ps1; ele também roda os testes)
	@$(DOTNET_COM_DOCKER) pwsh -NoProfile -File scripts/Invoke-CodeQuality.ps1

migration: restore ## Gera uma migração do EF Core (NOME=NomeDaMigracao)
	@test -n "$(NOME)" || { echo "use: make migration NOME=NomeDaMigracao"; exit 1; }
	@$(DOTNET) sh -c '$(EF) migrations add $(NOME) --project sources/Sombra.Infraestrutura --output-dir Persistencia/Migrations'

migration-check: restore ## Confere que as migrações cobrem o modelo atual do EF Core
	@$(DOTNET) sh -c '$(EF) migrations has-pending-model-changes --project sources/Sombra.Infraestrutura'

db-migrate: restore ## Aplica as migrações no banco local do make up
	@test -n "$(CORE_DB_PASSWORD)" || { echo "falta CORE_DB_PASSWORD no .env da raiz: rode make env"; exit 1; }
	@SOMBRA_CORE_DB="Host=127.0.0.1;Port=$(DB_PORTA);Database=sombra_core;Username=sombra_core;Password=$(CORE_DB_PASSWORD)" \
		$(SDK_RUN) --network host -e SOMBRA_CORE_DB $(SDK_IMAGE) sh -c '$(EF) database update --project sources/Sombra.Infraestrutura'

openapi: build ## Regenera docs/contratos/core-api.openapi.json a partir da API (o teste confere que não diverge)
	@$(DOTNET_COM_DOCKER) sh -c 'SOMBRA_ATUALIZAR_OPENAPI=1 dotnet test sources/Sombra.Api.IntegrationTests --no-build --nologo -v quiet --filter SaudeVersaoEDocumentacao'
