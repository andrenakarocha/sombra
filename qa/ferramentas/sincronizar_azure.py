# /// script
# requires-python = ">=3.12"
# dependencies = ["pyyaml>=6"]
# ///
"""Sobe `qa/especificacao/` para o Azure Boards (F10.4): o Epic, as 12 Features e os Test Cases, com os passos e os
vínculos. Idempotente: o mapa YAML ↔ ID do Azure fica em `qa/azure/mapa.json` (versionado), e uma segunda execução só
atualiza o que mudou, sem duplicar nada (P12).

- Epic → Feature pelo vínculo pai/filho (System.LinkTypes.Hierarchy).
- Feature → Test Case por "Tested By" (Microsoft.VSTS.Common.TestedBy): o caso aponta para a Feature que testa.
- Os passos do caso vão para Microsoft.VSTS.TCM.Steps (ação → resultado esperado), no XML que o Azure lê.
- O PAT vem só do ambiente e nunca é impresso. Nenhum dado pessoal real sobe: as pessoas são as personas fictícias.

Uso (na raiz):
    AZURE_DEVOPS_ORG=https://dev.azure.com/<organizacao> AZURE_DEVOPS_PROJETO=Sombra AZURE_DEVOPS_PAT=… \\
        uv run qa/ferramentas/sincronizar_azure.py
    uv run qa/ferramentas/sincronizar_azure.py --simular     # sem rede: mostra o que seria criado ou atualizado
"""

from __future__ import annotations

import base64
import html
import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

import yaml

RAIZ = Path(__file__).resolve().parents[2]
ESPEC = RAIZ / "qa" / "especificacao"
MAPA = RAIZ / "qa" / "azure" / "mapa.json"
API = "api-version=7.1"
PAI = "System.LinkTypes.Hierarchy-Reverse"
TESTA = "Microsoft.VSTS.Common.TestedBy-Reverse"
REPO = "https://github.com/andrenakarocha/sombra/blob/main/"


# ── os campos de cada work item, a partir do YAML ────────────────────────────────────────────────────────────────────


def p(texto) -> str:
    return f"<p>{html.escape(str(texto))}</p>"


def lista(itens: list[str]) -> str:
    return "<ul>" + "".join(f"<li>{i}</li>" for i in itens) + "</ul>"


def campos_do_epico(epico: dict) -> dict:
    return {"System.Title": epico["titulo"], "System.Description": p(epico["descricao"]), "System.Tags": "; ".join(epico["tags"])}


def campos_da_feature(f: dict) -> dict:
    historia = p(f"Como {f['como']}, quero {f['quero']}, para {f['para']}.")
    regras = p("Regras cobertas: " + ", ".join(f.get("regras", []))) if f.get("regras") else ""
    criterios = lista([
        f"<b>{html.escape(c['id'])}</b> · <b>Dado</b> {html.escape(c['dado'])}, <b>quando</b> {html.escape(c['quando'])}, "
        f"<b>então</b> {html.escape(c['entao'])}."
        for c in f["criterios_de_aceite"]
    ])
    return {
        "System.Title": f"{f['id']} · {f['titulo']}",
        "System.Description": historia + regras,
        "Microsoft.VSTS.Common.AcceptanceCriteria": criterios,
        "Microsoft.VSTS.Common.Priority": f["prioridade"],
        "Microsoft.VSTS.Common.BusinessValue": f["valor_de_negocio"],
        "System.Tags": "; ".join([*f.get("tags", []), f"priority:{f['moscow']}"]),
    }


def passos_xml(passos: list[dict]) -> str:
    """O XML de Microsoft.VSTS.TCM.Steps: cada passo com a ação e o resultado esperado (o conteúdo vai escapado duas
    vezes, porque o Azure guarda HTML dentro do XML)."""
    def texto(t) -> str:  # o YAML lê "esperado: 503" como número
        return html.escape(f"<P>{html.escape(str(t))}</P>")

    corpo = "".join(
        f'<step id="{n}" type="ValidateStep"><parameterizedString isformatted="true">{texto(s["acao"])}</parameterizedString>'
        f'<parameterizedString isformatted="true">{texto(s["esperado"])}</parameterizedString><description/></step>'
        for n, s in enumerate(passos, start=2)
    )
    return f'<steps id="0" last="{len(passos) + 1}">{corpo}</steps>'


