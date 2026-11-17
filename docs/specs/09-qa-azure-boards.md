# Spec 09 — Testing, Compliance & QA · Azure Boards

> **Fase:** F10 (🤝 F10.2–F10.3) · **Pasta:** `qa/` · **Checkpoints:** `PLANO.md` §8.11 · 🔑 conta do Azure DevOps
> **Entrega:** **PDF** com o **link do projeto no Azure** + prints de todas as telas de Features e de Test Cases · **professor como Admin** no projeto · **link do pitch (≤ 4 min)** nesta mesma tarefa
> **Avaliação:** Features **50%** · Test Cases **50%**

## Objetivo
Descrever o Sombra como **Features com critérios de aceite** e cobrir a homologação com **Test Cases caixa-preta, positivos e negativos**, todos no Azure Boards. Os casos ficam **rastreáveis até os testes automatizados** do código.

## Fonte da verdade como código
- `qa/especificacao/features.yaml` e `qa/especificacao/casos-de-teste.yaml`, revisados por PR.
- `qa/ferramentas/sincronizar_azure.py` usa a API REST do Azure DevOps com PAT (via variável de ambiente). Cria ou atualiza o Epic → Features → Test Cases, preenche os passos (`Microsoft.VSTS.TCM.Steps`), vincula com "Tested By" e é **idempotente** (mapa YAML ↔ ID guardado no repositório).
- `qa/ferramentas/gerar_pdf.py` monta o HTML (Features + Test Cases + matriz de rastreabilidade + prints) e gera o PDF com Chrome headless.

## Padrão (D6: confirmar com o material da aula)
- **Feature:** título · descrição *"Como <persona>, quero <ação>, para <benefício>"* · **critérios de aceite** em *Dado / Quando / Então* · prioridade · valor de negócio · tags da disciplina.
- **Test Case:** título · **objetivo** · **pré-condições (preparação)** · **dados de entrada** · **passos** (ação → resultado esperado) · **resultado esperado final** · tipo (positivo/negativo) · técnica (partição de equivalência, valor-limite, tabela de decisão, transição de estados) · vínculo com a Feature · ID do teste automatizado correspondente.

## Features (rascunho)
| ID | Feature | Persona |
|---|---|---|
| F01 | Monitoramento térmico da casa pela Sentinela | idoso / família |
| F02 | Previsão de calor perigoso para as próximas 24 h | família / operador |
| F03 | Alerta de calor com "estou bem" | idoso |
| F04 | Escalonamento em degraus para a rede de cuidado | família / vizinho |
| F05 | Pedido de ajuda | idoso |
| F06 | Cadastro do morador e da rede de cuidado | família |
| F07 | Pontos de resfriamento perto de casa | família |
| F08 | Modo Idoso acessível | idoso |
| F09 | Proteção de dados pessoais (PII / LGPD) | todos |
| F10 | Dashboard de Risco Cidadão | gestão / cidadão |
| F11 | Painel operacional e saúde da infraestrutura | operador |
| F12 | Check-in diário e lembrete de água | idoso / família |

## Test Cases (meta: 40 a 50)
Para cada Feature, de 3 a 5 casos, misturando positivos e **negativos**. Exemplos:
- **F03 · TC positivo:** índice de calor 40 °C → LED vermelho, OLED pede o check-in, botão apertado → alerta "Confirmado" em < 2 s no app.
- **F03 · TC negativo (valor-limite):** índice de calor 38,9 °C → **não** abre alerta de perigo, só "cautela extrema".
- **F04 · TC de transição de estados:** prazo vencido → lembrete leve à filha. Sem reconhecimento e em `PERIGO` →
  vizinha ("quem está perto"). Um alerta já resolvido **não** escalona.
- **F12 · TC negativo:** esquecer o check-in diário num dia sem risco → só lembrete leve, **nunca** alerta de emergência.
- **F06 · TC negativo:** CPF com dígito verificador errado → 422 com a mensagem clara.
- **F09 · TC negativo:** CPF em "observações" → aparece mascarado na consulta e no log.
- **F08 · TC:** fonte do sistema em 200% → nenhum texto cortado no Modo Idoso.

## Rastreabilidade (o diferencial)
Cada Test Case aponta para o teste automatizado que o executa: xUnit (Core), JUnit (Clima), Playwright (painel), Jest (app), cenário do simulador (Sentinela) ou cenário da auditoria (Cyber). O PDF traz a **matriz Feature × Test Case × teste automatizado × status no CI**.

## Checkpoints
A ordem de execução e os 🔎 ficam no `PLANO.md` (fase indicada no topo desta spec).

## Nível HENRY (F11)
Execução registrada dos casos (Test Runs) com o resultado · capa e diagramação com a identidade do Sombra · resumo de cobertura por Feature.

## Defesa
Diferença entre critério de aceite e caso de teste · por que casos negativos e de valor-limite · o que é caixa-preta · como a matriz liga a necessidade do cidadão ao código.
