"""Carga e limpeza dos CSVs horários do INMET (estação A701). Regras em docs/specs/03-ml-previsao-calor.md, passo 2."""

import hashlib
import json
import unicodedata
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np
import pandas as pd

FUSO_LOCAL = "America/Sao_Paulo"
LINHAS_DE_METADADOS = 8
MAX_HORAS_INTERPOLADAS = 3
MAX_FRACAO_FALTANTE_NO_DIA = 0.25

# Trecho do nome da coluna (sem acento, em maiúsculas) → nome interno. O INMET muda o nome completo entre os anos.
COLUNAS_INMET = {
    "TEMPERATURA DO AR - BULBO SECO": "temperatura_c",
    "UMIDADE RELATIVA DO AR, HORARIA": "umidade_pct",
    "TEMPERATURA DO PONTO DE ORVALHO": "orvalho_inmet_c",
    "PRESSAO ATMOSFERICA AO NIVEL DA ESTACAO": "pressao_hpa",
    "RADIACAO GLOBAL": "radiacao_kj_m2",
    "VENTO, VELOCIDADE HORARIA": "vento_ms",
    "PRECIPITACAO TOTAL": "chuva_mm",
}

FAIXAS_FISICAS = {
    "temperatura_c": (-5.0, 45.0),
    "umidade_pct": (1.0, 100.0),
    "orvalho_inmet_c": (-30.0, 40.0),
    "pressao_hpa": (850.0, 1000.0),
    "radiacao_kj_m2": (0.0, 5000.0),
    "vento_ms": (0.0, 60.0),
    "chuva_mm": (0.0, 200.0),
}

# Grandezas contínuas. Chuva e radiação são acumulados da hora: um valor inventado viraria chuva ou sol que não existiu.
INTERPOLAVEIS = ("temperatura_c", "umidade_pct", "orvalho_inmet_c", "pressao_hpa", "vento_ms")
ESSENCIAIS = ("temperatura_c", "umidade_pct")


@dataclass
class RelatorioDeLimpeza:
    """O que a limpeza fez, para o notebook documentar com números."""

    horas: int = 0
    ausentes_na_origem: dict[str, int] = field(default_factory=dict)
    fora_da_faixa: dict[str, int] = field(default_factory=dict)
    horas_interpoladas: int = 0
    dias_descartados: int = 0
    horas_descartadas: int = 0


def _sem_acento(texto: str) -> str:
    decomposto = unicodedata.normalize("NFKD", texto)
    return "".join(c for c in decomposto if not unicodedata.combining(c)).upper()


def ler_csv_inmet(caminho: Path) -> pd.DataFrame:
    """Lê um CSV anual: índice em UTC, colunas internas, ``-9999`` e vazio como ausente."""
    bruto = pd.read_csv(
        caminho,
        sep=";",
        skiprows=LINHAS_DE_METADADOS,
        encoding="latin-1",
        dtype=str,
        index_col=False,
        keep_default_na=False,
    )
    data, hora = bruto.columns[:2]
    momento = (
        bruto[data].str.replace("/", "-", regex=False)
        + " "
        + bruto[hora].str.replace(" UTC", "", regex=False).str.replace(":", "", regex=False).str[:4]
    )
    tabela = pd.DataFrame(index=pd.DatetimeIndex(pd.to_datetime(momento, format="%Y-%m-%d %H%M", utc=True), name="utc"))
    normalizadas = {_sem_acento(coluna): coluna for coluna in bruto.columns}
    for trecho, nome in COLUNAS_INMET.items():
        origem = next(original for chave, original in normalizadas.items() if trecho in chave)
        valores = pd.to_numeric(bruto[origem].str.replace(",", ".", regex=False), errors="coerce").to_numpy(float)
        tabela[nome] = np.where(valores == -9999, np.nan, valores)
    return tabela


def conferir_manifesto(pasta: Path) -> None:
    """Falha se algum CSV não for exatamente o que foi baixado do INMET (SHA-256 do manifesto da F0.15)."""
    manifesto = json.loads((pasta / "manifesto.json").read_text("utf-8"))
    for ano, dados in manifesto["arquivos"].items():
        caminho = pasta / f"A701_{ano}.csv"
        if hashlib.sha256(caminho.read_bytes()).hexdigest() != dados["sha256Csv"]:
            raise ValueError(f"{caminho.name} não confere com o manifesto")


def carregar_brutos(pasta: Path) -> pd.DataFrame:
    """Junta os anos em ordem. Hora repetida entre arquivos é erro: nunca se escolhe uma em silêncio."""
    tabela = pd.concat([ler_csv_inmet(caminho) for caminho in sorted(pasta.glob("A701_*.csv"))]).sort_index()
    if not tabela.index.is_unique:
        raise ValueError("há horas repetidas nos CSVs")
    return tabela


def _interpolar_buracos_curtos(serie: pd.Series) -> pd.Series:
    """Preenche só buracos internos de até 3 h. Um buraco maior fica inteiro, sem tocar nas primeiras horas dele."""
    ausente = serie.isna()
    grupos = (ausente != ausente.shift()).cumsum()
    tamanho = ausente.groupby(grupos).transform("sum")
    interpolada = serie.interpolate(method="time", limit_area="inside")
    return serie.where(~(ausente & (tamanho <= MAX_HORAS_INTERPOLADAS)), interpolada)


def limpar(bruto: pd.DataFrame) -> tuple[pd.DataFrame, RelatorioDeLimpeza]:
    """Grade horária completa, faixa física, interpolação curta e descarte dos dias ruins.

    A coluna ``observada`` diz se a temperatura e a umidade daquela hora foram medidas de verdade. Só uma hora observada
    vira amostra: assim, nenhuma interpolação usada por uma amostra enxerga valor posterior a ela.
    """
    grade = pd.date_range(bruto.index.min(), bruto.index.max(), freq="h", name="utc")
    tabela = bruto.reindex(grade).copy()
    for coluna in FAIXAS_FISICAS:
        if coluna not in tabela:
            tabela[coluna] = np.nan
    relatorio = RelatorioDeLimpeza(horas=len(tabela))
    relatorio.ausentes_na_origem = {c: int(tabela[c].isna().sum()) for c in FAIXAS_FISICAS}

    for coluna, (minimo, maximo) in FAIXAS_FISICAS.items():
        fora = tabela[coluna].notna() & ~tabela[coluna].between(minimo, maximo)
        relatorio.fora_da_faixa[coluna] = int(fora.sum())
        tabela.loc[fora, coluna] = np.nan

    observada = tabela[list(ESSENCIAIS)].notna().all(axis=1)
    antes = int(tabela["temperatura_c"].isna().sum())
    for coluna in INTERPOLAVEIS:
        tabela[coluna] = _interpolar_buracos_curtos(tabela[coluna])
    relatorio.horas_interpoladas = antes - int(tabela["temperatura_c"].isna().sum())

    dia_local = tabela.index.tz_convert(FUSO_LOCAL).date
    fracao = tabela[list(ESSENCIAIS)].isna().any(axis=1).groupby(dia_local).transform("mean")
    descartar = fracao.to_numpy() > MAX_FRACAO_FALTANTE_NO_DIA
    tabela.loc[descartar, list(FAIXAS_FISICAS)] = np.nan
    relatorio.dias_descartados = len(set(dia_local[descartar]))
    relatorio.horas_descartadas = int(descartar.sum())
    tabela["observada"] = observada & ~descartar
    return tabela, relatorio
