# Dados do modelo de previsão de calor

> **Dados reais e públicos.** A GS não aceita dados simulados no ML; nada aqui é inventado.

## Fonte

| Campo | Valor |
|---|---|
| **Instituição** | INMET (Instituto Nacional de Meteorologia), Banco de Dados Meteorológicos |
| **Página** | [portal.inmet.gov.br/dadoshistoricos](https://portal.inmet.gov.br/dadoshistoricos) |
| **Arquivo de origem** | um ZIP por ano com todas as estações automáticas: `https://portal.inmet.gov.br/uploads/dadoshistoricos/{ano}.zip` |
| **Estação** | **A701 · São Paulo – Mirante de Santana** (SE, SP), WMO A701, lat −23,4964, lon −46,6200, altitude 785,64 m, em operação desde 25/07/2006 |
| **Período** | 2010 a 2025, horário |
| **Uso** | dados públicos do INMET; a fonte é citada aqui, no notebook, no relatório e no vídeo |

## Como reproduzir

```bash
python3 -I ml/scripts/baixar_dados.py              # baixa 2010–2025 e extrai só a A701
python3 -I ml/scripts/baixar_dados.py --conferir   # confere os CSVs contra o manifesto (SHA-256)
```

- Os ZIPs (cerca de 100 MB por ano) ficam em `ml/dados/cache/`, fora do Git.
- Só o CSV da A701 de cada ano é versionado, em `brutos/A701_{ano}.csv`, **sem nenhuma alteração** (latin-1, `;`,
  vírgula decimal).
- [`brutos/manifesto.json`](brutos/manifesto.json) guarda, por ano, a URL, o nome do arquivo original dentro do ZIP, o
  SHA-256 do ZIP e do CSV, a data do download, os metadados da estação e a contagem de horas faltando.

## Formato dos CSVs

- 8 linhas de metadados (região, UF, estação, código WMO, latitude, longitude, altitude, data de fundação), depois o
  cabeçalho e uma linha por hora.
- **Horário em UTC** (coluna `Hora UTC`). O notebook converte para America/Sao_Paulo.
- **`-9999` significa dado faltante** (falha do sensor ou da transmissão): não é medição. O notebook trata como ausente
  (spec 03, passo 2), e a mesma regra vale no Clima Service ("`-9999` não é observação").
- O nome das colunas muda um pouco entre anos (por exemplo `DATA (YYYY-MM-DD)` nos anos antigos e `Data` nos recentes);
  o notebook normaliza.

Colunas usadas pelo modelo:

| Coluna no INMET | Unidade | Uso |
|---|---|---|
| `TEMPERATURA DO AR - BULBO SECO, HORARIA` | °C | índice de calor e features |
| `UMIDADE RELATIVA DO AR, HORARIA` | % | índice de calor e features |
| `TEMPERATURA DO PONTO DE ORVALHO` | °C | features |
| `PRESSAO ATMOSFERICA AO NIVEL DA ESTACAO, HORARIA` | mB | features (variação) |
| `RADIACAO GLOBAL` | kJ/m² | features (radiação acumulada) |
| `VENTO, VELOCIDADE HORARIA` | m/s | features |
| `PRECIPITAÇÃO TOTAL, HORÁRIO` | mm | features |

## Por ano

<!-- tabela gerada a partir de brutos/manifesto.json -->

| Ano | Horas | Sem temperatura | Sem umidade | SHA-256 do CSV |
|---|---|---|---|---|
| 2010 | 8.760 | 162 | 10 | `fd914734eb2c1d16…` |
| 2011 | 8.760 | 23 | 23 | `242da2ea7f7a1814…` |
| 2012 | 8.784 | 38 | 38 | `93a6f1406d709aa9…` |
| 2013 | 8.760 | 46 | 46 | `5f2a45f7be8129d3…` |
| 2014 | 8.760 | 204 | 204 | `6690c2d8364def66…` |
| 2015 | 8.760 | 0 | 0 | `a0c96324f7c0d534…` |
| 2016 | 8.784 | 67 | 67 | `9989f6f158c83059…` |
| 2017 | 8.760 | 73 | 73 | `1bac9171eb8660d5…` |
| 2018 | 8.760 | 0 | 0 | `1bec33fd03bd0179…` |
| 2019 | 8.760 | 0 | 0 | `51527c56c4ff94d0…` |
| 2020 | 8.784 | 1 | 1 | `aec112b14d90797d…` |
| 2021 | 8.760 | 1 | 2 | `815c01daf4a6da98…` |
| 2022 | 8.760 | 0 | 0 | `ceb3a4cde1596022…` |
| 2023 | 8.760 | 212 | 212 | `ae52e888aa15f03e…` |
| 2024 | 8.784 | 19 | 19 | `c5f365c069258734…` |
| 2025 | 8.760 | 47 | 47 | `1eac048271dd0339…` |
| **total** | **140.256** | **893 (0,64%)** | **742 (0,53%)** | |

"Sem" conta as horas com `-9999` ou vazias. O SHA-256 completo de cada CSV e de cada ZIP está no manifesto.
