import numpy as np
import pandas as pd
import pytest

from sombra_ml.avaliacao import avaliar_avisos, curva_de_aprendizado, dias_de_calor, diferenca_pr_auc_por_blocos
from sombra_ml.modelagem import novo_xgboost


def _indice_horario():
    """Quatro dias locais em janeiro (UTC-3 em 2023); só o dia 3 passa do limiar, às 15h locais."""
    indice = pd.date_range("2023-01-01 03:00", periods=24 * 4, freq="h", tz="UTC")
    serie = pd.Series(25.0, index=indice)
    serie.loc[pd.Timestamp("2023-01-03 18:00", tz="UTC")] = 33.0
    return serie


def _previsoes(alarmes):
    indice = pd.date_range("2023-01-01 03:00", periods=16, freq="6h", tz="UTC")
    alvo = (indice >= pd.Timestamp("2023-01-02 18:00", tz="UTC")) & (
        indice < pd.Timestamp("2023-01-03 18:00", tz="UTC")
    )
    probabilidade = pd.Series(0.0, index=indice)
    probabilidade.loc[[pd.Timestamp(a, tz="UTC") for a in alarmes]] = 0.9
    return probabilidade, pd.Series(alvo.astype(int), index=indice)


def test_dia_de_calor_guarda_a_primeira_hora_acima_do_limiar():
    dias = dias_de_calor(_indice_horario())
    assert list(dias) == [pd.Timestamp("2023-01-03 18:00", tz="UTC")]


def test_dia_e_avisado_quando_ha_alarme_nas_24_horas_anteriores():
    probabilidade, alvo = _previsoes(["2023-01-03 09:00"])
    r = avaliar_avisos(probabilidade, alvo, 0.5, _indice_horario())
    assert r["dias_de_calor"] == 1 and r["dias_avisados"] == 1
    assert r["antecedencia_mediana_h"] == 9
    assert r["dias_com_alarme_falso"] == 0


def test_alarme_depois_do_inicio_do_calor_nao_conta_como_aviso():
    probabilidade, alvo = _previsoes(["2023-01-03 21:00"])
    r = avaliar_avisos(probabilidade, alvo, 0.5, _indice_horario())
    assert r["dias_avisados"] == 0
    assert np.isnan(r["antecedencia_mediana_h"])


def test_alarme_em_dia_sem_calor_a_frente_e_falso_e_e_contado_uma_vez_por_dia():
    probabilidade, alvo = _previsoes(["2023-01-01 09:00", "2023-01-01 15:00"])
    r = avaliar_avisos(probabilidade, alvo, 0.5, _indice_horario())
    assert r["dias_com_alarme_falso"] == 1
    assert r["alarmes_falsos_por_mes"] == pytest.approx(1 / (4 / 30.44))


def test_bootstrap_de_modelos_iguais_tem_diferenca_zero():
    y = np.array([0, 1] * 30)
    p = np.linspace(0, 1, 60)
    grupos = np.repeat(np.arange(6), 10)
    r = diferenca_pr_auc_por_blocos(y, p, p, grupos, repeticoes=20)
    assert r == {"diferenca": 0.0, "ic95_inferior": 0.0, "ic95_superior": 0.0}


def test_curva_de_aprendizado_valida_sempre_no_mesmo_bloco_final():
    gerador = np.random.default_rng(0)
    X = gerador.normal(size=(300, 2)).astype(np.float32)
    y = (X[:, 0] > 0.5).astype(int)
    curva = curva_de_aprendizado(novo_xgboost(n_estimators=5, max_depth=2), X, y, fracoes=(0.5, 1.0))
    assert curva["amostras_de_treino"].tolist() == [118, 236]
    assert {"pr_auc_treino", "pr_auc_validacao"} <= set(curva.columns)
