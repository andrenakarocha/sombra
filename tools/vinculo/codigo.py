#!/usr/bin/env python3
"""Código de vínculo da Sentinela: o que vai no QR que o app lê (sombra://sentinela/vincular?v=1&d=…&c=…).

O código sai do deviceId por HMAC-SHA256 com a chave de vínculo do servidor (SENTINELA_CHAVE_DE_VINCULO no .env): os 30
primeiros bits do HMAC, 5 por caractere, no alfabeto do app (sem 0/O e 1/I). A mesma conta está no Core
(Sombra.Dominio.Sentinelas.CodigoDeVinculo) e vai para o firmware (F5). A chave abaixo é **só de teste**.

Uso:
    tools/vinculo/codigo.py snt-abc123        imprime o QR da Sentinela com a chave do .env (ou do ambiente)
    tools/vinculo/codigo.py --conferir        confere docs/contratos/vinculo-da-sentinela.vetores.json (sai com 0 se ok)
    tools/vinculo/codigo.py --gerar           reescreve o arquivo de vetores
"""

import hashlib
import hmac
import json
import os
import pathlib
import re
import sys

RAIZ = pathlib.Path(__file__).resolve().parents[2]
ARQUIVO = RAIZ / "docs" / "contratos" / "vinculo-da-sentinela.vetores.json"
ALFABETO = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"
CHAVE_DE_TESTE_HEX = bytes(range(32, 64)).hex()  # 202122…3f
DEVICES = ["snt-7f3a9c", "snt-2b81d4", "snt-abc123", "snt-000000", "snt-ffffff"]


def codigo(device_id: str, chave_hex: str) -> str:
    mac = hmac.new(bytes.fromhex(chave_hex), device_id.encode("utf-8"), hashlib.sha256).digest()
    bits = int.from_bytes(mac[:5], "big")
    return "".join(ALFABETO[(bits >> (35 - 5 * i)) & 31] for i in range(6))


def vetores() -> dict:
    return {
        "descricao": "Código de vínculo da Sentinela (QR do app). Chave só de teste; nunca use numa Sentinela real.",
        "alfabeto": ALFABETO,
        "chaveHex": CHAVE_DE_TESTE_HEX,
        "vetores": [{"deviceId": d, "codigo": codigo(d, CHAVE_DE_TESTE_HEX)} for d in DEVICES],
    }


def chave_do_ambiente() -> str:
    chave = os.environ.get("SENTINELA_CHAVE_DE_VINCULO", "")
    env = RAIZ / ".env"
    if not chave and env.exists():
        for linha in env.read_text(encoding="utf-8").splitlines():
            if linha.startswith("SENTINELA_CHAVE_DE_VINCULO="):
                chave = linha.split("=", 1)[1].strip()
    if len(chave) < 64:
        sys.exit("SENTINELA_CHAVE_DE_VINCULO ausente: rode make env.")
    return chave


def main() -> int:
    argumento = sys.argv[1] if len(sys.argv) > 1 else "--conferir"
    if argumento == "--gerar":
        ARQUIVO.write_text(json.dumps(vetores(), indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        print(f"{ARQUIVO.relative_to(RAIZ)} gerado")
        return 0
    if argumento == "--conferir":
        if json.loads(ARQUIVO.read_text(encoding="utf-8")) != vetores():
            print("vetores do código de vínculo divergem: rode tools/vinculo/codigo.py --gerar e revise", file=sys.stderr)
            return 1
        print(f"código de vínculo: {len(DEVICES)} vetores ok")
        return 0
    if not re.fullmatch(r"snt-[0-9a-f]{6}", argumento):
        sys.exit("uso: tools/vinculo/codigo.py snt-abc123")
    print(f"sombra://sentinela/vincular?v=1&d={argumento}&c={codigo(argumento, chave_do_ambiente())}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
