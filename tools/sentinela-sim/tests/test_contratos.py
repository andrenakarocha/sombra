"""O simulador é uma implementação do contrato: roda os mesmos vetores que o ESP32, o Core e o Clima rodam."""

import json
import pathlib

import pytest

from sentinela_sim import assinatura
from sentinela_sim.indice_calor import indice_de_calor_c, nivel

CONTRATOS = pathlib.Path(__file__).resolve().parents[3] / "docs" / "contratos"
INDICE = json.loads((CONTRATOS / "indice-de-calor.vetores.json").read_text(encoding="utf-8"))
ASSINATURA = json.loads((CONTRATOS / "mqtt-assinatura.vetores.json").read_text(encoding="utf-8"))


@pytest.mark.parametrize("v", INDICE["vetores"], ids=lambda v: v["caso"])
def test_indice_de_calor_bate_com_os_vetores(v):
    calculado = indice_de_calor_c(v["temperaturaC"], v["umidadePct"])
    assert calculado == pytest.approx(v["indiceDeCalorC"], abs=INDICE["toleranciaC"])
    assert nivel(v["indiceDeCalorC"]) == v["nivel"]


@pytest.mark.parametrize("v", ASSINATURA["vetores"], ids=lambda v: v["canonica"])
def test_assinatura_bate_com_os_vetores(v):
    texto = assinatura.canonica(v["tipo"], ASSINATURA["deviceId"], v["mensagem"])
    assert texto == v["canonica"]
    assert assinatura.assinar(ASSINATURA["segredoHex"], texto) == v["sig"]
