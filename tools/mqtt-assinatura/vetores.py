#!/usr/bin/env python3
"""Referência da assinatura HMAC das mensagens MQTT e vetores de teste compartilhados.

Contrato: docs/contratos/mqtt.asyncapi.yaml. O segredo abaixo é **só de teste** (público, neste repositório) e nunca
pode ser usado numa Sentinela real.

Uso:
    tools/mqtt-assinatura/vetores.py            confere docs/contratos/mqtt-assinatura.vetores.json (sai com 0 se ok)
    tools/mqtt-assinatura/vetores.py --gerar    reescreve o arquivo
"""

import hashlib
import hmac
import json
import pathlib
import sys

ARQUIVO = pathlib.Path(__file__).resolve().parents[2] / "docs" / "contratos" / "mqtt-assinatura.vetores.json"
SEGREDO_DE_TESTE_HEX = bytes(range(32)).hex()  # 000102…1f
DEVICE = "snt-7f3a9c"


def canonica(tipo: str, device_id: str, m: dict) -> str:
    if tipo == "telemetria":
        campos = ["T", m["v"], device_id, m["ts"], m["seq"], f"{m['t']:.1f}", f"{m['rh']:.1f}", f"{m['hi']:.1f}", m["nivel"]]
    elif tipo == "evento":
        campos = ["E", m["v"], device_id, m["ts"], m["seq"], m["tipo"]]
    elif tipo == "comando":
        campos = ["C", m["v"], device_id, m["ts"], m["seq"], m["tipo"], m["mensagem"]]
    else:
        raise ValueError(tipo)
    return "|".join(str(c) for c in campos)


def assinar(segredo_hex: str, texto: str) -> str:
    return hmac.new(bytes.fromhex(segredo_hex), texto.encode("utf-8"), hashlib.sha256).hexdigest()


CASOS = [
    ("telemetria", {"v": 1, "ts": 1763400000, "seq": 42, "t": 33.4, "rh": 40.0, "hi": 34.4, "nivel": "CAUTELA_EXTREMA"}),
    ("telemetria", {"v": 1, "ts": 1763400010, "seq": 43, "t": 36.0, "rh": 60.0, "hi": 48.1, "nivel": "PERIGO"}),
    ("evento", {"v": 1, "ts": 1763400060, "seq": 44, "tipo": "CHECKIN_ESTOU_BEM"}),
    ("evento", {"v": 1, "ts": 1763400120, "seq": 45, "tipo": "PEDIDO_DE_AJUDA"}),
    ("comando", {"v": 1, "ts": 1763400070, "seq": 7, "tipo": "PRE_ALERTA",
                 "mensagem": "Amanhã: calor perigoso. Encha sua garrafa de água."}),
    ("comando", {"v": 1, "ts": 1763400900, "seq": 8, "tipo": "FAMILIA_AVISADA", "mensagem": "Sua filha foi avisada."}),
]


def gerar() -> None:
    vetores = []
    for tipo, m in CASOS:
        texto = canonica(tipo, DEVICE, m)
        vetores.append({"tipo": tipo, "mensagem": m, "canonica": texto, "sig": assinar(SEGREDO_DE_TESTE_HEX, texto)})
    dados = {
        "aviso": "Segredo só de teste, público neste repositório. Nunca usar numa Sentinela real.",
        "segredoHex": SEGREDO_DE_TESTE_HEX,
        "deviceId": DEVICE,
        "vetores": vetores,
    }
    ARQUIVO.write_text(json.dumps(dados, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{len(vetores)} vetores gravados em {ARQUIVO}")


def conferir() -> int:
    dados = json.loads(ARQUIVO.read_text(encoding="utf-8"))
    falhas = []
    for v in dados["vetores"]:
        texto = canonica(v["tipo"], dados["deviceId"], v["mensagem"])
        if texto != v["canonica"]:
            falhas.append(f"canônica divergente: {v['canonica']}")
        if assinar(dados["segredoHex"], texto) != v["sig"]:
            falhas.append(f"assinatura divergente: {v['canonica']}")
    adulterado = dict(dados["vetores"][0]["mensagem"], t=40.0)
    if assinar(dados["segredoHex"], canonica("telemetria", dados["deviceId"], adulterado)) == dados["vetores"][0]["sig"]:
        falhas.append("payload adulterado continuou com a mesma assinatura")
    for f in falhas:
        print("FALHA:", f)
    print(f"{len(dados['vetores'])} vetores conferidos, {len(falhas)} falha(s)")
    return 1 if falhas else 0


if __name__ == "__main__":
    if "--gerar" in sys.argv[1:]:
        gerar()
    sys.exit(conferir())
