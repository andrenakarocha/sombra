import numpy as np
import pandas as pd
import pytest

from sombra_ml.dados import limpar
from sombra_ml.features import (
    CATALOGO,
    LIMIAR_ALVO_C,
    NOMES_DAS_FEATURES,
    calcular_features,
    features_da_janela,
    montar_amostras,
    orvalho_c,
    pico_futuro,
    series_base,
)
from sombra_ml.indice_calor import LIMIARES_C, indice_de_calor_c


def _bruto(horas=24 * 6, inicio="2020-06-01 03:00"):
    """Série sintética determinística, só para exercitar as regras (o modelo usa apenas dados reais)."""
    indice = pd.date_range(inicio, periods=horas, freq="h", tz="UTC", name="utc")
    fase = np.arange(horas) / 24 * 2 * np.pi
    return pd.DataFrame(
        {
            "temperatura_c": 24 + 6 * np.sin(fase),
            "umidade_pct": 60 - 15 * np.sin(fase),
            "orvalho_inmet_c": 14.0,
            "pressao_hpa": 925 + np.cos(fase),
            "radiacao_kj_m2": np.where(np.sin(fase) > 0, 1500 * np.sin(fase), np.nan),
            "vento_ms": 2 + np.cos(fase),
            "chuva_mm": 0.1,
        },
        index=indice,
    )


def _limpa(bruto=None):
    return limpar(_bruto() if bruto is None else bruto)[0]


def test_limiar_do_alvo_e_o_de_cautela_extrema_do_contrato():
    assert LIMIARES_C["CAUTELA_EXTREMA"] == LIMIAR_ALVO_C


def test_catalogo_so_usa_o_que_o_clima_service_tem_em_producao():
    permitidas = {"temperatura_c", "umidade_pct", "orvalho_c", "indice_calor_c", "pressao_hpa", "radiacao_kj_m2"}
    permitidas |= {"vento_ms", "chuva_mm", "tempo"}
    assert {f.variavel for f in CATALOGO} <= permitidas
    assert len(NOMES_DAS_FEATURES) == len(set(NOMES_DAS_FEATURES))


def test_nenhuma_feature_precisa_de_mais_que_as_ultimas_24_horas():
    assert max(f.janela_horas or 0 for f in CATALOGO if f.agregacao != "delta") == 24
    assert max(f.janela_horas for f in CATALOGO if f.agregacao == "delta") <= 23


def test_orvalho_derivado_bate_com_valor_conhecido():
    assert float(orvalho_c(30.0, 50.0)) == pytest.approx(18.4, abs=0.1)


def test_pico_futuro_olha_exatamente_de_t_mais_1_a_t_mais_24():
    indice = pd.date_range("2020-01-01", periods=60, freq="h", tz="UTC")
    serie = pd.Series(20.0, index=indice)
    serie.iloc[10] = 99.0  # o próprio t não conta
    assert pico_futuro(serie).iloc[10] == 20.0
    serie.iloc[10] = 20.0
    serie.iloc[34] = 50.0  # t + 24 conta
    assert pico_futuro(serie).iloc[10] == 50.0
    serie.iloc[34] = 20.0
    serie.iloc[35] = 50.0  # t + 25 não conta
    assert pico_futuro(serie).iloc[10] == 20.0


def test_pico_futuro_exige_18_das_24_horas():
    indice = pd.date_range("2020-01-01", periods=60, freq="h", tz="UTC")
    serie = pd.Series(20.0, index=indice)
    serie.iloc[11:18] = np.nan  # 7 ausentes, sobram 17
    assert np.isnan(pico_futuro(serie).iloc[10])
    serie.iloc[11] = 20.0  # sobram 18
    assert pico_futuro(serie).iloc[10] == 20.0


def test_mudar_o_futuro_nao_muda_as_features_de_t():
    bruto = _bruto()
    bruto.iloc[70:72, bruto.columns.get_loc("temperatura_c")] = np.nan  # buraco que termina logo antes de t
    t = bruto.index[72]
    original = calcular_features(_limpa(bruto)).loc[t]
    alterado = bruto.copy()
    colunas = ["temperatura_c", "umidade_pct", "pressao_hpa", "vento_ms", "chuva_mm", "radiacao_kj_m2"]
    alterado.loc[alterado.index > t, colunas] += 7.0
    pd.testing.assert_series_equal(original, calcular_features(_limpa(alterado)).loc[t])


def test_mudar_o_passado_nao_muda_o_alvo_de_t():
    bruto = _bruto()
    t = bruto.index[72]
    original = montar_amostras(_limpa(bruto)).loc[t, ["pico_futuro_c", "alvo"]]
    alterado = bruto.copy()
    alterado.loc[alterado.index <= t, "temperatura_c"] -= 3.0
    pd.testing.assert_series_equal(original, montar_amostras(_limpa(alterado)).loc[t, ["pico_futuro_c", "alvo"]])


def test_alvo_usa_o_limiar_com_intervalo_fechado():
    bruto = _bruto()
    amostras = montar_amostras(_limpa(bruto))
    esperado = amostras["pico_futuro_c"] >= LIMIAR_ALVO_C
    assert (amostras["alvo"] == esperado.astype(int)).all()


def test_amostras_a_cada_6_horas_no_horario_local():
    locais = montar_amostras(_limpa()).index.tz_convert("America/Sao_Paulo")
    assert set(locais.hour) == {0, 6, 12, 18}


def test_hora_interpolada_nao_vira_amostra():
    bruto = _bruto()
    t = bruto.index[72]  # meia-noite local
    bruto.loc[t, "temperatura_c"] = np.nan
    limpa = _limpa(bruto)
    assert not np.isnan(limpa.loc[t, "temperatura_c"])
    assert t not in montar_amostras(limpa).index


def test_amostra_exige_18_horas_de_historico():
    amostras = montar_amostras(_limpa())
    assert amostras.index.min() >= _bruto().index[17]


def test_radiacao_noturna_ausente_soma_zero():
    limpa = _limpa()
    noite = limpa.index[15]  # só horas sem sol na janela de 3 h
    assert calcular_features(limpa).loc[noite, "radiacao_soma_3h"] == 0


def test_indice_de_calor_da_serie_base_e_o_do_contrato():
    limpa = _limpa()
    base = series_base(limpa)
    assert base["indice_calor_c"].iloc[5] == pytest.approx(
        float(indice_de_calor_c(limpa["temperatura_c"].iloc[5], limpa["umidade_pct"].iloc[5]))
    )


def test_janela_de_producao_da_o_mesmo_resultado_que_o_calculo_em_lote():
    limpa = _limpa()
    lote = calcular_features(limpa)
    t = limpa.index[60]
    janela = limpa.loc[t - pd.Timedelta(hours=23) : t]
    pd.testing.assert_series_equal(
        features_da_janela(janela)[NOMES_DAS_FEATURES], lote.loc[t, NOMES_DAS_FEATURES], check_names=False
    )


def test_janela_de_producao_respeita_o_tempo_quando_falta_uma_hora():
    limpa = _limpa()
    lote = calcular_features(limpa.assign(**{c: limpa[c].where(limpa.index != limpa.index[58]) for c in limpa.columns}))
    t = limpa.index[60]
    janela = limpa.loc[t - pd.Timedelta(hours=23) : t].drop(limpa.index[58])
    resultado = features_da_janela(janela)
    assert resultado["temperatura_delta_3h"] == pytest.approx(lote.loc[t, "temperatura_delta_3h"])
    assert resultado["temperatura_media_3h"] == pytest.approx(lote.loc[t, "temperatura_media_3h"])
