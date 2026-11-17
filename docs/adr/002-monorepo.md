# ADR-002 · Monorepo, uma pasta por parte

**Status:** aceito · 2026-11-17 · decisão D2

## Contexto

A GS pede *"uma pasta para cada parte"* e a Ficha *"no início do README do repositório"*, no singular, e o ML pede uma
*"pasta `ml/`"*. As oito disciplinas constroem partes da **mesma** CitizenTech, que precisam conversar por contratos
comuns e rodar juntas numa demonstração só.

## Decisão

Um repositório público, `sombra`, com uma pasta por parte (`services/core-api`, `services/clima-service`, `mobile`,
`iot/sentinela`, `ml`, `infra`, `security`, `qa`, `web/painel`, `pitch`). Cada professor recebe o link direto da pasta
da sua disciplina. Contratos, design e plano ficam em `docs/` e `design/`, compartilhados.

## Consequências

- Um PR pode mudar um contrato e todos os lados dele juntos.
- O CI precisa de filtro por pasta para não rodar tudo em toda mudança, e de um check agregador (`ci-ok`) para o filtro
  não travar merges.
- O README de cada pasta precisa ser autossuficiente, porque é por ele que o professor entra.

## Alternativas consideradas

- **Um repositório por disciplina:** a Ficha se repetiria em sete lugares e a integração ficaria espalhada, contra o que
  a GS pede.
