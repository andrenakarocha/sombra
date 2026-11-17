"""Assinatura HMAC e verificação das mensagens MQTT (contrato: docs/contratos/mqtt.asyncapi.yaml)."""

import hashlib
import hmac

JANELA_S = 300


def canonica(tipo: str, device_id: str, m: dict) -> str:
    if tipo == "telemetria":
        campos = [
            "T",
            m["v"],
            device_id,
            m["ts"],
            m["seq"],
            f"{m['t']:.1f}",
            f"{m['rh']:.1f}",
            f"{m['hi']:.1f}",
            m["nivel"],
        ]
    elif tipo == "evento":
        campos = ["E", m["v"], device_id, m["ts"], m["seq"], m["tipo"]]
    elif tipo == "comando":
        campos = ["C", m["v"], device_id, m["ts"], m["seq"], m["tipo"], m["mensagem"]]
    else:
        raise ValueError(f"tipo desconhecido: {tipo}")
    return "|".join(str(c) for c in campos)


def assinar(segredo_hex: str, texto: str) -> str:
    return hmac.new(bytes.fromhex(segredo_hex), texto.encode("utf-8"), hashlib.sha256).hexdigest()


def assinada(tipo: str, device_id: str, segredo_hex: str, m: dict) -> dict:
    """Devolve a mensagem com o campo sig."""
    return {**m, "sig": assinar(segredo_hex, canonica(tipo, device_id, m))}


def verificar(tipo: str, device_id: str, segredo_hex: str, m: dict, ultimo_seq: int, agora: float) -> str | None:
    """Devolve None se a mensagem é aceita, ou o motivo da recusa."""
    try:
        esperada = assinar(segredo_hex, canonica(tipo, device_id, m))
    except (KeyError, TypeError, ValueError):
        return "campos_invalidos"
    if not isinstance(m.get("sig"), str) or not hmac.compare_digest(esperada, m["sig"]):
        return "assinatura_invalida"
    if m["seq"] <= ultimo_seq:
        return "replay"
    if abs(agora - m["ts"]) > JANELA_S:
        return "fora_da_janela"
    return None
