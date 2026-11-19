# Relatório de Machine Learning: calor nas próximas 24 horas

**GS 2026.2 · CitizenTech Sombra** · André Nakamatsu Rocha (RM555004) · João Marcelo Furtado Romero (RM555199) · Matheus
Rivera Montovaneli (RM555499)

## 1. Problema

Em ondas de calor, a pessoa idosa que mora sozinha sente menos sede, muitas vezes toma remédios que pioram a desidratação
e raramente percebe o perigo dentro de casa. O Sombra quer avisar a família **antes**. A pergunta de ML é:

> Com o que a estação do INMET mediu nas últimas 24 horas, o índice de calor em São Paulo vai alcançar 32,22 °C nas próximas
> 24 horas?

É uma classificação binária. Deixar de avisar custa mais do que um aviso a mais, mas o custo do alarme falso é real: na
entrevista, a família pediu para não receber alerta à toa.

## 2. Dados

Usamos as medições horárias reais da estação automática **A701, São Paulo – Mirante de Santana**, do
[INMET](https://portal.inmet.gov.br/dadoshistoricos), de 2010 a 2025: **140.256 horas**. Cada arquivo é conferido por
SHA-256. Fonte, colunas e regras estão em [`dados/README.md`](dados/README.md).

**Limpeza.** O código `-9999` vira ausente, o horário UTC é convertido para `America/Sao_Paulo`, buracos de até 3 h em
grandezas contínuas são interpolados (160 horas) e dias com mais de 25% faltando são descartados (40 dias).

**Alvo.** O índice de calor segue a equação da NWS, com os mesmos vetores de teste de todo o sistema. O alvo é 1 quando o
máximo das 24 h seguintes alcança **32,22 °C** (`CAUTELA_EXTREMA`). O nível `PERIGO` (39,44 °C) não era uma opção: o maior
índice medido em 16 anos foi **35,47 °C**. Há 2,5% de positivos no treino e 5,3% no teste.

**Features.** São 63, todas calculadas só com a hora atual e as anteriores, em janelas de até 24 h, e só com o que o
Clima Service tem em produção: temperatura, umidade, pressão, radiação, vento e chuva. O ponto de orvalho é calculado, com
erro médio de 0,07 °C em relação ao medido.

## 3. Como evitamos enganar a nós mesmos

- **Divisão temporal, sem embaralhar:** treino de 2010 a 2021 (17.365 amostras) e teste de 2022 a 2025 (5.702).
- **Folga de 24 h** entre treino e teste e entre as dobras da validação, porque o alvo olha 24 h à frente.
- **Hora interpolada não vira amostra**, para nenhuma interpolação usar valor posterior.
- Testes automatizados provam que **mudar o futuro não altera as features** e mudar o passado não altera o alvo.
- Hiperparâmetros, regularização e limiar foram escolhidos **pelas métricas da validação no treino**, e o notebook só usa o
  teste na avaliação final.
- **Ressalva:** um protótipo inicial, com regularização mais fraca, chegou a ser medido no teste (PR-AUC de 0,560) antes de
  a regularização ser ajustada. A escolha seguiu a validação, mas o teste não ficou totalmente intocado. O resultado final
  não superou a persistência, o que limita o efeito prático desse contato.

## 4. Modelo e treino

Comparamos três abordagens: **persistência** ("amanhã será como hoje"), **regressão logística** e **XGBoost**. O XGBoost foi
escolhido como modelo principal por tratar relações não lineares, aceitar dados ausentes, entregar probabilidade e
exportar para ONNX, o que permite servi-lo dentro do serviço em Java.

A busca usou `GridSearchCV` com `TimeSeriesSplit(5)` só no treino, em **12 combinações** de `max_depth` (3, 5, 7),
`learning_rate` (0,03; 0,1) e `n_estimators` (200, 500), otimizando a PR-AUC. A melhor foi `max_depth=3`,
`learning_rate=0,03` e `n_estimators=200`, com **PR-AUC de 0,332 ± 0,065** na validação. A tabela completa está em
[`modelo/busca_hiperparametros.csv`](modelo/busca_hiperparametros.csv).

O limiar de decisão (0,010) foi o maior que alcançou **recall ≥ 0,90** nas previsões fora da amostra do treino.

## 5. Resultados

| XGBoost | Precisão | Recall | F1 | F2 | ROC-AUC | PR-AUC | Brier |
|---|---|---|---|---|---|---|---|
| Treino | 0,114 | 1,000 | 0,205 | 0,392 | 0,980 | 0,658 | 0,014 |
| Validação temporal | 0,119 | 0,900 | 0,210 | 0,389 | 0,921 | 0,292 | 0,023 |
| **Teste 2022–2025** | **0,208** | **0,970** | **0,342** | **0,559** | **0,960** | **0,588** | **0,034** |

| No teste | Precisão | Recall | ROC-AUC | PR-AUC |
|---|---|---|---|---|
| Persistência, regra direta | 0,583 | 0,571 | 0,952 | 0,575 |
| Persistência, limiar para recall 0,90 | 0,225 | 0,944 | 0,952 | 0,575 |
| Regressão logística | 0,179 | 0,980 | 0,958 | 0,582 |
| **XGBoost** | **0,208** | **0,970** | **0,960** | **0,588** |

**O XGBoost não superou os baselines de forma conclusiva.** A diferença de PR-AUC para a persistência é de +0,013, com
intervalo de confiança de 95% de −0,099 a +0,114, calculado por blocos mensais. O pico das últimas 24 h já concentra quase
toda a informação que uma única estação oferece. A persistência, com o mesmo critério de recall, teve até precisão um
pouco maior. O ganho claro do XGBoost apareceu ao prever o **valor** do pico: erro médio de **1,65 °C**, contra **2,21 °C**
da persistência.

**Overfitting.** A PR-AUC cai de 0,658 no treino para 0,292 na validação, e aumentar a profundidade eleva o treino sem
melhorar a validação (figura `06_overfitting.png`). Por isso a busca ficou com as árvores mais rasas. A PR-AUC maior no
teste vem da frequência maior de positivos, não de um modelo melhor.

| Na prática, no teste (75 dias de calor) | Dias avisados | Dias com alarme falso por mês |
|---|---|---|
| Persistência, regra direta | 43 | 0,9 |
| Persistência, recall 0,90 | 74 | 6,6 |
| Regressão logística, recall 0,90 | 75 | 9,5 |
| **XGBoost, recall 0,90** | **74** | **7,8** |
| XGBoost, recall 0,80 | 72 | 4,9 |
| XGBoost, recall 0,70 | 71 | 3,7 |

**O que move o modelo (SHAP):** o pico recente do índice de calor, noites que não esfriam (temperatura mínima alta), a
época do ano e a pressão atmosférica.

## 6. Conclusão para o cidadão

> **Entre 2022 e 2025, de cada 10 dias de calor forte em São Paulo, o Sombra teria avisado a família de 9,9 com até 24 horas
> de antecedência, ao custo de 7,8 dias com alarme falso por mês.**

Esse número de alarmes é alto demais para ir direto à família. Por isso, a probabilidade sozinha não aciona a rede de
cuidado: no Sombra, o preventivo depende do pico previsto e do limite pessoal de quem mora na casa, e o escalonamento começa
por um lembrete leve. Na integração, o limiar de recall 0,70 é um candidato razoável: mantém 9,5 de cada 10 dias avisados
com menos da metade dos alarmes falsos.

## 7. Limitações

- **Uma estação não é uma casa:** ela mede o ar externo, e o interior pode ser mais quente. Esse é o papel da Sentinela.
- **O alvo não é perigo clínico:** é o nível `CAUTELA_EXTREMA` da NWS, porque `PERIGO` nunca ocorreu na série.
- **Fonte diferente em produção:** o treino usa o INMET e a previsão ao vivo usa a Open-Meteo. Essa diferença não foi medida.
- **Clima em mudança:** o teste tem o dobro de positivos do treino, e as probabilidades ficaram abaixo da frequência real.
- **A mediana do pico subestima os dias de calor** (−2,19 °C em média neles). Para uma pessoa vulnerável, use a margem de
  +2,30 °C, e nunca menos que a persistência.
- **Só uma estação:** a generalização para outros bairros não foi testada.
- O Sombra **não é um dispositivo médico** e não substitui o SAMU nem a Defesa Civil.

## 8. Reprodutibilidade

`make -C ml setup test notebook` recria tudo com versões fixadas e semente definida. O modelo foi exportado para ONNX e
reproduz o XGBoost com diferença máxima de 4,8 × 10⁻⁷ nas amostras de paridade.
