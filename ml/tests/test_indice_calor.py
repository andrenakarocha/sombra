import json
from pathlib import Path

import numpy as np
import pytest

from sombra_ml.indice_calor import LIMIARES_C, classificar, indice_de_calor_c

CONTRATO = json.loads(
    (Path(__file__).resolve().parents[2] / "docs" / "contratos" / "indice-de-calor.vetores.json").read_text("utf-8")
)


@pytest.mark.parametrize("vetor", CONTRATO["vetores"], ids=lambda v: v["caso"])
def test_passa_nos_vetores_compartilhados(vetor):
    obtido = float(indice_de_calor_c(vetor["temperaturaC"], vetor["umidadePct"]))
    assert obtido == pytest.approx(vetor["indiceDeCalorC"], abs=CONTRATO["toleranciaC"])
    assert classificar(obtido) == vetor["nivel"]


def test_limiares_iguais_ao_contrato():
    assert CONTRATO["limiaresC"] == LIMIARES_C


def test_vetorizado_igual_ao_escalar_e_propaga_ausente():
    temperaturas = np.array([20.0, 35.0, np.nan])
    umidades = np.array([60.0, 50.0, 50.0])
    resultado = indice_de_calor_c(temperaturas, umidades)
    assert resultado[0] == pytest.approx(float(indice_de_calor_c(20.0, 60.0)))
    assert resultado[1] == pytest.approx(float(indice_de_calor_c(35.0, 50.0)))
    assert np.isnan(resultado[2])
