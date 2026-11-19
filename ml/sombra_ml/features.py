"""Features e alvo. Tudo sai das seis medições que o Clima Service guarda e das últimas 24 h, como em produção.

``CATALOGO`` é a definição única: gera as colunas de treino, a função de janela de produção (``features_da_janela``)
e o ``features.json`` que o Clima Service segue (F9.1).
"""

import warnings
from dataclasses import dataclass

import numpy as np
import pandas as pd

from sombra_ml.dados import FUSO_LOCAL
from sombra_ml.indice_calor import LIMIARES_C, indice_de_calor_c

LIMIAR_ALVO_C = LIMIARES_C["CAUTELA_EXTREMA"]
HORIZONTE_HORAS = 24
MIN_HORAS_COM_DADO = 18
HORAS_DE_AMOSTRA = (0, 6, 12, 18)
JANELAS = (3, 6, 12, 24)
DELTAS = (3, 6, 12, 18)
MAGNUS_A = 17.625
MAGNUS_B_C = 243.04


@dataclass(frozen=True)
class Feature:
    nome: str
    variavel: str
    agregacao: str
    janela_horas: int | None = None


PREFIXOS = {
    "temperatura_c": "temperatura",
    "umidade_pct": "umidade",
    "orvalho_c": "orvalho",
    "indice_calor_c": "indice_calor",
    "pressao_hpa": "pressao",
    "radiacao_kj_m2": "radiacao",
    "vento_ms": "vento",
    "chuva_mm": "chuva",
}


def _montar_catalogo() -> tuple[Feature, ...]:
    def janela(variavel: str, agregacao: str, horas: int) -> Feature:
        return Feature(f"{PREFIXOS[variavel]}_{agregacao}_{horas}h", variavel, agregacao, horas)

    itens = [
        Feature(f"{PREFIXOS[v]}_atual", v, "atual")
        for v in ("temperatura_c", "umidade_pct", "orvalho_c", "indice_calor_c", "pressao_hpa", "vento_ms")
    ]
    for horas in JANELAS:
        for variavel in ("temperatura_c", "indice_calor_c"):
            itens += [janela(variavel, "media", horas), janela(variavel, "maximo", horas)]
        itens += [janela("umidade_pct", "media", horas), janela("orvalho_c", "media", horas)]
        itens += [janela("radiacao_kj_m2", "soma", horas), janela("chuva_mm", "soma", horas)]
    itens += [janela("temperatura_c", "minimo", 24), janela("temperatura_c", "amplitude", 24)]
    itens += [janela("vento_ms", "media", 6), janela("vento_ms", "media", 24), janela("pressao_hpa", "media", 24)]
    for horas in DELTAS:
        for variavel in ("temperatura_c", "indice_calor_c", "umidade_pct", "pressao_hpa"):
            itens.append(janela(variavel, "delta", horas))
    itens += [Feature(nome, "tempo", nome) for nome in ("hora_sen", "hora_cos", "dia_do_ano_sen", "dia_do_ano_cos")]
    return tuple(itens)


CATALOGO = _montar_catalogo()
NOMES_DAS_FEATURES = [f.nome for f in CATALOGO]


def orvalho_c(temperatura_c, umidade_pct):
    """Ponto de orvalho (Magnus). É derivado porque o Clima Service não recebe o orvalho da Open-Meteo."""
    t = np.asarray(temperatura_c, dtype=float)
    ur = np.clip(np.asarray(umidade_pct, dtype=float), 1.0, 100.0)
    gama = np.log(ur / 100.0) + MAGNUS_A * t / (MAGNUS_B_C + t)
    return MAGNUS_B_C * gama / (MAGNUS_A - gama)


def series_base(tabela: pd.DataFrame) -> pd.DataFrame:
    """As séries horárias de onde saem todas as features."""
    colunas = ["temperatura_c", "umidade_pct", "pressao_hpa", "radiacao_kj_m2", "vento_ms", "chuva_mm"]
    base = tabela[colunas].astype(float).copy()
    base["orvalho_c"] = orvalho_c(base["temperatura_c"], base["umidade_pct"])
    base["indice_calor_c"] = indice_de_calor_c(base["temperatura_c"], base["umidade_pct"])
    return base


def _tempo(indice: pd.DatetimeIndex) -> dict[str, np.ndarray]:
    local = indice.tz_convert(FUSO_LOCAL)
    hora = 2 * np.pi * local.hour.to_numpy() / 24
    dia = 2 * np.pi * local.day_of_year.to_numpy() / 365.25
    return {
        "hora_sen": np.sin(hora),
        "hora_cos": np.cos(hora),
        "dia_do_ano_sen": np.sin(dia),
        "dia_do_ano_cos": np.cos(dia),
    }


