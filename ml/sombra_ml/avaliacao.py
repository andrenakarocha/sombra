"""Avaliação na linguagem do cidadão: dias de calor avisados a tempo e alarmes falsos, além das métricas por amostra."""

import numpy as np
import pandas as pd
from sklearn.base import clone
from sklearn.metrics import average_precision_score

from sombra_ml.dados import FUSO_LOCAL
from sombra_ml.features import HORIZONTE_HORAS, LIMIAR_ALVO_C
from sombra_ml.modelagem import AMOSTRAS_POR_HORIZONTE, SEMENTE

DIAS_POR_MES = 30.44


def dias_de_calor(indice_horario: pd.Series) -> pd.Series:
    """Para cada dia local que alcançou o limiar, o primeiro instante (UTC) em que isso aconteceu."""
    acima = indice_horario[indice_horario >= LIMIAR_ALVO_C]
    dias = acima.index.tz_convert(FUSO_LOCAL).date
    return pd.Series(acima.index, index=dias).groupby(level=0).first()


def avaliar_avisos(
    probabilidade: pd.Series, alvo: pd.Series, limiar: float, indice_horario: pd.Series
) -> dict[str, float]:
    """Um dia de calor foi avisado se houve alarme nas 24 h antes da primeira hora de calor daquele dia.

    Um dia tem alarme falso se alarmou e nenhum alarme daquele dia tinha calor nas 24 h seguintes. ``indice_horario``
    deve cobrir só o período avaliado: a duração dele é a base dos alarmes por mês.
    """
    alarmes = probabilidade.index[probabilidade >= limiar]
    calor = dias_de_calor(indice_horario.loc[probabilidade.index.min() : probabilidade.index.max()])
    antecedencias = []
    for inicio in calor:
        janela = alarmes[(alarmes >= inicio - pd.Timedelta(hours=HORIZONTE_HORAS)) & (alarmes < inicio)]
        if len(janela):
            antecedencias.append((inicio - janela.min()).total_seconds() / 3600)
    acertos = alvo.loc[alarmes]
    por_dia = acertos.groupby(alarmes.tz_convert(FUSO_LOCAL).date).max()
    dias_falsos = int((por_dia == 0).sum())
    dias_no_periodo = round((indice_horario.index.max() - indice_horario.index.min()).total_seconds() / 86400)
    return {
        "dias_de_calor": len(calor),
        "dias_avisados": len(antecedencias),
        "taxa_de_aviso": len(antecedencias) / len(calor) if len(calor) else float("nan"),
        "antecedencia_mediana_h": float(np.median(antecedencias)) if antecedencias else float("nan"),
        "dias_com_alarme": len(por_dia),
        "dias_com_alarme_falso": dias_falsos,
        "alarmes_falsos_por_mes": dias_falsos / (dias_no_periodo / DIAS_POR_MES),
    }


def diferenca_pr_auc_por_blocos(
    y: np.ndarray, a: np.ndarray, b: np.ndarray, grupos: np.ndarray, repeticoes: int = 2000
) -> dict[str, float]:
    """PR-AUC de ``a`` menos a de ``b``, com IC de 95% sorteando blocos inteiros (meses): calor vem em sequência."""
    gerador = np.random.default_rng(SEMENTE)
    posicoes = [np.flatnonzero(grupos == g) for g in np.unique(grupos)]
    diferencas = []
    for _ in range(repeticoes):
        sorteio = gerador.integers(0, len(posicoes), len(posicoes))
        linhas = np.concatenate([posicoes[i] for i in sorteio])
        if y[linhas].sum() == 0:
            continue
        diferencas.append(average_precision_score(y[linhas], a[linhas]) - average_precision_score(y[linhas], b[linhas]))
    inferior, superior = np.percentile(diferencas, [2.5, 97.5])
    return {
        "diferenca": float(average_precision_score(y, a) - average_precision_score(y, b)),
        "ic95_inferior": float(inferior),
        "ic95_superior": float(superior),
    }


def curva_de_aprendizado(modelo, X: np.ndarray, y: np.ndarray, fracoes=(0.2, 0.4, 0.6, 0.8, 1.0)) -> pd.DataFrame:
    """Treina com histórico crescente (sempre os dados mais recentes antes da validação) e valida nos 20% finais."""
    corte = int(len(y) * 0.8)
    fim_do_ajuste = corte - AMOSTRAS_POR_HORIZONTE
    linhas = []
    for fracao in fracoes:
        inicio = fim_do_ajuste - int(fim_do_ajuste * fracao)
        ajustado = clone(modelo).fit(X[inicio:fim_do_ajuste], y[inicio:fim_do_ajuste])
        linhas.append(
            {
                "amostras_de_treino": fim_do_ajuste - inicio,
                "pr_auc_treino": average_precision_score(
                    y[inicio:fim_do_ajuste], ajustado.predict_proba(X[inicio:fim_do_ajuste])[:, 1]
                ),
                "pr_auc_validacao": average_precision_score(y[corte:], ajustado.predict_proba(X[corte:])[:, 1]),
            }
        )
    return pd.DataFrame(linhas)
