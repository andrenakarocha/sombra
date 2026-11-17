"""Log JSON por linha, no padrão de docs/observabilidade.md §1."""

import datetime
import json
import os
import sys

SERVICO = "sentinela-sim"


def registrar(event: str, message: str, level: str = "Information", correlation_id: str = "", **campos) -> None:
    linha = {
        "timestamp": datetime.datetime.now(datetime.UTC).isoformat(timespec="milliseconds").replace("+00:00", "Z"),
        "level": level,
        "service": SERVICO,
        "version": os.environ.get("SOMBRA_VERSAO", "dev"),
        "environment": os.environ.get("SOMBRA_AMBIENTE", "Development"),
        "correlationId": correlation_id,
        "event": event,
        "message": message,
        **campos,
    }
    print(json.dumps(linha, ensure_ascii=False), file=sys.stdout, flush=True)
