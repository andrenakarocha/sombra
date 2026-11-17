"""Integração com um broker de verdade (o Mosquitto do compose). Pulado se não houver broker em localhost:1883."""

import json
import socket
import threading
import uuid

import paho.mqtt.client as mqtt
import pytest

from sentinela_sim import assinatura
from sentinela_sim.sentinela import Sentinela, publicar_comando

SEGREDO = "cd" * 32


def _broker_no_ar() -> bool:
    try:
        with socket.create_connection(("localhost", 1883), timeout=1):
            return True
    except OSError:
        return False


pytestmark = pytest.mark.skipif(not _broker_no_ar(), reason="sem broker MQTT em localhost:1883 (make up)")


@pytest.fixture
def sentinela(tmp_path):
    s = Sentinela(f"snt-{uuid.uuid4().hex[:6]}", SEGREDO, estado_dir=tmp_path)
    s.conectar()
    yield s
    s.desconectar()


def test_recebe_e_obedece_um_comando_assinado(sentinela):
    publicar_comando(sentinela.device_id, SEGREDO, "PRE_ALERTA", "Amanhã: calor perigoso.", seq=1)
    assert sentinela.esperar_comando(5)
    assert [c["tipo"] for c in sentinela.comandos_aceitos] == ["PRE_ALERTA"]


def test_recusa_replay_e_comando_adulterado(sentinela):
    publicar_comando(sentinela.device_id, SEGREDO, "LEMBRETE_AGUA", "Beba água.", seq=1)
    assert sentinela.esperar_comando(5)
    publicar_comando(sentinela.device_id, SEGREDO, "LEMBRETE_AGUA", "Beba água.", seq=1)
    assert sentinela.esperar_comando(5)
    publicar_comando(sentinela.device_id, SEGREDO, "NORMAL", "Tudo bem.", seq=2, adulterar=True)
    assert sentinela.esperar_comando(5)
    assert len(sentinela.comandos_aceitos) == 1
    assert sentinela.recusas == ["replay", "assinatura_invalida"]


def test_telemetria_publicada_tem_assinatura_valida(sentinela):
    recebida, pronto, inscrito = [], threading.Event(), threading.Event()
    ouvinte = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    ouvinte.on_message = lambda _c, _u, msg: (recebida.append(json.loads(msg.payload)), pronto.set())
    ouvinte.on_subscribe = lambda *_a: inscrito.set()
    ouvinte.connect("localhost", 1883)
    ouvinte.loop_start()
    try:
        ouvinte.subscribe(sentinela.topico("telemetria"), qos=0)
        assert inscrito.wait(5)
        sentinela.telemetria(36.0, 60.0)
        assert pronto.wait(5)
    finally:
        ouvinte.loop_stop()
        ouvinte.disconnect()
    m = recebida[0]
    assert m["nivel"] == "PERIGO" and m["hi"] == pytest.approx(48.1, abs=0.05)
    sem_rssi = {k: v for k, v in m.items() if k != "rssi"}
    assert assinatura.verificar("telemetria", sentinela.device_id, SEGREDO, sem_rssi, 0, m["ts"]) is None
