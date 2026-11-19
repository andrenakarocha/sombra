# Painel web

> Central do operador e Dashboard Executivo de Risco Cidadão · fase **F7** do [plano](../../docs/PLANO.md) ·
> spec [`06-web-painel.md`](../../docs/specs/06-web-painel.md)

Vite · React 19 · TypeScript · Tailwind v4 com os tokens da direção de arte (`design/tokens.css`) · TanStack Router e
Query · SignalR. Build estático: na VM e no compose, o nginx serve o `dist/` e repassa `/api` e `/hubs` ao Core.

```bash
npm install
npm run dev        # http://localhost:5173, com o Core do make up em :5080 (o Vite faz o proxy de /api e /hubs)
npm run build      # tipos + build em dist/
npm test           # Vitest
```

## E2E e evidências

Com o ambiente no ar (`make up`, que já sobe o painel em `http://localhost:5173` pelo nginx):

```bash
make e2e-painel          # Playwright: login, recusa da família, roteiro com o simulador, casas sem PII,
                         # Risco Cidadão e operação mudando ao vivo
make evidencias-painel   # regrava docs/evidencias/ com as cinco páginas em 1440, 768 e 390 px
```

O E2E faz um login do operador pela API e reaproveita o token: o Core limita o login a 5 por minuto (proteção contra
força bruta). As credenciais da demo vêm do `.env` da raiz, nunca do código.

| Página | Evidência |
|---|---|
| Visão geral (KPIs, indicador de impacto, feed ao vivo) | [1440](docs/evidencias/visao-geral-1440.png) · [768](docs/evidencias/visao-geral-768.png) · [390](docs/evidencias/visao-geral-390.png) |
| Alertas (filtros e linha do tempo dos degraus) | [1440](docs/evidencias/alertas-1440.png) · [768](docs/evidencias/alertas-768.png) · [390](docs/evidencias/alertas-390.png) |
| Casas (sem dado pessoal) | [1440](docs/evidencias/casas-1440.png) · [768](docs/evidencias/casas-768.png) · [390](docs/evidencias/casas-390.png) |
| Risco Cidadão (Dashboard Executivo do Cyber) | [1440](docs/evidencias/risco-cidadao-1440.png) · [768](docs/evidencias/risco-cidadao-768.png) · [390](docs/evidencias/risco-cidadao-390.png) |
| Operação (saúde, versões, alertas da VM) | [1440](docs/evidencias/operacao-1440.png) · [768](docs/evidencias/operacao-768.png) · [390](docs/evidencias/operacao-390.png) |
