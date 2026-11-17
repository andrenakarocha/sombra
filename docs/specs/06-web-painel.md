# Spec 06 — Painel Web (operador + Dashboard Executivo de Risco Cidadão)

> **Fase:** F7 (polimento na F11.6) · **Pasta:** `web/painel/` · **Checkpoints:** `PLANO.md` §8.8
> **Atende:** **Cyber** (Dashboard Executivo, 2 pts) · **OS** (a "aplicação Web CitizenTech" hospedada na VM) · **pitch** (as telas mais bonitas da demo) · a "página web" que o brief pede no protótipo

## Objetivo
Uma central para quem acompanha muitas casas (operador de UBS/CRAS, agente comunitário de saúde) e para a gestão: alertas ao vivo, indicador de impacto, saúde da operação e o **farol de Risco Cidadão**, com **privacidade por padrão** (só aparece dado agregado por bairro, e PII sempre mascarada).

## Stack
Vite · React 19 · TypeScript · Tailwind v4 (tema dos tokens) · TanStack Router + Query · `@microsoft/signalr` · Recharts · (opcional) MapLibre com mapa por bairro. É um build estático servido pelo nginx da VM.

## Páginas
| Página | Conteúdo |
|---|---|
| **Visão geral** | KPIs: casas monitoradas, alertas ativos, **tempo mediano até o "estou bem"** (indicador de impacto), escalonamentos hoje por degrau, **alarmes desnecessários para a família** (indicador da entrevista), Sentinelas offline. Previsão de calor de 24 h por região. Feed de alertas ao vivo |
| **Alertas** | lista filtrável + linha do tempo de cada alerta (aberto → aguardando → escalonado → resolvido) |
| **Casas** | lista com PII mascarada (`***.456.***-00`) e nível atual |
| **Risco Cidadão** (Cyber) | **farol global** verde/amarelo/vermelho + farol por categoria (saúde, identificação, contato, credenciais). Feed de eventos de segurança com a **tradução da IA** para o cidadão e a **sugestão de correção** para o time. Série temporal de bloqueios por sink (log, resposta HTTP, saída externa). Botão "ver evidência técnica" |
| **Operação** | status dos serviços (`/health`), versão de cada um, alertas de CPU, RAM e disco vindos do `sombra-monitor` da VM |

## Checkpoints
A ordem de execução e os 🔎 ficam no `PLANO.md` (fase indicada no topo desta spec).

## Nível HENRY (F11)
Transições suaves no farol · gráficos com a paleta do Sombra · modo apresentação (tela cheia, fontes maiores, para gravar o pitch) · mapa de calor por bairro (agregado, sem endereços).

## Defesa
Por que o painel não mostra endereço nem nome completo · como o SignalR atualiza a tela sem polling · de onde vem cada número do indicador de impacto.
