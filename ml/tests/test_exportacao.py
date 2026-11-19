import json
from pathlib import Path

import numpy as np
import pandas as pd
import pytest
import xgboost as xgb

from sombra_ml.exportacao import (
    ARQUIVO_CLASSIFICADOR,
    ARQUIVO_REGRESSOR,
    especificacao,
    exportar_onnx,
    prever_onnx,
    selecionar_paridade,
)
from sombra_ml.features import LIMIAR_ALVO_C, NOMES_DAS_FEATURES, features_da_janela
from sombra_ml.modelagem import margem_superior, novo_regressor_pico, novo_xgboost

MODELO = Path(__file__).resolve().parents[1] / "modelo"
N = len(NOMES_DAS_FEATURES)


def _dados():
    gerador = np.random.default_rng(1)
    X = gerador.normal(size=(300, N)).astype(np.float32)
    X[::5, 3] = np.nan
    return X, (np.nan_to_num(X[:, 0]) + np.nan_to_num(X[:, 3]) > 0.5).astype(int)


def test_onnx_do_classificador_reproduz_o_xgboost_inclusive_com_ausentes(tmp_path):
    X, y = _dados()
    modelo = novo_xgboost(n_estimators=20, max_depth=3).fit(X, y)
    caminho = exportar_onnx(modelo, tmp_path / "c.onnx")
    assert np.abs(prever_onnx(caminho, X) - modelo.predict_proba(X)[:, 1]).max() < 1e-5


def test_onnx_do_regressor_reproduz_o_xgboost_inclusive_com_ausentes(tmp_path):
    X, _ = _dados()
    alvo = 25 + 3 * np.nan_to_num(X[:, 0])
    modelo = novo_regressor_pico(n_estimators=20).fit(X, alvo)
    caminho = exportar_onnx(modelo, tmp_path / "r.onnx")
    assert np.abs(prever_onnx(caminho, X) - modelo.predict(X)).max() < 1e-4


def test_regressor_preve_a_mediana():
    assert isinstance(novo_regressor_pico(), xgb.XGBRegressor)
    assert novo_regressor_pico().get_params()["quantile_alpha"] == 0.5


def test_margem_e_o_quantil_90_do_erro_para_baixo():
    real = np.arange(100.0)
    previsto = real - 2.0
    previsto[:10] = real[:10] + 1.0
    assert margem_superior(real, previsto) == pytest.approx(2.0)


def test_paridade_tem_20_amostras_das_duas_classes_em_ordem():
    indice = pd.date_range("2022-01-01", periods=400, freq="6h", tz="UTC")
    amostras = pd.DataFrame({"alvo": (np.arange(400) % 7 == 0).astype(int)}, index=indice)
    escolhidas = selecionar_paridade(amostras)
    assert len(escolhidas) == 20 and amostras.loc[escolhidas, "alvo"].sum() == 10
    assert escolhidas.is_monotonic_increasing


def test_especificacao_descreve_cada_feature_na_ordem_do_modelo():
    spec = especificacao(limiar_decisao=0.01, margem_c=1.5)
    assert [f["nome"] for f in spec["features"]] == NOMES_DAS_FEATURES
    assert spec["alvo"]["limiarC"] == LIMIAR_ALVO_C
    assert spec["modelos"]["probabilidade"]["arquivo"] == ARQUIVO_CLASSIFICADOR
    assert spec["modelos"]["pico"]["arquivo"] == ARQUIVO_REGRESSOR
    assert spec["modelos"]["probabilidade"]["limiarDeDecisao"] == 0.01
    assert spec["modelos"]["pico"]["margemSuperiorP90C"] == 1.5


@pytest.mark.skipif(not (MODELO / "paridade.csv").exists(), reason="modelo ainda não exportado")
class TestArtefatosPublicados:
    def test_onnx_bate_com_as_probabilidades_python(self):
        paridade = pd.read_csv(MODELO / "paridade.csv")
        assert len(paridade) == 20
        X = paridade[NOMES_DAS_FEATURES].to_numpy(dtype=np.float32)
        probabilidade = prever_onnx(MODELO / ARQUIVO_CLASSIFICADOR, X)
        pico = prever_onnx(MODELO / ARQUIVO_REGRESSOR, X)
        assert np.abs(probabilidade - paridade["probabilidade_python"]).max() < 1e-5
        assert np.abs(pico - paridade["pico_python_c"]).max() < 1e-3

    def test_janelas_brutas_geram_as_mesmas_features(self):
        casos = json.loads((MODELO / "paridade-janelas.json").read_text("utf-8"))["casos"]
        assert len(casos) == 20
        for caso in casos:
            janela = pd.DataFrame(caso["observacoes"])
            janela.index = pd.to_datetime(janela.pop("instante"), utc=True)
            obtido = features_da_janela(janela.astype(float))
            esperado = pd.Series(caso["features"], dtype=float)[NOMES_DAS_FEATURES]
            pd.testing.assert_series_equal(obtido, esperado, check_names=False, rtol=1e-9, atol=1e-9)

    def test_features_json_acompanha_o_codigo(self):
        spec = json.loads((MODELO / "features.json").read_text("utf-8"))
        assert [f["nome"] for f in spec["features"]] == NOMES_DAS_FEATURES
