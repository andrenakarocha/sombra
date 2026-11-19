#!/usr/bin/env python3
"""Gera testes/vetores.h a partir dos vetores do contrato (a fonte da verdade continua sendo o JSON)."""
import json
import sys
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[3]
calor = json.loads((RAIZ / "docs/contratos/indice-de-calor.vetores.json").read_text(encoding="utf-8"))
mqtt = json.loads((RAIZ / "docs/contratos/mqtt-assinatura.vetores.json").read_text(encoding="utf-8"))


def c(texto: str) -> str:
    return json.dumps(texto, ensure_ascii=False)


linhas = ["// Gerado por testes/gerar_vetores.py a partir de docs/contratos/. Não edite.", "#pragma once", ""]
linhas.append(f"static const double TOLERANCIA_C = {calor['toleranciaC']};")
linhas.append("struct VetorDeCalor { double t, rh, hi; const char* nivel; const char* caso; };")
linhas.append("static const VetorDeCalor VETORES_DE_CALOR[] = {")
for v in calor["vetores"]:
    linhas.append(
        f"  {{{v['temperaturaC']}, {v['umidadePct']}, {v['indiceDeCalorC']}, {c(v['nivel'])}, {c(v['caso'])}}},"
    )
linhas.append("};")
linhas.append(f"static const char* SEGREDO_DE_TESTE = {c(mqtt['segredoHex'])};")
linhas.append(f"static const char* DEVICE_DE_TESTE = {c(mqtt['deviceId'])};")
linhas.append(
    "struct VetorDeAssinatura { char tipo; unsigned long ts, seq; double t, rh, hi; "
    "const char* campo; const char* mensagem; const char* canonica; const char* sig; };"
)
linhas.append("static const VetorDeAssinatura VETORES_DE_ASSINATURA[] = {")
for v in mqtt["vetores"]:
    m = v["mensagem"]
    tipo = {"telemetria": "T", "evento": "E", "comando": "C"}[v["tipo"]]
    campo = m.get("nivel") or m.get("tipo")
    linhas.append(
        f"  {{'{tipo}', {m['ts']}, {m['seq']}, {m.get('t', 0)}, {m.get('rh', 0)}, {m.get('hi', 0)}, {c(campo)}, "
        f"{c(m.get('mensagem', ''))}, {c(v['canonica'])}, {c(v['sig'])}}},"
    )
linhas.append("};")
Path(sys.argv[1]).write_text("\n".join(linhas) + "\n", encoding="utf-8")