def _agregar(valores: np.ndarray, agregacao: str) -> np.ndarray:
    """Agrega na última dimensão, ignorando ausentes. Soma sem nenhum dado é 0 (a radiação noturna não é informada)."""
    with warnings.catch_warnings():
        warnings.simplefilter("ignore", category=RuntimeWarning)
        match agregacao:
            case "media":
                return np.nanmean(valores, axis=-1)
            case "maximo":
                return np.nanmax(valores, axis=-1)
            case "minimo":
                return np.nanmin(valores, axis=-1)
            case "amplitude":
                return np.nanmax(valores, axis=-1) - np.nanmin(valores, axis=-1)
            case "soma":
                return np.nansum(valores, axis=-1)
    raise ValueError(f"agregação desconhecida: {agregacao}")


def calcular_features(limpa: pd.DataFrame) -> pd.DataFrame:
    """Features de todas as horas de uma grade horária completa. Cada linha só enxerga aquela hora e as anteriores."""
    base = series_base(limpa)
    colunas: dict[str, np.ndarray] = {}
    tempo = _tempo(base.index)
    for feature in CATALOGO:
        serie = base[feature.variavel].to_numpy() if feature.variavel != "tempo" else None
        if feature.agregacao == "atual":
            colunas[feature.nome] = serie
        elif feature.agregacao == "delta":
            colunas[feature.nome] = serie - pd.Series(serie).shift(feature.janela_horas).to_numpy()
        elif feature.variavel == "tempo":
            colunas[feature.nome] = tempo[feature.nome]
        else:
            preenchida = np.concatenate([np.full(feature.janela_horas - 1, np.nan), serie])
            janelas = np.lib.stride_tricks.sliding_window_view(preenchida, feature.janela_horas)
            colunas[feature.nome] = _agregar(janelas, feature.agregacao)
    return pd.DataFrame(colunas, index=base.index)[NOMES_DAS_FEATURES]


def features_da_janela(janela: pd.DataFrame) -> pd.Series:
    """Uma previsão, como em produção: recebe só as observações existentes, e o tempo vem do índice, não da posição."""
    base = series_base(janela.sort_index())
    agora = base.index.max()
    tempo = _tempo(pd.DatetimeIndex([agora]))
    valores = {}
    for feature in CATALOGO:
        if feature.variavel == "tempo":
            valores[feature.nome] = float(tempo[feature.nome][0])
            continue
        serie = base[feature.variavel]
        if feature.agregacao == "atual":
            valores[feature.nome] = float(serie.loc[agora])
        elif feature.agregacao == "delta":
            anterior = agora - pd.Timedelta(hours=feature.janela_horas)
            valores[feature.nome] = float(serie.loc[agora] - serie.get(anterior, np.nan))
        else:
            inicio = agora - pd.Timedelta(hours=feature.janela_horas)
            trecho = serie[serie.index > inicio].to_numpy()
            valores[feature.nome] = float(_agregar(trecho, feature.agregacao))
    return pd.Series(valores, dtype=float)[NOMES_DAS_FEATURES]


def pico_futuro(indice_calor: pd.Series) -> pd.Series:
    """Máximo de t+1 a t+24 numa grade horária; ausente se menos de 18 dessas horas tiverem dado."""
    invertida = indice_calor.iloc[::-1]
    pico = invertida.rolling(HORIZONTE_HORAS, min_periods=MIN_HORAS_COM_DADO).max().iloc[::-1]
    return pico.shift(-1)


def montar_amostras(limpa: pd.DataFrame) -> pd.DataFrame:
    """Uma amostra a cada 6 h (horário local), só em horas medidas e com histórico e futuro suficientes."""
    features = calcular_features(limpa)
    pico = pico_futuro(series_base(limpa)["indice_calor_c"])
    com_dado = limpa[["temperatura_c", "umidade_pct"]].notna().all(axis=1).astype(float)
    historico = com_dado.rolling(HORIZONTE_HORAS, min_periods=1).sum()
    hora_local = limpa.index.tz_convert(FUSO_LOCAL).hour
    selecao = (
        np.isin(hora_local, HORAS_DE_AMOSTRA)
        & limpa["observada"].astype(bool).to_numpy()
        & (historico >= MIN_HORAS_COM_DADO).to_numpy()
        & pico.notna().to_numpy()
    )
    amostras = features[selecao].copy()
    amostras["pico_futuro_c"] = pico[selecao]
    amostras["alvo"] = (amostras["pico_futuro_c"] >= LIMIAR_ALVO_C).astype(int)
    return amostras
