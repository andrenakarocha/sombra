# Spec 05 — Mobile Development & IoT · App Sombra

> **Fase:** F6 🤝 (polimento na F11.5, vídeo na F13.6) · **Pasta:** `mobile/` · **Checkpoints:** `PLANO.md` §8.7
> **Entrega:** link do GitHub (pasta) com README (Ficha, como rodar, prints) + vídeo ≤ 3 min
> **Critérios:** telas e navegação · consumo de dados · banco de dados · acessibilidade · aderência ao problema · documentação
> **Observabilidade:** [`docs/observabilidade.md`](../observabilidade.md) §3 e §5 (código curto do erro na tela; nada de PII no console)

## Objetivo
A **porta de entrada** do Sombra no celular, com dois públicos:
- **Família e cuidador:** acompanha a casa, recebe o alerta e liga na hora.
- **Modo Idoso:** uma tela só, gigante, com o "ESTOU BEM", para quem tem celular mas não tem intimidade com ele.

## Stack
Expo SDK 54 · Expo Router 6 · TypeScript estrito · TanStack Query (API) · zustand (sessão) · **expo-sqlite** (banco local) · expo-secure-store (token) · `@microsoft/signalr` (tempo real) · expo-notifications (notificação local nos eventos) · **expo-speech** (o Modo Idoso fala) · expo-haptics · react-native-svg (gráficos).

## Telas e navegação
```
app/
├── (auth)/entrar.tsx
├── (familia)/_layout.tsx                 abas: Início · Alertas · Perto de mim · Privacidade · Ajustes
│   ├── index.tsx                         cards das casas: farol, temperatura, índice de calor, último "estou bem", Sentinela online
│   ├── casa/[id].tsx                     gráfico de 24 h, previsão, rede de cuidado, botão LIGAR
│   ├── casa/[id]/morador.tsx             cadastro e edição (CPF, Cartão SUS e saúde aparecem **mascarados**)
│   ├── casa/[id]/rotina.tsx              check-in diário e lembrete de água (horários)
│   ├── alertas/index.tsx · alertas/[id].tsx   linha do tempo do escalonamento + "Estou indo / Já liguei"
│   ├── perto.tsx                         pontos de resfriamento: distância, horário, acessível, "abrir rota"
│   └── privacidade.tsx                   **farol de Risco Cidadão** + mensagens traduzidas pela IA (Cyber)
└── (idoso)/index.tsx                     Modo Idoso: temperatura gigante + ESTOU BEM + PRECISO DE AJUDA + voz
```

## Dados
- **API:** só o Core (padrão BFF): REST via TanStack Query + SignalR para atualização ao vivo.
- **Banco local (expo-sqlite, com migrações versionadas):** cache das casas e alertas (abre offline), **fila de check-ins offline** (sincroniza quando volta a rede) e preferências de acessibilidade.
- **Modo demonstração:** se `EXPO_PUBLIC_API_URL` não responder, o app avisa e oferece dados de exemplo **embutidos**, para o professor conseguir abrir mesmo sem subir o backend.

## Notificações em degraus (D13, da entrevista)
A filha pediu: *"não queria receber um alerta de emergência toda vez que ela esquecesse de apertar um botão"*.
- **Lembrete leve** (som padrão, sem tela cheia): *"A Dona Lurdes ainda não confirmou que está bem. Quer ligar?"*
- **"Quem está perto"** (contato `Vizinho`): *"Pode dar uma olhada na Dona Lurdes?"*, com "Estou indo" e "Ela está bem".
- **Alerta** (som de alarme, tela cheia) só em perigo sem resposta ou pedido de ajuda.
- No Modo Idoso: uma notificação por vez, sempre dizendo o que acontece se ela não responder.

## Acessibilidade (checklist, com prints no README)
- [ ] `accessibilityLabel`, `accessibilityRole` e `accessibilityHint` em todo elemento tocável
- [ ] área de toque ≥ 48 dp (botões do Modo Idoso ≥ 120 dp)
- [ ] contraste ≥ 4,5:1, com **Atkinson Hyperlegible**
- [ ] respeita o tamanho de fonte do sistema (`allowFontScaling`) **+** controle de tamanho no app
- [ ] **o farol nunca depende só da cor** (cor + ícone + texto)
- [ ] testado com TalkBack/VoiceOver (vídeo curto de evidência)
- [ ] Modo Idoso lê a situação em voz alta e dá vibração ao confirmar
- [ ] respeita "reduzir movimento"

## Checkpoints
A ordem de execução e os 🔎 ficam no `PLANO.md` (fase indicada no topo desta spec).

## Nível HENRY (F11)
Micro-animações com Reanimated (o farol "respira" em alerta) · ilustrações vazias com a identidade do Sombra · tema escuro · ícone e splash próprios · onboarding de 3 telas.

## Roteiro do vídeo (≤ 3 min)
0:00 quem usa e por quê → 0:20 Início e Casa → 0:50 **alerta chegando ao vivo** (vindo da Sentinela) → 1:20 Modo Idoso com voz → 1:50 escalonamento + ligar → 2:20 offline e SQLite → 2:40 acessibilidade (fonte grande, TalkBack) → 2:55 fechamento.

## Defesa
Por que o app só fala com o Core · o que fica no SQLite e por quê · como a fila offline evita perder um "estou bem" · decisões de acessibilidade para pessoas idosas.
