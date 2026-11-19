#!/usr/bin/env python3
"""Gera a entrega de Cybersecurity (spec 07): o PDF com a documentação e, separado, o ZIP com o código.

Nada no PDF é escrito à mão: os trechos de código saem dos arquivos-fonte (com o caminho e as linhas), as tabelas saem
dos relatórios gerados pelos testes (`security/privacidade`, `security/auditoria`), o Semgrep roda de novo, e as
traduções da IA são eventos reais do Core (guardadas em `traducoes.json` para o PDF poder ser refeito sem o Core no ar).

Uso (na raiz):  make entrega-cyber        →  security/entrega/cyber.pdf  e  security/entrega/sombra-codigo.zip
"""

import html
import json
import os
import pathlib
import re
import subprocess
import sys
import urllib.request

RAIZ = pathlib.Path(__file__).resolve().parents[2]
AQUI = RAIZ / "security" / "entrega"
PRIV = RAIZ / "services" / "core-api" / "sources" / "Sombra.Privacidade"
REPO = "https://github.com/andrenakarocha/sombra"
INTEGRANTES = [
    ("André Nakamatsu Rocha", "RM555004"),
    ("João Marcelo Furtado Romero", "RM555199"),
    ("Matheus Rivera Montovaneli", "RM555499"),
]

# ── trechos de código reais ──────────────────────────────────────────────────────────────────────────────────────────


def trecho(arquivo: pathlib.Path, inicio: str, ate: str | None = None) -> tuple[str, int, int]:
    """Do primeiro casamento de `inicio` até o fim do bloco (chaves balanceadas) ou até a linha que casa `ate`.
    Tira os comentários de documentação (///) para caber na página; o arquivo continua sendo a fonte."""
    linhas = arquivo.read_text(encoding="utf-8").splitlines()
    i = next(n for n, l in enumerate(linhas) if re.search(inicio, l))
    fim, nivel, abriu = i, 0, False
    for n in range(i, len(linhas)):
        if ate and n > i and re.search(ate, linhas[n]):
            fim = n
            break
        nivel += linhas[n].count("{") - linhas[n].count("}")
        abriu = abriu or "{" in linhas[n]
        if (abriu and nivel <= 0) or (not abriu and linhas[n].rstrip().endswith(";")):
            fim = n
            break
    corpo = [l for l in linhas[i : fim + 1] if not l.strip().startswith("///")]
    recuo = min(len(l) - len(l.lstrip()) for l in corpo if l.strip())
    return "\n".join(l[recuo:] for l in corpo), i + 1, fim + 1


PALAVRAS = (
    "public private internal static readonly sealed partial record struct class enum const return if else for foreach while "
    "new var bool int double string out in is not and or switch when throw true false null this override async await "
    "using namespace get init typeof"
).split()


def colorir(codigo: str, linguagem: str = "cs") -> str:
    """Realce simples (comentário, texto, número, palavra-chave), o bastante para ler no papel."""
    if linguagem == "json":
        partes = re.split(r'("(?:[^"\\]|\\.)*")', codigo)
        saida = []
        for n, p in enumerate(partes):
            e = html.escape(p)
            if n % 2:
                classe = "ch" if partes[n + 1].lstrip().startswith(":") else "tx"
                saida.append(f'<span class="{classe}">{e}</span>')
            else:
                saida.append(re.sub(r"\b(\d+(?:\.\d+)?|true|false|null)\b", r'<span class="nu">\1</span>', e))
        return "".join(saida)
    padrao = re.compile(r'(//[^\n]*)|(\$?@?"(?:[^"\\\n]|\\.)*")|(\b\d+(?:\.\d+)?\b)|(\b[A-Za-z_]\w*\b)')
    saida, pos = [], 0
    for m in padrao.finditer(codigo):
        saida.append(html.escape(codigo[pos : m.start()]))
        t = html.escape(m.group(0))
        if m.group(1):
            saida.append(f'<span class="co">{t}</span>')
        elif m.group(2):
            saida.append(f'<span class="tx">{t}</span>')
        elif m.group(3):
            saida.append(f'<span class="nu">{t}</span>')
        elif m.group(4) in PALAVRAS:
            saida.append(f'<span class="kw">{t}</span>')
        elif m.group(4)[0].isupper():
            saida.append(f'<span class="ti">{t}</span>')
        else:
            saida.append(t)
        pos = m.end()
    saida.append(html.escape(codigo[pos:]))
    return "".join(saida)


def bloco(arquivo: pathlib.Path, inicio: str, ate: str | None = None, legenda: str = "") -> str:
    codigo, de, a = trecho(arquivo, inicio, ate)
    caminho = arquivo.relative_to(RAIZ).as_posix()
    return (
        f'<figure class="codigo"><figcaption><code>{caminho}</code> · linhas {de}–{a}'
        f'{" · " + legenda if legenda else ""}</figcaption><pre>{colorir(codigo)}</pre></figure>'
    )


