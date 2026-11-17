# Spec 03 — IA & Machine Learning · Previsão de calor perigoso em 24 h

> **Fase:** F4 🤝 (ONNX no Clima na F9.1, polimento na F11.3, vídeo na F13.6) · **Pasta:** `ml/` · **Checkpoints:** `PLANO.md` §8.5
> **Entrega:** links do repositório e do vídeo (≤ 5 min, não listado) · **Dados simulados não são aceitos**

## A pergunta de ML
> **"Com o que a estação do INMET mediu nas últimas 24 horas, o índice de calor em São Paulo vai chegar ao nível de perigo nas próximas 24 horas?"**

Classificação binária. Um falso negativo (não avisar num dia perigoso) custa muito mais que um falso positivo, então a métrica principal é o **recall** com precisão aceitável (F2 / PR-AUC).

## Dados (reais e públicos)
- **INMET**, dados históricos horários (portal.inmet.gov.br/dadoshistoricos), estação **A701 – São Paulo/Mirante de Santana**, **2010–2025**. Colunas: temperatura do bulbo seco, umidade relativa, ponto de orvalho, pressão, radiação global, vento e precipitação, em UTC.
- Opcional, para robustez: A771 (Interlagos) como validação geográfica.
- `ml/scripts/baixar_dados.py` baixa os ZIPs anuais, extrai **só a A701**, verifica checksum e grava em `ml/dados/brutos/`. O `ml/dados/README.md` documenta **fonte, link, período, licença, colunas, fuso e o código `-9999` de dado faltante**.

## Pipeline do notebook (`ml/notebooks/sombra_calor.ipynb`, entregue executado)
1. **Problema e dados:** contexto do cidadão, a pergunta, a fonte, o período, o volume.
2. **Limpeza:** `-9999` → NaN, horário UTC → America/Sao_Paulo, buracos curtos interpolados (≤ 3 h) e dias com > 25% faltando descartados (com contagem documentada).
3. **Alvo:** índice de calor horário (Rothfusz, os mesmos vetores de teste do resto do sistema). `alvo = 1` se o **máximo das próximas 24 h ≥ limiar**. O limiar é definido na EDA, ancorado na tabela NOAA/NWS (≥ 32 °C, "cautela extrema", ou ≥ 39 °C, "perigo", conforme o balanceamento real), e justificado.
4. **Features:** médias, máximos e tendências das últimas 3/6/12/24 h, deltas, ponto de orvalho, radiação acumulada, pressão (variação), hora e dia do ano em seno/cosseno. Uma amostra **a cada 6 h** (00/06/12/18 h) para reduzir autocorrelação.
5. **Divisão temporal, sem embaralhar:** treino **2010–2021**, teste **2022–2025** (inclui as ondas de calor recordes de 2023–2024, que o modelo nunca viu).
6. **Modelos:** baseline de **persistência** ("amanhã igual a hoje") + **Regressão Logística** como referência + **XGBoost** (modelo principal).
   - Por que XGBoost: dados tabulares, relações não lineares entre temperatura, umidade e radiação, lida com faltantes, rápido e exportável para ONNX.
7. **Treino com hiperparâmetros:** `GridSearchCV` com **`TimeSeriesSplit(5)` só no treino**, 3 hiperparâmetros × **12 combinações**: `max_depth ∈ {3, 5, 7}` × `learning_rate ∈ {0,03; 0,1}` × `n_estimators ∈ {200, 500}`. Scoring `average_precision`. Tabela com as 12 combinações e o desvio de cada uma.
8. **Resultados em treino × teste, lado a lado:** precisão, recall, F1, F2, ROC-AUC, PR-AUC e Brier. Matrizes de confusão, curvas ROC/PR, curva de aprendizado e **discussão de overfitting** (gap treino-teste, efeito do `max_depth`).
9. **Limiar de decisão** escolhido no **treino** (validação temporal) para recall ≥ 0,9, depois aplicado uma vez no teste.
10. **Interpretação:** importância das features e SHAP ("o que faz o modelo soar o alarme").
11. **Conclusão para o cidadão:** *"Entre 2022 e 2025, de cada 10 dias perigosos, o Sombra teria avisado a família de X com até 24 h de antecedência, ao custo de Y alarmes falsos por mês."*
12. **Produção:** exportar `ml/modelo/modelo-calor-v1.onnx` + `features.json` (ordem, janelas, unidades) + `paridade.csv` (20 amostras com a probabilidade Python), consumidos pelo Clima Service.

## Rubrica → como

| Critério | Pts | Como |
|---|---|---|
| Problema e dados reais | 2,0 | pergunta ligada ao cidadão + INMET A701 com fonte, link e período documentados |
| Escolha justificada do modelo | 1,0 | XGBoost × baselines, com argumentos e números |
| Preparação e divisão treino/teste | 2,0 | limpeza documentada, features sem vazamento, divisão temporal |
| Treino com diferentes parâmetros | 2,0 | 12 combinações de 3 hiperparâmetros, `TimeSeriesSplit` só no treino |
| Resultados em treino e teste | 2,0 | métricas lado a lado + gráficos + overfitting |
| Conclusão e leitura para o cidadão | 1,0 | a frase de impacto + limitações |

## Entregáveis
- [ ] `ml/notebooks/sombra_calor.ipynb` **executado**, com saídas (+ export HTML)
- [ ] `ml/dados/README.md` · `ml/requirements.txt` (versões fixadas) · `ml/scripts/baixar_dados.py`
- [ ] `ml/RELATORIO_ML.md` (2 a 4 páginas: problema → dados → modelo → treino → resultados → conclusão → limitações, como a diferença INMET × Open-Meteo em produção)
- [ ] `ml/modelo/` (ONNX + features + paridade) · `ml/figuras/`
- [ ] testes das funções de preparação (`pytest`): sem vazamento de futuro, `-9999` tratado
- [ ] vídeo ≤ 5 min (spec 10)

## Nível HENRY (F11)
Figuras com os tokens de design do Sombra · comparação com A771 (o modelo generaliza para outro bairro?) · "replay" da onda de calor de nov/2023 mostrando a probabilidade subindo hora a hora (o mesmo gráfico vai para o pitch).

## Defesa
Por que não embaralhar os dados · o que é vazamento de futuro e como a gente evitou · por que validação cruzada só no treino · por que recall acima de precisão · o que o SHAP mostrou.