def campos_do_caso(c: dict) -> dict:
    entradas = ", ".join(f"{k} = {v}" for k, v in (c.get("entradas") or {}).items())
    automacao = [
        f'{html.escape(a["suite"])} · <a href="{REPO}{html.escape(a["arquivo"])}">{html.escape(a["arquivo"])}</a> · '
        f'<code>{html.escape(a["teste"])}</code>'
        for a in c.get("automatizado") or []
    ]
    descricao = (
        p(f"Objetivo: {c['objetivo']}")
        + p("Preparação:") + lista([html.escape(x) for x in c.get("preparacao", [])])
        + (p(f"Entradas: {entradas}") if entradas else "")
        + p(f"Resultado esperado: {c['resultado_esperado']}")
        + p(f"Tipo: {c['tipo']} · técnica: {c['tecnica']} · critério: {c['criterio']} · execução: {c['execucao']}")
        + (p("Testes automatizados que executam este caso:") + lista(automacao) if automacao else "")
    )
    return {
        "System.Title": f"{c['id']} · {c['titulo']}",
        "System.Description": descricao,
        "Microsoft.VSTS.TCM.Steps": passos_xml(c["passos"]),
        "System.Tags": "; ".join([c["feature"], c["tipo"], c["tecnica"], c["execucao"]]),
    }


# ── o cliente REST do Azure DevOps ───────────────────────────────────────────────────────────────────────────────────


def igual(campo: str, no_azure, nosso) -> bool:
    """O Azure devolve as tags na ordem dele: elas valem como conjunto."""
    if campo == "System.Tags":
        def conjunto(t) -> set[str]:
            return {x.strip().lower() for x in str(t or "").split(";") if x.strip()}
        return conjunto(no_azure) == conjunto(nosso)
    if isinstance(nosso, str):
        return html_normal(no_azure) == html_normal(nosso)
    return no_azure == nosso


def html_normal(valor) -> str:
    """O Azure reescreve o HTML que recebe (resolve `&#x27;`, codifica emoji, põe espaço antes de `</p>` e de `/>`, passa
    `<P>` para `<p>`): compara sem isso."""
    texto = html.unescape(html.unescape(str(valor or "")))  # os passos do Test Case vêm escapados duas vezes
    texto = re.sub(r"<(/?)([A-Za-z]+)", lambda m: "<" + m[1] + m[2].lower(), texto)
    texto = re.sub(r"\s+(?=</|/>)", "", texto)
    return re.sub(r"\s+", " ", texto).strip()


class Azure:
    def __init__(self, org: str, projeto: str, pat: str) -> None:
        self.base = f"{org.rstrip('/')}/{urllib.parse.quote(projeto)}/_apis/wit/workitems"
        self._auth = "Basic " + base64.b64encode(f":{pat}".encode()).decode()
        self.chamadas = 0

    def _pedir(self, metodo: str, url: str, corpo: list | None = None) -> dict:
        self.chamadas += 1
        dados = json.dumps(corpo).encode() if corpo is not None else None
        pedido = urllib.request.Request(url, data=dados, method=metodo, headers={
            "Authorization": self._auth,
            "Content-Type": "application/json-patch+json",
            "Accept": "application/json",
        })
        try:
            with urllib.request.urlopen(pedido, timeout=30) as r:
                return json.load(r)
        except urllib.error.HTTPError as erro:
            detalhe = erro.read().decode("utf-8", "replace")[:300]
            raise SystemExit(f"Azure respondeu {erro.code} em {metodo} {url.split('?')[0]}: {detalhe}") from None

    def url_do_item(self, item_id: int) -> str:
        return f"{self.base}/{item_id}"

    def ler(self, item_id: int) -> dict:
        return self._pedir("GET", f"{self.base}/{item_id}?$expand=relations&{API}")

    def criar(self, tipo: str, campos: dict, vinculos: list[tuple[str, int]]) -> int:
        corpo = [{"op": "add", "path": f"/fields/{k}", "value": v} for k, v in campos.items()]
        corpo += [{"op": "add", "path": "/relations/-", "value": {"rel": rel, "url": self.url_do_item(alvo)}} for rel, alvo in vinculos]
        return self._pedir("POST", f"{self.base}/${urllib.parse.quote(tipo)}?{API}", corpo)["id"]

    def atualizar(self, item_id: int, campos: dict, vinculos: list[tuple[str, int]]) -> bool:
        """Muda só o que difere do Azure e acrescenta só o vínculo que falta. Devolve se mudou algo."""
        atual = self.ler(item_id)
        corpo = [{"op": "add", "path": f"/fields/{k}", "value": v} for k, v in campos.items() if not igual(k, atual["fields"].get(k), v)]
        existentes = {(r["rel"], r["url"].rstrip("/").rsplit("/", 1)[-1]) for r in atual.get("relations") or []}
        corpo += [
            {"op": "add", "path": "/relations/-", "value": {"rel": rel, "url": self.url_do_item(alvo)}}
            for rel, alvo in vinculos if (rel, str(alvo)) not in existentes
        ]
        if corpo:
            self._pedir("PATCH", f"{self.base}/{item_id}?{API}", corpo)
        return bool(corpo)


