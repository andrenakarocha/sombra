import numpy as np
import pandas as pd
import pytest
from sklearn.base import BaseEstimator, ClassifierMixin

from sombra_ml.modelagem import (
    GRADE,
    INICIO_TESTE,
    buscar_hiperparametros,
    dividir_temporal,
    limiar_para_recall,
    metricas,
    nova_logistica,
    probabilidades_fora_da_amostra,
    tabela_da_busca,
    validacao_temporal,
)


def _amostras():
    indice = pd.date_range("2021-12-29 03:00", "2022-01-03 03:00", freq="6h", tz="UTC")
    return pd.DataFrame({"x": np.arange(len(indice), dtype=float), "alvo": 0}, index=indice)


class Espiao(ClassifierMixin, BaseEstimator):
    """Guarda o que recebeu no treino e devolve a maior posição vista como probabilidade fictícia."""

    vistos = []

    def fit(self, X, y):
        Espiao.vistos.append(X[:, 0].copy())
        self.classes_ = np.array([0, 1])
        return self

    def predict_proba(self, X):
        return np.column_stack([np.full(len(X), 0.5), np.full(len(X), 0.5)])


def test_teste_comeca_na_virada_de_2022_no_horario_local():
    treino, teste = dividir_temporal(_amostras())
    assert teste.index.min() >= INICIO_TESTE
    assert treino.index.max() < INICIO_TESTE


def test_treino_nao_tem_alvo_que_entra_no_periodo_de_teste():
    treino, _ = dividir_temporal(_amostras())
    assert (treino.index + pd.Timedelta(hours=24)).max() < INICIO_TESTE


def test_divisao_nao_embaralha():
    treino, teste = dividir_temporal(_amostras())
    assert treino.index.is_monotonic_increasing and teste.index.is_monotonic_increasing


def test_validacao_tem_5_dobras_com_folga_de_24_horas():
    dobras = list(validacao_temporal().split(np.zeros((100, 1))))
    assert len(dobras) == 5
    for ajuste, validacao in dobras:
        assert validacao.min() - ajuste.max() - 1 == 4  # 4 amostras de 6 h = 24 h sem uso entre treino e validação


def test_fora_da_amostra_nunca_treina_com_o_que_vai_prever():
    Espiao.vistos = []
    X = np.arange(60, dtype=float).reshape(-1, 1)
    y = np.array([0, 1] * 30)
    resultado = probabilidades_fora_da_amostra(Espiao(), X, y)
    previstos = np.flatnonzero(~np.isnan(resultado))
    assert np.isnan(resultado[0])
    primeiro_validado = previstos.min()
    assert Espiao.vistos[0].max() < primeiro_validado
    assert len(Espiao.vistos) == 5


def test_limiar_e_o_maior_que_ainda_alcanca_o_recall():
    y = np.array([1, 1, 1, 1, 0, 0])
    p = np.array([0.9, 0.8, 0.7, 0.2, 0.75, 0.1])
    assert limiar_para_recall(y, p, 0.75) == pytest.approx(0.7)
    assert limiar_para_recall(y, p, 1.0) == pytest.approx(0.2)


def test_metricas_conhecidas():
    y = np.array([1, 1, 0, 0])
    p = np.array([0.9, 0.2, 0.8, 0.1])
    resultado = metricas(y, p, limiar=0.5)
    assert resultado["precisao"] == 0.5
    assert resultado["recall"] == 0.5
    assert resultado["f1"] == 0.5
    assert resultado["f2"] == 0.5
    assert resultado["roc_auc"] == 0.75
    assert resultado["brier"] == pytest.approx((0.01 + 0.64 + 0.64 + 0.01) / 4)


def test_metricas_sem_probabilidade_nao_inventam_brier():
    resultado = metricas(np.array([1, 0]), np.array([35.0, 20.0]), limiar=32.22, probabilidade=False)
    assert np.isnan(resultado["brier"])
    assert resultado["recall"] == 1


def test_logistica_aceita_ausentes():
    X = np.array([[1.0, np.nan], [2.0, 1.0], [3.0, 2.0], [4.0, np.nan]])
    assert nova_logistica().fit(X, [0, 0, 1, 1]).predict_proba(X).shape == (4, 2)


def _dados_separaveis(n=240):
    gerador = np.random.default_rng(0)
    X = gerador.normal(size=(n, 3)).astype(np.float32)
    return X, (X[:, 0] > 0.8).astype(int)


def test_grade_tem_3_hiperparametros_e_12_combinacoes():
    assert set(GRADE) == {"max_depth", "learning_rate", "n_estimators"}
    assert np.prod([len(v) for v in GRADE.values()]) == 12


def test_busca_usa_so_a_validacao_temporal_e_gera_a_tabela_completa():
    X, y = _dados_separaveis()
    grade = {"max_depth": [2, 3], "learning_rate": [0.1], "n_estimators": [5]}
    busca = buscar_hiperparametros(X, y, grade)
    assert busca.scoring == "average_precision"
    assert busca.cv.n_splits == 5 and busca.cv.gap == 4
    tabela = tabela_da_busca(busca)
    assert len(tabela) == 2
    assert {"pr_auc_validacao", "desvio_validacao", "pr_auc_treino", "diferenca"} <= set(tabela.columns)
    assert tabela["pr_auc_validacao"].is_monotonic_decreasing
