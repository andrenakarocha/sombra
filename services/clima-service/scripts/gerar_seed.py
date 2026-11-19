#!/usr/bin/env python3
"""Gera a migração V4 (seed) do Clima a partir de dados reais e públicos, sem nada inventado.

- Observações: o CSV versionado da estação A701 (INMET, nov/2023, a onda de calor), conferido contra o SHA-256 do
  manifesto da F0.15. Linhas sem temperatura ou umidade (vazio ou -9999) ficam de fora, porque -9999 não é observação;
  nas grandezas opcionais, o -9999 vira NULL. O índice de calor e o nível saem da implementação de referência do
  contrato (tools/indice-de-calor/vetores.py), a mesma que os testes do Java conferem.
- Pontos de resfriamento: lugares públicos ou abertos ao público do OpenStreetMap (scripts/pontos-osm.json), com o
  horário que está no próprio OSM. `acessivel` só é verdadeiro quando o OSM confirma (wheelchair=yes).

Uso: python3 -I services/clima-service/scripts/gerar_seed.py
"""

from __future__ import annotations

import csv
import hashlib
import io
import json
import re
import sys
import uuid
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[3]
CSV_A701 = RAIZ / "ml/dados/brutos/A701_2023.csv"
MANIFESTO = RAIZ / "ml/dados/brutos/manifesto.json"
PONTOS_OSM = Path(__file__).with_name("pontos-osm.json")
SAIDA = RAIZ / "services/clima-service/src/main/resources/db/migration/V4__seed_mirante_nov_2023.sql"

sys.path.insert(0, str(RAIZ / "tools/indice-de-calor"))
import vetores  # noqa: E402  (implementação de referência do índice de calor)

MES = "2023/11/"
COLUNAS = {
    "temperatura": "TEMPERATURA DO AR - BULBO SECO, HORARIA (°C)",
    "umidade": "UMIDADE RELATIVA DO AR, HORARIA (%)",
    "pressao": "PRESSAO ATMOSFERICA AO NIVEL DA ESTACAO, HORARIA (mB)",
    "radiacao": "RADIACAO GLOBAL (Kj/m²)",
    "vento": "VENTO, VELOCIDADE HORARIA (m/s)",
    "precipitacao": "PRECIPITAÇÃO TOTAL, HORÁRIO (mm)",
}


def numero(texto: str) -> float | None:
    texto = (texto or "").strip()
    if not texto:
        return None
    valor = float(texto.replace(",", "."))
    return None if valor == -9999.0 else valor


def sql(valor: float | None) -> str:
    return "NULL" if valor is None else repr(round(valor, 2))


def texto_sql(valor: str) -> str:
    return "'" + valor.replace("'", "''") + "'"


def observacoes() -> tuple[list[str], int, str]:
    bruto = CSV_A701.read_bytes()
    sha = hashlib.sha256(bruto).hexdigest()
    esperado = json.loads(MANIFESTO.read_text(encoding="utf-8"))["arquivos"]["2023"]["sha256Csv"]
    if sha != esperado:
        sys.exit(f"o CSV da A701 não bate com o manifesto ({sha} != {esperado})")
    linhas = bruto.decode("latin-1").splitlines()[8:]  # 8 linhas de metadados antes do cabeçalho
    valores, descartadas = [], 0
    for linha in csv.DictReader(io.StringIO("\n".join(linhas)), delimiter=";"):
        if not linha["Data"].startswith(MES):
            continue
        t, ur = numero(linha[COLUNAS["temperatura"]]), numero(linha[COLUNAS["umidade"]])
        if t is None or ur is None:
            descartadas += 1
            continue
        hora = linha["Hora UTC"].split()[0]
        momento = f"{linha['Data'].replace('/', '-')}T{hora[:2]}:{hora[2:]}:00Z"
        hi = round(vetores.indice_de_calor_c(t, ur), 2)
        valores.append(
            f"('A701', '{momento}', {sql(t)}, {sql(ur)}, {sql(numero(linha[COLUNAS['pressao']]))}, "
            f"{sql(numero(linha[COLUNAS['radiacao']]))}, {sql(numero(linha[COLUNAS['vento']]))}, "
            f"{sql(numero(linha[COLUNAS['precipitacao']]))}, {hi}, '{vetores.nivel(hi)}', 'INMET')"
        )
    return valores, descartadas, sha


def pontos() -> tuple[list[str], str]:
    dados = json.loads(PONTOS_OSM.read_text(encoding="utf-8"))
    valores = []
    for p in dados["pontos"]:
        abre, fecha = re.search(r"(\d\d:\d\d)\s*-\s*(\d\d:\d\d)", p["opening_hours"]).groups()
        ident = uuid.uuid5(uuid.NAMESPACE_URL, f"https://www.openstreetmap.org/{p['osm']}")
        fonte = f"OpenStreetMap (ODbL), {p['osm']}; horário no OSM: {p['opening_hours']}"
        valores.append(
            f"('{ident}', {texto_sql(p['nome'])}, '{p['tipo']}', {p['lat']}, {p['lon']}, "
            f"{'TRUE' if p.get('wheelchair') == 'yes' else 'FALSE'}, '{abre}', '{fecha}', {texto_sql(fonte)})"
        )
    return valores, dados["dadosDoOsmEm"]


def main() -> None:
    obs, descartadas, sha = observacoes()
    pts, osm_em = pontos()
    SAIDA.write_text(
        f"""-- V4 · seed com dados reais e públicos (gerado por services/clima-service/scripts/gerar_seed.py: não edite à mão).
--
-- Estação A701 (São Paulo – Mirante de Santana), INMET, Banco de Dados Meteorológicos:
--   https://portal.inmet.gov.br/uploads/dadoshistoricos/2023.zip → ml/dados/brutos/A701_2023.csv (sha256 {sha}).
--   Novembro de 2023, a onda de calor: {len(obs)} horas; {descartadas} hora(s) sem temperatura ou umidade ficaram de fora
--   (-9999 não é observação). Hora em UTC, como no CSV.
-- Pontos de resfriamento: OpenStreetMap, © colaboradores do OpenStreetMap, licença ODbL, dados de {osm_em}.

INSERT INTO estacao (codigo, nome, latitude, longitude, altitude_m)
VALUES ('A701', 'São Paulo – Mirante de Santana', -23.496389, -46.62, 785.64);

INSERT INTO regiao_climatica (codigo, nome, estacao_codigo)
VALUES ('SP-MIRANTE', 'Zona Norte de São Paulo (Mirante de Santana)', 'A701');

INSERT INTO observacao (estacao_codigo, observada_em, temperatura_c, umidade_pct, pressao_hpa, radiacao_kj_m2, vento_ms,
                        precipitacao_mm, indice_de_calor_c, nivel, fonte)
VALUES
{(","+chr(10)).join(obs)};

INSERT INTO ponto_de_resfriamento (id, nome, tipo, latitude, longitude, acessivel, abre, fecha, fonte)
VALUES
{(","+chr(10)).join(pts)};
""",
        encoding="utf-8",
    )
    print(f"{SAIDA.relative_to(RAIZ)}: {len(obs)} observações ({descartadas} descartadas) e {len(pts)} pontos")


if __name__ == "__main__":
    main()