# ── a sincronização ──────────────────────────────────────────────────────────────────────────────────────────────────


def sincronizar(azure: Azure | None, features_doc: dict, casos_doc: dict, mapa: dict) -> dict[str, int]:
    """Cria o que não está no mapa e atualiza o resto. Com `azure=None`, só conta (o --simular)."""
    contagem = {"criados": 0, "atualizados": 0, "iguais": 0}
    mapa.setdefault("features", {})
    mapa.setdefault("casos", {})

    def aplicar(tipo: str, chave: str, campos: dict, vinculos: list[tuple[str, int]], guardar) -> int:
        item_id = guardar.get(chave)
        if azure is None:
            contagem["atualizados" if item_id else "criados"] += 1
            return item_id or 0
        if item_id:
            contagem["atualizados" if azure.atualizar(item_id, campos, vinculos) else "iguais"] += 1
            return item_id
        item_id = azure.criar(tipo, campos, vinculos)
        guardar[chave] = item_id
        contagem["criados"] += 1
        return item_id

    raiz = {"epico": mapa.get("epico")}
    epico = aplicar("Epic", "epico", campos_do_epico(features_doc["epico"]), [], raiz)
    mapa["epico"] = raiz["epico"]
    for f in features_doc["features"]:
        aplicar("Feature", f["id"], campos_da_feature(f), [(PAI, epico)] if epico else [], mapa["features"])
    for c in casos_doc["casos"]:
        feature = mapa["features"].get(c["feature"])
        aplicar("Test Case", c["id"], campos_do_caso(c), [(TESTA, feature)] if feature else [], mapa["casos"])
    return contagem


def main() -> int:
    features_doc = yaml.safe_load((ESPEC / "features.yaml").read_text(encoding="utf-8"))
    casos_doc = yaml.safe_load((ESPEC / "casos-de-teste.yaml").read_text(encoding="utf-8"))
    mapa = json.loads(MAPA.read_text(encoding="utf-8")) if MAPA.exists() else {}

    if "--simular" in sys.argv:
        c = sincronizar(None, features_doc, casos_doc, mapa)
        print(f"simulação: {c['criados']} a criar e {c['atualizados']} a conferir "
              f"(1 Epic, {len(features_doc['features'])} Features, {len(casos_doc['casos'])} Test Cases)")
        return 0

    faltam = [v for v in ("AZURE_DEVOPS_ORG", "AZURE_DEVOPS_PROJETO", "AZURE_DEVOPS_PAT") if not os.environ.get(v)]
    if faltam:
        print("Defina " + ", ".join(faltam) + " (o PAT com escopo Work Items: Read & write).", file=sys.stderr)
        return 2
    org, projeto = os.environ["AZURE_DEVOPS_ORG"], os.environ["AZURE_DEVOPS_PROJETO"]
    azure = Azure(org, projeto, os.environ["AZURE_DEVOPS_PAT"])
    try:
        c = sincronizar(azure, features_doc, casos_doc, mapa)
    finally:
        # o mapa é gravado mesmo se parar no meio: a próxima execução continua de onde parou, sem duplicar
        mapa["organizacao"], mapa["projeto"] = org, projeto
        MAPA.parent.mkdir(parents=True, exist_ok=True)
        MAPA.write_text(json.dumps(mapa, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"Azure Boards: {c['criados']} criados, {c['atualizados']} atualizados, {c['iguais']} sem mudança "
          f"({azure.chamadas} chamadas) · mapa em {MAPA.relative_to(RAIZ)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
