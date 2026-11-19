"""Índice de calor e níveis de risco, conforme docs/contratos/niveis-de-risco.md (vetorizado para séries horárias)."""

import numpy as np

LIMIARES_C = {"ATENCAO": 26.67, "CAUTELA_EXTREMA": 32.22, "PERIGO": 39.44, "PERIGO_EXTREMO": 51.67}


def indice_de_calor_c(temperatura_c, umidade_pct):
    """Equação da NWS/WPC na leitura do contrato. Aceita escalares ou arrays; ausente (NaN) continua ausente."""
    t = np.asarray(temperatura_c, dtype=float) * 9.0 / 5.0 + 32.0
    ur = np.asarray(umidade_pct, dtype=float)
    simples = 0.5 * (t + 61.0 + (t - 68.0) * 1.2 + ur * 0.094)
    rothfusz = (
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
    umidade_baixa = (ur < 13.0) & (t >= 80.0) & (t <= 112.0)
    umidade_alta = (ur > 85.0) & (t >= 80.0) & (t <= 87.0)
    raiz = np.sqrt(np.clip(17.0 - np.abs(t - 95.0), 0.0, None) / 17.0)
    rothfusz = rothfusz - np.where(umidade_baixa, (13.0 - ur) / 4.0 * raiz, 0.0)
    rothfusz = rothfusz + np.where(umidade_alta, (ur - 85.0) / 10.0 * (87.0 - t) / 5.0, 0.0)
    resultado_f = np.where((simples + t) / 2.0 >= 80.0, rothfusz, simples)
    return (resultado_f - 32.0) * 5.0 / 9.0


def classificar(indice_c: float) -> str:
    """Nível de risco: intervalo fechado embaixo e aberto em cima."""
    for nome in ("PERIGO_EXTREMO", "PERIGO", "CAUTELA_EXTREMA", "ATENCAO"):
        if indice_c >= LIMIARES_C[nome]:
            return nome
    return "NORMAL"
