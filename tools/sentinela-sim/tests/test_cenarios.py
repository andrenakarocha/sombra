"""Os cenários fazem o que o roteiro da demonstração (PLANO §3.4) precisa."""

import pytest

from sentinela_sim.cenarios import CENARIOS
from sentinela_sim.indice_calor import indice_de_calor_c, nivel


def niveis(nome: str) -> list[str]:
    return [nivel(indice_de_calor_c(*d)) for a, d in CENARIOS[nome][0]() if a == "telemetria"]


def eventos(nome: str) -> list[str]:
    return [d for a, d in CENARIOS[nome][0]() if a == "evento"]


@pytest.mark.parametrize("nome", sorted(CENARIOS))
def test_medicoes_dentro_da_faixa_do_contrato(nome):
    for acao, dado in CENARIOS[nome][0]():
        if acao in ("telemetria", "adulterada"):
            t, rh = dado
            assert -10 <= t <= 60 and 0 <= rh <= 100


def test_onda_de_calor_chega_a_perigo_e_termina_com_estou_bem():
    assert "PERIGO" in niveis("onda-de-calor")
    assert eventos("onda-de-calor")[-1] == "CHECKIN_ESTOU_BEM"


def test_sem_resposta_fica_em_perigo_sem_checkin():
    assert niveis("sem-resposta")[-1] == "PERIGO"
    assert "CHECKIN_ESTOU_BEM" not in eventos("sem-resposta")


def test_esqueceu_checkin_diario_nunca_sai_do_normal():
    assert set(niveis("esqueceu-checkin-diario")) == {"NORMAL"}
    assert eventos("esqueceu-checkin-diario") == ["BOOT"]


def test_payload_adulterado_tem_uma_mensagem_alterada():
    assert sum(1 for a, _ in CENARIOS["payload-adulterado"][0]() if a == "adulterada") == 1
