#!/usr/bin/env python3
"""Tokens de design do Sombra: gera o CSS e confere o contraste (WCAG 2.2).

Uso:
    design/tokens.py gerar      escreve design/tokens.css e mobile/src/tema/tokens.ts a partir de design/tokens.json
    design/tokens.py conferir   confere os pares de contraste dos dois temas (sai com 0 se todos passam)
"""

import json
import pathlib
import re
import sys

PASTA = pathlib.Path(__file__).resolve().parent
TOKENS_TS = PASTA.parent / "mobile" / "src" / "tema" / "tokens.ts"
TOKENS = json.loads((PASTA / "tokens.json").read_text(encoding="utf-8"))
NIVEIS = ("normal", "atencao", "cautelaExtrema", "perigo", "perigoExtremo")


def kebab(nome: str) -> str:
    return re.sub(r"(?<!^)(?=[A-Z])", "-", nome).lower()


def folhas(no: dict, caminho=()):
    """Percorre o JSON e devolve (caminho, token) de cada token ($value)."""
    for chave, valor in no.items():
        if chave.startswith("$"):
            continue
        if isinstance(valor, dict) and "$value" in valor:
            yield caminho + (chave,), valor
        elif isinstance(valor, dict):
            yield from folhas(valor, caminho + (chave,))


def resolver(valor: str) -> str:
    """Resolve um alias {a.b.c} até o valor final."""
    while isinstance(valor, str) and valor.startswith("{") and valor.endswith("}"):
        no = TOKENS
        for parte in valor[1:-1].split("."):
            no = no[parte]
        valor = no["$value"]
    return valor


def var_css(caminho) -> str:
    return "--" + "-".join(kebab(p) for p in caminho)


def valor_css(valor: str) -> str:
    if isinstance(valor, str) and valor.startswith("{"):
        return f"var({var_css(tuple(valor[1:-1].split('.')))})"
    return valor


def bloco_tema(tema: str, indent: str) -> list[str]:
    return [f"{indent}{var_css(c)}: {valor_css(t['$value'])};" for c, t in folhas(TOKENS["tema"][tema])]


def gerar() -> None:
    primitivos = [f"  {var_css(c)}: {t['$value']};" for c, t in folhas(TOKENS) if c[0] != "tema"]
    linhas = [
        "/* Gerado por design/tokens.py a partir de design/tokens.json. Não edite à mão. */",
        "",
        ":root {",
        *primitivos,
        "",
        "  color-scheme: light dark;",
        *bloco_tema("claro", "  "),
        "}",
        "",
        "@media (prefers-color-scheme: dark) {",
        "  :root:not([data-tema=\"claro\"]) {",
        *bloco_tema("escuro", "    "),
        "  }",
        "}",
        "",
        ":root[data-tema=\"escuro\"] {",
        *bloco_tema("escuro", "  "),
        "}",
        "",
    ]
    (PASTA / "tokens.css").write_text("\n".join(linhas), encoding="utf-8")
    print(f"design/tokens.css gerado ({len(primitivos)} primitivos)")
    gerar_ts()


def numero(valor: str) -> float:
    """'15px' → 15 e '150ms' → 150: o React Native usa números sem unidade."""
    n = float(re.sub(r"(px|ms)$", "", valor))
    return int(n) if n.is_integer() else n


def arvore(no: dict) -> dict:
    """Devolve os valores finais (aliases resolvidos), sem os metadados $type e $value."""
    saida = {}
    for chave, valor in no.items():
        if chave.startswith("$"):
            continue
        if "$value" in valor:
            final = resolver(valor["$value"])
            saida[chave] = numero(final) if valor["$type"] in ("dimension", "duration") else final
        else:
            saida[chave] = arvore(valor)
    return saida


def gerar_ts() -> None:
    # As pilhas de fonte do CSS não valem no app: lá as famílias são as carregadas em mobile/src/tema/fontes.ts.
    valores = arvore({k: v for k, v in TOKENS.items() if k != "fonte"})
    linhas = [
        "// Gerado por design/tokens.py a partir de design/tokens.json. Não edite à mão.",
        "",
        f"export const tokens = {json.dumps(valores, indent=2, ensure_ascii=False)} as const;",
        "",
    ]
    TOKENS_TS.parent.mkdir(parents=True, exist_ok=True)
    TOKENS_TS.write_text("\n".join(linhas), encoding="utf-8")
    print("mobile/src/tema/tokens.ts gerado")


def luminancia(hex_cor: str) -> float:
    canais = [int(hex_cor[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    lin = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in canais]
    return 0.2126 * lin[0] + 0.7152 * lin[1] + 0.0722 * lin[2]


def contraste(a: str, b: str) -> float:
    la, lb = sorted((luminancia(a), luminancia(b)), reverse=True)
    return (la + 0.05) / (lb + 0.05)


def cor(tema: str, caminho: str) -> str:
    no = TOKENS["tema"][tema]
    for parte in caminho.split("."):
        no = no[parte]
    return resolver(no["$value"])


def pares(tema: str):
    yield "texto", "fundo", 7.0, "texto principal (AAA)"
    yield "texto", "superficie", 7.0, "texto em cartão (AAA)"
    yield "texto", "superficieAlternativa", 7.0, "texto em faixa alternativa (AAA)"
    yield "textoSuave", "fundo", 4.5, "texto secundário"
    yield "textoSuave", "superficie", 4.5, "texto secundário em cartão"
    yield "marca", "fundo", 4.5, "marca usada como texto"
    yield "textoSobreMarca", "marca", 4.5, "texto em botão da marca"
    yield "destaque", "fundo", 4.5, "destaque usado como texto"
    yield "foco", "fundo", 3.0, "anel de foco"
    yield "foco", "superficie", 3.0, "anel de foco em cartão"
    for n in NIVEIS:
        yield f"risco.{n}.texto", f"risco.{n}.fundo", 4.5, f"texto do selo {n}"
        yield f"risco.{n}.borda", "fundo", 3.0, f"contorno do selo {n} (componente)"
        yield f"risco.{n}.borda", "superficie", 3.0, f"contorno do selo {n} em cartão"


def conferir() -> int:
    falhas = 0
    for tema in ("claro", "escuro"):
        print(f"\nTema {tema}")
        for frente, fundo, minimo, uso in pares(tema):
            razao = contraste(cor(tema, frente), cor(tema, fundo))
            ok = razao >= minimo
            falhas += not ok
            marca = "ok  " if ok else "FALHA"
            print(f"  {marca} {razao:5.2f}:1 (mín. {minimo}:1)  {uso}")
    print(f"\n{falhas} falha(s)")
    return 1 if falhas else 0


if __name__ == "__main__":
    comando = sys.argv[1] if len(sys.argv) > 1 else "conferir"
    if comando == "gerar":
        gerar()
        sys.exit(0)
    sys.exit(conferir())
