# /// script
# requires-python = ">=3.12"
# dependencies = ["pyyaml>=6"]
# ///
"""Gera o PDF da entrega de QA (F10.5): o link do projeto no Azure Boards, o professor como Admin, o resumo, a matriz
Feature × Test Case × teste automatizado e os prints de todas as telas de Features e de Test Cases.

Os números e a matriz saem de `qa/especificacao/` (a mesma fonte que sobe para o Azure); os prints, de
`qa/entrega/prints/` (o `make qa-prints` tira um por work item). Um print que falta aparece como aviso no PDF, para
nada sair incompleto sem ninguém ver.

Uso (na raiz):  make qa-pdf                         →  qa/entrega/qa.pdf
                PITCH_URL=https://youtu.be/… make qa-pdf   (o link do pitch, que a tarefa de QA pede)
"""

from __future__ import annotations

import collections
import html
import json
import os
import subprocess
from pathlib import Path

import yaml

RAIZ = Path(__file__).resolve().parents[2]
AQUI = RAIZ / "qa" / "entrega"
PRINTS = AQUI / "prints"
INTEGRANTES = [("André Nakamatsu Rocha", "RM555004"), ("João Marcelo Furtado Romero", "RM555199"), ("Matheus Rivera Montovaneli", "RM555499")]
TECNICAS = {
    "particao-de-equivalencia": "partição de equivalência",
    "valor-limite": "valor-limite",
    "tabela-de-decisao": "tabela de decisão",
    "transicao-de-estados": "transição de estados",
}


def e(texto) -> str:
    return html.escape(str(texto))


def imagem(nome: str, legenda: str) -> str:
    arquivo = PRINTS / f"{nome}.png"
    if not arquivo.exists():
        return f'<figure class="print falta"><div>Print ainda não tirado: <code>{e(nome)}.png</code> (make qa-prints)</div><figcaption>{e(legenda)}</figcaption></figure>'
    return f'<figure class="print"><div class="janela"><img src="prints/{e(nome)}.png" alt="{e(legenda)}"></div><figcaption>{e(legenda)}</figcaption></figure>'


