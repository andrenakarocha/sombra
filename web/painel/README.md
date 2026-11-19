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
