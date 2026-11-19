"""Exporta os modelos para ONNX e publica o contrato que o Clima Service segue na F9.1."""

from pathlib import Path

import numpy as np
import onnxruntime as ort
import pandas as pd
from onnxmltools import convert_xgboost
from onnxmltools.convert.common.data_types import FloatTensorType

from sombra_ml.dados import FUSO_LOCAL
from sombra_ml.features import (
    CATALOGO,
    HORIZONTE_HORAS,
    LIMIAR_ALVO_C,
    MAGNUS_A,
    MAGNUS_B_C,
    MIN_HORAS_COM_DADO,
    NOMES_DAS_FEATURES,
)

ARQUIVO_CLASSIFICADOR = "modelo-calor-v1.onnx"
ARQUIVO_REGRESSOR = "modelo-pico-calor-v1.onnx"
NOME_DA_ENTRADA = "features"
OPSET = 15
AMOSTRAS_DE_PARIDADE_POR_CLASSE = 10

DESCRICAO_DAS_AGREGACOES = {
    "atual": "valor da observação mais recente (t)",
    "media": "média dos valores presentes em (t - janelaHoras, t]",
    "maximo": "máximo dos valores presentes em (t - janelaHoras, t]",
    "minimo": "mínimo dos valores presentes em (t - janelaHoras, t]",
    "amplitude": "máximo menos mínimo dos valores presentes em (t - janelaHoras, t]",
    "soma": "soma dos valores presentes em (t - janelaHoras, t]; sem nenhum valor, 0",
    "delta": "valor em t menos o valor exatamente janelaHoras antes; ausente se um dos dois faltar",
    "hora_sen": "sen(2π · hora local / 24)",
    "hora_cos": "cos(2π · hora local / 24)",
    "dia_do_ano_sen": "sen(2π · dia do ano local / 365,25)",
    "dia_do_ano_cos": "cos(2π · dia do ano local / 365,25)",
}


def exportar_onnx(modelo, caminho: Path) -> Path:
    """Converte um XGBoost treinado sem nomes de coluna (o ONNX identifica as features pela posição)."""
    entrada = [(NOME_DA_ENTRADA, FloatTensorType([None, modelo.n_features_in_]))]
    convertido = convert_xgboost(modelo, initial_types=entrada, target_opset=OPSET)
    caminho = Path(caminho)
    caminho.parent.mkdir(parents=True, exist_ok=True)
    caminho.write_bytes(convertido.SerializeToString())
    return caminho


def prever_onnx(caminho: Path, X: np.ndarray) -> np.ndarray:
    """Probabilidade da classe positiva (classificador) ou valor previsto (regressor), pelo ONNX Runtime."""
    sessao = ort.InferenceSession(str(caminho), providers=["CPUExecutionProvider"])
    saidas = sessao.run(None, {NOME_DA_ENTRADA: np.asarray(X, dtype=np.float32)})
    return saidas[1][:, 1] if len(saidas) == 2 else saidas[0].ravel()


def selecionar_paridade(amostras: pd.DataFrame) -> pd.DatetimeIndex:
    """Dez amostras de cada classe, espaçadas ao longo do período, sem sorteio."""
    escolhidas = []
    for classe in (0, 1):
        indice = amostras.index[amostras["alvo"] == classe]
        posicoes = np.linspace(0, len(indice) - 1, AMOSTRAS_DE_PARIDADE_POR_CLASSE).round().astype(int)
        escolhidas.append(indice[posicoes])
    return escolhidas[0].append(escolhidas[1]).sort_values()


def especificacao(limiar_decisao: float, margem_c: float, limiares_por_recall: dict[str, float] | None = None) -> dict:
    """O ``features.json``: tudo de que outra linguagem precisa para gerar a mesma entrada dos modelos."""
    return {
        "versao": "v1",
        "pergunta": "O índice de calor vai alcançar o limiar nas próximas 24 h?",
        "alvo": {"limiarC": LIMIAR_ALVO_C, "nivel": "CAUTELA_EXTREMA", "horizonteHoras": HORIZONTE_HORAS},
        "modelos": {
            "probabilidade": {
                "arquivo": ARQUIVO_CLASSIFICADOR,
                "saida": "probabilities[:, 1]",
                "limiarDeDecisao": limiar_decisao,
                "limiaresPorRecallNaValidacao": limiares_por_recall or {},
                "significado": "probabilidade de o pico das próximas 24 h alcançar 32,22 °C",
            },
            "pico": {
                "arquivo": ARQUIVO_REGRESSOR,
                "saida": "variable[:, 0]",
                "unidade": "°C",
                "significado": "mediana do pico do índice de calor nas próximas 24 h",
                "margemSuperiorP90C": margem_c,
                "margemSignificado": "somada à mediana, cobre 90% dos picos reais na validação temporal do treino",
            },
        },
        "entrada": {
            "nome": NOME_DA_ENTRADA,
            "tipo": "float32",
            "formato": ["n", len(NOMES_DAS_FEATURES)],
            "ausente": "NaN",
        },
        "janela": {
            "instanteDeReferencia": "t = observação mais recente da janela (não a posição na lista)",
            "historicoHoras": HORIZONTE_HORAS,
            "minimoDeHorasComDado": MIN_HORAS_COM_DADO,
            "fusoHorario": FUSO_LOCAL,
            "abaixoDoMinimo": "usar o previsor por regra",
        },
        "derivadas": {
            "indice_calor_c": "indice-de-calor conforme docs/contratos/niveis-de-risco.md, sem arredondar",
            "orvalho_c": {
                "formula": "gama = ln(UR/100) + a·T/(b+T); orvalho = b·gama/(a-gama); UR limitada a [1, 100]",
                "a": MAGNUS_A,
                "b": MAGNUS_B_C,
            },
        },
        "agregacoes": DESCRICAO_DAS_AGREGACOES,
        "features": [
            {
                "indice": i,
                "nome": f.nome,
                "variavel": f.variavel,
                "agregacao": f.agregacao,
                "janelaHoras": f.janela_horas,
            }
            for i, f in enumerate(CATALOGO)
        ],
    }
