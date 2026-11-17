# Decisões de arquitetura (ADRs)

Cada ADR registra uma decisão que muda a forma do sistema: o contexto, a decisão, as consequências e as alternativas
descartadas. Decisão de produto e de processo fica no [plano](../PLANO.md#2-decisões).

| ADR | Decisão | Plano |
|---|---|---|
| [001](001-linguagem-ubiqua.md) | Linguagem ubíqua em português, sufixos técnicos em inglês | D11 |
| [002](002-monorepo.md) | Monorepo, uma pasta por parte | D2 |
| [003](003-dois-servicos-donos-dos-dados.md) | Dois serviços (Core em C#, Clima em Java), cada um dono do seu banco | D4 |
| [004](004-mqtt-publico-assinado.md) | MQTT em broker público, com mensagens assinadas e sem dado pessoal | — |
| [005](005-llm-com-fallback.md) | LLM para traduzir falhas, sem dado pessoal e com fallback | D7 |
| [006](006-indice-de-calor-replicado.md) | Fórmula do índice de calor replicada de propósito, com vetores compartilhados | — |

Um ADR novo ganha o próximo número. Um ADR não é editado depois de aceito: quando a decisão muda, um ADR novo o
substitui e o antigo passa a "substituído por".
