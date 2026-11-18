#!/usr/bin/env python3
"""Extrai a onda de calor de novembro de 2023 da estação A701 (INMET) para o replay do modo demonstração.

Lê o CSV versionado em ml/dados/brutos (sem alteração, como veio do INMET), confere o SHA-256 contra o manifesto e
grava só temperatura e umidade de cada hora da janela, com a proveniência. Horas com -9999 ou vazio não são
medição: ficam de fora e entram na contagem de faltantes. O índice de calor é calculado no próprio app.

Uso:
    mobile/scripts/extrair_replay_inmet.py              grava mobile/src/infrastructure/demo/replay-a701-2023-11.json
    mobile/scripts/extrair_replay_inmet.py --conferir   sai com 1 se o JSON versionado não bate com o CSV
"""

import datetime as dt
import hashlib
import json
import pathlib
import sys

RAIZ = pathlib.Path(__file__).resolve().parents[2]
CSV = RAIZ / "ml" / "dados" / "brutos" / "A701_2023.csv"
MANIFESTO = RAIZ / "ml" / "dados" / "brutos" / "manifesto.json"
SAIDA = RAIZ / "mobile" / "src" / "infrastructure" / "demo" / "replay-a701-2023-11.json"

# Do domingo 12/11 à quinta 16/11/2023, 0h em São Paulo (3h UTC): a subida, o pico de 14 e 15/11 e o alívio.
INICIO = dt.datetime(2023, 11, 12, 3, tzinfo=dt.timezone.utc)
HORAS = 96


def numero(texto: str):
    texto = texto.strip()
    if texto in ("", "-9999"):
        return None
    return float(texto.replace(",", "."))


def extrair() -> dict:
    bruto = CSV.read_bytes()
    sha = hashlib.sha256(bruto).hexdigest()
    entrada = json.loads(MANIFESTO.read_text(encoding="utf-8"))["arquivos"]["2023"]
    if sha != entrada["sha256Csv"]:
        raise SystemExit(f"SHA-256 do CSV não bate com o manifesto: {sha}")

    linhas = bruto.decode("latin-1").splitlines()
    cabecalho = linhas[8].split(";")
    i_temp = next(i for i, c in enumerate(cabecalho) if c.startswith("TEMPERATURA DO AR - BULBO SECO"))
    i_umid = next(i for i, c in enumerate(cabecalho) if c.startswith("UMIDADE RELATIVA DO AR, HORARIA"))
    fim = INICIO + dt.timedelta(hours=HORAS)

    horas, faltando = [], 0
    for linha in linhas[9:]:
        c = linha.split(";")
        momento = dt.datetime.strptime(f"{c[0]} {c[1][:4]}", "%Y/%m/%d %H%M").replace(tzinfo=dt.timezone.utc)
        if not INICIO <= momento < fim:
            continue
        temperatura, umidade = numero(c[i_temp]), numero(c[i_umid])
        if temperatura is None or umidade is None:
            faltando += 1
            continue
        horas.append({"momento": momento.strftime("%Y-%m-%dT%H:%M:%SZ"), "temperaturaC": temperatura, "umidadePct": umidade})

    return {
        "fonte": "INMET, Banco de Dados Meteorológicos (dados públicos)",
        "url": entrada["url"],
        "arquivoOriginal": entrada["arquivoOriginal"],
        "sha256Csv": sha,
        "estacao": "A701 · São Paulo – Mirante de Santana (estação a céu aberto)",
        "janela": {"de": INICIO.strftime("%Y-%m-%dT%H:%M:%SZ"), "horas": HORAS},
        "horasFaltando": faltando,
        "regra": "-9999 ou vazio não é medição: a hora fica de fora",
        "horas": horas,
    }


def main() -> int:
    texto = json.dumps(extrair(), ensure_ascii=False, indent=1) + "\n"
    if "--conferir" in sys.argv[1:]:
        if not SAIDA.exists() or SAIDA.read_text(encoding="utf-8") != texto:
            print(f"{SAIDA.relative_to(RAIZ)} desatualizado: rode mobile/scripts/extrair_replay_inmet.py")
            return 1
        print("replay do INMET confere com o CSV e o manifesto")
        return 0
    SAIDA.write_text(texto, encoding="utf-8")
    dados = json.loads(texto)
    print(f"{len(dados['horas'])} horas gravadas em {SAIDA.relative_to(RAIZ)} ({dados['horasFaltando']} faltando)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
