#!/usr/bin/env python3
"""Checklist da entrega (F14.1): o que cada uma das 8 tarefas do Teams exige, conferido no repositório.

Para cada disciplina: os arquivos exigidos, os três integrantes, o link do vídeo (sem placeholder) e, com --online, se
cada link abre sem login (o repositório no GitHub, os vídeos no YouTube pelo oEmbed, que responde para vídeo não listado
e recusa vídeo privado, e o projeto do Wokwi). Sai com 0 só quando nada falta.

Uso:  make checklist            (offline)
      make checklist ONLINE=1   (também abre os links)
"""

import pathlib
import re
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request

RAIZ = pathlib.Path(__file__).resolve().parents[1]
REPO = "https://github.com/andrenakarocha/sombra"
INTEGRANTES = ["André Nakamatsu Rocha", "João Marcelo Furtado Romero", "Matheus Rivera Montovaneli"]
RMS = ["555004", "555199", "555499"]
YOUTUBE = re.compile(r"https://(?:www\.)?(?:youtube\.com/watch\?v=|youtu\.be/)[\w-]{11}")
WOKWI = re.compile(r"https://wokwi\.com/projects/\d+")
PLACEHOLDER = re.compile(r"‹[^›]*›")

ONLINE = "--online" in sys.argv
resultados: list[tuple[str, str, str]] = []


def marcar(disciplina: str, ok: bool | None, descricao: str) -> None:
    resultados.append((disciplina, "✔" if ok else ("⏸" if ok is None else "✘"), descricao))


def texto(caminho: str) -> str:
    arquivo = RAIZ / caminho
    if not arquivo.exists():
        return ""
    if arquivo.suffix == ".pdf":
        try:
            return subprocess.run(["pdftotext", "-layout", str(arquivo), "-"], capture_output=True, text=True, check=True).stdout
        except (OSError, subprocess.CalledProcessError):
            return ""
    return arquivo.read_text(encoding="utf-8", errors="replace")


def abre(url: str) -> bool:
    """O link responde sem login (sem cookie, sem token)."""
    if "youtu" in url:
        url = "https://www.youtube.com/oembed?format=json&url=" + urllib.parse.quote(url, safe="")
    pedido = urllib.request.Request(url, headers={"User-Agent": "sombra-checklist"}, method="GET")
    try:
        with urllib.request.urlopen(pedido, timeout=10) as r:
            return r.status == 200
    except (urllib.error.URLError, TimeoutError):
        return False


def arquivos(disciplina: str, *caminhos: str) -> None:
    for c in caminhos:
        marcar(disciplina, (RAIZ / c).exists(), f"`{c}` existe")


def integrantes(disciplina: str, caminho: str) -> None:
    t = texto(caminho)
    faltam = [n for n, rm in zip(INTEGRANTES, RMS) if n not in t and rm not in t]
    marcar(disciplina, not faltam if t else False, f"os 3 integrantes em `{caminho}`" + (f" (faltam: {', '.join(faltam)})" if t and faltam else ""))


def link(disciplina: str, caminho: str, padrao: re.Pattern, nome: str) -> None:
    t = texto(caminho)
    achado = padrao.search(t)
    if not achado:
        marcar(disciplina, False, f"link do {nome} em `{caminho}`" + (" (ainda placeholder)" if PLACEHOLDER.search(t) else ""))
        return
    if ONLINE:
        marcar(disciplina, abre(achado.group(0)), f"o {nome} abre sem login: {achado.group(0)}")
    else:
        marcar(disciplina, True, f"link do {nome} em `{caminho}`")


def links_relativos(disciplina: str) -> None:
    """Todo link relativo dos .md versionados aponta para um arquivo ou pasta que existe (o 🔎 da F12.1)."""
    arquivos_md = subprocess.run(["git", "-C", str(RAIZ), "ls-files", "*.md"], capture_output=True, text=True, check=True).stdout.split()
    quebrados = []
    for md in arquivos_md:
        caminho = RAIZ / md
        if not caminho.exists():
            continue
        sem_codigo = re.sub(r"```.*?```", "", caminho.read_text(encoding="utf-8", errors="replace"), flags=re.S)
        for alvo in re.findall(r"\]\(([^)\s]+)\)", sem_codigo):
            if re.match(r"[a-z]+:", alvo) or alvo.startswith("#"):
                continue
            destino = (caminho.parent / urllib.parse.unquote(alvo.split("#", 1)[0])).resolve()
            if not destino.exists():
                quebrados.append(f"{md} → {alvo}")
    marcar(disciplina, not quebrados, f"nenhum link relativo quebrado nos {len(arquivos_md)} .md" + (": " + "; ".join(quebrados[:8]) if quebrados else ""))


def main() -> int:
    t = "Geral"
    integrantes(t, "README.md")
    links_relativos(t)
    arquivos(t, "prompts.md")
    if ONLINE:
        marcar(t, abre(REPO), f"o repositório abre sem login: {REPO}")

    t = "C# Software Development"
    arquivos(t, "services/core-api/README.md")
    integrantes(t, "services/core-api/README.md")

    t = "Cybersecurity"
    arquivos(t, "security/entrega/cyber.pdf", "security/entrega/sombra-codigo.zip")
    integrantes(t, "security/entrega/cyber.pdf")
    link(t, "security/entrega/README.md", YOUTUBE, "vídeo do dashboard")

    t = "SOA e Web Services"
    arquivos(t, "services/clima-service/README.md")
    integrantes(t, "services/clima-service/README.md")

    t = "IA & Machine Learning"
    arquivos(t, "ml/README.md")
    integrantes(t, "ml/README.md")
    link(t, "ml/README.md", YOUTUBE, "vídeo (até 5 min)")

    t = "Physical Computing"
    arquivos(t, "iot/sentinela/diagram.json", "iot/sentinela/sketch.ino", "iot/sentinela/integrantes.txt")
    integrantes(t, "iot/sentinela/integrantes.txt")
    link(t, "iot/sentinela/README.md", WOKWI, "projeto no Wokwi")
    link(t, "iot/sentinela/README.md", YOUTUBE, "vídeo (até 4 min)")

    t = "Mobile Development"
    arquivos(t, "mobile/README.md")
    integrantes(t, "mobile/README.md")
    link(t, "mobile/README.md", YOUTUBE, "vídeo (até 3 min)")

    t = "Operating Systems"
    arquivos(t, "infra/README.md", "infra/evidencias/reproducao.txt")
    marcar(t, len(list((RAIZ / "infra/evidencias").glob("*.png"))) >= 10, "os 10 prints de evidência em `infra/evidencias/`")
    integrantes(t, "infra/README.md")
    link(t, "infra/README.md", YOUTUBE, "vídeo (até 7 min)")

    t = "Testing, Compliance & QA"
    arquivos(t, "qa/entrega/qa.pdf")
    integrantes(t, "qa/entrega/qa.pdf")
    marcar(t, None, "o professor como Admin no projeto do Azure DevOps (conferir na tela do Azure)")

    atual = ""
    for disciplina, sinal, descricao in resultados:
        if disciplina != atual:
            print(f"\n{disciplina}")
            atual = disciplina
        print(f"  {sinal} {descricao}")
    faltando = sum(s == "✘" for _, s, _ in resultados)
    print(f"\n{'tudo pronto' if not faltando else f'{faltando} item(ns) faltando'}"
          + ("" if ONLINE else " (offline: make checklist ONLINE=1 também abre os links)"))
    return 1 if faltando else 0


if __name__ == "__main__":
    sys.exit(main())
