from pathlib import Path

import numpy as np
import pandas as pd
import pytest

from sombra_ml.dados import carregar_brutos, conferir_manifesto, ler_csv_inmet, limpar

BRUTOS = Path(__file__).resolve().parents[1] / "dados" / "brutos"
METADADOS = (
    "REGIAO:;SE\nUF:;SP\nESTACAO:;X\nCODIGO (WMO):;A701\nLATITUDE:;-23\nLONGITUDE:;-46\nALTITUDE:;792\nFUNDACAO:;2006\n"
)
COLUNAS = (
    "PRECIPITAÇÃO TOTAL, HORÁRIO (mm);PRESSAO ATMOSFERICA AO NIVEL DA ESTACAO, HORARIA (mB);"
    "RADIACAO GLOBAL (KJ/m²);TEMPERATURA DO AR - BULBO SECO, HORARIA (°C);"
    "TEMPERATURA DO PONTO DE ORVALHO (°C);UMIDADE RELATIVA DO AR, HORARIA (%);VENTO, VELOCIDADE HORARIA (m/s);"
)


def _csv(tmp_path, cabecalho, linhas):
    caminho = tmp_path / "A701_teste.csv"
    caminho.write_bytes((METADADOS + cabecalho + COLUNAS + "\n" + "\n".join(linhas) + "\n").encode("latin-1"))
    return caminho


def _horas(inicio, valores, **fixos):
    indice = pd.date_range(inicio, periods=len(valores), freq="h", tz="UTC")
    base = {"temperatura_c": valores, "umidade_pct": 50.0, "pressao_hpa": 925.0, "vento_ms": 2.0}
    base.update(fixos)
    return pd.DataFrame(base, index=indice).assign(radiacao_kj_m2=np.nan, chuva_mm=0.0).astype(float)


def test_le_formato_antigo_com_virgula_decimal_e_9999_vira_ausente(tmp_path):
    caminho = _csv(
        tmp_path,
        "DATA (YYYY-MM-DD);HORA (UTC);",
        ["2010-01-01;00:00;,6;923,8;-9999;22,2;18,5;83;2,8;", "2010-01-01;01:00;0;-9999;10;-9999;18,9;-9999;2;"],
    )
    tabela = ler_csv_inmet(caminho)
    assert tabela.index[0] == pd.Timestamp("2010-01-01 00:00", tz="UTC")
    assert tabela.loc[tabela.index[0], "temperatura_c"] == 22.2
    assert tabela.loc[tabela.index[0], "chuva_mm"] == 0.6
    assert np.isnan(tabela.iloc[0]["radiacao_kj_m2"])
    assert tabela.iloc[1][["temperatura_c", "umidade_pct", "pressao_hpa"]].isna().all()


def test_le_formato_novo_com_hora_utc_e_campo_vazio(tmp_path):
    caminho = _csv(tmp_path, "Data;Hora UTC;", ["2024/01/01;1300 UTC;0;926,9;;19,5;15;75;3,4;"])
    tabela = ler_csv_inmet(caminho)
    assert tabela.index[0] == pd.Timestamp("2024-01-01 13:00", tz="UTC")
    assert np.isnan(tabela.iloc[0]["radiacao_kj_m2"])
    assert tabela.iloc[0]["umidade_pct"] == 75


def test_buraco_curto_e_interpolado_e_marcado():
    tabela = _horas("2020-06-01 03:00", [10.0, np.nan, np.nan, np.nan, 14.0] + [15.0] * 19)
    limpa, _ = limpar(tabela)
    assert limpa["temperatura_c"].iloc[1:4].tolist() == pytest.approx([11.0, 12.0, 13.0])
    assert limpa["observada"].iloc[1:4].sum() == 0
    assert limpa["observada"].iloc[0] and limpa["observada"].iloc[4]


def test_buraco_longo_fica_inteiro_sem_preencher():
    tabela = _horas("2020-06-01 03:00", [10.0, *[np.nan] * 4, 15.0] + [15.0] * 18)
    limpa, relatorio = limpar(tabela)
    assert limpa["temperatura_c"].iloc[1:5].isna().all()
    assert relatorio.horas_interpoladas == 0


def test_interpolacao_nunca_inventa_nas_pontas():
    limpa, _ = limpar(_horas("2020-06-01 03:00", [np.nan, 10.0, 11.0, np.nan]))
    assert np.isnan(limpa["temperatura_c"].iloc[0]) and np.isnan(limpa["temperatura_c"].iloc[-1])


def test_chuva_e_radiacao_nao_sao_interpoladas():
    tabela = _horas("2020-06-01 03:00", [20.0] * 24, radiacao_kj_m2=np.nan)
    tabela["chuva_mm"] = [1.0, np.nan, 1.0] + [0.0] * 21
    limpa, _ = limpar(tabela)
    assert np.isnan(limpa["chuva_mm"].iloc[1])


def test_dia_local_com_mais_de_25_por_cento_faltando_e_descartado():
    # 03:00 UTC é meia-noite em São Paulo (UTC-3 em junho): as 24 horas abaixo são um dia local inteiro
    tabela = _horas("2020-06-01 03:00", [20.0] * 10 + [np.nan] * 7 + [20.0] * 7)
    limpa, relatorio = limpar(tabela)
    assert limpa["temperatura_c"].isna().all()
    assert not limpa["observada"].any()
    assert relatorio.dias_descartados == 1


def test_valor_fisicamente_impossivel_vira_ausente():
    tabela = _horas("2020-06-01 03:00", [20.0] * 24)
    tabela.iloc[5, tabela.columns.get_loc("umidade_pct")] = 140.0
    limpa, relatorio = limpar(tabela)
    assert relatorio.fora_da_faixa["umidade_pct"] == 1
    assert not limpa["observada"].iloc[5]


def test_hora_que_nao_veio_no_arquivo_entra_na_grade_como_ausente():
    tabela = _horas("2020-06-01 03:00", [20.0] * 24).drop(pd.Timestamp("2020-06-01 10:00", tz="UTC"))
    limpa, _ = limpar(tabela)
    assert len(limpa) == 24
    assert not limpa.loc[pd.Timestamp("2020-06-01 10:00", tz="UTC"), "observada"]


def test_dados_reais_batem_com_o_manifesto_e_com_a_contagem_documentada():
    conferir_manifesto(BRUTOS)
    tabela = carregar_brutos(BRUTOS)
    assert len(tabela) == 140_256
    assert tabela.index.is_unique and tabela.index.is_monotonic_increasing
    assert tabela["temperatura_c"].isna().sum() == 893
    assert tabela["umidade_pct"].isna().sum() == 742
    assert tabela["temperatura_c"].max() < 45


def test_manifesto_acusa_arquivo_alterado(tmp_path):
    (tmp_path / "manifesto.json").write_text('{"arquivos": {"2020": {"sha256Csv": "00"}}}', "utf-8")
    (tmp_path / "A701_2020.csv").write_text("x", "utf-8")
    with pytest.raises(ValueError, match="A701_2020"):
        conferir_manifesto(tmp_path)
