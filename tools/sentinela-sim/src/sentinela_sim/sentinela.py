"""Sentinela simulada: publica telemetria e eventos assinados e obedece comandos verificados."""

import json
import os
import pathlib
import threading
import time
from collections.abc import Callable

import paho.mqtt.client as mqtt

from . import assinatura, log
from .indice_calor import indice_de_calor_c, nivel

ESTADO_PADRAO = pathlib.Path(__file__).resolve().parents[2] / ".estado"


class Sentinela:
    def __init__(
        self,
        device_id: str,
        segredo_hex: str,
        host: str = "localhost",
        porta: int = 1883,
        estado_dir: pathlib.Path | None = None,
        prefixo: str = "sombra/v1",
        relogio: Callable[[], float] = time.time,
    ) -> None:
        self.device_id = device_id
        self.segredo = segredo_hex
        self.host = host
        self.porta = porta
        self.prefixo = prefixo
        self.relogio = relogio
        self.arquivo_estado = (estado_dir or ESTADO_PADRAO) / f"{device_id}.json"
        estado = self._carregar()
        self.seq = estado.get("seq", 0)
        self.ultimo_seq_comando = estado.get("ultimoSeqComando", 0)
        self.nivel_atual: str | None = None
        self.comandos_aceitos: list[dict] = []
        self.recusas: list[str] = []
        self._conectado = threading.Event()
        self._novo_comando = threading.Event()
        self.cliente = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, client_id=f"{device_id}-sim")
        self.cliente.will_set(self.topico("status"), "offline", qos=1, retain=True)
        self.cliente.on_connect = self._ao_conectar
        self.cliente.on_message = self._ao_receber

    def topico(self, canal: str) -> str:
        return f"{self.prefixo}/{self.device_id}/{canal}"

    # ── conexão ──────────────────────────────────────────────────────────────────
    def conectar(self, timeout: float = 5.0) -> None:
        self.cliente.connect(self.host, self.porta, keepalive=30)
        self.cliente.loop_start()
        if not self._conectado.wait(timeout):
            self.cliente.loop_stop()
            raise ConnectionError(f"sem conexão com o broker {self.host}:{self.porta}")

    def desconectar(self) -> None:
        self.cliente.publish(self.topico("status"), "offline", qos=1, retain=True).wait_for_publish(3)
        self.cliente.disconnect()
        self.cliente.loop_stop()
        log.registrar("mqtt.desconectado", "Sentinela desconectada", deviceId=self.device_id)

    def _ao_conectar(self, cliente, _userdata, _flags, reason_code, _properties) -> None:
        if reason_code.is_failure:
            log.registrar(
                "mqtt.desconectado", f"Conexão recusada: {reason_code}", level="Error", deviceId=self.device_id
            )
            return
        cliente.subscribe(self.topico("comando"), qos=1)
        cliente.publish(self.topico("status"), "online", qos=1, retain=True)
        log.registrar("mqtt.conectado", f"Conectada a {self.host}:{self.porta}", deviceId=self.device_id)
        self._conectado.set()

    # ── envio ────────────────────────────────────────────────────────────────────
    def telemetria(self, temperatura_c: float, umidade_pct: float, adulterar: bool = False) -> dict:
        t, rh = round(temperatura_c, 1), round(umidade_pct, 1)
        hi = round(indice_de_calor_c(t, rh), 1)
        n = nivel(hi)
        seq = self._proximo_seq()
        m = {"v": 1, "ts": int(self.relogio()), "seq": seq, "t": t, "rh": rh, "hi": hi, "nivel": n}
        msg = assinatura.assinada("telemetria", self.device_id, self.segredo, m)
        if adulterar:
            msg["t"] = round(t + 5.0, 1)
        msg["rssi"] = -60
        self.cliente.publish(self.topico("telemetria"), json.dumps(msg), qos=0)
        cid = f"{self.device_id}:{seq}"
        if adulterar:
            log.registrar(
                "telemetria.adulterada_enviada",
                "Telemetria alterada depois de assinada (teste)",
                level="Warning",
                correlation_id=cid,
                seq=seq,
            )
        else:
            log.registrar("telemetria.enviada", f"{t} °C, {rh}% → índice {hi} °C", correlation_id=cid, seq=seq, nivel=n)
        if n != self.nivel_atual:
            log.registrar(
                "nivel.mudou", f"{self.nivel_atual or 'início'} → {n}", correlation_id=cid, de=self.nivel_atual, para=n
            )
            self.nivel_atual = n
        return msg

    def evento(self, tipo: str) -> dict:
        seq = self._proximo_seq()
        m = {"v": 1, "ts": int(self.relogio()), "seq": seq, "tipo": tipo}
        msg = assinatura.assinada("evento", self.device_id, self.segredo, m)
        self.cliente.publish(self.topico("evento"), json.dumps(msg), qos=1)
        nome = "checkin.enviado" if tipo == "CHECKIN_ESTOU_BEM" else "evento.enviado"
        log.registrar(nome, tipo, correlation_id=f"{self.device_id}:{seq}", seq=seq, tipo=tipo)
        return msg

    # ── recebimento ──────────────────────────────────────────────────────────────
    def _ao_receber(self, _cliente, _userdata, mensagem) -> None:
        try:
            m = json.loads(mensagem.payload)
        except (ValueError, UnicodeDecodeError):
            self._recusar("json_invalido", None)
            return
        motivo = assinatura.verificar(
            "comando", self.device_id, self.segredo, m, self.ultimo_seq_comando, self.relogio()
        )
        if motivo:
            self._recusar(motivo, m)
            return
        self.ultimo_seq_comando = m["seq"]
        self._salvar()
        self.comandos_aceitos.append(m)
        log.registrar(
            "comando.aceito",
            f"OLED: {m['mensagem']}",
            correlation_id=f"{self.device_id}:c{m['seq']}",
            tipo=m["tipo"],
            seq=m["seq"],
        )
        self._novo_comando.set()

    def _recusar(self, motivo: str, m: dict | None) -> None:
        self.recusas.append(motivo)
        seq = m.get("seq") if isinstance(m, dict) else None
        log.registrar(
            "comando.recusado",
            f"Comando recusado: {motivo}",
            level="Warning",
            correlation_id=f"{self.device_id}:c{seq}",
            motivo=motivo,
        )
        self._novo_comando.set()

    def esperar_comando(self, timeout: float) -> bool:
        recebeu = self._novo_comando.wait(timeout)
        self._novo_comando.clear()
        return recebeu

    # ── estado persistente (seq continua crescendo entre execuções) ────────────
    def _proximo_seq(self) -> int:
        self.seq += 1
        self._salvar()
        return self.seq

    def _carregar(self) -> dict:
        try:
            return json.loads(self.arquivo_estado.read_text(encoding="utf-8"))
        except FileNotFoundError:
            return {}

    def _salvar(self) -> None:
        self.arquivo_estado.parent.mkdir(parents=True, exist_ok=True)
        temporario = self.arquivo_estado.with_suffix(".tmp")
        temporario.write_text(
            json.dumps({"seq": self.seq, "ultimoSeqComando": self.ultimo_seq_comando}), encoding="utf-8"
        )
        os.replace(temporario, self.arquivo_estado)


def publicar_comando(
    device_id: str,
    segredo_hex: str,
    tipo: str,
    mensagem: str,
    seq: int,
    host: str = "localhost",
    porta: int = 1883,
    prefixo: str = "sombra/v1",
    ts: int | None = None,
    adulterar: bool = False,
) -> dict:
    """Publica um comando assinado como o Core faria (para testes e demonstração sem o Core)."""
    m = {"v": 1, "ts": int(time.time()) if ts is None else ts, "seq": seq, "tipo": tipo, "mensagem": mensagem}
    msg = assinatura.assinada("comando", device_id, segredo_hex, m)
    if adulterar:
        msg["mensagem"] = msg["mensagem"] + "!"
    cliente = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, client_id=f"core-sim-{device_id}-{seq}")
    cliente.connect(host, porta, keepalive=10)
    cliente.loop_start()
    cliente.publish(f"{prefixo}/{device_id}/comando", json.dumps(msg), qos=1).wait_for_publish(5)
    cliente.disconnect()
    cliente.loop_stop()
    return msg
