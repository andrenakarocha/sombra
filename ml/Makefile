# ML do Sombra · `make -C ml help` lista tudo.
SHELL := bash
.SHELLFLAGS := -eu -o pipefail -c
.DEFAULT_GOAL := help
PY := .venv/bin/python

.PHONY: help setup lint test

help: ## Lista os alvos
	@grep -hE '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | sed -E 's/:.*## /\t/'

setup: ## Cria o .venv com as versões fixadas
	uv venv --python 3.12 --allow-existing .venv
	uv pip install -q -p $(PY) -r requirements.txt

lint: ## Ruff (regras e formato)
	.venv/bin/ruff check .
	.venv/bin/ruff format --check .

test: ## Testes da preparação, do treino e da exportação
	$(PY) -m pytest -q
