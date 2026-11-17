#!/usr/bin/env python3
"""Lê o relatório Cobertura do coverlet e reprova se a cobertura de linhas de um assembly ficar abaixo do mínimo.

Uso: scripts/cobertura.py <pasta-dos-resultados> <assembly> <minimo-em-%>
"""

import pathlib
import sys
import xml.etree.ElementTree as ET

pasta, assembly, minimo = pathlib.Path(sys.argv[1]), sys.argv[2], float(sys.argv[3])
relatorio = next(pasta.rglob("coverage.cobertura.xml"))
pacote = next(p for p in ET.parse(relatorio).getroot().iter("package") if p.get("name") == assembly)
linhas = float(pacote.get("line-rate")) * 100
ramos = float(pacote.get("branch-rate")) * 100
print(f"{assembly}: {linhas:.1f}% das linhas, {ramos:.1f}% dos ramos (mínimo {minimo:.0f}% de linhas)")
sys.exit(0 if linhas >= minimo else 1)
