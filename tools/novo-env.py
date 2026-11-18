#!/usr/bin/env python3
"""Cria ou completa o .env a partir do .env.example.

- Gera os segredos que faltam (vazios ou "troque-me").
- Nunca sobrescreve um valor real que já esteja no .env (ex.: a chave da Gemini).
- Mantém, no fim do arquivo, variáveis locais que não existem no .env.example.
- Grava com permissão 600 e mostra só os NOMES do que gerou, nunca os valores.

Uso: python3 -I tools/novo-env.py   (ou make env)
"""

import base64
import os
import pathlib
import secrets

RAIZ = pathlib.Path(__file__).resolve().parents[1]
EXEMPLO = RAIZ / ".env.example"
DESTINO = RAIZ / ".env"
VAZIO = {"", "troque-me"}


def chave_base64() -> str:
    return base64.b64encode(secrets.token_bytes(32)).decode()


GERADORES = {
    "POSTGRES_PASSWORD": lambda: secrets.token_hex(16),
    "CORE_DB_PASSWORD": lambda: secrets.token_hex(16),
    "CLIMA_DB_PASSWORD": lambda: secrets.token_hex(16),
    "Jwt__Segredo": lambda: secrets.token_urlsafe(48),
    "Privacidade__ChaveAes": chave_base64,
    "Privacidade__ChaveIndiceCego": chave_base64,
    "Ops__TokenDoMonitor": lambda: secrets.token_hex(24),
    "Demo__Senha": lambda: secrets.token_urlsafe(12),
    "SENTINELA_SEGREDO_HEX": lambda: secrets.token_hex(32),
    "SENTINELA_CHAVE_DE_VINCULO": lambda: secrets.token_hex(32),
}


def ler(caminho: pathlib.Path) -> dict[str, str]:
    valores = {}
    if caminho.exists():
        for linha in caminho.read_text(encoding="utf-8").splitlines():
            if linha.strip() and not linha.lstrip().startswith("#") and "=" in linha:
                chave, valor = linha.split("=", 1)
                valores[chave.strip()] = valor.strip()
    return valores


def main() -> None:
    atuais = ler(DESTINO)
    saida, gerados, faltando, vistos = [], [], [], set()
    for linha in EXEMPLO.read_text(encoding="utf-8").splitlines():
        if not linha.strip() or linha.lstrip().startswith("#") or "=" not in linha:
            saida.append(linha)
            continue
        chave, padrao = (parte.strip() for parte in linha.split("=", 1))
        vistos.add(chave)
        atual = atuais.get(chave)
        if atual is not None and atual not in VAZIO:
            valor = atual
        elif chave in GERADORES:
            valor = GERADORES[chave]()
            gerados.append(chave)
        else:
            valor = padrao
            if valor in VAZIO:
                faltando.append(chave)
        saida.append(f"{chave}={valor}")

    extras = [f"{c}={v}" for c, v in atuais.items() if c not in vistos]
    if extras:
        saida += ["", "# ── Extras locais (não estão no .env.example) ──", *extras]

    fd = os.open(DESTINO, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as arquivo:
        arquivo.write("\n".join(saida) + "\n")
    os.chmod(DESTINO, 0o600)

    print(f".env pronto ({len(vistos)} variáveis, permissão 600)")
    print("  gerados agora:", ", ".join(gerados) or "nenhum")
    print("  preencher à mão:", ", ".join(faltando) or "nada")


if __name__ == "__main__":
    main()
