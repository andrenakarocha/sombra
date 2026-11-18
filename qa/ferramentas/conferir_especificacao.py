# /// script
# requires-python = ">=3.12"
# dependencies = ["pyyaml>=6"]
# ///
"""Confere `qa/especificacao/` antes de subir para o Azure (F10.2 e F10.3).

Regras da spec 09 e do plano (§8.11):
- 12 Features (F01–F12), cada uma com Como/Quero/Para e critérios Dado/Quando/Então;
- 40 a 50 Test Cases, cada um com objetivo, preparação, entradas, passos e resultado esperado (D9);
- toda Feature com pelo menos 1 caso negativo, e todo critério de aceite coberto por algum caso;
- todo teste automatizado citado existe de verdade no repositório (a rastreabilidade não aponta para o vazio).

Uso: `uv run qa/ferramentas/conferir_especificacao.py` (sai com 1 e lista os problemas, se houver).
"""

from __future__ import annotations

import re
import sys
from collections import Counter
from pathlib import Path

import yaml

RAIZ = Path(__file__).resolve().parents[2]
ESPECIFICACAO = RAIZ / "qa" / "especificacao"

TIPOS = {"positivo", "negativo"}
TECNICAS = {
    "particao-de-equivalencia",
    "valor-limite",
    "tabela-de-decisao",
    "transicao-de-estados",
}
EXECUCOES = {"automatizado", "manual"}
SUITES = {"mstest", "jest", "pytest"}
FEATURES_ESPERADAS = [f"F{n:02d}" for n in range(1, 13)]
MINIMO_DE_CASOS, MAXIMO_DE_CASOS = 40, 50


def teste_existe(suite: str, arquivo: Path, teste: str) -> bool:
    texto = arquivo.read_text(encoding="utf-8")
    if suite == "mstest":
        classe, _, metodo = teste.partition(".")
        return (
            f"class {classe}" in texto
            and re.search(rf"\b{re.escape(metodo)}\(", texto) is not None
        )
    if suite == "pytest":
        return re.search(rf"def {re.escape(teste)}\(", texto) is not None
    return teste in texto  # jest: o título do it(...) aparece literalmente no arquivo


def conferir(features_doc: dict, casos_doc: dict) -> list[str]:
    problemas: list[str] = []
    features = features_doc.get("features") or []
    casos = casos_doc.get("casos") or []

    ids = [f.get("id") for f in features]
    if ids != FEATURES_ESPERADAS:
        problemas.append(
            f"Features devem ser {FEATURES_ESPERADAS[0]}–{FEATURES_ESPERADAS[-1]} em ordem; veio {ids}"
        )

    criterios: dict[str, str] = {}
    for f in features:
        for campo in (
            "titulo",
            "como",
            "quero",
            "para",
            "prioridade",
            "valor_de_negocio",
            "tags",
        ):
            if not f.get(campo):
                problemas.append(f"{f.get('id')}: falta {campo}")
        if f.get("prioridade") not in (1, 2, 3, 4):
            problemas.append(f"{f.get('id')}: prioridade fora de 1–4")
        if not 0 <= int(f.get("valor_de_negocio") or -1) <= 100:
            problemas.append(f"{f.get('id')}: valor_de_negocio fora de 0–100")
        if not f.get("criterios_de_aceite"):
            problemas.append(f"{f.get('id')}: sem critérios de aceite")
        for c in f.get("criterios_de_aceite") or []:
            if c.get("id") in criterios:
                problemas.append(f"critério repetido: {c.get('id')}")
            criterios[c.get("id")] = f.get("id")
            for campo in ("dado", "quando", "entao"):
                if not c.get(campo):
                    problemas.append(f"{c.get('id')}: falta {campo}")

    if not MINIMO_DE_CASOS <= len(casos) <= MAXIMO_DE_CASOS:
        problemas.append(
            f"são {len(casos)} casos; a meta é de {MINIMO_DE_CASOS} a {MAXIMO_DE_CASOS}"
        )
    repetidos = [i for i, n in Counter(c.get("id") for c in casos).items() if n > 1]
    if repetidos:
        problemas.append(f"casos repetidos: {repetidos}")

    negativos: Counter[str] = Counter()
    cobertos: set[str] = set()
    for c in casos:
        cid = c.get("id")
        for campo in (
            "titulo",
            "objetivo",
            "preparacao",
            "entradas",
            "passos",
            "resultado_esperado",
        ):
            if not c.get(campo):
                problemas.append(f"{cid}: falta {campo}")
        if c.get("feature") not in ids:
            problemas.append(f"{cid}: feature desconhecida {c.get('feature')}")
        if criterios.get(c.get("criterio")) != c.get("feature"):
            problemas.append(
                f"{cid}: o critério {c.get('criterio')} não é da {c.get('feature')}"
            )
        cobertos.add(c.get("criterio"))
        if c.get("tipo") not in TIPOS:
            problemas.append(f"{cid}: tipo inválido {c.get('tipo')}")
        if c.get("tecnica") not in TECNICAS:
            problemas.append(f"{cid}: técnica inválida {c.get('tecnica')}")
        if c.get("tipo") == "negativo":
            negativos[c.get("feature")] += 1
        for passo in c.get("passos") or []:
            if not passo.get("acao") or not passo.get("esperado"):
                problemas.append(f"{cid}: passo sem ação ou sem resultado esperado")

        execucao = c.get("execucao")
        referencias = c.get("automatizado") or []
        if execucao not in EXECUCOES:
            problemas.append(f"{cid}: execução inválida {execucao}")
        if execucao == "automatizado" and not referencias:
            problemas.append(f"{cid}: automatizado sem nenhum teste")
        for ref in referencias:
            arquivo = RAIZ / str(ref.get("arquivo"))
            if ref.get("suite") not in SUITES:
                problemas.append(f"{cid}: suíte inválida {ref.get('suite')}")
            elif not arquivo.is_file():
                problemas.append(f"{cid}: arquivo não existe: {ref.get('arquivo')}")
            elif not teste_existe(ref["suite"], arquivo, str(ref.get("teste"))):
                problemas.append(
                    f"{cid}: teste não encontrado em {ref['arquivo']}: {ref.get('teste')}"
                )

    for fid in ids:
        if negativos[fid] == 0:
            problemas.append(f"{fid}: nenhum caso negativo")
    for criterio in sorted(set(criterios) - cobertos):
        problemas.append(f"{criterio}: nenhum caso de teste cobre este critério")
    return problemas


def main() -> int:
    features_doc = yaml.safe_load(
        (ESPECIFICACAO / "features.yaml").read_text(encoding="utf-8")
    )
    casos_doc = yaml.safe_load(
        (ESPECIFICACAO / "casos-de-teste.yaml").read_text(encoding="utf-8")
    )
    problemas = conferir(features_doc, casos_doc)
    for p in problemas:
        print(f"✗ {p}", file=sys.stderr)
    if problemas:
        return 1

    casos = casos_doc["casos"]
    tipos = Counter(c["tipo"] for c in casos)
    tecnicas = Counter(c["tecnica"] for c in casos)
    automatizados = sum(c["execucao"] == "automatizado" for c in casos)
    print(
        f"✓ {len(features_doc['features'])} Features · {len(casos)} casos "
        f"({tipos['positivo']} positivos, {tipos['negativo']} negativos) · {automatizados} automatizados"
    )
    print("  técnicas: " + ", ".join(f"{t} {n}" for t, n in sorted(tecnicas.items())))
    return 0


if __name__ == "__main__":
    sys.exit(main())