def documento() -> str:
    features = yaml.safe_load((RAIZ / "qa/especificacao/features.yaml").read_text(encoding="utf-8"))
    casos = yaml.safe_load((RAIZ / "qa/especificacao/casos-de-teste.yaml").read_text(encoding="utf-8"))["casos"]
    mapa_arquivo = RAIZ / "qa/azure/mapa.json"
    mapa = json.loads(mapa_arquivo.read_text(encoding="utf-8")) if mapa_arquivo.exists() else {}
    projeto = f"{mapa['organizacao'].rstrip('/')}/{mapa['projeto']}" if mapa else ""
    link_azure = f'<a href="{e(projeto)}/_workitems">{e(projeto.removeprefix("https://"))}</a>' if mapa else "‹o projeto ainda não foi sincronizado: make qa-azure›"
    pitch = os.environ.get("PITCH_URL", "")
    criterios = sum(len(f["criterios_de_aceite"]) for f in features["features"])
    tipos = collections.Counter(c["tipo"] for c in casos)
    execucao = collections.Counter(c["execucao"] for c in casos)
    tecnicas = collections.Counter(c["tecnica"] for c in casos)
    por_feature = collections.defaultdict(list)
    for c in casos:
        por_feature[c["feature"]].append(c)

    matriz = "".join(
        f"<tr><td><b>{e(c['feature'])}</b></td><td>{e(c['criterio'])}</td><td><b>{e(c['id'])}</b> {e(c['titulo'])}</td>"
        f"<td class='{c['tipo']}'>{e(c['tipo'])}</td><td>{e(TECNICAS.get(c['tecnica'], c['tecnica']))}</td>"
        f"<td>{'<br>'.join(f'{e(a['suite'])} · <code>{e(a['teste'])}</code>' for a in c.get('automatizado') or []) or 'manual'}</td></tr>"
        for c in casos
    )
    features_html = "".join(
        f"""<section class="feature"><h3>{e(f['id'])} · {e(f['titulo'])}</h3>
        <p class="historia">Como <b>{e(f['como'])}</b>, quero <b>{e(f['quero'])}</b>, para <b>{e(f['para'])}</b>.</p>
        <p class="meta">prioridade {e(f['prioridade'])} · {e(f['moscow'])} · valor de negócio {e(f['valor_de_negocio'])} ·
        {len(f['criterios_de_aceite'])} critérios · {len(por_feature[f['id']])} Test Cases ({sum(c['tipo'] == 'negativo' for c in por_feature[f['id']])} negativos)</p>
        {imagem(f['id'], f"A Feature {f['id']} no Azure Boards, com a história e os critérios de aceite.")}
        {''.join(imagem(c['id'], f"{c['id']} · {c['titulo']} ({c['tipo']}, {TECNICAS.get(c['tecnica'], c['tecnica'])}): os passos e o vínculo Tested By.") for c in por_feature[f['id']])}
        </section>"""
        for f in features["features"]
    )
    integrantes = "".join(f"<li>{n} <span>{rm}</span></li>" for n, rm in INTEGRANTES)
    tokens = (RAIZ / "design/tokens.css").read_text(encoding="utf-8")

    return f"""<!doctype html>
<html lang="pt-BR"><head><meta charset="utf-8"><title>Sombra · QA</title>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Atkinson+Hyperlegible+Mono:wght@400;600&family=Atkinson+Hyperlegible+Next:ital,wght@0,400;0,600;0,800;1,400&family=Fraunces:opsz,wght,SOFT@9..144,400..800,0..100&display=swap">
<style>
{tokens}
@page {{ size: A4; margin: 15mm 14mm 16mm; }}
@page :first {{ margin: 0; }}
* {{ box-sizing: border-box; }}
html {{ -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
body {{ margin: 0; font-family: var(--fonte-texto); font-size: 10pt; line-height: 1.45; color: var(--cor-tinta-900); background: #fff; }}
h1, h2, h3 {{ font-family: var(--fonte-display); color: var(--cor-sombra-900); line-height: 1.15; }}
h2 {{ font-size: 20pt; margin: 0 0 4mm; break-before: page; }}
h3 {{ font-size: 13pt; margin: 0 0 2mm; }}
code {{ font-family: var(--fonte-mono); font-size: .88em; }}
a {{ color: var(--cor-sombra-700); }}
.capa {{ height: 297mm; padding: 26mm 22mm; background: var(--cor-sombra-900); color: var(--cor-areia-50); display: flex; flex-direction: column; break-after: page; }}
.capa .marca {{ font-family: var(--fonte-display); font-size: 54pt; font-weight: 700; margin: 0; color: var(--cor-areia-0); }}
.capa h1 {{ color: var(--cor-ocre-300); font-size: 25pt; margin: 30mm 0 6mm; max-width: 160mm; }}
.capa a {{ color: var(--cor-areia-0); }}
.capa .links p {{ font-size: 11pt; margin: 0 0 2mm; }}
.capa .rodape {{ margin-top: auto; display: grid; grid-template-columns: 1fr 1fr; gap: 8mm; }}
.capa .rodape h3 {{ color: var(--cor-ocre-300); font-family: var(--fonte-texto); font-size: 9pt; text-transform: uppercase; letter-spacing: .06em; }}
.capa ul {{ list-style: none; padding: 0; margin: 0; }} .capa li span {{ color: var(--cor-sombra-300); font-family: var(--fonte-mono); margin-left: 2mm; }}
.numeros {{ display: grid; grid-template-columns: repeat(4, 1fr); gap: 3mm; margin: 2mm 0 5mm; }}
.numeros div {{ border: 1px solid var(--cor-areia-200); border-radius: 6px; padding: 3mm; }}
.numeros b {{ display: block; font-family: var(--fonte-display); font-size: 20pt; color: var(--cor-sombra-900); }}
table {{ width: 100%; border-collapse: collapse; font-size: 7.6pt; }}
th {{ text-align: left; background: var(--cor-sombra-900); color: var(--cor-areia-0); padding: 1.4mm 1.6mm; }}
td {{ padding: 1.1mm 1.6mm; border-bottom: 1px solid var(--cor-areia-200); vertical-align: top; }}
tr {{ break-inside: avoid; }}
td.negativo {{ color: var(--cor-telha-700); font-weight: 600; }} td.positivo {{ color: var(--cor-risco-verde); font-weight: 600; }}
.feature {{ break-before: page; }}
.historia {{ margin: 0 0 1mm; }} .meta {{ font-size: 8.5pt; color: var(--cor-tinta-500); margin: 0 0 3mm; }}
figure.print {{ margin: 0 0 4mm; break-inside: avoid; }}
figure.print .janela {{ height: 112mm; overflow: hidden; border: 1px solid var(--cor-areia-200); border-radius: 6px; }}
figure.print img {{ display: block; width: 100%; }}
figure.print figcaption {{ font-size: 8pt; color: var(--cor-tinta-500); margin-top: 1mm; }}
figure.falta > div {{ height: 30mm; display: grid; place-items: center; border: 2px dashed var(--cor-risco-vermelho); border-radius: 6px; color: var(--cor-risco-vermelho); font-weight: 600; }}
</style></head><body>

<section class="capa">
  <p class="marca">Sombra</p>
  <h1>Features e Test Cases do Sombra no Azure Boards</h1>
  <div class="links">
    <p><b>Projeto no Azure DevOps:</b> {link_azure}</p>
    <p><b>Pitch (até 4 min):</b> {f'<a href="{e(pitch)}">{e(pitch)}</a>' if pitch else '‹link do pitch›'}</p>
    <p><b>Repositório:</b> <a href="https://github.com/andrenakarocha/sombra/tree/main/qa">github.com/andrenakarocha/sombra/tree/main/qa</a></p>
  </div>
  <div class="rodape">
    <div><h3>Integrantes</h3><ul>{integrantes}</ul></div>
    <div><h3>Entrega</h3><p>Testing, Compliance &amp; QA<br>Global Solution 2026.2 · FIAP<br>Engenharia de Software, 3º ano<br>19 de novembro de 2026</p></div>
  </div>
</section>

<h2>Resumo</h2>
<p>O Sombra avisa a rede de cuidado de uma pessoa idosa que mora sozinha quando o calor dentro da casa fica perigoso. As
Features e os Test Cases vivem no repositório como código (<code>qa/especificacao/</code>), revisados por PR, e sobem
para o Azure Boards por um script idempotente (<code>make qa-azure</code>): cada Feature é filha do Epic, e cada Test Case
fica ligado à sua Feature por <b>Tested By</b>, com os passos em ação → resultado esperado.</p>
<div class="numeros">
  <div><b>{len(features['features'])}</b>Features, com Como/Quero/Para</div>
  <div><b>{criterios}</b>critérios de aceite Dado/Quando/Então</div>
  <div><b>{len(casos)}</b>Test Cases caixa-preta: {tipos['positivo']} positivos e {tipos['negativo']} negativos</div>
  <div><b>{execucao['automatizado']}</b>casos executados por testes automatizados ({execucao['manual']} manual)</div>
</div>
<p>Técnicas: {', '.join(f"{TECNICAS.get(t, t)} ({n})" for t, n in tecnicas.most_common())}. Toda Feature tem ao menos um caso
negativo, e todo critério de aceite é coberto por algum caso (conferido por <code>qa/ferramentas/conferir_especificacao.py</code>
no CI, que também confere se cada teste automatizado citado existe no repositório).</p>
{imagem("administradores", "O professor como administrador do projeto (Project Administrators).")}

<h2>Rastreabilidade: Feature × Test Case × teste automatizado</h2>
<table><thead><tr><th>Feature</th><th>Critério</th><th>Test Case</th><th>Tipo</th><th>Técnica</th><th>Executado por</th></tr></thead><tbody>{matriz}</tbody></table>

<h2>As telas no Azure Boards</h2>
{imagem("lista-features", "As 12 Features no Azure Boards.")}
{imagem("lista-casos", "Os Test Cases no Azure Boards.")}
{imagem("epico", "O Epic do Sombra, com as Features filhas.")}
{features_html}
</body></html>
"""


def main() -> None:
    pagina = AQUI / "qa.html"
    pagina.write_text(documento(), encoding="utf-8")
    chrome = os.environ.get("CHROME", "google-chrome-stable")
    subprocess.run([chrome, "--headless=new", "--no-sandbox", "--disable-gpu", "--no-pdf-header-footer", "--virtual-time-budget=15000",
                    f"--print-to-pdf={AQUI / 'qa.pdf'}", pagina.as_uri()], check=True, capture_output=True)
    faltam = [p for p in ["administradores", "lista-features", "lista-casos", "epico"] if not (PRINTS / f"{p}.png").exists()]
    print(f"ok: qa/entrega/qa.pdf" + (f" (sem os prints ainda: make qa-prints; faltam {len(faltam)}+ telas)" if faltam else ""))


if __name__ == "__main__":
    main()
