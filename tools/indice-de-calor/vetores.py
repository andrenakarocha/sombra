#!/usr/bin/env python3
"""Implementação de referência do índice de calor e dos vetores de teste compartilhados.

Fonte: NWS/WPC, "The Heat Index Equation" (Rothfusz, 1990, SR 90-23),
https://www.wpc.ncep.noaa.gov/html/heatindex_equation.shtml

Leitura adotada pelo Sombra (vale para C#, Java, C++ e Python):
1. calcula a fórmula simples (Steadman) em °F;
2. se a média entre ela e a temperatura for >= 80 °F, usa a regressão de Rothfusz com os ajustes;
3. senão, o índice é a própria fórmula simples.

Uso:
    tools/indice-de-calor/vetores.py            confere docs/contratos/indice-de-calor.vetores.json (sai com 0 se ok)
    tools/indice-de-calor/vetores.py --gerar    reescreve o arquivo a partir dos casos abaixo
"""

import json
import math
import pathlib
import sys

ARQUIVO = pathlib.Path(__file__).resolve().parents[2] / "docs" / "contratos" / "indice-de-calor.vetores.json"
FONTE = "NWS/WPC, The Heat Index Equation (Rothfusz 1990, SR 90-23): https://www.wpc.ncep.noaa.gov/html/heatindex_equation.shtml"
TOLERANCIA_C = 0.05

# Limiares da tabela de índice de calor da NWS (80, 90, 103 e 125 °F) convertidos para °C com duas casas.
LIMIARES_C = (
    ("PERIGO_EXTREMO", 51.67),
    ("PERIGO", 39.44),
    ("CAUTELA_EXTREMA", 32.22),
    ("ATENCAO", 26.67),
)

CASOS = [
    (10.0, 90.0, "frio e úmido: fórmula simples"),
    (20.0, 60.0, "ameno: fórmula simples"),
    (25.0, 50.0, "abaixo do limiar de 80 °F: fórmula simples"),
    (26.0, 80.0, "fórmula simples já em ATENCAO"),
    (27.0, 40.0, "logo acima do limiar: regressão"),
    (28.0, 85.0, "umidade 85%: sem ajuste de umidade alta"),
    (28.0, 90.0, "umidade alta (> 85%, 80–87 °F): ajuste somado"),
    (29.0, 100.0, "saturado (> 85%, 80–87 °F): ajuste somado"),
    (30.0, 50.0, "tarde quente em SP"),
    (30.0, 70.0, "tarde quente e úmida"),
    (31.0, 95.0, "acima de 87 °F: sem ajuste de umidade alta"),
    (32.0, 40.0, "dia seco de onda de calor"),
    (32.0, 60.0, "onda de calor úmida"),
    (34.0, 30.0, "muito quente e seco"),
    (35.0, 50.0, "pico de onda de calor"),
    (36.0, 60.0, "casa fechada no pico da tarde"),
    (38.0, 10.0, "umidade baixa (< 13%, 80–112 °F): ajuste subtraído"),
    (40.0, 30.0, "calor extremo seco"),
    (40.0, 60.0, "calor extremo úmido"),
    (43.0, 50.0, "fora da faixa típica de SP"),
]


def indice_de_calor_f(t_f: float, ur: float) -> float:
    simples = 0.5 * (t_f + 61.0 + (t_f - 68.0) * 1.2 + ur * 0.094)
    if (simples + t_f) / 2 < 80.0:
        return simples
    hi = (-42.379 + 2.04901523 * t_f + 10.14333127 * ur - 0.22475541 * t_f * ur
          - 0.00683783 * t_f * t_f - 0.05481717 * ur * ur + 0.00122874 * t_f * t_f * ur
          + 0.00085282 * t_f * ur * ur - 0.00000199 * t_f * t_f * ur * ur)
    if ur < 13.0 and 80.0 <= t_f <= 112.0:
        hi -= ((13.0 - ur) / 4.0) * math.sqrt((17.0 - abs(t_f - 95.0)) / 17.0)
    elif ur > 85.0 and 80.0 <= t_f <= 87.0:
        hi += ((ur - 85.0) / 10.0) * ((87.0 - t_f) / 5.0)
    return hi


def indice_de_calor_c(t_c: float, ur: float) -> float:
    return (indice_de_calor_f(t_c * 9.0 / 5.0 + 32.0, ur) - 32.0) * 5.0 / 9.0


def nivel(hi_c: float) -> str:
    for nome, limiar in LIMIARES_C:
        if hi_c >= limiar:
            return nome
    return "NORMAL"


def gerar() -> None:
    vetores = []
    for t_c, ur, caso in CASOS:
        hi = round(indice_de_calor_c(t_c, ur), 2)
        vetores.append({"temperaturaC": t_c, "umidadePct": ur, "indiceDeCalorC": hi, "nivel": nivel(hi), "caso": caso})
    dados = {
        "fonte": FONTE,
        "toleranciaC": TOLERANCIA_C,
        "limiaresC": {nome: valor for nome, valor in reversed(LIMIARES_C)},
        "vetores": vetores,
    }
    ARQUIVO.parent.mkdir(parents=True, exist_ok=True)
    ARQUIVO.write_text(json.dumps(dados, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{len(vetores)} vetores gravados em {ARQUIVO}")


def conferir() -> int:
    dados = json.loads(ARQUIVO.read_text(encoding="utf-8"))
    falhas = []
    if dados["limiaresC"] != {nome: valor for nome, valor in reversed(LIMIARES_C)}:
        falhas.append("limiares divergentes da referência")
    niveis_cobertos = set()
    for v in dados["vetores"]:
        esperado = indice_de_calor_c(v["temperaturaC"], v["umidadePct"])
        if abs(esperado - v["indiceDeCalorC"]) > dados["toleranciaC"]:
            falhas.append(f"{v['caso']}: {v['indiceDeCalorC']} != {esperado:.2f}")
        if nivel(v["indiceDeCalorC"]) != v["nivel"]:
            falhas.append(f"{v['caso']}: nível {v['nivel']} != {nivel(v['indiceDeCalorC'])}")
        niveis_cobertos.add(v["nivel"])
    faltando = {"NORMAL", *(n for n, _ in LIMIARES_C)} - niveis_cobertos
    if faltando:
        falhas.append(f"níveis sem vetor: {sorted(faltando)}")
    for f in falhas:
        print("FALHA:", f)
    print(f"{len(dados['vetores'])} vetores conferidos, {len(falhas)} falha(s)")
    return 1 if falhas else 0


if __name__ == "__main__":
    if "--gerar" in sys.argv[1:]:
        gerar()
    sys.exit(conferir())
