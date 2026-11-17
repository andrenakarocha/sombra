# Sombra · Core API (.NET 10). Todo comando dotnet roda na imagem oficial do SDK: reprodutível e igual ao CI.
SHELL := bash
.SHELLFLAGS := -eu -o pipefail -c
.DEFAULT_GOAL := help
SDK_IMAGE ?= mcr.microsoft.com/dotnet/sdk:10.0.404
RAIZ := $(abspath ../..)
GITLEAKS := $(shell command -v gitleaks 2>/dev/null)
DOTNET := docker run --rm --user $$(id -u):$$(id -g) -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp -e DOTNET_NOLOGO=1 \
	-e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e NUGET_PACKAGES=/nuget \
	-e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=* \
	-v $(RAIZ):/repo -v $(HOME)/.nuget/packages:/nuget $(if $(GITLEAKS),-v $(GITLEAKS):/usr/local/bin/gitleaks:ro) \
	-w /repo/services/core-api $(SDK_IMAGE)

.PHONY: help restore build format format-fix quality

help: ## Lista os alvos
	@grep -hE '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | awk 'BEGIN{FS=":.*## "}{printf "  %-11s %s\n", $$1, $$2}'

restore: ## Restaura os pacotes
	@mkdir -p $(HOME)/.nuget/packages
	@$(DOTNET) dotnet restore Sombra.Core.slnx --nologo

build: restore ## Compila com warnings tratados como erro
	@$(DOTNET) dotnet build Sombra.Core.slnx --no-restore --nologo -warnaserror -v minimal

format: restore ## Confere formatação e estilo sem mudar nada
	@$(DOTNET) dotnet format Sombra.Core.slnx --verify-no-changes --no-restore --severity warn

format-fix: restore ## Aplica formatação e estilo
	@$(DOTNET) dotnet format Sombra.Core.slnx --no-restore --severity warn

quality: restore ## Pipeline de qualidade do professor (scripts/Invoke-CodeQuality.ps1)
	@$(DOTNET) pwsh -NoProfile -File scripts/Invoke-CodeQuality.ps1
