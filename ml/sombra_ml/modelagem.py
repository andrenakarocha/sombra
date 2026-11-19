"""Divisão temporal, baselines, busca de hiperparâmetros e limiar de decisão."""

import numpy as np
import pandas as pd
import xgboost as xgb
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
from sklearn.model_selection import GridSearchCV, TimeSeriesSplit
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler

from sombra_ml.dados import FUSO_LOCAL
from sombra_ml.features import HORIZONTE_HORAS, NOMES_DAS_FEATURES

INICIO_TESTE = pd.Timestamp("2022-01-01", tz=FUSO_LOCAL)
AMOSTRAS_POR_HORIZONTE = 4  # uma amostra a cada 6 h: 4 amostras cobrem as 24 h do alvo
SEMENTE = 42

# Os 3 hiperparâmetros da busca. A regularização abaixo é fixa e foi comparada antes, também só na validação do treino.
GRADE = {"max_depth": [3, 5, 7], "learning_rate": [0.03, 0.1], "n_estimators": [200, 500]}
REGULARIZACAO = {"subsample": 0.8, "colsample_bytree": 0.6, "min_child_weight": 10}


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


def novo_xgboost(**parametros) -> xgb.XGBClassifier:
    """Sem peso de classe: a saída precisa continuar sendo uma probabilidade; o desbalanceamento vai para o limiar."""
    base = {**REGULARIZACAO, "tree_method": "hist", "eval_metric": "logloss", "random_state": SEMENTE, "n_jobs": -1}
    return xgb.XGBClassifier(**{**base, **parametros})


def buscar_hiperparametros(X: np.ndarray, y: np.ndarray, grade: dict | None = None) -> GridSearchCV:
    """GridSearchCV só no treino, com a validação temporal, otimizando a área sob a curva precisão-recall."""
    busca = GridSearchCV(
        novo_xgboost(),
        grade or GRADE,
        scoring="average_precision",
        cv=validacao_temporal(),
        return_train_score=True,
    )
    return busca.fit(X, y)


def tabela_da_busca(busca: GridSearchCV) -> pd.DataFrame:
    """Uma linha por combinação, da melhor para a pior, com o desvio entre as dobras e a distância treino-validação."""
    r = pd.DataFrame(busca.cv_results_)
    tabela = pd.DataFrame(
        {
            "max_depth": r["param_max_depth"].astype(int),
            "learning_rate": r["param_learning_rate"].astype(float),
            "n_estimators": r["param_n_estimators"].astype(int),
            "pr_auc_validacao": r["mean_test_score"],
            "desvio_validacao": r["std_test_score"],
            "pr_auc_treino": r["mean_train_score"],
        }
    )
    tabela["diferenca"] = tabela["pr_auc_treino"] - tabela["pr_auc_validacao"]
    return tabela.sort_values("pr_auc_validacao", ascending=False).reset_index(drop=True)


def novo_regressor_pico(**parametros) -> xgb.XGBRegressor:
    """Mediana do pico das próximas 24 h em °C: o ``indiceDeCalorPrevistoC`` que o Core compara ao limite pessoal."""
    base = {
        **REGULARIZACAO,
        "objective": "reg:quantileerror",
        "quantile_alpha": 0.5,
        "max_depth": 5,
        "learning_rate": 0.05,
        "n_estimators": 400,
        "tree_method": "hist",
        "random_state": SEMENTE,
        "n_jobs": -1,
    }
    return xgb.XGBRegressor(**{**base, **parametros})


def previsoes_fora_da_amostra(modelo, X: np.ndarray, y: np.ndarray) -> np.ndarray:
    """Como ``probabilidades_fora_da_amostra``, para um regressor."""
    resultado = np.full(len(y), np.nan)
    for ajuste, validacao in validacao_temporal().split(X):
        resultado[validacao] = clone(modelo).fit(X[ajuste], y[ajuste]).predict(X[validacao])
    return resultado


def margem_superior(real: np.ndarray, previsto: np.ndarray) -> float:
    """Quanto somar à mediana para cobrir 90% dos picos reais, medido fora da amostra."""
    return float(np.quantile(np.asarray(real) - np.asarray(previsto), 0.9))
