#!/usr/bin/env python3
"""Baixa os dados históricos horários do INMET e guarda só a estação A701 (São Paulo – Mirante de Santana).

Fonte: INMET, Banco de Dados Meteorológicos, "Dados históricos" (um ZIP por ano com todas as estações automáticas):
https://portal.inmet.gov.br/dadoshistoricos

- Os ZIPs ficam em cache fora do Git (ml/dados/cache/, ignorado); só o CSV da A701 de cada ano é versionado.
- Extrai o membro por nome, sem confiar no caminho dentro do ZIP (evita zip slip).
- Registra o SHA-256 de cada ZIP e de cada CSV em ml/dados/brutos/manifesto.json. Rodar de novo confere tudo contra
  o manifesto e só baixa o que falta.
- Confere as colunas esperadas e conta os -9999 (dado faltante) de temperatura e umidade.

Uso:
    python3 -I ml/scripts/baixar_dados.py                 # 2010 a 2025
    python3 -I ml/scripts/baixar_dados.py --anos 2023 2024
    python3 -I ml/scripts/baixar_dados.py --conferir      # só confere os CSVs contra o manifesto
"""

import argparse
import datetime
import hashlib
import io
import json
import pathlib
import sys
import time
import urllib.request
import zipfile

ESTACAO = "A701"
URL = "https://portal.inmet.gov.br/uploads/dadoshistoricos/{ano}.zip"
ML = pathlib.Path(__file__).resolve().parents[1]
CACHE = ML / "dados" / "cache"
BRUTOS = ML / "dados" / "brutos"
MANIFESTO = BRUTOS / "manifesto.json"
COLUNA_TEMPERATURA = "TEMPERATURA DO AR - BULBO SECO, HORARIA"
COLUNA_UMIDADE = "UMIDADE RELATIVA DO AR, HORARIA"


def sha256(dados: bytes) -> str:
    return hashlib.sha256(dados).hexdigest()


def baixar(ano: int) -> bytes:
    destino = CACHE / f"{ano}.zip"
    if destino.exists():
        return destino.read_bytes()
    pedido = urllib.request.Request(URL.format(ano=ano), headers={"User-Agent": "sombra-ml/1.0"})
    for tentativa in range(1, 4):
        try:
            with urllib.request.urlopen(pedido, timeout=120) as resposta:
                dados = resposta.read()
            break
        except OSError as erro:
            print(f"  {ano}: tentativa {tentativa} falhou ({erro})", file=sys.stderr)
            if tentativa == 3:
                raise
            time.sleep(5 * tentativa)
    CACHE.mkdir(parents=True, exist_ok=True)
    temporario = destino.with_suffix(".parcial")
    temporario.write_bytes(dados)
    temporario.replace(destino)
    return dados


def extrair_a701(ano: int, dados_zip: bytes) -> tuple[str, bytes]:
    with zipfile.ZipFile(io.BytesIO(dados_zip)) as arquivo:
        candidatos = [n for n in arquivo.namelist() if f"_{ESTACAO}_" in n.upper() and n.upper().endswith(".CSV")]
        if len(candidatos) != 1:
            raise RuntimeError(f"{ano}: esperava 1 arquivo da {ESTACAO}, achei {len(candidatos)}")
        nome = candidatos[0]
        return pathlib.PurePosixPath(nome).name, arquivo.read(nome)


def inspecionar(csv: bytes) -> dict:
    linhas = csv.decode("latin-1").splitlines()
    cabecalho_idx = next(i for i, l in enumerate(linhas[:15]) if "HORA" in l.upper() and ";" in l)
    colunas = [c.strip() for c in linhas[cabecalho_idx].split(";")]
    idx_t = next(i for i, c in enumerate(colunas) if COLUNA_TEMPERATURA in c.upper())
    idx_u = next(i for i, c in enumerate(colunas) if COLUNA_UMIDADE in c.upper())
    registros = [l.split(";") for l in linhas[cabecalho_idx + 1:] if l.strip()]
    faltando = lambda i: sum(1 for r in registros if len(r) <= i or r[i].strip() in ("-9999", "", "-9999,0"))  # noqa: E731
    return {
        "metadados": {l.split(":", 1)[0].strip(): l.split(":", 1)[1].strip(";").strip() for l in linhas[:cabecalho_idx]
                      if ":" in l},
        "horas": len(registros),
        "faltandoTemperatura": faltando(idx_t),
        "faltandoUmidade": faltando(idx_u),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--anos", type=int, nargs="+", default=list(range(2010, 2026)))
    parser.add_argument("--conferir", action="store_true")
    args = parser.parse_args()

    manifesto = json.loads(MANIFESTO.read_text(encoding="utf-8")) if MANIFESTO.exists() else {"arquivos": {}}
    BRUTOS.mkdir(parents=True, exist_ok=True)
    falhas = 0
    for ano in args.anos:
        csv_destino = BRUTOS / f"{ESTACAO}_{ano}.csv"
        registro = manifesto["arquivos"].get(str(ano))
        if args.conferir or (registro and csv_destino.exists()):
            if not registro or not csv_destino.exists() or sha256(csv_destino.read_bytes()) != registro["sha256Csv"]:
                print(f"{ano}: FALHA, CSV ausente ou diferente do manifesto")
                falhas += 1
            else:
                print(f"{ano}: ok (confere com o manifesto)")
            continue
        dados_zip = baixar(ano)
        nome_original, csv = extrair_a701(ano, dados_zip)
        info = inspecionar(csv)
        csv_destino.write_bytes(csv)
        manifesto["arquivos"][str(ano)] = {
            "url": URL.format(ano=ano),
            "arquivoOriginal": nome_original,
            "sha256Zip": sha256(dados_zip),
            "sha256Csv": sha256(csv),
            "baixadoEm": datetime.datetime.now(datetime.UTC).isoformat(timespec="seconds"),
            **info,
        }
        print(f"{ano}: {info['horas']} horas, -9999 em temperatura: {info['faltandoTemperatura']}, "
              f"em umidade: {info['faltandoUmidade']}")
        MANIFESTO.write_text(json.dumps(manifesto, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return 1 if falhas else 0


if __name__ == "__main__":
    sys.exit(main())