def bloco_json(titulo: str, dado, legenda: str) -> str:
    texto = json.dumps(dado, ensure_ascii=False, indent=2)
    return f'<figure class="codigo"><figcaption><code>{titulo}</code> · {legenda}</figcaption><pre>{colorir(texto, "json")}</pre></figure>'


# ── evidências geradas ───────────────────────────────────────────────────────────────────────────────────────────────


def tabela_md(arquivo: pathlib.Path, titulo: str) -> str:
    """A tabela que vem logo depois do título `titulo` num relatório Markdown gerado pelos testes."""
    linhas = arquivo.read_text(encoding="utf-8").splitlines()
    i = linhas.index(titulo) + 1
    while not linhas[i].startswith("|"):
        i += 1
    tabela = []
    while i < len(linhas) and linhas[i].startswith("|"):
        tabela.append([c.strip() for c in linhas[i].strip("|").split("|")])
        i += 1
    cab, corpo = tabela[0], tabela[2:]

    def celula(c: str) -> str:
        e = html.escape(c)
        return re.sub(r"`([^`]+)`", r"<code>\1</code>", re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", e))

    return (
        "<table><thead><tr>" + "".join(f"<th>{celula(c)}</th>" for c in cab) + "</tr></thead><tbody>"
        + "".join("<tr>" + "".join(f"<td>{celula(c)}</td>" for c in l) + "</tr>" for l in corpo)
        + "</tbody></table>"
    )


def resumo_md(arquivo: pathlib.Path) -> str:
    linha = next(l for l in arquivo.read_text(encoding="utf-8").splitlines() if l.startswith("**"))
    return re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", html.escape(linha))


def semgrep() -> str:
    """Roda o taint estático de novo (canário + código de produção) e guarda a saída; sem Docker, usa a última."""
    guardado = AQUI / "semgrep.txt"
    try:
        r = subprocess.run([str(RAIZ / "security/semgrep/rodar.sh")], capture_output=True, text=True, timeout=600, check=True)
        guardado.write_text(r.stdout.strip() + "\n", encoding="utf-8")
    except (OSError, subprocess.SubprocessError) as erro:
        print(f"semgrep: usando a última execução ({erro})", file=sys.stderr)
    return guardado.read_text(encoding="utf-8").strip()


def env(chave: str) -> str:
    for l in (RAIZ / ".env").read_text(encoding="utf-8").splitlines():
        if l.startswith(chave + "="):
            return l.split("=", 1)[1]
    return ""


def sem_acento(texto: str) -> bool:
    """A mesma regra da GuardaDaTraducao do Core, com a lista lida do próprio arquivo C# (uma fonte só)."""
    codigo = (PRIV / "Traducao/GuardaDaTraducao.cs").read_text(encoding="utf-8")
    lista = re.search(r"_semAcento\s*=\s*\[(.*?)\];", codigo, re.S).group(1)
    palavras = set(re.findall(r'"(\w+)"', lista))
    return any(p in palavras for p in re.findall(r"[^\W\d_]+", texto.lower()))


def traducoes() -> dict:
    """Eventos reais do Core, só com metadados (o evento nunca carrega o dado): uma tradução da IA por tipo e categoria
    que passa na guarda de hoje, a tradução real que a guarda passou a recusar e um fallback em template."""
    guardado = AQUI / "traducoes.json"
    try:
        core = os.environ.get("CORE", "http://localhost:5080")
        login = urllib.request.Request(
            f"{core}/api/v1/auth/login",
            data=json.dumps({"email": "operador@demo.sombra", "senha": env("Demo__Senha")}).encode(),
            headers={"content-type": "application/json"},
        )
        with urllib.request.urlopen(login, timeout=5) as r:
            token = json.load(r)["token"]
        vistos, escolhidos = set(), {"ia": [], "erros": [], "template": None}
        erros_vistos = set()
        for pagina in range(1, 8):
            pedido = urllib.request.Request(f"{core}/api/v1/privacidade/eventos?pagina={pagina}&tamanho=100",
                                            headers={"authorization": f"Bearer {token}"})
            with urllib.request.urlopen(pedido, timeout=5) as r:
                itens = json.load(r)["itens"]
            for e in itens:
                t = e.get("traducao") or {}
                resumo = {k: e[k] for k in ("momento", "tipo", "categoria", "sink", "regra", "bloqueado", "traducao")}
                revisao = revisao_de(resumo) if t else None
                errada = bool(t) and ((revisao and revisao[0] == "errada") or sem_acento(t["cidadao"]) or sem_acento(t["gestao"]))
                if t.get("fonte") == "template":
                    escolhidos["template"] = escolhidos["template"] or resumo
                elif t.get("fonte") == "IA" and errada and (e["tipo"], t["cidadao"]) not in erros_vistos:
                    erros_vistos.add((e["tipo"], t["cidadao"]))
                    escolhidos["erros"].append(resumo)
                elif t.get("fonte") == "IA" and not errada and (e["tipo"], e["categoria"]) not in vistos:
                    vistos.add((e["tipo"], e["categoria"]))
                    escolhidos["ia"].append(resumo)
            if not itens:
                break
        if escolhidos["ia"]:
            guardado.write_text(json.dumps(escolhidos, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    except (OSError, KeyError, ValueError) as erro:
        print(f"traduções: usando as guardadas ({erro})", file=sys.stderr)
    return json.loads(guardado.read_text(encoding="utf-8"))


# A nossa revisão das traduções reais que entram no PDF (pelo tipo e pelo começo do texto do cidadão): a guarda confere
# a forma e uma coerência mínima com o evento; o sentido, a gente conferiu lendo.
REVISAO = {
    ("MQTT_REPLAY", "Detectamos uma mensagem repetida"): (
        "certa", "Fiel ao evento nas três audiências, e a correção técnica é a certa (o segredo HMAC e o relógio). "
        "A guarda de hoje aprova."),
    ("MQTT_ASSINATURA_INVALIDA", "Identificamos um acesso incorreto"): (
        "errada", "Inventa um fato (\"nossa equipe já está cuidando disso\") e a correção técnica sugere Sensivel<T> em vez "
        "do segredo HMAC. Hoje a guarda recusa (cidadao_promete_o_que_nao_existe) e vale o template."),
    ("ACESSO_NEGADO", "Tentaram acessar seus dados de identificacao"): (
        "errada", "Sem acento (\"identificacao\", \"autorizacao\", \"informacoes\"), embora o prompt peça a ortografia correta. "
        "Hoje a guarda recusa (sem_acento)."),
    ("ACESSO_NEGADO", "Alguém tentou ver as informações da sua casa"): (
        "certa", "Fiel ao evento, e a correção técnica é a do prompt (o token e o correlationId). Gerada depois da correção."),
    ("ACESSO_NEGADO", "Tentaram acessar seus dados de saúde sem permissão"): (
        "certa", "Fiel ao evento, cita o art. 11 para saúde e a correção certa (o token). Gerada depois da correção."),
    ("ACESSO_NEGADO", "Tentamos enviar"): (
        "errada", "Erra o sentido: alguém sem login tentou ver o dado de saúde e foi barrado, mas o texto diz que o Sombra "
        "\"tentou enviar\"; e a correção não cita o token. Hoje a guarda recusa (cidadao_contradiz_o_evento)."),
}


def revisao_de(e: dict) -> tuple[str, str] | None:
    return next((v for (tipo, inicio), v in REVISAO.items() if e["tipo"] == tipo and e["traducao"]["cidadao"].startswith(inicio)), None)


def cartao(e: dict, nota: str = "") -> str:
    revisao = revisao_de(e)
    if revisao and not nota:
        nota = f'<p class="revisao {revisao[0]}"><b>Nossa revisão:</b> {html.escape(revisao[1])}</p>'
    return f"""<div class="traducao"><p class="meta">{html.escape(e['tipo'])} · {html.escape(e['categoria'] or 'sem dado pessoal')}
        · {html.escape(e['sink'] or 'MQTT')} · {'bloqueado' if e['bloqueado'] else 'vazou'} · {e['momento'][:16].replace('T', ' ')} UTC
        · fonte: {html.escape(e['traducao']['fonte'])}</p>{nota}
        <dl><dt>Cidadão</dt><dd>{html.escape(e['traducao']['cidadao'])}</dd>
        <dt>Gestão</dt><dd>{html.escape(e['traducao']['gestao'])}</dd>
        <dt>Time técnico</dt><dd>{html.escape(e['traducao']['tecnico'])}</dd></dl></div>"""


def imagem(caminho: str, legenda: str, altura_mm: int) -> str:
    """Um print do painel recortado no topo: a página inteira do dashboard não cabe numa folha."""
    return (f'<figure class="print"><div class="janela" style="height:{altura_mm}mm"><img src="{os.path.relpath(RAIZ / caminho, AQUI)}" '
            f'alt="{html.escape(legenda)}"></div><figcaption>{legenda}</figcaption></figure>')


# ── o documento ──────────────────────────────────────────────────────────────────────────────────────────────────────


def documento() -> str:
    auditoria = json.loads((RAIZ / "security/auditoria/relatorio.json").read_text(encoding="utf-8"))
    regras = json.loads((PRIV / "Regras/pii-regras.json").read_text(encoding="utf-8"))
    a1 = next(c for c in auditoria if c["id"] == "A1")
    resposta_a1 = json.loads(a1["ligado"]["resposta"])
    exemplos = traducoes()
    canario = semgrep()

    linhas_auditoria = "".join(
        f"<tr><td><b>{c['id']}</b></td><td>{html.escape(c['nome'])}</td><td>{html.escape(c['esperado'])}</td>"
        f"<td class='{'mal' if c['desligado']['vazou'] else 'bem'}'>{c['desligado']['status']} · {'vazou' if c['desligado']['vazou'] else 'bloqueado'}</td>"
        f"<td class='{'mal' if c['ligado']['vazou'] else 'bem'}'>{c['ligado']['status']} · {'vazou' if c['ligado']['vazou'] else 'bloqueado'}</td></tr>"
        for c in auditoria
    )
    vazam = sum(c["desligado"]["vazou"] for c in auditoria)
    bloqueia = sum(not c["ligado"]["vazou"] for c in auditoria)

    cartoes_ia = "".join(cartao(e) for e in exemplos["ia"][:3])
    caso_recusado = "".join(cartao(e) for e in exemplos.get("erros", []))
    template = exemplos.get("template")
    caso_template = cartao(template, '<p class="nota-recusa">O Gemini respondeu 503 ou passou do limite de 8 s, e o '
                           'tradutor usou o template, que também passa pela guarda.</p>') if template else ""
    integrantes = "".join(f"<li>{n} <span>{rm}</span></li>" for n, rm in INTEGRANTES)
    tokens = (RAIZ / "design/tokens.css").read_text(encoding="utf-8")

    return f"""<!doctype html>
<html lang="pt-BR"><head><meta charset="utf-8"><title>Sombra · Cybersecurity</title>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Atkinson+Hyperlegible+Mono:wght@400;600&family=Atkinson+Hyperlegible+Next:ital,wght@0,400;0,600;0,800;1,400&family=Fraunces:opsz,wght,SOFT@9..144,400..800,0..100&display=swap">
<style>
{tokens}
@page {{ size: A4; margin: 16mm 15mm 18mm; }}
@page :first {{ margin: 0; }}
* {{ box-sizing: border-box; }}
html {{ -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
body {{ margin: 0; font-family: var(--fonte-texto); font-size: 10.5pt; line-height: 1.5; color: var(--cor-tinta-900); background: #fff; }}
h1, h2, h3 {{ font-family: var(--fonte-display); color: var(--cor-sombra-900); line-height: 1.15; font-variation-settings: "SOFT" 50; }}
h2 {{ font-size: 21pt; margin: 0 0 4mm; break-before: page; }}
h2 small {{ display: block; font-family: var(--fonte-texto); font-size: 10pt; color: var(--cor-telha-700); font-weight: 600; letter-spacing: .04em; text-transform: uppercase; margin-bottom: 1.5mm; }}
h3 {{ font-size: 13pt; margin: 6mm 0 2mm; break-after: avoid; }}
p {{ margin: 0 0 2.5mm; }}
code {{ font-family: var(--fonte-mono); font-size: .9em; background: var(--cor-areia-100); padding: 0 .25em; border-radius: 3px; }}
ul {{ margin: 0 0 3mm; padding-left: 5mm; }} li {{ margin-bottom: 1mm; }}
.capa {{ height: 297mm; padding: 26mm 22mm; background: var(--cor-sombra-900); color: var(--cor-areia-50); display: flex; flex-direction: column; break-after: page; }}
.capa .marca {{ font-family: var(--fonte-display); font-size: 54pt; font-weight: 700; color: var(--cor-areia-0); margin: 0; }}
.capa .sub {{ font-size: 15pt; color: var(--cor-sombra-100); margin: 2mm 0 0; max-width: 150mm; }}
.capa h1 {{ color: var(--cor-ocre-300); font-size: 25pt; margin: 30mm 0 4mm; max-width: 160mm; }}
.capa .pilares {{ display: flex; gap: 3mm; flex-wrap: wrap; margin: 0; padding: 0; list-style: none; }}
.capa .pilares li {{ border: 1px solid var(--cor-sombra-300); border-radius: 999px; padding: 1mm 4mm; font-size: 10pt; }}
.capa .rodape {{ margin-top: auto; display: grid; grid-template-columns: 1fr 1fr; gap: 8mm; font-size: 10.5pt; }}
.capa .rodape h3 {{ color: var(--cor-ocre-300); font-family: var(--fonte-texto); font-size: 9pt; text-transform: uppercase; letter-spacing: .06em; margin: 0 0 2mm; }}
.capa .rodape ul {{ list-style: none; padding: 0; }} .capa .rodape li span {{ color: var(--cor-sombra-300); font-family: var(--fonte-mono); margin-left: 2mm; }}
.capa a {{ color: var(--cor-areia-0); }}
table {{ width: 100%; border-collapse: collapse; margin: 2mm 0 4mm; font-size: 9pt; break-inside: auto; }}
th {{ text-align: left; background: var(--cor-sombra-900); color: var(--cor-areia-0); font-weight: 600; padding: 1.6mm 2mm; }}
td {{ padding: 1.4mm 2mm; border-bottom: 1px solid var(--cor-areia-200); vertical-align: top; }}
tr {{ break-inside: avoid; }}
td.mal {{ color: var(--cor-risco-vermelho); font-weight: 600; white-space: nowrap; }} td.bem {{ color: var(--cor-risco-verde); font-weight: 600; white-space: nowrap; }}
.rubrica td:first-child {{ font-weight: 600; white-space: nowrap; }}
figure {{ margin: 3mm 0 4mm; }}
figure.codigo {{ break-inside: avoid; border: 1px solid var(--cor-areia-200); border-radius: 6px; overflow: hidden; }}
figure.codigo figcaption {{ font-size: 8pt; background: var(--cor-areia-100); color: var(--cor-tinta-700); padding: 1.2mm 3mm; }}
figure.codigo figcaption code {{ background: none; padding: 0; }}
pre {{ font-variant-ligatures: none; font-feature-settings: "liga" 0, "calt" 0; margin: 0; padding: 3mm; font-family: var(--fonte-mono); font-size: 7.6pt; line-height: 1.45; white-space: pre-wrap; background: var(--cor-areia-0); }}
.kw {{ color: var(--cor-sombra-700); font-weight: 600; }} .ti {{ color: var(--cor-tinta-900); font-weight: 600; }} .tx {{ color: var(--cor-telha-700); }}
.nu {{ color: #866409; }} .co {{ color: var(--cor-tinta-500); font-style: italic; }} .ch {{ color: var(--cor-sombra-700); }}
figure.print {{ break-inside: avoid; }}
figure.print .janela {{ overflow: hidden; border: 1px solid var(--cor-areia-200); border-radius: 6px; }}
figure.print img {{ display: block; width: 100%; }}
figure.print figcaption, figure.codigo + p.nota {{ font-size: 8.5pt; color: var(--cor-tinta-500); }}
.lado {{ display: grid; grid-template-columns: 1.9fr 1fr; gap: 5mm; align-items: start; }}
.destaque {{ background: var(--cor-sombra-100); border-left: 3px solid var(--cor-sombra-500); padding: 3mm 4mm; border-radius: 0 6px 6px 0; margin: 3mm 0; break-inside: avoid; }}
.numero {{ font-family: var(--fonte-display); font-size: 20pt; color: var(--cor-sombra-900); font-weight: 700; }}
.numeros {{ display: grid; grid-template-columns: repeat(3, 1fr); gap: 4mm; margin: 3mm 0 4mm; }}
.numeros div {{ border: 1px solid var(--cor-areia-200); border-radius: 6px; padding: 3mm; }} .numeros p {{ margin: 0; font-size: 9pt; color: var(--cor-tinta-700); }}
.traducao {{ border: 1px solid var(--cor-areia-200); border-radius: 6px; padding: 3mm 4mm; margin-bottom: 3mm; break-inside: avoid; }}
.revisao {{ font-size: 8.5pt; margin: 0 0 1.5mm; }} .revisao.certa {{ color: var(--cor-risco-verde); }}
.revisao.ressalva {{ color: #866409; }} .revisao.errada {{ color: var(--cor-risco-vermelho); }}
.nota-recusa {{ font-size: 8.5pt; color: var(--cor-telha-700); margin: 0 0 1.5mm; }}
.traducao .meta {{ font-family: var(--fonte-mono); font-size: 8pt; color: var(--cor-tinta-500); }}
.traducao dl {{ margin: 0; display: grid; grid-template-columns: 26mm 1fr; gap: 1mm 3mm; }} .traducao dt {{ font-weight: 600; color: var(--cor-sombra-700); }} .traducao dd {{ margin: 0; }}
pre.saida {{ background: var(--cor-noite-900); color: var(--cor-areia-50); border-radius: 6px; font-size: 8pt; }}
</style></head><body>

<section class="capa">
  <p class="marca">Sombra</p>
  <p class="sub">Proteção de pessoas idosas que moram sozinhas durante ondas de calor em São Paulo.</p>
  <h1>Sombra.Privacidade: os dados de saúde de quem é cuidado nunca saem por descuido</h1>
  <ul class="pilares"><li>Parsing de PII</li><li>Taint Analysis</li><li>IA na remediação</li><li>Dashboard de Risco Cidadão</li></ul>
  <div class="rodape">
    <div><h3>Integrantes</h3><ul>{integrantes}</ul></div>
    <div><h3>Entrega</h3><p>Cybersecurity · Prof. Vitor Lasse<br>Global Solution 2026.2 · FIAP<br>Engenharia de Software, 3º ano<br>19 de novembro de 2026</p>
    <p>Código: <a href="{REPO}">{REPO.removeprefix('https://')}</a><br>e o ZIP <code style="background:none;color:inherit">sombra-codigo.zip</code>, entregue à parte.</p></div>
  </div>
</section>

<h2><small>Visão geral</small>O que protegemos e onde está cada critério</h2>
<p>O Sombra acompanha pessoas idosas que moram sozinhas: uma Sentinela mede o calor de dentro da casa, o Core decide quando
pedir o "estou bem" e quando avisar a família, e o operador da prefeitura acompanha tudo num painel. Para isso o sistema
guarda <b>condições de saúde, remédios, CPF, Cartão SUS e telefones</b>, que a LGPD trata como dado pessoal sensível
(art. 5º, II, e art. 11). O módulo <code>Sombra.Privacidade</code> fica dentro do Core (C#) e garante que esses dados não
saiam por log, por resposta, por mensagem de erro, para a IA nem pelo broker MQTT, e que cada tentativa vire uma
explicação clara para quem é afetado.</p>
<table class="rubrica"><thead><tr><th>Critério</th><th>Pts</th><th>Como o Sombra atende</th><th>Evidência</th></tr></thead><tbody>
<tr><td>Robustez do motor de PII</td><td>3</td><td>8 regras em JSON (configuráveis sem recompilar), validação de dígito do CPF e do Cartão SUS, detecção por chave e por conteúdo, mascaramento e cifra AES-256-GCM em repouso</td><td>§1 · 63 casos, 0 falso negativo</td></tr>
<tr><td>Eficácia do Taint Analysis</td><td>3</td><td>taint dinâmico (o dado marcado na entrada e conferido em 6 sinks, com bloqueio) e estático (Semgrep em modo taint, com canário)</td><td>§2 · auditoria A1–A8: {vazam} de 8 vazam sem o módulo, {bloqueia} de 8 bloqueados com ele</td></tr>
<tr><td>Inovação com IA na remediação</td><td>2</td><td>cada evento vira três traduções (cidadão, gestão, time técnico) por um prompt só com metadados, com guarda na saída da IA e fallback em templates</td><td>§3 · traduções reais do Gemini</td></tr>
<tr><td>Dashboard Executivo de Risco Cidadão</td><td>2</td><td>farol ponderado por categoria, desfecho e severidade, com decaimento no tempo, ao vivo pelo SignalR</td><td>§4 · prints do painel</td></tr>
</tbody></table>
<div class="destaque">Todos os dados deste documento são <b>sintéticos</b>: CPF e Cartão SUS gerados por semente, pessoas fictícias
do seed. Os trechos de código são cópias dos arquivos do repositório, com o caminho e as linhas, e as tabelas saem dos
relatórios que os próprios testes geram. Este PDF é refeito por <code>make entrega-cyber</code>.</div>

<h2><small>§1 · 3 pontos</small>Motor de parsing de PII</h2>
<p><b>Regras como dados.</b> Cada tipo de dado pessoal é uma regra em <code>pii-regras.json</code>, validada contra um schema
na carga: chaves de campo, padrão, validador, máscara e a base legal. Uma regra nova entra sem recompilar o Core. As 8
regras de hoje: {", ".join(f"<code>{r['id']}</code>" for r in regras["regras"])}.</p>
<div class="lado">
{bloco_json("Sombra.Privacidade/Regras/pii-regras.json", regras["regras"][0], "a regra do CPF")}
<div><p><b>Por chave e por conteúdo.</b> O campo <code>documento</code> é PII pelo nome; o mesmo CPF escrito no meio de
"observações" é PII pelo conteúdo. O <code>PiiScanningMiddleware</code> percorre o JSON de toda requisição e marca os
valores no <code>ContextoDeTaint</code>, sem nunca logar o valor.</p>
<p><b>Sem falso alarme.</b> Onze dígitos só são CPF se os dois verificadores baterem (ao lado), e o Cartão SUS passa pela
validação oficial (soma ponderada múltipla de 11).</p></div>
</div>
{bloco(PRIV / "Deteccao/Validadores.cs", r"public static bool Cpf\(", legenda="CPF de verdade, não qualquer número de 11 dígitos")}
{bloco(PRIV / "Deteccao/Validadores.cs", r"private static int VerificadorDoCpf\(")}
<h3>Mascaramento e cifra em repouso</h3>
<p>A resposta real do cenário A1 da auditoria, com o módulo ligado: o operador vê o necessário, nunca o dado inteiro.</p>
{bloco_json("POST /api/v1/domicilios/{{Lurdes}}/moradores → 201", {k: resposta_a1[k] for k in ("nome", "cpf", "cartaoSus", "perfilDeSaude")}, "resposta real da auditoria (dados sintéticos)")}
<p>No banco, CPF, Cartão SUS, perfil de saúde e telefones são gravados com <b>AES-256-GCM</b> por value converters do EF
Core, e o CPF tem um <b>índice cego HMAC</b> para ser buscado sem ser decifrado. As chaves vêm do ambiente, nunca do código
(<code>Repouso/CifraDeCampos.cs</code>).</p>
<h3>Relatório de acertos</h3>
<p>{resumo_md(RAIZ / "security/privacidade/relatorio-de-acertos.md")} — positivos, negativos e casos difíceis (CPF com o
verificador trocado, CID no meio de uma frase, JSON aninhado, unicode). Gerado pelo teste <code>RelatorioDeAcertos</code>.</p>
{tabela_md(RAIZ / "security/privacidade/relatorio-de-acertos.md", "## Por regra")}

<h2><small>§2 · 3 pontos</small>Taint Analysis: da fonte ao sink, com bloqueio</h2>
<p><b>Fontes:</b> corpo, query e headers da requisição (marcados pelo middleware) e os campos sensíveis lidos do banco.
<b>Sinks guardados:</b> log, resposta HTTP, saída externa (Clima e IA), MQTT, exceções e a própria entrada (PII em texto
livre). Cada guarda confere a saída contra o <code>ContextoDeTaint</code> e os detectores, bloqueia ou redige, e registra um
<code>VazamentoBloqueado</code> só com metadados.</p>
<h3>O taint como tipo: vazar por descuido vira erro</h3>
{bloco(PRIV / "Taint/Sensivel.cs", r"public readonly record struct Sensivel<T>", legenda="ToString() devolve [PII:SAUDE] e o JSON recusa serializar")}
{bloco(PRIV / "Taint/Sensivel.cs", r"private sealed class Recusa<T>")}
<h3>Auditoria do protótipo: antes × depois</h3>
<p>O mesmo roteiro roda com o módulo <b>desligado</b> (modo inseguro, aceito só no ambiente <code>Demo</code>) e
<b>ligado</b>, num PostgreSQL real. Teste <code>OitoCenariosVazamDesligadoESaoBloqueadosLigado</code>, relatório em
<code>security/auditoria/relatorio.md</code>.</p>
<table><thead><tr><th>#</th><th>Cenário</th><th>Esperado</th><th>Sem o módulo</th><th>Com o módulo</th></tr></thead><tbody>{linhas_auditoria}</tbody></table>
<p class="destaque"><b>A1, na prática:</b> sem o módulo, o CPF escrito no texto livre de saúde chega ao banco
(<code>{html.escape(a1['desligado']['evidencia'][:60])}…</code>). Com o módulo, o texto é mascarado antes de gravar e de
logar, e o evento <code>{html.escape(a1['ligado']['eventos'][0] if a1['ligado']['eventos'] else '')}</code> vai para o dashboard.</p>
<h3>Taint estático (Semgrep em modo taint)</h3>
<p>Complementa o dinâmico no código: fontes (DTOs com CPF, Cartão SUS, laudo, senha e <code>[FromBody]</code>), sinks
(<code>ILogger.Log*</code>, <code>Console.Write*</code>, <code>Problem(detail:…)</code>) e sanitizers
(<code>MascaradorDePii.Mascarar</code>, <code>Sensivel&lt;T&gt;</code>). Um arquivo <b>canário</b> com vazamentos de
propósito prova que a regra pega; o código de produção sai limpo. Roda no CI com SARIF.</p>
<pre class="saida">$ make semgrep
{html.escape(canario)}</pre>

<h2><small>§3 · 2 pontos</small>IA na remediação: o evento vira explicação, e a IA nunca vê o dado</h2>
<p>O <code>TradutorDeRiscoWorker</code> lê os eventos de segurança e pede ao Gemini três textos: para o <b>cidadão</b> (até 2
frases, tom calmo, sem jargão), para a <b>gestão</b> (impacto e artigo da LGPD) e para o <b>time técnico</b> (a correção, que
é a remediação). O prompt é montado só com metadados: tipo, sink, categoria, severidade, regra, rota sem valores e se foi
bloqueado.</p>
{bloco(PRIV / "Traducao/PromptDeRisco.cs", r"public static string Montar\(", legenda="o prompt só com metadados")}
<p><b>A saída da IA também é guardada:</b> passa pelos mesmos detectores de PII, por uma lista de jargões proibidos para o
cidadão, por limite de tamanho e de frases. Se reprovar, ou se a IA não responder no tempo, entra um template.</p>
{bloco(PRIV / "Traducao/GuardaDaTraducao.cs", r"public static bool Aprovar\(", legenda="o que reprova uma tradução")}
<h3>Traduções reais do Gemini, revisadas por nós</h3>
{cartoes_ia}
<h3>O que a nossa revisão achou, e a guarda agora recusa</h3>
<p>Ao montar este documento, a gente leu as traduções que o Core tinha gerado e achou erros que a guarda deixava passar:
um texto sem acento, uma promessa inventada, uma correção técnica errada e um texto com o sentido trocado. Cada um virou
uma regra da guarda e um teste com o texto real. E a tradução guardada no cache agora passa de novo pela guarda antes de
ser reaproveitada: foi assim que os eventos novos ganharam as traduções certas acima, em vez de herdar as erradas.</p>
{caso_recusado}
{bloco(PRIV / "Traducao/GuardaDaTraducao.cs", r"public static bool Aprovar\(TraducaoDeRisco traducao, EventoParaTraduzir", legenda="a coerência com o evento")}
<h3>Quando a IA não responde</h3>
{caso_template}
<p class="destaque"><b>O limite, dito com clareza:</b> a guarda confere a forma e uma coerência mínima por tipo de evento
(o remédio técnico certo, o verbo do texto do cidadão, nenhuma promessa que o sistema não cumpre). Isso não é entender o
texto. Por isso a tradução nunca anda sozinha: o painel mostra ao lado a evidência técnica do evento (tipo, sink, regra,
desfecho), e todo tipo de evento tem um template revisado por nós.</p>

<h2><small>§4 · 2 pontos</small>Dashboard Executivo de Risco Cidadão</h2>
<p>O farol soma os eventos dos últimos 7 dias, ponderados pela <b>categoria</b> (saúde e credencial pesam mais que contato e
localização), pela <b>severidade</b> e pelo <b>desfecho</b> (um vazamento pesa 3 vezes um bloqueio), com meia-vida de 72 h.
Verde abaixo de 30, amarelo até 70, vermelho a partir de 70. O painel atualiza ao vivo pelo evento
<code>RiscoCidadaoAtualizado</code> do SignalR, e o app mostra à família o farol da própria casa.</p>
{bloco(PRIV / "Risco/RiscoCidadao.cs", r"public static double Pontos\(", legenda="a pontuação de um evento")}
{imagem("web/painel/docs/evidencias/risco-cidadao-1440.png", "O dashboard no painel do operador (1440 px, o topo da página): o farol geral, o farol por categoria e um gráfico por sink nas últimas 24 h. Abaixo deles ficam as regras que mais disparam e o feed com as três traduções.", 118)}
<div class="lado">
<div><h3>Para quem decide</h3>
<ul><li><b>Farol geral e por categoria</b>, com cor, ícone e texto (nunca só a cor).</li>
<li><b>Um gráfico pequeno por sink</b> (resposta HTTP, log, saída externa, MQTT, exceção, entrada), na mesma escala, para
comparar onde o dado tenta sair.</li>
<li><b>As regras que mais disparam</b>, para priorizar a correção.</li>
<li><b>O feed</b> com a tradução para o cidadão, para a gestão e para o time técnico, e a evidência técnica sob demanda.</li>
<li><b>Ao vivo:</b> o Core publica o <code>RiscoCidadaoAtualizado</code> a cada lote de eventos, e o painel recalcula sem
recarregar.</li></ul></div>
{imagem("web/painel/docs/evidencias/risco-cidadao-390.png", "No celular (390 px).", 120)}
</div>

<h2><small>Reproduzir</small>Como conferir cada evidência</h2>
<table><thead><tr><th>Comando (na raiz)</th><th>O que prova</th></tr></thead><tbody>
<tr><td><code>make up</code></td><td>sobe o Sombra inteiro (Core, Clima, banco, broker e painel) com os segredos gerados no <code>.env</code></td></tr>
<tr><td><code>make -C services/core-api pii-relatorio</code></td><td>refaz o relatório de acertos do motor de PII (§1)</td></tr>
<tr><td><code>security/auditoria/rodar-auditoria.sh</code></td><td>refaz a auditoria A1–A8, antes e depois (§2)</td></tr>
<tr><td><code>make semgrep</code></td><td>roda o taint estático: o canário dispara e a produção sai limpa (§2)</td></tr>
<tr><td><code>make test-core</code></td><td>todos os testes do Core, incluindo os do módulo de privacidade</td></tr>
<tr><td><code>make entrega-cyber</code></td><td>refaz este PDF e o ZIP do código</td></tr>
</tbody></table>
<p>Código: <a href="{REPO}/tree/main/services/core-api/sources/Sombra.Privacidade">services/core-api/sources/Sombra.Privacidade</a> ·
auditoria e relatórios: <a href="{REPO}/tree/main/security">security/</a> · spec: <code>docs/specs/07-cyber-privacidade.md</code>.</p>
</body></html>
"""


def main() -> None:
    AQUI.mkdir(parents=True, exist_ok=True)
    pagina = AQUI / "cyber.html"
    pagina.write_text(documento(), encoding="utf-8")
    chrome = os.environ.get("CHROME", "google-chrome-stable")
    subprocess.run(
        [chrome, "--headless=new", "--no-sandbox", "--disable-gpu", "--no-pdf-header-footer", "--virtual-time-budget=15000",
         f"--print-to-pdf={AQUI / 'cyber.pdf'}", pagina.as_uri()],
        check=True, capture_output=True,
    )
    # o código vai separado do PDF, como o professor pediu: o ZIP é o repositório no commit atual
    subprocess.run(["git", "-C", str(RAIZ), "archive", "--format=zip", "--prefix=sombra/", "-o", str(AQUI / "sombra-codigo.zip"), "HEAD"], check=True)
    print(f"ok: {(AQUI / 'cyber.pdf').relative_to(RAIZ)} e {(AQUI / 'sombra-codigo.zip').relative_to(RAIZ)}")


if __name__ == "__main__":
    main()
