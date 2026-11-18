# Contratos

Tudo o que atravessa a fronteira entre duas partes do Sombra está aqui. **Mudou um contrato? Atualize este arquivo no
mesmo PR** (§0 do plano).

| Contrato | Entre | Arquivo | Como conferir |
|---|---|---|---|
| Níveis de risco e índice de calor | todos | [`niveis-de-risco.md`](niveis-de-risco.md) + [`indice-de-calor.vetores.json`](indice-de-calor.vetores.json) | `tools/indice-de-calor/vetores.py` |
| MQTT (AsyncAPI 3.1) | Sentinela ↔ Core | [`mqtt.asyncapi.yaml`](mqtt.asyncapi.yaml) + [`mqtt-assinatura.vetores.json`](mqtt-assinatura.vetores.json) | `npx @asyncapi/cli@6.3.1 validate` e `tools/mqtt-assinatura/vetores.py` |
| REST do Core | app, painel, monitor → Core | [`core-api.openapi.json`](core-api.openapi.json) (fonte da verdade, gerado pelo Core) + [`core-api.md`](core-api.md) (visão geral) | o teste de API falha se divergirem; `make -C services/core-api openapi` regenera |
| REST do Clima | Core → Clima | [`clima-service.md`](clima-service.md) (vira `clima-service.openapi.json` na F3.4) | teste de contrato no Core (F3.9) |
| Tempo real | Core → app, painel | [`signalr.md`](signalr.md) | testes de integração do Core |
| Vínculo da Sentinela (QR) | Sentinela → app → Core | [`vinculo-da-sentinela.vetores.json`](vinculo-da-sentinela.vetores.json) (`sombra://sentinela/vincular?v=1&d=…&c=…`) | `tools/vinculo/codigo.py --conferir` e os testes do domínio do Core |

## Portas e nomes

| Componente | Porta | Nome na VM | Banco |
|---|---|---|---|
| Core API | 5080 | `api.sombra.local` | `sombra_core` (papel `sombra_core`) |
| Clima Service | 8081 | `clima.sombra.local` | `sombra_clima` (papel `sombra_clima`) |
| Painel | 5173 em dev · 80 pelo nginx | `painel.sombra.local` | — |
| PostgreSQL 16 | 5432 no compose e na VM · 55432 no host do dev (`DB_PORTA_LOCAL`) | — | — |
| Mosquitto (dev e CI) | 1883 | — | — |
| VM | — | `sombra-srv01` · `192.168.56.10` | — |

## Variáveis de ambiente

A lista completa, com exemplos, está no [`.env.example`](../../.env.example) da raiz. Regras:
- segredo só no `.env`, que nunca vai para o Git (gitleaks no CI);
- o Core usa a convenção do .NET (`Secao__Chave`) e o Clima a do Spring (`SPRING_…` e `CLIMA_…`);
- o app usa `EXPO_PUBLIC_…` e o painel `VITE_…`, e **nada secreto** vai nesses dois, porque eles ficam visíveis no cliente.
