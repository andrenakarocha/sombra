"""Estilo das figuras: cores da direção de arte do Sombra (design/tokens.json), paleta conferida para daltonismo."""

from pathlib import Path

import matplotlib as mpl
import matplotlib.pyplot as plt

PASTA_FIGURAS = Path(__file__).resolve().parents[1] / "figuras"

FUNDO = "#FFFDF8"  # areia.0
TINTA = "#142224"  # tinta.900
TINTA_SUAVE = "#5E6E70"  # tinta.500
GRADE = "#E9DEC8"  # areia.200
XGBOOST = "#B04F27"  # telha.500
PERSISTENCIA = "#008A7E"  # sombra.500 com mais croma, para passar no validador de daltonismo
LOGISTICA = "#4A5FA5"  # azul só de gráfico: os tons quentes dos tokens se confundem com a telha no daltonismo
LIMIAR = "#9A4E0E"  # risco.laranjaEscuro, a cor do nível CAUTELA_EXTREMA


def aplicar_estilo() -> None:
    mpl.rcParams.update(
        {
            "figure.facecolor": FUNDO,
            "axes.facecolor": FUNDO,
            "savefig.facecolor": FUNDO,
            "figure.dpi": 110,
            "savefig.dpi": 160,
            "savefig.bbox": "tight",
            "font.size": 10.5,
            "text.color": TINTA,
            "axes.labelcolor": TINTA_SUAVE,
            "axes.edgecolor": GRADE,
            "axes.titlecolor": TINTA,
            "axes.titlesize": 12,
            "axes.titleweight": "bold",
            "axes.titlelocation": "left",
            "axes.spines.top": False,
            "axes.spines.right": False,
            "axes.grid": True,
            "axes.axisbelow": True,
            "grid.color": GRADE,
            "grid.linewidth": 0.7,
            "xtick.color": TINTA_SUAVE,
            "ytick.color": TINTA_SUAVE,
            "xtick.bottom": False,
            "ytick.left": False,
            "lines.linewidth": 2,
            "legend.frameon": False,
        }
    )


def salvar(figura: plt.Figure, nome: str) -> Path:
    PASTA_FIGURAS.mkdir(exist_ok=True)
    caminho = PASTA_FIGURAS / f"{nome}.png"
    figura.savefig(caminho)
    return caminho
