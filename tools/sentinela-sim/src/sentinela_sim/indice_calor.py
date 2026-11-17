"""Índice de calor e nível de risco (contrato: docs/contratos/niveis-de-risco.md)."""

import math

LIMIARES_C = (
    ("PERIGO_EXTREMO", 51.67),
    ("PERIGO", 39.44),
    ("CAUTELA_EXTREMA", 32.22),
    ("ATENCAO", 26.67),
)


def indice_de_calor_c(temperatura_c: float, umidade_pct: float) -> float:
    t = temperatura_c * 9.0 / 5.0 + 32.0
    ur = umidade_pct
    simples = 0.5 * (t + 61.0 + (t - 68.0) * 1.2 + ur * 0.094)
    if (simples + t) / 2 < 80.0:
        hi = simples
    else:
        hi = (
            -42.379
            + 2.04901523 * t
            + 10.14333127 * ur
            - 0.22475541 * t * ur
            - 0.00683783 * t * t
            - 0.05481717 * ur * ur
            + 0.00122874 * t * t * ur
            + 0.00085282 * t * ur * ur
            - 0.00000199 * t * t * ur * ur
        )
        if ur < 13.0 and 80.0 <= t <= 112.0:
            hi -= ((13.0 - ur) / 4.0) * math.sqrt((17.0 - abs(t - 95.0)) / 17.0)
        elif ur > 85.0 and 80.0 <= t <= 87.0:
            hi += ((ur - 85.0) / 10.0) * ((87.0 - t) / 5.0)
    return (hi - 32.0) * 5.0 / 9.0


def nivel(indice_c: float) -> str:
    for nome, limiar in LIMIARES_C:
        if indice_c >= limiar:
            return nome
    return "NORMAL"
