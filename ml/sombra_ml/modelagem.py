"""Divisão temporal, baselines, busca de hiperparâmetros e limiar de decisão."""

import numpy as np
import pandas as pd
from sklearn.base import clone
from sklearn.impute import SimpleImputer
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import (
    average_precision_score,
    brier_score_loss,
    fbeta_score,
    precision_score,
    recall_score,
    roc_auc_score,
)
from sklearn.model_selection import TimeSeriesSplit
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler

from sombra_ml.dados import FUSO_LOCAL
from sombra_ml.features import HORIZONTE_HORAS, NOMES_DAS_FEATURES

INICIO_TESTE = pd.Timestamp("2022-01-01", tz=FUSO_LOCAL)
AMOSTRAS_POR_HORIZONTE = 4  # uma amostra a cada 6 h: 4 amostras cobrem as 24 h do alvo
SEMENTE = 42


def dividir_temporal(amostras: pd.DataFrame) -> tuple[pd.DataFrame, pd.DataFrame]:
    """Treino até 2021, teste de 2022 em diante, sem embaralhar.

    As últimas amostras de 2021 saem do treino: o alvo delas olharia as primeiras horas do teste.
    """
    limite = INICIO_TESTE - pd.Timedelta(hours=HORIZONTE_HORAS)
    return amostras[amostras.index < limite], amostras[amostras.index >= INICIO_TESTE]


def matriz(amostras: pd.DataFrame) -> np.ndarray:
    """Features em float32, o tipo que a árvore e o ONNX usam: treino e produção comparam os mesmos números."""
    return amostras[NOMES_DAS_FEATURES].to_numpy(dtype=np.float32)


def validacao_temporal() -> TimeSeriesSplit:
    """Cinco dobras em ordem do tempo, com 24 h sem uso entre o ajuste e a validação."""
    return TimeSeriesSplit(n_splits=5, gap=AMOSTRAS_POR_HORIZONTE)


def nova_logistica():
    return make_pipeline(
        SimpleImputer(strategy="median"), StandardScaler(), LogisticRegression(max_iter=3000, random_state=SEMENTE)
    )


def probabilidades_fora_da_amostra(modelo, X: np.ndarray, y: np.ndarray) -> np.ndarray:
    """Probabilidade de cada linha dada por um modelo que não a viu. O primeiro bloco nunca é validado e fica NaN."""
    resultado = np.full(len(y), np.nan)
    for ajuste, validacao in validacao_temporal().split(X):
        resultado[validacao] = clone(modelo).fit(X[ajuste], y[ajuste]).predict_proba(X[validacao])[:, 1]
    return resultado


def limiar_para_recall(y: np.ndarray, pontuacao: np.ndarray, recall_minimo: float) -> float:
    """O maior limiar cujo recall ainda alcança o mínimo (menos alarmes para a mesma proteção)."""
    positivos = np.sort(pontuacao[np.asarray(y) == 1])
    perdidos_permitidos = int(np.floor(len(positivos) * (1.0 - recall_minimo) + 1e-9))
    return float(positivos[perdidos_permitidos])


def metricas(y: np.ndarray, pontuacao: np.ndarray, limiar: float, probabilidade: bool = True) -> dict[str, float]:
    """Métricas da decisão (no limiar) e da ordenação. Brier só existe quando a pontuação é uma probabilidade."""
    decisao = pontuacao >= limiar
    return {
        "precisao": float(precision_score(y, decisao, zero_division=0)),
        "recall": float(recall_score(y, decisao, zero_division=0)),
        "f1": float(fbeta_score(y, decisao, beta=1, zero_division=0)),
        "f2": float(fbeta_score(y, decisao, beta=2, zero_division=0)),
        "roc_auc": float(roc_auc_score(y, pontuacao)),
        "pr_auc": float(average_precision_score(y, pontuacao)),
        "brier": float(brier_score_loss(y, pontuacao)) if probabilidade else float("nan"),
    }
