#!/usr/bin/env bash
# Sombra · auditoria do protótipo, antes × depois (spec 07 §5, F2.8).
# Roda os oito cenários (A1–A8) contra o Core real (PostgreSQL e broker MQTT em contêiner, na imagem oficial do SDK),
# primeiro com o Sombra.Privacidade desligado (modo inseguro, aceito só no ambiente Demo) e depois ligado. Grava
# security/auditoria/relatorio.md e relatorio.json com a requisição, a resposta, o log e os eventos de cada lado, e
# falha se algum cenário não vazar desligado ou não for bloqueado ligado.
set -euo pipefail

RAIZ=$(cd "$(dirname "$0")/../.." && pwd)
make -C "$RAIZ/services/core-api" auditoria
