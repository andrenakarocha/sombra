---
title: Sombra — plano mestre (GS 2026.2 · CitizenTech)
status: active
created: 2026-11-17
last-updated: 2026-11-17
---

# Sombra — plano mestre

> Fonte de verdade da GS 2026.2. Todo agente (Claude Code ou outro, de qualquer integrante) começa pela **§0**.
> Aqui está **o que** será construído, **por quê**, **como** e **em que ordem**, com critérios de aceite verificáveis e
> checkpoints para retomar o trabalho de qualquer estado. O detalhe de design e a rubrica de cada disciplina ficam nas
> **specs** (`specs/NN-*.md`); a ordem de execução fica **só aqui**.

## Sumário

0. [Protocolo para agentes](#0-protocolo-para-agentes)
1. [Contexto e objetivo](#1-contexto-e-objetivo)
2. [Decisões](#2-decisões)
3. [Arquitetura](#3-arquitetura)
4. [Mapa das disciplinas](#4-mapa-das-disciplinas)
5. [Convenções de engenharia](#5-convenções-de-engenharia)
6. [Observabilidade e logs](#6-observabilidade-e-logs)
7. [A entrevista e as Etapas 1 a 4 da GS](#7-a-entrevista-e-as-etapas-1-a-4-da-gs)
8. [Fases, checkpoints e critérios de aceite](#8-fases-checkpoints-e-critérios-de-aceite)
9. [Paralelismo e delegação](#9-paralelismo-e-delegação)
10. [Riscos](#10-riscos)
11. [Premissas a verificar](#11-premissas-a-verificar)
12. [Painel de status](#12-painel-de-status)

Specs: [00 Fundação](specs/00-fundacao-contratos.md) · [01 C#](specs/01-csharp-core-api.md) · [02 SOA](specs/02-soa-clima-service.md) ·
[03 ML](specs/03-ml-previsao-calor.md) · [04 IoT](specs/04-iot-sentinela.md) · [05 Mobile](specs/05-mobile-app.md) ·
[06 Painel](specs/06-web-painel.md) · [07 Cyber](specs/07-cyber-privacidade.md) · [08 OS](specs/08-os-infra.md) ·
[09 QA](specs/09-qa-azure-boards.md) · [10 Vídeos](specs/10-videos-pitch.md)

---

## 0. Protocolo para agentes

**Legenda:** 🧑 passo humano (do operador) · 🤖 passo de agente · 🧑🤖 sessão conjunta · 🤝 bom ponto de partida (cabe
numa sessão nova só com a spec indicada) · 🔑 usa conta ou acesso externo · ✅ critério de aceite · 🔎 como verificar ·
⛔ bloqueio (nada depois começa antes dele) · ⏸ pausado à espera de insumo

Ao começar qualquer sessão neste projeto:

1. Leia a §1 (contexto) e a §2 (decisões). As **fechadas** (§2.1) não se re-litigam nem mudam sem pedido explícito do
   operador. As **abertas** (§2.2) só fecham no checkpoint indicado.
2. Vá à **§12 (Painel de status)**: identifique a fase atual e o último checkpoint marcado `[x]`.
3. **Confie, mas verifique:** rode o 🔎 do último checkpoint marcado. Se falhar, o estado real é anterior: desmarque e
   corrija antes de seguir.
4. Execute o **próximo checkpoint `[ ]`** da fase, na ordem. Fases só correm em paralelo onde a §9 permite.
5. **Um checkpoint = uma unidade de trabalho = um commit atômico** (Conventional Commits em inglês, com o escopo da
   pasta: `feat(core): …`, `test(clima): …`, `chore(infra): …`). Nunca junte checkpoints num commit. Os commits vão
   em lote num branch (`fN-assunto`) e entram na `main` por **PR com rebase merge**, que preserva um commit por
   checkpoint. O agente termina abrindo o PR; o **dono do repositório revisa e faz o merge** depois do CI verde. Em máquinas com os
   hooks `block-dangerous-git` e `guard-autonomous-policy`, o `git push` é do operador e o agente só roda um merge,
   release ou publicação com o ok explícito dele. Ninguém faz push direto na `main` (D15).
6. Ao concluir: marque `[x]` com a data. Ao fechar o último checkpoint de uma fase, **assine a fase** na §12 (✅ e
   data). Não há log de execução: o checkpoint marcado e o commit contam a história.
7. **Passos 🧑 bloqueiam.** Não simule, não pule, não invente credenciais, entrevista, fotos ou dados. Pare, diga
   exatamente o que o operador precisa fazer e espere.
8. Plano diverge da realidade (API mudou, flag não existe, premissa da §11 falsa)? **Pare**, proponha
   o ajuste ao operador e só então edite o plano.
9. Uma fase só termina quando **todos** os ✅ dela passam. Marque-a como concluída na §12.
10. Escopo: só o que está aqui. Ideias novas vão para a §2.3, não para o código.
11. Idioma: documentação, UI e mensagens ao cidadão em pt-BR. Código, commits e arquivos em inglês, **exceto a
    linguagem ubíqua do domínio, que fica em português** (D11).
12. **Nenhum dado pessoal real** em repositório, fixtures, logs, prompts ou serviços externos. Personas fictícias,
    CPF e CNS sintéticos. Dados climáticos são reais e públicos, sempre com a fonte.
13. Antes de dar uma UI por pronta: screenshots revisados de verdade (painel em 390/768/1440 px; app em dois tamanhos
    de tela) e o checklist de acessibilidade da spec.
14. Afirmação sobre serviço externo (cota gratuita, flag de CLI, endpoint) só entra no plano com fonte primária
    conferida ou como premissa na §11. Resumo de busca não é fonte.
15. Todo serviço segue a **§6 (Observabilidade e logs)**. Um checkpoint que cria um serviço só fecha com o log
    estruturado e o `/health` funcionando.
16. **`prompts.md`** (exigência da GS): ao fechar uma parte, acrescente os prompts principais dela, escritos como
    prompt, sem copiar a conversa.

---

## 1. Contexto e objetivo

### 1.1 A GS e a régua

- **GS 2026.2 "CitizenTech"**, Engenharia de Software, 3º ano (Fev). Uma única CitizenTech atravessa **8 disciplinas**;
  cada disciplina avalia a sua parte de 0 a 10. **Prazo: quinta, 19/11/2026, 23h55**, na tarefa de cada disciplina no
  Teams. 20/11 é feriado.
- A equipe (André Nakamatsu Rocha · RM555004, João Marcelo Furtado Romero · RM555199, Matheus Rivera Montovaneli ·
  RM555499) já tem nota garantida pelo Challenge. **A régua aqui é outra:** production-ready, tudo integrado e
  funcionando bonito, vídeos no nível do pitch HENRY (Challenge Ford, `~/University/year3/challenge/sprint4-video-pitch/`).
- **Primeiro o piso, depois o polimento:** cada fase de componente cobre 100% da rubrica da disciplina; o acabamento
  nível HENRY fica concentrado na F11, para nenhuma nota depender de detalhe estético.

### 1.2 O Sombra

> **Ninguém enfrenta o calor sozinho.**

Em ondas de calor, a pessoa idosa que mora sozinha corre mais risco: sente menos sede, muitas vezes toma remédios que
pioram a desidratação e quase nunca percebe que a casa virou um forno. A família só descobre quando já aconteceu.

1. A **Sentinela** (ESP32) mede o calor da casa e pede um **"estou bem"** num botão físico grande. Funciona sem internet.
2. Um **modelo de ML treinado com dados reais do INMET** prevê o risco das **próximas 24 h** e avisa **antes**.
3. Sem "estou bem" no prazo, o Sombra **escalona para a rede de cuidado** pelo app, com "ligar agora".
4. Os dados de saúde ficam protegidos por um **motor de privacidade** (PII, taint e IA) com **farol de Risco Cidadão**.

### 1.3 O que o Sombra não é

- Não é um dispositivo médico nem faz diagnóstico.
- Não substitui o SAMU nem a Defesa Civil: avisa as pessoas certas mais cedo.
- Não é um produto em operação com usuários reais: é um protótipo com **engenharia de produção** e dados fictícios de
  pessoas (dados de clima reais).

### 1.4 Ficha da CitizenTech (vai no topo do README do repositório)

| Campo | Conteúdo (v0; o bairro e a persona fecham na F12.2, depois da entrevista) |
|---|---|
| **Nome** | Sombra |
| **Cidade e bairro** | São Paulo/SP · bairro da pessoa entrevistada. Clima: estação INMET A701 (Mirante de Santana) |
| **Cidadão beneficiado** | Pessoa idosa que mora sozinha e sua rede de cuidado (família, vizinhos, agentes comunitários de saúde) |
| **Problema, em uma frase** | *"Em ondas de calor, idosos que moram sozinhos em São Paulo não percebem o perigo dentro de casa, e a família só fica sabendo quando já é tarde."* |
| **Como funciona** | A Sentinela mede o calor da casa e pede um "estou bem"; o ML prevê o risco das próximas 24 h; sem resposta, o Sombra avisa a rede de cuidado pelo app; um motor de privacidade protege os dados de saúde |
| **O que cada parte faz** | §4 |
| **Link do repositório** | [github.com/andrenakarocha/sombra](https://github.com/andrenakarocha/sombra) |
| **Indicador de impacto** | **Tempo mediano entre o alerta e o "estou bem"** (meta < 15 min) · % de alertas resolvidos sem chegar ao último contato · **% dos dias perigosos previstos com 24 h de antecedência** (recall no teste) · **alarmes desnecessários para a família por semana** (meta: perto de zero; pedido da filha na entrevista) |
| **ODS** | **3** (3.d) · **10** (10.2) · **11** (11.5) · **13** (13.1) |

### 1.5 As seis perguntas do briefing

| Pergunta | Resposta |
|---|---|
| Quem é o cidadão? | A pessoa idosa que mora sozinha em SP e quem cuida dela à distância |
| Qual problema? | Exaustão e insolação silenciosas dentro de casa durante ondas de calor |
| Como funciona? | Sentinela + previsão de 24 h + check-in físico + escalonamento + farol de privacidade |
| Quais tecnologias? | ESP32/MQTT · .NET 10 · Java/Spring · XGBoost→ONNX · Expo · React · PostgreSQL · LLM · Linux/systemd · Azure Boards |
| Quem participa? | Idoso, família, vizinhos, UBS e Agentes Comunitários de Saúde, CRAS, Defesa Civil, INMET, Prefeitura |
| Como medir e ampliar? | O indicador da Ficha medido pelo próprio sistema; escala B2G (UBS distribuindo Sentinelas de baixo custo) e B2C (família) |

### 1.6 Enunciados

Em `year3/second-gs/` (fora do repositório; não versionar PDFs de terceiros sem necessidade):
`GS CitizenTech - 3ES Fev - FIAP 2026 - .pdf` (geral, 40 slides) · `Explicação Global Solutions.pdf` (Cyber, prof. Vitor
Lasse) · `intrucoes-os.txt` (OS) · `Global Solution 2026.2_Arquitetura Orientada a Serviços e Web Services 1.html`
(guia de SOA com autoavaliação).

---

## 2. Decisões

### 2.1 Decisões fechadas

| # | Decisão | Origem |
|---|---|---|
| D1 | CitizenTech **Sombra**: calor extremo e idosos que moram sozinhos | equipe, 2026-11-17 |
| D2 | **Monorepo** público, uma pasta por parte, Ficha no topo do README (o brief pede isso) (ADR-002) | equipe, 2026-11-17 |
| D3 | **Nível técnico máximo nas 8 disciplinas, tudo integrado** (revisto em 2026-11-18): código, arquitetura, testes e CI no padrão alto; trabalho manual só o que a entrega exige (D18) | equipe, 2026-11-18 |
| D4 | **Dois backends que conversam:** Core em C#/.NET 10 (disciplina C#) e Clima em Java/Spring (disciplina SOA). Cada um é dono do seu banco (ADR-003) | plano, aceito |
| D5 | **Cyber dentro do Core**, no projeto `Sombra.Privacidade` (middleware e guardas no backend, como o professor descreveu); dashboard no painel e no app | plano, aceito |
| D6 | **ML com INMET A701 (2010–2025)**, XGBoost exportado em ONNX e servido pelo Clima | plano, aceito |
| D7 | **LLM: Gemini Flash** (chave já existe), com cache e fallback em templates (ADR-005) | equipe, 2026-11-17 |
| D8 | **VM local com Vagrant** para a entrega de OS; os mesmos scripts sobem uma VM no Azure como extra (F11.8) | equipe, 2026-11-17 |
| D9 | **QA:** Feature com *Como/Quero/Para* + critérios *Dado/Quando/Então*; Test Case com objetivo, preparação, entradas, passos e saída esperada | equipe, 2026-11-17 |
| D10 | **Cada integrante é operador da sua parte:** escolhe os checkpoints, decide dentro da spec e entrega por PR; o dono do repositório revisa e faz o merge. As contas do projeto (Azure, Wokwi, Gemini, YouTube, VM) estão com o dono do repositório, e quem precisar de uma pede a ele. Os 🤝 são bons pontos de partida | equipe, 2026-11-17 |
| D11 | Linguagem ubíqua do domínio em **português** (Domicílio, Sentinela, Alerta de calor) + sufixos técnicos em inglês (`Repository`, `Controller`), no padrão do guia de SOA (ADR-001) (ADR-001) | plano, aceito |
| D12 | Tipografia **Atkinson Hyperlegible** e farol acessível (cor + ícone + texto) em todas as superfícies | plano, aceito |
| D13 | **Ajustes da entrevista** (Dona Cida e a filha): escalonamento em degraus (lembrete à pessoa → lembrete leve à família → "quem está perto" → alerta só em perigo ou pedido de ajuda); Sentinela discreta (sem piscar nem apitar sem parar); check-in diário opcional; lembrete de água no pico da tarde; lembretes nunca entre 22h e 7h; "o que acontece se eu esquecer" sempre explicado | entrevista, 2026-11-17 |
| D15 | **Repositório público `andrenakarocha/sombra`** com a `main` protegida para todos, inclusive o admin: PR obrigatório (0 aprovações), histórico linear, sem force push. **Um PR por lote**, com **rebase merge** (um commit por checkpoint na `main`). **Quem revisa e faz o merge é o dono do repositório.** Em máquinas com os hooks de segurança, o push é manual do operador e o agente só roda merge, release ou publicação com o ok explícito dele. Os checks do CI viram obrigatórios na F0.12 | equipe, 2026-11-17 (fecha a DA1) |
| D16 | **Direção de arte "luz filtrada pela folhagem"** (`design/`): tokens com temas claro e escuro, contraste conferido por script (≥ 7:1 no texto principal), Fraunces + Atkinson Hyperlegible Next, risco com cor + forma + texto | equipe, 2026-11-17 (fecha a DA5) |
| D14 | **Fotos do território fora das entregas** (padronizadas pelo time); uso possível só no pitch, com a placa do carro desfocada. A transcrição vai para o repositório com "a filha" no lugar do nome dela | equipe, 2026-11-17 |
| D18 | **Corte do trabalho manual que nenhuma entrega exige** (conferido nos enunciados): sem VM no Azure (fecha a DA3: não); F11 só com acessibilidade e uso, sem o cosmético; sem Test Plan pago nem APK; pitch só se sobrar tempo (vale só para o prêmio). Os **5 vídeos obrigatórios** (Cyber, ML ≤ 5, IoT ≤ 4, Mobile ≤ 3, OS ≤ 7) e os prints saem por script de **uma rodada** do `make demo`, cortada por matéria | dono do repositório, 2026-11-18 |
| D17 | **O app é a referência da API do Cuidador:** onde os schemas Zod do `mobile/` e o Core divergem, o Core se adapta ao app, inclusive na R3 (F6.21–F6.26) | dono do repositório, 2026-11-18 |
| D19 | **O preventivo segue o perigo de quem mora na casa** (fecha a DA6): o Core abre o `AlertaPreventivo` quando o nível previsto para a região **ou** o pico previsto do índice de calor (`indiceDeCalorPrevistoC`, do Clima) pelos limites pessoais do morador mais vulnerável (R3) chega a `PERIGO`; a R5 continua exigindo perigo. No replay real de nov/2023 (pico de 35,47 °C), o preventivo abre na Casa do Seu Joaquim (85 anos, cardiopatia: perigo a partir de 34,94 °C) e não na da Dona Lurdes (78 anos, diurético: 37,94 °C). O alvo do ML (F4.2) continua livre | dono do repositório, 2026-11-18 (fecha a DA6) |

As decisões de arquitetura têm ADR em [`docs/adr/`](adr/README.md): 001 (D11), 002 (D2), 003 (D4), 004 (MQTT assinado e sem PII), 005 (D7) e 006 (índice de calor replicado com vetores).

### 2.2 Decisões abertas

| # | Decisão | Padrão até lá | Fecha em |
|---|---|---|---|
| DA2 | Bairro da Ficha · persona fictícia do sistema | cidade **São Paulo** confirmada (a A701 continua valendo); bairro "a preencher"; persona "Dona Lurdes, 78 anos", inspirada na Dona Cida | F12.1 (não bloqueia nada) |
| DA4 | Voz do pitch: TTS (Gemini, como no HENRY) ou voz de alguém do time | TTS | F13.3 |

### 2.3 Fora do escopo (ideias para depois)

Integração real com a Defesa Civil · SMS de verdade (fica simulado) · app do agente de saúde · múltiplas Sentinelas por
casa · previsão por bairro com várias estações.

---

## 3. Arquitetura

### 3.1 Visão geral

```
                      ┌──────────────────────── VM Linux "sombra-srv01" (OS · provisionada por bash) ───────────────────────┐
 ┌────────────────┐   │  nginx ─ painel.sombra.local ──► Painel Web (React) ◄──── SignalR ────┐                             │
 │ Sentinela      │   │        ─ api.sombra.local ─────► CORE API (.NET 10 · C#) ─────────────┘                             │
 │ ESP32 · Wokwi  │MQTT│                                  ├─ Sombra.Privacidade (Cyber: PII · taint · IA)                   │
 │ DHT22·OLED·btn │◄──►│ broker MQTT ◄──── ingestão e comandos (HMAC, sem PII)                                           │
 └────────────────┘   │                                  ├─ escalonador de alertas ─► rede de cuidado (app)                │
                      │                                  └─ REST resiliente ──► CLIMA SERVICE (Java · Spring · SOA)        │
 ┌────────────────┐   │                                                           ├─ Open-Meteo (ao vivo) / INMET (replay) │
 │ App Expo       │◄──┼── HTTPS + WebSocket ──► CORE API                          └─ modelo ONNX (treinado no ml/)        │
 │ família + idoso│   │  PostgreSQL: sombra_core (dono: Core) · sombra_clima (dono: Clima)                                │
 └────────────────┘   │  sombra-monitor.service (CPU · RAM · disco · serviços) ──► POST /ops/alertas ──► painel "Operação"│
                      └───────────────────────────────────────────────────────────────────────────────────────────────────┘
   LLM (Gemini) ◄── eventos de segurança SEM PII ── Core            Azure Boards (QA) ◄──► testes automatizados
```

**Princípios:** cada serviço é dono dos próprios dados · nenhuma PII no MQTT nem na LLM · tudo tem fallback (Sentinela
offline, Clima por regra se o modelo cair, IA por template) · tudo observável (§6).

### 3.2 Serviços, portas e nomes

| Componente | Porta | Nome na VM | Banco | Spec |
|---|---|---|---|---|
| Core API | 5080 | `api.sombra.local` | `sombra_core` | 01, 07 |
| Clima Service | 8081 | `clima.sombra.local` | `sombra_clima` | 02 |
| Painel | 5173 (dev) / 80 (nginx) | `painel.sombra.local` | — | 06 |
| PostgreSQL 16 | 5432 (só local) | — | — | — |
| Mosquitto (dev) / broker público (Wokwi) | 1883 | — | — | 00, 04 |
| VM | — | `sombra-srv01` · `192.168.56.10` | — | 08 |

### 3.3 Estrutura do repositório

```
sombra/
├── README.md · AGENTS.md · prompts.md · LICENSE · compose.yaml · Makefile · .env.example
├── docs/        PLANO.md · specs/ · glossario.md · arquitetura.md · observabilidade.md · contratos/ · adr/
│                entrevista.md · problema.md · rabiscos/ · defesa/ · evidencias/
├── design/      tokens.json · README.md (direção de arte)
├── services/core-api/        C# · .NET 10 (+ src/Sombra.Privacidade = Cyber)
├── services/clima-service/   Java 21 · Spring Boot 3.5
├── web/painel/               Vite · React · TS
├── mobile/                   Expo SDK 58
├── iot/sentinela/            ESP32 · Wokwi
├── ml/                       notebook · RELATORIO_ML.md · dados/ · modelo/
├── infra/                    bash · systemd · nginx · Vagrant
├── security/                 Semgrep taint · auditoria · entrega (PDF)
├── qa/                       especificacao/*.yaml · ferramentas/ · entrega (PDF)
├── pitch/                    HyperFrames (pitch + kit técnico)
├── tools/                    sentinela-sim/ · doctor.sh
└── .github/workflows/        CI por pasta + release
```

### 3.4 Roteiro de demonstração: "a onda de calor da Dona Lurdes"

Usado em todos os vídeos e no pitch, e automatizado como `make demo` (F9.2):

1. Painel e app: casa no **verde**, Sentinela online, último "estou bem" às 9h.
2. O Clima entra em **replay** da onda de calor de nov/2023 (INMET real). O pico previsto (35,47 °C) é **perigo para o
   Seu Joaquim** (85 anos, cardiopatia, D19) → alerta preventivo na casa dele → *"Amanhã: calor perigoso. Encha sua
   garrafa de água."* → notificação no app do filho; na casa da Dona Lurdes, o mesmo calor ainda não é perigo.
3. No Wokwi a temperatura sobe → a Sentinela fica **vermelha, apita** e pede o check-in → o app e o painel ficam
   vermelhos **ao vivo**.
4. **Caminho A:** ela aperta o botão → alerta resolvido → o painel mostra o **tempo até o check-in**.
5. **Caminho B (degraus, D13):** sem resposta → a Sentinela lembra de novo e explica o próximo passo → **lembrete leve**
   no app da filha ("ela ainda não confirmou, quer ligar?") → sem reconhecimento e ainda em perigo, a **vizinha** recebe
   "pode dar uma olhada?" → só então o **alerta** para toda a rede. O OLED mostra *"Sua filha foi avisada"*.
6. **Privacidade:** CPF e laudo chegam num campo livre e um erro tenta logar dado de saúde → mascarado e bloqueado → o
   farol de Risco Cidadão fica **amarelo** com a mensagem traduzida pela IA.
7. **Infra:** tudo roda na VM; a API é derrubada e o systemd a recupera; um pico de CPU vira alerta no painel.

---

## 4. Mapa das disciplinas

| Disciplina | Pasta | Fase | Spec | Entrega no Teams | Vídeo |
|---|---|---|---|---|---|
| C# Software Development | `services/core-api/` | F1 | 01 | link do GitHub (pasta) | — |
| Cybersecurity | `services/core-api/src/Sombra.Privacidade/` + `security/` | F2 (+F7.3, F12.3) | 07 | **PDF** + **ZIP ou link**, separados | dashboard |
| SOA e Web Services | `services/clima-service/` | F3 | 02 | link do GitHub (pasta) | — |
| IA & Machine Learning | `ml/` | F4 | 03 | links do repo e do vídeo | ≤ 5 min |
| Physical Computing | `iot/sentinela/` | F5 | 04 | link do Wokwi + vídeo + `.txt` | ≤ 4 min |
| Mobile Development | `mobile/` | F6 | 05 | link do GitHub (pasta) + vídeo | ≤ 3 min |
| Operating Systems | `infra/` | F8 | 08 | link do GitHub (pasta) + vídeo | ≤ 7 min |
| Testing, Compliance & QA | `qa/` | F10 | 09 | PDF (link do Azure + prints) + **link do pitch** | pitch ≤ 4 min |
| (painel, serve Cyber, OS e pitch) | `web/painel/` | F7 | 06 | — | — |

---

## 5. Convenções de engenharia

- **Qualidade por stack:**
  - **C#:** `Directory.Build.props` + `.editorconfig` + StyleCop do CP06, warnings como erro, `Invoke-CodeQuality.ps1`
    do professor (meta 100/100), NetArchTest.
  - **Java:** Maven Wrapper, ArchUnit, `-Werror` no compilador.
  - **TS (app e painel):** TypeScript estrito, ESLint, Prettier.
  - **Python:** Ruff, pytest.
  - **Bash:** `set -Eeuo pipefail` e shellcheck.
  - **C++ (ESP32):** funções puras testáveis (índice de calor, estados).
- **Validação nas bordas:** DTOs com validação (DataAnnotations/FluentValidation, Bean Validation, zod), payload MQTT
  com HMAC. Dentro do domínio, invariantes nos construtores.
- **Tempo:** relógio injetável em todo serviço (`IRelogio`, `Clock`), para testes e para acelerar a demo. Datas em UTC
  no fio, America/Sao_Paulo na tela.
- **Testes:** unitários no domínio · integração com Testcontainers (Postgres e Mosquitto) · contrato entre Core e Clima
  · E2E do roteiro (§3.4) com o simulador · vetores de teste do índice de calor compartilhados por C#, Java, C++ e Python.
- **Simplicidade:** nada fora do plano, nenhuma abstração especulativa, nenhum tratamento de erro para cenário
  impossível.
- **Segredos:** `.env` nunca no Git; `.env.example` sempre; gitleaks no CI.
- **Commits:** Conventional Commits em inglês, um por checkpoint. Branch por lote, PR com CI verde, rebase merge (D15).
- **Cada pasta tem README** com: o que é, como rodar, como testar, variáveis, evidências e integrantes.

---

## 6. Observabilidade e logs

**Tudo o que roda deixa rastro, e nenhum rastro carrega PII.**

- **Formato:** JSON por linha no stdout (vira journald na VM). Campos obrigatórios: `timestamp` (ISO-8601 UTC), `level`,
  `service`, `version` (commit), `environment`, `correlationId`, `event` (nome estável, ex.: `alerta.escalonado`),
  `message`. Campos de domínio com ID, nunca com nome ou documento.
- **Correlação ponta a ponta:** `X-Correlation-Id` entra no HTTP ou nasce na ingestão MQTT, atravessa Core → Clima →
  SignalR → outbox. Um alerta é rastreável do botão da Sentinela até a tela do app (F9.5).
- **Níveis:** `Information` para eventos de negócio, `Warning` para falha recuperável (retry, fallback, circuit aberto),
  `Error` para falha que precisou de intervenção. Nada de log de corpo de requisição.
- **PII:** a guarda de log do `Sombra.Privacidade` (F2.4) bloqueia e redige; a auditoria (F2.8) prova isso.
- **Auditoria:** eventos de segurança persistidos (outbox) · trilha do provisionamento em `auditoria.jsonl` (o quê,
  quando, quem, resultado) · `auditd` na VM.
- **Saúde:** `/health/live`, `/health/ready` e `/version` (commit e data do build) em todo serviço; o monitor da VM
  consulta todos.
- **Dispositivo:** log serial com nível e `seq`; eventos de boot e reconexão vão também no MQTT.

Detalhe em [`docs/observabilidade.md`](observabilidade.md) (F0.8), com o checklist de pronto de cada serviço.

---

## 7. A entrevista e as Etapas 1 a 4 da GS

**O que a GS pede.** O percurso da equipe tem 6 etapas. As **etapas 1 a 4 (pesquisa e ideia)** vêm antes das
disciplinas: **Observar** (escolher um lugar real e registrar o problema com fotos e anotações), **Escutar** (*"conversem
com quem convive com o problema. O que essa pessoa faz hoje? O que a atrapalha?"*), **Definir** (o cidadão e o problema
em uma frase) e **Rabiscar** (telas, fluxo de dados e circuito no papel). O brief repete a regra: *"vale qualquer
problema real da cidade, desde que a equipe consiga conversar com alguém que convive com ele."*

**Vale nota?** Nenhuma rubrica de disciplina tem uma linha só para a entrevista. Ela pesa **indiretamente**:
- Mobile: "aderência ao problema";
- ML: "problema e dados reais" e "leitura para o cidadão";
- Pitch (prêmio): "o problema, a solução e quem ela ajuda";
- Ficha: o campo "cidadão beneficiado".

É pouco esforço com muito retorno, e um professor pode perguntar.

**O que o operador traz (🧑, F0.16):**
1. **Transcrição** em perguntas e respostas (digitada de memória logo depois da conversa já serve, ou um áudio
   transcrito). Roteiro de 8 perguntas na spec 00.
2. **3 a 6 fotos do território:** a rua, a fachada (sem número), o cômodo mais quente, a garrafa d'água, a UBS do bairro.
   **Sem rosto** (ou com autorização) e sem placa, número ou documento à vista.
3. **Metadados:** data, bairro, relação com a pessoa (ex.: "avó de um integrante"), idade aproximada.
4. **Consentimento verbal** registrado: *"posso usar suas respostas, sem seu nome, num trabalho da faculdade?"*

**Se não houver uma pessoa idosa por perto:** vale entrevistar quem cuida de uma (filho, filha, vizinho) ou um agente
comunitário de saúde. Continua sendo "quem convive com o problema".

**O que o agente faz com isso (🤖, F12.2):** escreve `docs/entrevista.md` (a transcrição com "a filha" no lugar do nome
dela, os achados e o que mudou no projeto: D13), `docs/problema.md` e `docs/rabiscos/`. As fotos ficam **fora do
repositório** e podem entrar só no pitch (D14). A entrevista também fecha a Ficha e alimenta os atos 1 e 2 do pitch.

---

## 8. Fases, checkpoints e critérios de aceite

### 8.0 Visão geral

| Fase | Objetivo | Depende de | Janela-alvo |
|---|---|---|---|
| F0 | Fundação: repositório, contas, contratos, design, ambiente, CI, dados INMET | — | ter 17 |
| F1 | **Core API (C#)** | F0 | ter 17 noite → qua 18 manhã |
| F2 | **Privacidade (Cyber)**, backend | F1.6 | qua 18 manhã |
| F3 | **Clima Service (SOA)** | F0 | qua 18 |
| F4 | **ML** | F0.15 | qua 18 · 🤝 |
| F5 | **Sentinela (IoT)** | F0.7, F0.11 | qua 18 · 🤝 firmware |
| F6 | **App (Mobile)** | F0.7, F1.7 | qua 18 · 🤝 |
| F7 | **Painel web** | F1.7, F2.5 | qua 18 tarde |
| F8 | **Infra (OS)** | F0 (F8.1–F8.2) · F1, F3, F7 (F8.4) | qua 18 tarde e noite |
| F9 | Integração ponta a ponta | F1–F8 | qua 18 noite |
| F10 | **QA (Azure Boards)** | F0 (YAML) · F9 (rastreabilidade) | qua 18 → qui 19 · 🤝 YAML |
| F11 | Polimento nível HENRY | aceite da fase de cada componente | qui 19 manhã |
| F12 | Documentação final, PDFs e defesa | F2, F10, F0.16 | qui 19 |
| F13 | Vídeos e pitch | F9 | qui 19 |
| F14 | Entrega | tudo | qui 19, até 22h (folga até 23h55) |

### 8.1 F0 — Fundação

**Objetivo:** terreno pronto para qualquer componente começar sem bloquear os outros. Nenhum código de negócio.
**Spec:** 00.

- [x] **F0.1** 🤖 Plano mestre neste formato + specs por disciplina — 2026-11-17.
  🔎 `docs/PLANO.md` e `docs/specs/` existem no repositório `year3/second-gs/sombra/`.
- [x] **F0.2** 🤖 `tools/doctor.sh`: confere git, Docker + Compose, .NET SDK 10, JDK 21, Node ≥ 22, uv (instala o Python
  3.12 por projeto), `gh` logado, jq e shellcheck; opcionais por fase (VirtualBox, Vagrant, FFmpeg, gitleaks, psql). O
  pwsh não é preciso: o script de qualidade roda na imagem do SDK. Imprime o que falta e como instalar — 2026-11-17 16:07.
  🔎 sai com 0 na máquina do operador, ou com a lista exata do que instalar.
- [x] **F0.3** 🧑 🔑 Contas e chaves de que o código precisa agora: dono do repositório (DA1), `gh auth status`, chave
  Gemini no `.env` local. Wokwi, Azure DevOps e YouTube podem esperar as fases deles (F5.1, F10.1, F13.7).
  🔎 `gh auth status` ok; `.env` local tem `GEMINI_API_KEY` (nunca commitado). — 2026-11-17 16:09.
- [x] **F0.4** 🤖 `git init` do monorepo `sombra` com a estrutura da §3.3 — 2026-11-17 16:09:
  - `README.md` com a Ficha v0, `AGENTS.md` (aponta para `docs/PLANO.md` §0), `prompts.md`, `LICENSE` (MIT),
    `.gitignore`, `.editorconfig`, `.env.example`;

  🔎 `tree -L 2` bate com a §3.3 (o `compose.yaml` e o `Makefile` chegam na F0.10); `git log` tem o commit.
- [x] **F0.5** 🧑🤖 🔑 GitHub: repositório público `sombra`, push, `main` protegida (PR + CI), `CODEOWNERS`, labels por
  disciplina, templates de PR e issue. Fecha a DA1 — 2026-11-17 16:15.
  🔎 o link abre numa aba anônima.
- [x] **F0.6** 🤖 Linguagem ubíqua (`docs/glossario.md`), tabela de níveis de risco (`docs/contratos/niveis-de-risco.md`)
  e vetores de teste do índice de calor (`docs/contratos/indice-de-calor.vetores.json`, ≥ 15 pares T/UR → índice,
  calculados pela equação oficial da NWS/WPC) — 2026-11-17 16:18.
  🔎 script de conferência dos vetores sai com 0 (P1).
- [x] **F0.7** 🤖 Contratos: `mqtt.asyncapi.yaml`, `core-api.md`, `clima-service.md`, `signalr.md`, portas, nomes e variáveis
  de ambiente (§3.2) — 2026-11-17 16:23.
  🔎 `npx @asyncapi/cli validate docs/contratos/mqtt.asyncapi.yaml` passa.
- [x] **F0.8** 🤖 `docs/observabilidade.md` (§6 detalhada: campos, nomes de evento, correlação, exemplos por stack) — 2026-11-17 16:28.
  🔎 cada spec de serviço cita a seção que lhe cabe.
- [x] **F0.9** 🧑🤖 Direção de arte: `design/tokens.json` + `design/README.md` + uma página de amostra (farol nos 5 níveis,
  cartão de casa, botão gigante, tipografia, claro e escuro). Fecha a DA5 — 2026-11-17 16:40.
  🔎 o operador aprova a amostra.
- [x] **F0.10** 🤖 `compose.yaml` (Postgres com os 2 bancos via init, Mosquitto, placeholders do Core, do Clima e do painel
  com `/health`) + `Makefile` (`env`, `up`, `down`, `ps`, `logs`, `db-reset`, `contratos`, `lint`, `test`; o `demo` chega
  na F9.2) — 2026-11-17 16:43.
  🔎 `make up` → os três `/health` respondem 200; o papel de um serviço é barrado no banco do outro.
- [x] **F0.11** 🤖 Simulador da Sentinela (`tools/sentinela-sim/`, Python): telemetria e eventos assinados (HMAC), obedece
  comandos; cenários `normal`, `onda-de-calor`, `sem-resposta`, `pedido-de-ajuda`, `esqueceu-checkin-diario`,
  `payload-adulterado`; testes — 2026-11-17 16:48.
  🔎 `pytest` verde; contra o Mosquitto do compose, publica e recebe um comando de teste.
- [x] **F0.12** 🤖 CI: um workflow só (`ci.yml`) com filtro por pasta, mais gitleaks, shellcheck e actionlint, e um job
  agregador `ci-ok`, que vira o check obrigatório da `main` (D15). Actions fixadas por SHA. O `release.yml` nasce na F8.3,
  quando houver artefatos.
  🔎 CI verde no PR; a proteção da `main` lista o `ci-ok` — 2026-11-17 (PR #3).
- [x] **F0.13** 🤖 ADRs 001–006 (`docs/adr/`): linguagem ubíqua · monorepo · dois serviços donos dos seus bancos · MQTT
  público com HMAC e sem PII · LLM com fallback · índice de calor replicado de propósito com vetores compartilhados.
  🔎 os ADRs existem e a §2.1 os cita — 2026-11-17 16:53.
- [x] **F0.14** 🤖 Handoff para delegar, **sem issues nem quadro** (decisão da equipe): o `AGENTS.md` ensina o agente de
  quem chega dizendo "sou o João, quero fazer a minha parte" (lista os 🤝 abertos, confirma, segue a §0 e a spec,
  entrega por PR) e traz o prompt pronto para o grupo — 2026-11-17 16:56.
  🔎 o `AGENTS.md` tem o fluxo de delegação e o prompt.
- [x] **F0.15** 🤖 `ml/scripts/baixar_dados.py`: baixa os históricos anuais do INMET (2010–2025), extrai só a A701,
  registra e confere SHA-256 e grava em `ml/dados/brutos/`, com a proveniência em `ml/dados/README.md` e
  `brutos/manifesto.json` — 2026-11-17 16:57. Insumo comum do ML (F4) e do seed do Clima (F3.3).
  🔎 16 arquivos anuais da A701 presentes, colunas esperadas, `-9999` contado (P2).
- [x] **F0.16** 🧑 Entrevista + fotos + metadados + consentimento (§7). **Não bloqueia código**; bloqueia a F12.2 e o pitch
  — 2026-11-17.
  🔎 transcrição e fotos entregues ao agente, com moradia e origem das fotos confirmadas (casa; fotos padronizadas, D14).

**✅ Aceite F0:** repositório público com CI verde · `make up` verde · contratos, níveis e vetores publicados · direção de
arte aprovada · simulador funcionando · dados INMET baixados · `doctor.sh` verde.

### 8.2 F1 — Core API (C#)

**Objetivo:** a rubrica de C# coberta no piso, com o roteiro de alerta rodando contra o simulador. **Spec:** 01.

- [x] **F1.1** 🤖 Scaffold .NET 10 (`.slnx`) em `sources/` (o layout que o pipeline do professor lê): `Sombra.Dominio`,
  `Sombra.Aplicacao`, `Sombra.Infraestrutura`, `Sombra.Privacidade`, `Sombra.Api`; props, editorconfig e StyleCop do
  CP06; Central Package Management; `Invoke-CodeQuality.ps1` do professor, sem alteração. Todo `dotnet` roda na imagem
  do SDK 10.0.404 (o SDK do Arch não traz o ASP.NET Core). Os projetos de teste (MSTest) nascem com o primeiro teste
  de cada um, e o job do Core no CI chega com eles (F1.2) — 2026-11-17.
  🔎 `make build` sem warnings; `make quality` = 100 (P3).
- [x] **F1.2** 🤖 Value objects com testes: `Cpf`, `CartaoSus`, `Telefone`, `Endereco`, `Temperatura`, `UmidadeRelativa`,
  `IndiceDeCalor`, `LimitesTermicos`.
  🔎 testes verdes; os vetores compartilhados passam — 2026-11-17 (48 testes; quality 100/100; job do Core no CI).
- [x] **F1.3** 🤖 Agregados `Domicilio`, `Morador` (+ `PerfilDeSaude`, fatores de risco polimórficos), `RedeDeCuidado`,
  `Sentinela`, `Leitura`; regras R1, R3, R8 e R9.
  🔎 testes de cada regra, inclusive as violações — 2026-11-17 (61 testes; R10 também, na leitura).
- [x] **F1.4** 🤖 `AlertaDeCalor` abstrato → `AlertaPreventivo`, `AlertaDeSentinela` e `AlertaDePedidoDeAjuda`; máquina
  de estados; escalonamento em degraus (D13); `CheckIn`; `Rotina` (R11, R12); eventos de domínio; regras R4–R7 e R10.
  🔎 testes de todas as transições válidas e inválidas; cobertura do domínio ≥ 90%. — 2026-11-17 (80 testes; 91,7% das linhas; `make coverage` no CI).
- [x] **F1.5** 🤖 Aplicação: casos de uso, portas (`IDomicilioRepository`, `IAlertaRepository`, `IClimaGateway`,
  `INotificador`, `IRelogio`, `IUnitOfWork`), DTOs e validação.
  🔎 testes dos casos de uso com fakes — 2026-11-17 (22 testes de aplicação + 83 de domínio; cobertura 92,2%; quality
  100/100). Portas extras `ILeituraRepository` e `ICheckInRepository`; a R4 vale para os alertas que pedem resposta (o
  preventivo convive com eles); o pedido de ajuda num alerta de Sentinela aberto leva esse alerta ao degrau 3.
- [x] **F1.6** 🤖 Infraestrutura EF Core + Npgsql: configurações, owned types, migrações **Inicial**, **AlertasECheckIns**,
  **RedeDeCuidadoEIndices**, **SeedDemo** (personas fictícias); repositórios; consultas LINQ do painel.
  🔎 `make db-reset` cria do zero; testes de integração dos repositórios com Testcontainers (P4) — 2026-11-17
  (`db-reset` aplica as 4 migrações e o seed; 8 testes de integração num PostgreSQL 16 real, 113 no total; quality
  100/100). Endereço e rotina como tipos complexos (o sucessor dos owned types para VOs), rede de cuidado em `jsonb`
  (guarda a ordem), alertas em TPH; `make migration-check` no CI.
- [x] **F1.7** 🤖 API v1: controllers, ProblemDetails, JWT com papéis, rate limiting, `/health/*`, `/version`, OpenAPI +
  Scalar com resumo, tag, exemplo e erros em todo endpoint.
  🔎 o Scalar lista todos os endpoints com os 4xx; testes de API conferem status e corpo — 2026-11-17 (28 operações, todas
  com resumo, tag e 4xx; 6 testes de API ponta a ponta num PostgreSQL real, 120 no total; quality 100/100). Contas em
  `usuarios` (migração 5, senha em PBKDF2), Cuidador restrito às casas dele, contas da demo pelo `Demo__Senha` do `.env`;
  `docs/contratos/core-api.openapi.json` vira a fonte da verdade (`make openapi`). Ficam para depois: `/ops/alertas` e o
  POST assinado de leituras (F1.9), `/privacidade/*` (F2.5), pontos de resfriamento (F3.9).
- [x] **F1.8** 🤖 Logs estruturados (§6): Serilog JSON, middleware de correlação, log de requisição sem corpo, versão no log.
  🔎 uma requisição gera uma linha JSON com `correlationId` e nenhum dado pessoal — 2026-11-17 (no contêiner do `make up`
  e num teste de API: os 8 campos, a rota-modelo, a correlação aceita, devolvida e gerada; nenhum nome, CPF, CNS, senha
  ou token no log). Os fatos de negócio saem depois de gravados, só por ID, com a mesma correlação da requisição; o Core
  real substitui o placeholder no compose (Dockerfile Alpine, sem root).
- [x] **F1.9** 🤖 Workers e tempo real:
  - `IngestaoMqttWorker`: MQTTnet → HMAC + anti-replay → `Channel<T>` → `RegistrarLeitura`;
  - `EscalonadorDeAlertasWorker`: a cada 15 s; na mesma volta roda o caso de uso `LembrarRotina` (check-in diário
    esquecido vira lembrete leve, R11; lembrete de água no pico da tarde, no máximo 2 por dia, nunca à noite, R12);
  - `MonitorDePrevisaoWorker`: contra um stub do contrato do Clima até a F3.9;
  - `PublicadorDeComandosWorker`: outbox → MQTT;
  - hub SignalR `/hubs/sombra`.

  🔎 com o simulador: `onda-de-calor` abre o alerta e emite no hub; `sem-resposta` escalona; `payload-adulterado` é
  descartado e gera evento de segurança; os testes do `LembrarRotina` cobrem R11 e R12 com relógio falso — 2026-11-18
  (com o simulador Python no `make up`: `onda-de-calor` abre, agrava e o botão resolve, com a tela recebendo os comandos
  assinados; `sem-resposta` escalona até a rede com o relógio adiantado pela API; `payload-adulterado` vira
  `MQTT_ASSINATURA_INVALIDA`. O hub é conferido em 4 testes de API num Mosquitto e num PostgreSQL reais; 132 testes;
  quality 100/100). O 🔎 achou a R13 (carência depois do "estou bem", na spec 01). O `Demo__EscalaDeTempo` deu lugar a
  `POST /api/v1/demo/relogio/avancar`, porque uma escala quebraria a janela anti-replay das mensagens da Sentinela. O
  monitor de previsão usa o `ClimaSimulado` (`Clima__Simulado__{regiao}`) até a F3.9.
- [x] **F1.10** 🤖 Testes de arquitetura (NetArchTest) e teste de integração dos caminhos A e B do roteiro.
  🔎 `make test` verde; relatório de cobertura gerado — 2026-11-18 (8 regras de arquitetura: o domínio não depende de
  nada, a aplicação só do domínio, a infraestrutura nunca da API, controllers sem acesso direto ao banco, domínio
  selado, portas com `I`; o roteiro da Dona Lurdes de ponta a ponta, passos 1 a 5, com Sentinela MQTT assinada, hub e
  painel; 140 testes; `make coverage-report` gera o HTML: 89,1% das linhas do Core inteiro).
- [x] **F1.11** 🤖 README da pasta (Ficha, diagrama de classes em Mermaid, como rodar, comando que cria o banco, link do
  Scalar, integrantes) + `docs/evidencias/` (Scalar, migrações, testes, quality 100/100).
  🔎 todos os itens da tabela de rubrica da spec 01 com evidência — 2026-11-18 (a tabela do README liga cada critério ao
  código e a um print em `services/core-api/docs/evidencias/`: Scalar, operações com os 4xx, migrações e tabelas,
  testes, quality 100/100 e cobertura de 88,7% do Core; prompts da F1 no `prompts.md`).

**✅ Aceite F1:** POO, Web API, EF Core + LINQ, Async e Qualidade com evidência cada · `make quality` = 100 · `make test`
verde · roteiro de alerta funcionando com o simulador.

### 8.3 F2 — Privacidade (Cyber), backend

**Objetivo:** os quatro pilares do prof. Vitor no backend; o dashboard visual fecha na F7.3. **Spec:** 07.

- [x] **F2.1** (2026-11-18) 🤖 `Sombra.Privacidade`: regras em `pii-regras.json` (com schema validado na carga); detectores CPF, CNS,
  RG, telefone, e-mail, CEP, saúde (CID-10 + dicionário) e credenciais; testes em tabela (positivos, negativos, difíceis).
  🔎 zero falso negativo na tabela; relatório de acertos gerado.
- [x] **F2.2** (2026-11-18) 🤖 `PiiScanningMiddleware` + `ContextoDeTaint` + `MascaradorDePii` + política de campo livre.
  🔎 cenário A1 como teste de integração.
- [x] **F2.3** (2026-11-18) 🤖 Criptografia em repouso: converters AES-256-GCM, índice cego HMAC para CPF, chave por ambiente,
  migração **CamposSensiveisCifrados**.
  🔎 no `psql`, as colunas aparecem cifradas; a API devolve mascarado.
- [x] **F2.4** (2026-11-18) 🤖 `Sensivel<T>` + guardas de sink: log (decorador dos sinks do Serilog, que é o provedor de log do Core), resposta HTTP (middleware),
  saída externa (`DelegatingHandler` + publicador MQTT) e exceções.
  🔎 cenários A2–A7 como testes de integração.
- [x] **F2.5** (2026-11-18) 🤖 Eventos de segurança (outbox), cálculo do `RiscoCidadao` (pesos e decaimento) e endpoints
  `/privacidade/eventos`, `/privacidade/risco-cidadao`, `/domicilios/{id}/privacidade`.
  🔎 testes do cálculo do farol.
- [x] **F2.6** (2026-11-18) 🤖 🔑 `TradutorDeRiscoWorker`: prompt sem PII, três audiências (cidadão, gestão, time técnico com sugestão
  de correção), guardas na saída da IA, cache e fallback.
  🔎 com a chave, tradução real registrada; sem chave ou com timeout, cai no template (`fonte: template`); um teste
  prova que o prompt nunca tem PII (P5).
- [x] **F2.7** (2026-11-18) 🤖 Semgrep em modo taint (`security/semgrep/`) + canário + job no CI com SARIF.
  🔎 o Semgrep acha o canário e nenhum vazamento no código real (P6).
- [x] **F2.8** (2026-11-18) 🤖 Auditoria antes × depois (`security/auditoria/rodar-auditoria.sh`; modo inseguro **só** no ambiente
  `Demo`) + `relatorio.md` e JSON.
  🔎 os 8 cenários: vazam com o módulo desligado e são bloqueados com ele ligado.

**✅ Aceite F2:** motor de PII, taint (dinâmico e estático), IA na remediação e API do farol prontos, com a auditoria
provando o antes e o depois.

### 8.4 F3 — Clima Service (SOA)

**Objetivo:** a autoavaliação do guia do professor em 10/10, com o Core consumindo o serviço. **Spec:** 02.

- [x] **F3.1** 🤖 Projeto Spring Boot 3.5 / Java 21 com os pacotes `dominio/`, `aplicacao/`, `infraestrutura/` e `api/`,
  Dockerfile, tratamento de erros e padrão de testes reaproveitados do Orbital Guardian (GS 1).
  🔎 `./mvnw verify` verde — 2026-11-18 (Spring Boot 3.5.16, aviso de compilação é erro, erros em ProblemDetails pt-BR
  com 7 testes; o compose, o `make test-clima` e o job `clima` do CI já usam o serviço real).
- [x] **F3.2** 🤖 Domínio com testes: índice de calor (vetores), classificação, `-9999` não é observação, umidade 0–100,
  ponto com `abre < fecha`, região exige estação válida.
  🔎 testes de domínio verdes, sem banco e sem HTTP — 2026-11-18 (66 testes de domínio, os 20 vetores do contrato lidos
  do próprio JSON, cada regra com o seu código estável; JaCoCo exige 90% das linhas do domínio e está em 91%).
- [x] **F3.3** 🤖 Flyway V1 (regiões, estações, observações), V2 (pontos de resfriamento), V3 (evolução), V4 (seed: região
  SP-MIRANTE, A701, observações reais de nov/2023 extraídas da F0.15, pontos públicos com fonte); repositórios e consultas
  (período paginado, proximidade); `ddl-auto=validate`.
  🔎 `docker compose down -v && up` + migrate num banco vazio; testes de repositório — 2026-11-18 (`sombra_clima`
  recriado vazio: V1–V4 aplicadas pelo serviço e por `./mvnw flyway:migrate`; seed gerado por script do CSV conferido pelo
  SHA-256: 637 horas reais, 83 sem dado descartadas, pico 35,47 °C; 9 pontos reais do OpenStreetMap com o horário do OSM;
  9 testes de repositório no PostgreSQL 16 do Testcontainers, incluindo a paridade do índice de todas as horas).
- [x] **F3.4** 🤖 API, DTOs com Bean Validation, erros (400, 404, 422) e springdoc completo (resumo, tag, exemplo, erros).
  🔎 Swagger mostra os 4xx em todos os endpoints — 2026-11-18 (13 operações, cada uma com resumo, tag, exemplo e os 4xx
  com o corpo `Problema`; o `ContratoOpenApiTest` confere isso e o `docs/contratos/clima-service.openapi.json`
  versionado; 31 testes de API com status e corpo no PostgreSQL real; o teste pegou e a F3.4 corrigiu o horário
  deslocado pelo fuso da JVM).
- [x] **F3.5** 🤖 `FonteMeteorologica`: `OpenMeteoClient` agendado e resiliente + `ReplayInmet` (`POST`/`DELETE /replay`).
  🔎 o replay de nov/2023 avança hora a hora; o modo ao vivo traz dado atual (P7) — 2026-11-18 (no compose: a coleta
  gravou as 25 horas de hoje da Open-Meteo e o replay avançou 3 h em 3 s na velocidade 3600; nos testes, o relógio
  controlado avança uma hora de dado por minuto e o replay encerra sozinho no fim dos dados; o cliente tenta 3 vezes com
  espera crescente, pula hora nula ou fora da faixa e converte a radiação de W/m² para kJ/m²; a coleta não duplica hora).
- [x] **F3.6** 🤖 `PrevisorDeCalor` com `RegraPrevisorDeCalor` (o ONNX entra na F9.1) e `/risco-calor` completo.
  🔎 teste de API do risco, com status e corpo — 2026-11-18 (regra = persistência do pico das últimas 24 h, o baseline
  da F4.3, com probabilidade logística em torno do limiar de PERIGO; no pico real de 15/11/2023 o risco sai
  `CAUTELA_EXTREMA`, 35,47 °C, p = 0,07, `fonteDados: replay-inmet`; sem observação nas últimas 3 h, 503; o campo novo
  `indiceDeCalorPrevistoC` deixa a DA6 resolvível no Core pelo limiar pessoal da R3).
- [x] **F3.7** 🤖 Logs JSON (§6) aceitando o `X-Correlation-Id` do Core + actuator health.
  🔎 uma chamada do Core aparece nos dois logs com o mesmo `correlationId` — 2026-11-18, lado do Clima (no container:
  `servico.iniciado` com a configuração sem segredo; uma chamada com `X-Correlation-Id` sai com o mesmo ID em
  `previsao.calculada` e `requisicao.concluida`, sem o corpo; ID fora do formato é trocado; readiness com banco e o
  previsor como detalhe; 9 testes). O lado do Core (o `ClimaClient` mandando o header) é conferido na F3.9.
- [x] **F3.8** 🤖 Testes de API (MockMvc + Testcontainers) + ArchUnit + README (Ficha, como rodar, comando do banco,
  link e print da doc, endpoints, fonte dos dados, integrantes) + evidências.
  🔎 autoavaliação do guia preenchida com 10/10 e evidência por subitem — 2026-11-18 (README da pasta com os 22
  subitens → código → print; 8 regras ArchUnit, com a de "entidade não vaza no JSON" conferida por mutação; os 5 prints
  de `docs/evidencias/` saem de `scripts/evidencias.sh` e foram revisados; 142 testes no `./mvnw verify`).
- [x] **F3.9** 🤖 No Core: o `ClimaClient` real (HttpClient + resiliência) substitui o stub; teste de contrato.
  🔎 o `MonitorDePrevisaoWorker` abre alerta preventivo a partir do replay real — 2026-11-18: com
  `Clima__UrlBase` (compose e VM), o risco e os pontos vêm do Clima, com timeout de 3 s, circuito que abre em 3 falhas
  seguidas por 30 s, último risco conhecido por até 30 min, correlação no header e o guarda de saída; os testes leem as
  amostras reais que o Clima grava (`docs/contratos/amostras-clima/`). No compose: o worker consultou o Clima
  (`previsao:6c700f00` nos dois logs), caiu em `clima.indisponivel` quando o Clima ainda subia, e "Perto de mim" trouxe
  os pontos reais do OSM com o mesmo `correlationId` no Core e no Clima. Com a D19 (DA6 fechada), o replay real no pico
  de 15/11/2023 abriu o preventivo, nível `PERIGO`, só na Casa do Seu Joaquim (`previsao:cb9e7e9f` em `alerta.aberto` no
  Core e em `previsao.calculada` no Clima). Sem `Clima__UrlBase`, o Core segue no modo offline (testes). Core: 365
  testes, cobertura do domínio 93,2% e `make quality` = 100.

**✅ Aceite F3:** autoavaliação 10/10 com evidência · o Core consome o Clima de verdade.

### 8.5 F4 — ML 🤝

**Objetivo:** os 6 critérios da rubrica de ML, com o modelo pronto para produção. **Spec:** 03.

- [x] **F4.1** 🤖 `requirements.txt` fixado, carga dos CSVs da F0.15, limpeza (`-9999`, fuso, buracos) em funções testadas.
  🔎 `pytest` verde (sem vazamento de futuro, `-9999` tratado) — 2026-11-18 (33 testes; os CSVs conferem com o SHA-256 do
  manifesto; 160 horas interpoladas e 40 dias locais descartados; hora interpolada nunca vira amostra).
- [x] **F4.2** 🤖 EDA, alvo (limiar ancorado na tabela NWS e no balanceamento real) e features.
  🔎 seções 1–4 do notebook executadas; limiar justificado no texto — 2026-11-18 (alvo em `CAUTELA_EXTREMA`, 32,22 °C: o
  maior índice da A701 em 16 anos é 35,47 °C, e `PERIGO` nunca ocorreu; 63 features só com o que o Clima tem em produção,
  em janelas de até 24 h; 49 testes, incluindo mudar o futuro sem mudar as features).
- [x] **F4.3** 🤖 Divisão temporal (treino 2010–2021, teste 2022–2025) e baselines (persistência, regressão logística).
  🔎 métricas dos baselines registradas — 2026-11-18 (seção 5 do notebook, só na validação temporal: PR-AUC 0,360 da
  persistência e 0,335 da logística; 24 h de folga entre treino e teste e entre as dobras; 58 testes).
- [x] **F4.4** 🤖 XGBoost com `GridSearchCV` + `TimeSeriesSplit(5)` só no treino: 3 hiperparâmetros, 12 combinações.
  🔎 tabela das 12 combinações, com média e desvio — 2026-11-18 (seção 6 do notebook e `ml/modelo/busca_hiperparametros.csv`;
  melhor: `max_depth=3`, `learning_rate=0.03`, `n_estimators=200`, PR-AUC 0,332 ± 0,065 na validação).
- [x] **F4.5** 🤖 Treino × teste lado a lado, gráficos, overfitting, limiar de decisão escolhido no treino, SHAP.
  🔎 figuras em `ml/figuras/` — 2026-11-18 (8 figuras; no teste, recall 0,97, precisão 0,21 e PR-AUC 0,588, empatada com a
  persistência, 0,575: a diferença de +0,013 tem IC 95% de −0,099 a +0,114; 74 de 75 dias de calor avisados, com 7,8 dias de
  alarme falso por mês).
- [x] **F4.6** 🤖 Conclusão para o cidadão + exportação ONNX + `features.json` + `paridade.csv`.
  🔎 o ONNX (onnxruntime) bate com o XGBoost em 20 amostras (|Δ| < 1e-5) (P8) — 2026-11-18 (maior diferença 4,8e-07 nas 20
  amostras e 3,4e-07 nas 5.702 do teste). Além do pedido: `modelo-pico-calor-v1.onnx` (mediana do pico em °C, para o
  `indiceDeCalorPrevistoC` da D19; erro médio de 1,65 °C contra 2,21 °C da persistência) e `paridade-janelas.json`
  (observações brutas, para testar as features em Java). ⚠ Para a F9.1: no replay de nov/2023 o pico previsto chega a
  32,58 °C (34,88 °C com a margem), abaixo dos 34,94 °C do Seu Joaquim; usar o maior entre a persistência e a mediana +
  margem, senão o preventivo da demonstração deixa de abrir.
- [x] **F4.7** 🤖 `RELATORIO_ML.md` (2–4 páginas), `dados/README.md` e README da pasta.
  🔎 checklist da rubrica de ML marcado; `jupyter nbconvert --execute` roda do início ao fim sem erro — 2026-11-18
  (`make -C ml notebook`: 27 células de código sem erro, com as mesmas métricas ao repetir; 75 testes também num ambiente
  novo criado do `requirements.txt`; rubrica no README da pasta). O vídeo é a F13.6.

**✅ Aceite F4:** os 6 critérios com evidência · notebook executado · modelo e paridade publicados.

### 8.6 F5 — Sentinela (IoT)

**Objetivo:** a porta física do Sombra, integrada de verdade. **Spec:** 04.

- [ ] **F5.1** 🧑🤖 🔑 Projeto no Wokwi (conta do projeto) + circuito + `diagram.json` versionado.
  🔎 o OLED mostra a temperatura do DHT22.
  ⏸ 2026-11-19: o circuito está no `iot/sentinela/diagram.json` (ESP32 DevKit, DHT22, OLED I²C, LED RGB com 3 × 220 Ω,
  buzzer e os dois botões, com as teclas V e A) e o firmware compila para o ESP32 com `--warnings all`
  (`make compilar-sentinela`: 80% da flash, 17% da RAM). Falta o operador criar o projeto no Wokwi e colar os arquivos.
- [ ] **F5.2** 🤖 🤝 Índice de calor (vetores), máquina de estados offline, LED, buzzer e botões.
  🔎 mexendo no DHT22, os estados mudam conforme a tabela de níveis.
  ⏸ 2026-11-19: coberto no host (`make test-sentinela`, também no CI): os 20 vetores do índice e 14 cenários da máquina
  de estados (luz fixa por nível, 3 bipes, silêncio das 22h às 7h fora do perigo, prazo de 30/15 min, comandos do Core).
  O 🔎 mexendo no DHT22 do Wokwi fica com o operador, junto com a F5.1.
- [x] **F5.3** 🤖 Wi-Fi, NTP, MQTT, HMAC, `seq` e LWT.
  🔎 o Core aceita a telemetria; uma mensagem adulterada é rejeitada (P9) — 2026-11-19 (`make integrar-sentinela`:
  as mensagens saem dos mesmos headers do firmware e vão para o Core de verdade, pelo broker do compose e pelo público
  `broker.hivemq.com`, o caminho do Wokwi. A telemetria vira leitura, a com o nível trocado depois da assinatura cai
  em `mqtt.assinatura_invalida`, e a repetida, em `mqtt.replay_descartado`; as duas aparecem no dashboard de segurança.
  O `seq` segue a regra do Core para os comandos, `max(último + 1, agora)`, e continua crescente mesmo com a flash
  zerada, o que o Wokwi faz a cada simulação. Evidência em `iot/sentinela/evidencias/`).
- [ ] **F5.4** 🤖 Comandos do Core no OLED + fila offline de eventos.
  🔎 roteiro, passos 2–5, com a Sentinela real; um check-in feito offline chega depois.
  ⏸ 2026-11-19: um "estou bem" de 2 min atrás chega depois e o Core aceita; o perigo abre o alerta e o Core publica o
  comando `ALERTA` assinado (`comando.publicado`), e o código do firmware confere o comando do vetor do contrato e recusa
  o adulterado. Falta a rodada que recebe o comando pelo broker público: o ouvinte assinava tarde (uma corrida, agora
  resolvida com um tópico de prova) e a carência de 1 h do D13 adiou a nova tentativa (`make integrar-sentinela`). A
  fila descarta sozinha o que passou da janela de 5 min do Core, em vez
  de gerar um falso replay. O roteiro com a Sentinela rodando no Wokwi fica com o operador (`make demo-wokwi`).
- [ ] **F5.5** 🤖 README (circuito, pinagem, lista de materiais com custo pesquisado, estados em Mermaid, contrato, link)
  + `integrantes.txt`.
  🔎 o link do Wokwi abre deslogado.
  ⏸ 2026-11-19: README e `integrantes.txt` prontos, com o roteiro do Wokwi e o aviso da carência de 1 h para a gravação.
  Os custos da lista de materiais são estimativas de ordem de grandeza, e o README diz isso; falta colar o link público
  do Wokwi.

**✅ Aceite F5:** integração real demonstrável (não é projeto paralelo) · link público · `.txt` com os integrantes.

### 8.7 F6 — App (Mobile) 🤝

**Objetivo:** os 6 critérios da rubrica de Mobile. **Spec:** 05.

- [x] **F6.1** 🤖 Scaffold Expo SDK 58 + Expo Router + TS estrito + tema dos tokens + `Farol`, `CartaoCasa`,
  `BotaoGigante` + lint, typecheck e Jest — 2026-11-17 17:56.
  🔎 `npx expo-doctor` ok; o app abre no Expo Go.
- [ ] **F6.2** 🤖 Cliente da API (TanStack Query), login (secure-store) e SQLite com migrações para cache.
  🔎 abre offline com os últimos dados.
  ⏸ 2026-11-17: coberto em Jest (API fora do ar → dados do SQLite) e com as migrações num SQLite real; o 🔎 no
  aparelho espera a API do Core (F1.7).
- [ ] **F6.3** 🤖 Início, Casa (gráfico de 24 h), Alertas (com reconhecer) e SignalR.
  🔎 o simulador deixa o card vermelho sem recarregar (P10).
  ⏸ 2026-11-17: coberto em Jest (evento `LeituraRegistrada` → card em `PERIGO` sem recarregar); o 🔎 no aparelho
  espera o hub do Core (F1.7).
- [ ] **F6.4** 🤖 Modo Idoso (gigante, voz, háptico) + fila offline de check-in.
  🔎 check-in feito em modo avião sincroniza ao voltar a rede.
  ⏸ 2026-11-17: coberto em Jest (sem rede → fila no SQLite → enviado com o horário do toque quando a rede volta); o
  🔎 no aparelho espera a API do Core (F1.7).
- [ ] **F6.5** 🤖 Perto de mim, Privacidade (farol) e cadastro de morador com máscaras.
  🔎 CPF aparece mascarado; o farol reflete `/risco-cidadao`.
  ⏸ 2026-11-17: coberto em Jest (CPF, CNS e saúde mascarados; farol da casa com cor + ícone + texto). O app é
  `Cuidador`, então lê `GET /domicilios/{id}/privacidade` (o `/privacidade/risco-cidadao` é do `Operador`). O 🔎
  no aparelho espera a API do Core (F1.7).
- [x] **F6.6** 🤖 Modo demonstração (fixtures quando a API não responde) + notificações locais — 2026-11-17 18:29.
  🔎 sem backend, o app abre com dados de exemplo e um aviso claro.
**Nível alto (operador, 2026-11-17).** A régua são os apps mobile anteriores do time (Pitlane, Obscurial, Orbital
Guardian): domínio puro e testável, local-first de verdade, recursos nativos com propósito, qualidade medida no CI.
Roda **antes** da F6.7 e da F6.8. Ordem de execução: F6.9 → F6.10 → F6.12 → F6.13 → F6.11 (os repositórios e a
outbox nascem testados sozinhos; a F6.11 faz a virada das telas) → F6.14 em diante. O app continua abrindo no Expo
Go; o APK via EAS é um extra.

- [x] **F6.9** 🤖 Domínio puro (`src/domain`, sem React Native): `Result`, `Clock`, value objects com factory
  (`Temperatura`, `UmidadeRelativa`, `IndiceDeCalor`, `Cpf`, `CartaoSus`, `Telefone`) e a classificação dos 5 níveis.
  🔎 o `IndiceDeCalor` em TypeScript passa nos 20 vetores de `docs/contratos/indice-de-calor.vetores.json` — 2026-11-17.
- [x] **F6.10** 🤖 Escalonamento e risco no domínio: máquina de estados do alerta (degraus 0 → 3, prazos por nível,
  regra da noite, D13) e risco personalizado explicável (R3: limiar por fator, contribuição de cada um).
  🔎 testes de toda transição válida e inválida; cada fator explica quanto baixou o limiar — 2026-11-17. As reduções
  do R3 (≥ 80 anos −2 °C · cardiopatia −2,5 · diurético −1,5) são propostas do app; o Core passou a segui-las na F6.24 (D17).
- [x] **F6.11** 🤖 Casos de uso, ports, composition root e Zod no contrato: as telas falam só com os casos de uso;
  toda resposta da API passa por um schema Zod antes de entrar no domínio. Com a virada: SQLite como fonte da verdade
  das telas, escritas otimistas com rebase das pendentes sobre cada retrato do servidor, Unit of Work com transações
  serializadas, e a demonstração como um `CoreGateway` que usa a mesma máquina de estados do domínio.
  🔎 teste de arquitetura: `domain` não importa nada de fora; `presentation` não importa `infrastructure` — 2026-11-17.
  Os testes de tela acharam dois bugs de concorrência, corrigidos com teste de regressão: uma escrita durante uma
  drenagem esperava a próxima rede, e duas cadeias dividiam a mesma transação (um ROLLBACK desfazia o envio da outra).
- [x] **F6.12** 🤖 Local-first: porta `SqlDatabase` (`expo-sqlite` no aparelho, `sql.js` no Jest), migrações
  versionadas e repositórios; o SQLite vira a fonte da verdade das telas (a virada das telas é na F6.11).
  🔎 repositórios testados com SQL real no Jest — 2026-11-17 (inclui a migração 3 → 4 sem perder check-in da fila).
- [x] **F6.13** 🤖 Outbox única + motor de sincronização: toda escrita que pode esperar a rede (check-in e
  reconhecer alerta) com `operationId` idempotente (vai como `Idempotency-Key`), backoff exponencial com jitter e
  dead-letter. O cadastro de morador fica **fora** de propósito: na fila, CPF e CNS inteiros ficariam gravados no
  aparelho; ele exige conexão. A aplicação otimista entra com a virada das telas (F6.11).
  🔎 testes: offline → fila → volta a rede → enviado uma vez só; erro definitivo → dead-letter — 2026-11-17.
- [x] **F6.14** 🤖 Central de sincronização (pendentes, dead-letter, reenviar, forçar offline) + sincronização em
  segundo plano (`expo-background-task`, quando o sistema permitir).
  🔎 forçar offline na tela → check-in fica pendente → desligar → enviado — 2026-11-17 (Jest). O worker só roda no
  APK: no Expo Go do Android o TaskManager não existe, e a Central mostra "não disponível" em vez de fingir.
- [x] **F6.15** 🤖 Replay da onda de calor **real** de nov/2023: script extrai as horas da A701 dos CSVs versionados
  (`ml/dados/brutos`) para um JSON com proveniência (SHA-256); o modo demonstração toca hora a hora com o índice de
  calor calculado no aparelho.
  🔎 o replay real chega ao pico de `CAUTELA_EXTREMA` (laranja) com dados do INMET; o vermelho ao vivo vem do "Simular
  onda de calor", rotulado como simulação — 2026-11-18 (Jest).
  Ajuste do 🔎 (decisão do operador, 2026-11-18): a premissa original ("chega a `PERIGO` com dados reais") é falsa para
  a A701. O índice de calor real máximo é 35,5 °C (14 e 15/11), `CAUTELA_EXTREMA`, e nenhuma hora chega a `PERIGO`
  (39,44 °C) pela régua da NWS. A Dona Lurdes segue como está, e o app mostra o nível padrão e o personalizado (R3).
  ⚠ Para o Clima (F3) e o ML (F4): o passo 2 da §3.4 ("o modelo prevê perigo em 24 h" a partir do replay real) depende
  da mesma premissa. O vermelho do passo 3 vem do Wokwi e não é afetado.
- [x] **F6.16** 🤖 Risco explicável na tela da casa: "por que a Dona Lurdes está em risco" (fatores e limiar).
  🔎 o limiar exibido bate com o domínio para cada combinação de fatores — 2026-11-17 (8 combinações × 2 idades).
- [ ] **F6.17** 🤖 Perto de mim com GPS e bússola: distância (haversine) e uma seta grande até o ponto fresco.
  🔎 testes de distância e rumo; seta conferida no aparelho do operador.
  ⏸ 2026-11-17: testes de distância, rumo e direção em palavras verdes; falta o operador conferir a seta girando no
  celular (Expo Go tem localização e bússola).
- [ ] **F6.18** 🤖 Parear a Sentinela por QR (câmera; o segredo HMAC vai para o SecureStore) + Rotina (check-in
  diário e lembrete de água agendados, nunca entre 22h e 7h).
  🔎 testes do parser do QR e dos horários; QR lido no aparelho.
  ⏸ 2026-11-17: parser, rotina (R11/R12), fuso de SP, vínculo e telas testados; o formato do QR
  (`sombra://sentinela/vincular?v=1&d=snt-…&c=…`) é proposta do app e precisa ser exibido pela Sentinela (F5);
  falta ler um QR de verdade no aparelho.
- [x] **F6.19** 🤖 Biometria na área da família (protege a PII) + Centro de Privacidade (exportar os dados locais em
  JSON, LGPD art. 18, e apagar tudo do aparelho).
  🔎 testes do bloqueio e do export sem PII inteira — 2026-11-17. Face ID não roda no Expo Go do iOS (digital no
  Android, sim); o Modo Idoso nunca é bloqueado.
- [ ] **F6.20** 🤖🔑 Qualidade: teste do app inteiro (rotas reais), gate de cobertura ≥ 95% no CI e APK via EAS
  (`eas.json`; o job roda só com `EXPO_TOKEN` nos secrets, pedido ao dono do repositório).
  🔎 CI verde com o gate; APK gerado quando o token existir.
  ⏸ 2026-11-18: local verde com o gate (284 testes; 97,7% statements · 98,5% linhas · 95,8% funções · 91,3% branches;
  limite 95/95/95/90), teste do app inteiro com as rotas reais, `expo-doctor` 20/20 e bundle Hermes. Fora da conta, de
  propósito: os adaptadores nativos (`infrastructure/native`, expo-sqlite, container do aparelho), que só rodam no
  celular. Falta o CI no PR. Sem APK nesta entrega (decisão do operador, 2026-11-18): o app é avaliado pelo Expo Go,
  e o job `apk` fica pulado enquanto `EXPO_TOKEN` e `EAS_PROJECT_ID` não existirem nos secrets.
**O Core no contrato do app (D17).** O app nasceu antes da API v1 e, contra o Core real (2026-11-18), 5 das 7 leituras
falhavam nos schemas Zod e o cadastro de morador e a rotina davam 400. O Core se adapta ao app, sem mudar o `mobile/`.
Roda antes de qualquer outra fase; os commits são `feat(core)`.

- [x] **F6.21** 🤖 Leituras: a lista de casas devolve o formato do detalhe; o alerta leva `apelidoDaCasa` e a linha do
  tempo (persistida, migração nova); os estados saem como o app os conhece (`INFORMATIVO` e `AGUARDANDO_CHECK_IN` →
  `AGUARDANDO_CHECKIN`, `EXPIRADO` → `RESOLVIDO`) e o prazo do preventivo é a validade de 24 h; rede como lista com
  `id`; morador com `telefone`; rotina como `checkInDiario` e `lembreteDeAgua`.
  🔎 as respostas reais do Core passam nos schemas de `mobile/src/infrastructure/http/schemas.ts` — 2026-11-18 (as 8
  leituras do app, logado como `ana.filha`; telefones inteiros só para o Cuidador da casa, que liga dali; 142 testes).
- [x] **F6.22** 🤖 Escritas: morador por `idade` (o Core guarda a data aproximada), com `telefone` e `perfilDeSaude`
  (sempre mascarados na saída), edição completa e `IDADE_AVANCADA` declarável; Cartão SUS obrigatório no cadastro,
  porque a leitura do app sempre espera um; rotina no formato do app; vínculo da Sentinela com o `codigoDeVinculo` do
  QR (6 caracteres derivados do `deviceId` por uma chave de vínculo) e resposta com `segredo`.
  🔎 cadastro, edição, rotina e vínculo no formato do app voltam 2xx e passam nos schemas — 2026-11-18 (contra o Core
  real: 13 de 13 respostas nos schemas do app; código errado volta 422 `VINCULO`; `make vinculo DEVICE=…` imprime o QR;
  153 testes).
- [x] **F6.23** 🤖 Fila offline de verdade: o check-in aceita o `momento` do toque (nunca no futuro, até 24 h atrás) e
  o `Idempotency-Key` vale no check-in e no reconhecer (a mesma chave devolve a mesma resposta, sem repetir o efeito).
  🔎 check-in feito offline resolve o alerta com o horário do toque; reenviar com a mesma chave não duplica nem vira 422
  — 2026-11-18 (R14 na spec 01; teste de API com PostgreSQL real: a repetição volta com `Idempotency-Replayed`, um alerta
  só, e o reconhecer reenviado volta 200; migração 9; 157 testes).
- [x] **F6.24** 🤖 R3 do app no Core: cardiopatia −2,5 °C, sem teto, nenhum limiar abaixo do início de `ATENCAO`;
  vetores em `docs/contratos/risco-personalizado.vetores.json`, conferidos pelo Core e pelo app.
  🔎 os dois lados passam nos mesmos vetores; spec 01 atualizada — 2026-11-18 (16 casos, 8 combinações × 79 e 80 anos, no
  Jest do app e no domínio do Core; 173 testes no Core). Para a DA6: com ≥ 80 anos e cardiopatia, o `PERIGO` pessoal começa
  em 34,94 °C, e o pico real de nov/2023 (35,47 °C) já é perigo para essa pessoa.
- [x] **F6.25** 🤖 As rotas que o app já usa: `GET /domicilios/{id}/privacidade` (farol pelos eventos de segurança da
  casa, mensagens por template; a F2.5 troca o cálculo por dentro) e `GET /domicilios/{id}/pontos-de-resfriamento`
  (lista versionada, rotulada como exemplo; a F3.9 passa a buscar no Clima).
  🔎 as duas respostas passam nos schemas do app, e o formato não muda na F2.5 nem na F3.9 — 2026-11-18 (15 de 15
  respostas do Core real nos schemas do app; uma telemetria adulterada pelo simulador deixa o farol da casa amarelo; a
  referência da casa é a estação A701 da região; 181 testes).
- [x] **F6.26** 🤖 Contrato vivo: um teste de integração do Core grava amostras reais em `docs/contratos/amostras/` e o
  Jest do app passa cada uma pelos schemas Zod; README do Core e `core-api.md` em dia.
  🔎 mudar um campo no Core sem mudar o app deixa o CI vermelho — 2026-11-18 (17 amostras; renomear `apelidoDaCasa` numa
  amostra derruba o teste do Core e o Jest do app; `make -C services/core-api amostras` regrava; Core com 182 testes,
  app com 318).
- [ ] **F6.7** 🧑🤖 Acessibilidade (checklist da spec) + testes Jest.
  🔎 checklist marcado com prints; vídeo curto com TalkBack gravado no celular do operador.
  ⏸ 2026-11-18: checklist da spec no README; faltavam a dica do leitor de tela em interruptores, chips e 16 botões
  (agora obrigatória no tipo do `Botao`) e o tamanho do texto dentro do app (Normal, Grande, Maior, gravado no
  aparelho), ambos com teste. Falta o operador gravar o vídeo com TalkBack.
- [ ] **F6.8** 🤖 README (Ficha, como rodar, variáveis, prints de todas as telas).
  🔎 todos os critérios da rubrica de Mobile com evidência.
  ⏸ 2026-11-18: README com a Ficha, a rubrica com onde está e a evidência de cada critério, a arquitetura, as 13 telas,
  o checklist de acessibilidade com a situação honesta de cada item, como rodar com e sem o Core, as variáveis, a
  qualidade e as limitações; faltam os prints das telas, tirados pelo operador no celular.

**✅ Aceite F6:** os 6 critérios com evidência · app roda com e sem backend.

### 8.8 F7 — Painel web

**Objetivo:** central do operador + Dashboard Executivo de Risco Cidadão. **Spec:** 06.

- [x] **F7.1** 🤖 Scaffold Vite, React 19, TS, Tailwind v4 com os tokens, layout e login de operador.
  🔎 `npm run build` ok — 2026-11-18 (Vite 8, React 19.3, Tailwind 4.3 com os semânticos do `design/tokens.css` como
  utilitários e o tema escuro automático; a folhagem, a marca e os ícones de forma da direção de arte; rota protegida
  só para Operador e Admin; JWT no sessionStorage; erros com o código curto; um hub SignalR para o painel inteiro; o
  Vite repassa `/api` e `/hubs` ao Core, sem CORS; login conferido em 1440 e 390 px).
- [x] **F7.2** 🤖 Visão geral (KPIs + indicador de impacto), Alertas e Casas (PII mascarada) com SignalR.
  🔎 os KPIs mudam ao vivo com o simulador — 2026-11-18 (Chrome automatizado contra o Core do compose, com o operador da
  demo: durante o `make sim CENARIO=onda-de-calor`, os alertas ativos foram de 2 para 1 e a mediana até o "estou bem"
  apareceu sem recarregar; o feed mostrou as leituras, o "estou bem" e a resolução, com o apelido da casa. Casas sem dado
  pessoal: o Core nem entrega moradores ao papel Operador. 8 testes no Vitest, um deles com o evento do hub mudando os
  KPIs; conferido em 1440 e 390 px).
- [x] **F7.3** 🤖 Risco Cidadão: farol global e por categoria, feed com as três traduções, série por sink, evidência
  técnica. Fecha o pilar "Dashboard" do Cyber.
  🔎 rodando a auditoria (F2.8), o farol muda ao vivo — 2026-11-18 (a auditoria da F2.8 roda num Core descartável, então
  o 🔎 usou o cenário de controle de acesso contra o Core do compose: com a página aberta, 6 pedidos sem login aos dados
  de saúde de uma moradora levaram o farol de 51 para 63 eventos sem recarregar. Para isso o Core passou a emitir o
  `RiscoCidadaoAtualizado` do contrato do hub, que ninguém emitia (`fix(core)`, com teste ouvindo o hub). Série por sink
  em small multiples num matiz só, com tabela (vermelho, âmbar e verde são do farol); o evento de integridade sem sink
  ganhou rótulo próprio; resposta fora do contrato vira erro com código curto; conferido em 1440 e 390 px).
- [x] **F7.4** 🤖 Operação: saúde e versão dos serviços, alertas de infra recebidos em `/ops/alertas`.
  🔎 um `POST` manual em `/ops/alertas` aparece na tela — 2026-11-18 (com a página aberta, o `POST` com o token do
  monitor voltou 202 e o alerta crítico de CPU apareceu na hora, pelo hub; Core com banco e broker, Clima com banco e o
  previsor, e as versões, conferidos a cada 15 s; do Clima o navegador só alcança as sondas `/clima/actuator/*`, e o JWT
  do operador não vai para ele).
- [x] **F7.5** 🤖 Vitest + Playwright E2E do roteiro com o simulador.
  🔎 E2E verde — 2026-11-19 (`make e2e-painel`: 6 testes contra o `make up`, com o painel já servido pelo nginx do compose
  (proxy de `/api` e `/hubs` ao Core; do Clima só as sondas): login, a família recusada, o roteiro com o simulador, casas
  sem PII, o farol e a operação ao vivo. O E2E respeita o limite de 5 logins por minuto do Core e a carência de alerta da
  D13. 12 testes no Vitest; os 15 prints das cinco páginas em 1440, 768 e 390 px saem de `make evidencias-painel` e
  foram revisados; na revisão, o preventivo deixou de aparecer como "aguardando o estou bem" (ele não pede resposta, R5)).

**✅ Aceite F7:** as cinco páginas funcionando ao vivo · E2E verde · screenshots revisados (§0.13).

### 8.9 F8 — Infra (OS) 🔑

**Objetivo:** os 6 critérios da rubrica de OS, com a VM reproduzível do zero. **Spec:** 08.

- [x] **F8.1** 🤖 `lib/log.sh` + `lib/auditoria.sh` (o quê, quando, quem, resultado; JSONL; `trap ERR`), `provision.sh`
  orquestrador idempotente e `Vagrantfile`.
  🔎 `vagrant up` grava `provision.log` e `auditoria.jsonl` (P11) — 2026-11-19 (Ubuntu 24.04 `bento/ubuntu-24.04` no
  VirtualBox 7.2 com o Vagrant 2.4.9: os dois arquivos em `/var/log/sombra`, cada linha com quando, quem (o usuário por
  trás do sudo), uid, host, ação e resultado; a primeira rodada falhou de verdade no apt e o `trap` registrou a linha, o
  comando e o código 100, e o orquestrador parou na etapa; 17 testes das bibliotecas num Ubuntu descartável no CI).
- [x] **F8.2** 🤖 Etapas 10 (host), 20 (rede), 30 (DNS com dnsmasq, zona `sombra.local`) e 40 (usuários e permissões).
  🔎 `hostnamectl`, `ip a`, `dig api.sombra.local`, `id sombra` — 2026-11-19 (na VM: `sombra-srv01.sombra.local` no fuso
  de São Paulo; `eth1` com 192.168.56.10/24 pelo netplan; `api`, `clima`, `painel` e o próprio host respondem
  192.168.56.10 e o resto sai pelo DNS da rede; `sombra` sem shell no grupo `sombra-ops`, com sudoers só para os
  serviços `sombra-*`; idempotência: a segunda rodada não mudou nada. Na VM a verificação pegou três defeitos: índice do
  apt velho, `escrever_arquivo` num pipe (subshell) e o dnsmasq parado pela configuração padrão do pacote).
- [ ] **F8.3** 🤖 Workflow de release (`release.yml`, adiado da F0.12): core linux-x64, jar do clima, dist do painel e
  `SHA256SUMS`, com a mesma fixação de actions por SHA do `ci.yml`.
  🔎 um release de teste no GitHub com os checksums.
- [x] **F8.4** 🤖 Etapas 50 (dependências), 60 (banco) e 70 (aplicação: download + SHA256, diretórios, env 0640, units
  endurecidas, migrações, nginx).
  🔎 `systemctl status sombra-*` ativos; `curl` nos `/health` pelos nomes respondem 200 — 2026-11-19 (as 7 etapas de
  primeira: PostgreSQL 16 com um papel por banco e `pg_hba` fechado, Mosquitto, JRE 21, nginx; artefatos conferidos pelo
  `SHA256SUMS` em `/opt/sombra/releases/<versão>` com o link `atual`; o Core self-contained e o Clima como `sombra`, com
  `ProtectSystem=strict`, `NoNewPrivileges`, sem capabilities e `Restart=always`; segredos gerados na VM, 0640
  `root:sombra-ops`, nunca no log; os `/health` de `api`, `clima` e `painel.sombra.local` 200 de dentro da VM e do host;
  só a porta 80 aberta para a rede, e escrita no Clima pelo nginx dá 403. O download do GitHub espera o primeiro release
  (F8.3); aqui os artefatos vieram de `infra/scripts/empacotar.sh`, que faz o mesmo que o `release.yml`).
- [x] **F8.5** 🤖 Monitor (CPU, RAM, disco e serviços; limites em `monitor.conf`; alerta em log, journald e `POST` no Core)
  + `sombra-monitor.service` (`Type=notify`, `WatchdogSec`, `Restart=always`) + logrotate + regras do auditd.
  🔎 `restart`, `stop` e `start` funcionam; um alerta aparece no painel — 2026-11-19 (na VM: `Type=notify` com
  `WatchdogSec=30` e o status mostrando CPU, RAM e disco; `stop` deixa inativo, `start` e `restart` voltam; cada ciclo
  grava `monitor.verificacao` no journal e em `/var/log/sombra/sombra-monitor.log`; com `stress-ng` a CPU foi a 100% e,
  depois de 3 verificações seguidas, o alerta crítico apareceu no painel da própria VM (`painel.sombra.local`) em 57 s;
  logrotate diário com 7 arquivos e a trilha de auditoria só por tempo; 6 regras do auditd em `/etc/sombra`,
  `/opt/sombra`, nas units e no sudoers).
- [x] **F8.6** 🤖 Etapa 90 (validação) + scripts de falha 01–07 + evidências.
  🔎 cada falha gera recuperação ou registro, com print em `infra/evidencias/` — 2026-11-19 (os 7 ✔ na VM: o monitor
  volta do `kill -9` em ~5 s; CPU, memória e disco viram alerta no log, no journal e no painel, e o disco registra a
  volta ao normal; a API volta em 5 s; depois do reboot os 7 serviços voltam sozinhos; a etapa com erro proposital fica
  na auditoria com linha 3, comando e código. A etapa 90 confere 26 pontos e pegou um bug real: a guarda de PII do Core
  barrava os pontos de resfriamento num raio de 3 km, porque os IDs do OSM têm a forma de telefone (`fix(clima)`, V5).
  Os 10 prints saem de `infra/scripts/evidencias.sh` numa rodada só e foram revisados).
- [x] **F8.7** 🧑🤖 Prova de reprodutibilidade: `vagrant destroy -f && vagrant up` sem nenhum passo manual; uma segunda
  execução do `provision.sh` não muda nada.
  🔎 os logs das duas execuções registrados nas evidências — 2026-11-19 (`infra/scripts/reproduzir.sh`: do zero em
  1 min 51 s, 9 etapas, 22 arquivos gravados, validação aprovada; a segunda execução em 10 s, **0** arquivos gravados,
  validação aprovada. Logs em `infra/evidencias/reproducao-*.log`).
- [x] **F8.8** 🤖 shellcheck no CI + README (arquitetura, como reproduzir, como operar, evidências, integrantes).
  🔎 os 6 critérios da rubrica de OS com evidência — 2026-11-19 (o job `shellcheck` do CI roda em todo `*.sh`; o
  `infra/README.md` liga cada critério ao script e ao print, mais os 7 testes de falha, a arquitetura em Mermaid, como
  reproduzir e operar, e os integrantes).

**✅ Aceite F8:** VM do zero ao Sombra inteiro no ar só com scripts · monitor tolerante a falhas · auditoria · 7 testes de
falha documentados.

### 8.10 F9 — Integração ponta a ponta

**Objetivo:** o roteiro da §3.4 inteiro, automatizado e provado em três ambientes.

- [ ] **F9.1** 🤖 `OnnxPrevisorDeCalor` no Clima (features pela `features.json`) + teste de paridade Java × Python.
  🔎 paridade 20/20 (P8).
- [ ] **F9.2** 🤖 `make demo`: o roteiro completo automatizado (compose, simulador e Playwright no painel), com relatório
  de tempos.
  🔎 verde 3 vezes seguidas.
- [ ] **F9.3** 🧑🤖 O roteiro com a Sentinela real no Wokwi, o app no celular e o painel no navegador.
  🔎 o operador roda e aprova.
- [ ] **F9.4** 🤖 O roteiro contra a VM (`*.sombra.local`), com a Sentinela no broker público.
  🔎 `make demo TARGET=vm` verde.
- [ ] **F9.5** 🤖 Rastreio ponta a ponta: o `correlationId` do MQTT até o SignalR.
  🔎 um alerta é seguido pelos logs do Core e do Clima com um único ID.

**✅ Aceite F9:** o roteiro roda igual no compose, na VM e com a Sentinela real.

### 8.11 F10 — QA (Azure Boards)

**Objetivo:** Features (50%) e Test Cases (50%) no Azure, rastreáveis até o código. **Spec:** 09.

- [ ] **F10.1** 🧑 🔑 ⛔ Organização e projeto no Azure DevOps, **professor de QA como Admin**, PAT para o script.
  🔎 o professor aparece em Project Administrators.
- [x] **F10.2** 🤖 🤝 `features.yaml` (F01–F12, Como/Quero/Para + Dado/Quando/Então).
  🔎 revisado pelo operador — 2026-11-18. `qa/especificacao/features.yaml`: 12 Features e 43 critérios de aceite,
  tirados das regras R1–R14 e do contrato de níveis, com prioridade, valor de negócio e MoSCoW (padrão do backlog da
  aula).
- [x] **F10.3** 🤖 🤝 `casos-de-teste.yaml` (40–50, positivos e negativos, técnica de cada um, vínculo com o teste
  automatizado).
  🔎 cada Feature tem pelo menos 1 negativo — 2026-11-18 (`qa/ferramentas/conferir_especificacao.py`: 50 casos, 22
  positivos e 28 negativos, as quatro técnicas, todo critério coberto e os 102 testes citados existem no repositório;
  48 automatizados e 2 manuais; com a F2, o TC39 também ficou automatizado: 49 e 1).
- [ ] **F10.4** 🤖 `sincronizar_azure.py` idempotente (REST + PAT).
  🔎 o Boards mostra tudo, com passos e vínculos; uma segunda execução não duplica nada (P12).
- [ ] **F10.5** 🧑🤖 Prints (Playwright com sessão ou manual), matriz de rastreabilidade e PDF.
  🔎 o PDF tem o link do projeto e prints de todas as telas exigidas.

**✅ Aceite F10:** Features e Test Cases completos no Azure · professor como Admin · PDF pronto.

### 8.12 F11 — Polimento nível HENRY

**Objetivo:** o acabamento que diferencia, sem tocar no que já garante a nota. Cada item vem da seção "Nível HENRY" da
spec. Um checkpoint por superfície. **Por D18, só entra o que melhora acessibilidade ou uso de verdade; o cosmético e a
F11.8 (VM no Azure) saíram.**

- [ ] **F11.1** 🤖 Core: Stryker no domínio, OpenTelemetry, C4 no README, arquivos `.http` da demonstração.
- [ ] **F11.2** 🤖 Clima: cache por região, métricas Micrometer, página de evidências.
- [ ] **F11.3** 🤖 ML: figuras com os tokens, validação na A771, gráfico do replay hora a hora.
  ⏸ 2026-11-18: figuras com os tokens e replay prontos na F4; falta a validação na A771, que exige baixar os dados dela.
- [ ] **F11.4** 🤖 Sentinela: ícones no OLED, luz que acende e apaga devagar (sem piscar, D13), `wokwi-cli` no CI.
- [ ] **F11.5** 🤖 App: animações com Reanimated, ilustrações, tema escuro, ícone, splash, onboarding.
- [ ] **F11.6** 🤖 Painel: transições do farol, modo apresentação, mapa por bairro (agregado).
- [ ] **F11.7** 🤖 Cyber: mapa regra × sink, relatório LGPD gerado, antes e depois lado a lado no dashboard.
- [ ] ~~**F11.8**~~ fora do escopo (D18): VM no Azure.
- [ ] **F11.9** 🧑🤖 Revisão visual final: screenshots do painel (390/768/1440) e do app (dois tamanhos), revisados.
  🔎 de cada um: o operador aprova.

**✅ Aceite F11:** o operador aprovou cada superfície visual.

### 8.13 F12 — Documentação final, PDFs e defesa

- [ ] **F12.1** 🤖 README raiz final: Ficha completa, mapa do repositório, link de cada disciplina, como rodar tudo,
  integrantes. `prompts.md` consolidado.
  🔎 lido numa aba anônima, sem link quebrado (verificado por script).
- [ ] **F12.2** 🤖 A partir da F0.16: `docs/entrevista.md`, `docs/problema.md`, `docs/rabiscos/` (wireframes, fluxo de
  dados, circuito) e a Ficha final.
  🔎 nenhuma informação que identifique a pessoa.
- [ ] **F12.3** 🤖 PDF de Cyber (`security/entrega/cyber.html` → PDF): capa com integrantes e uma seção para cada pilar
  (Parsing PII, Taint, IA, Dashboard), com prints de código e de evidência. ZIP do código **separado**.
  🔎 os 4 pilares no PDF; o ZIP abre e compila.
- [ ] **F12.4** 🧑🤖 `docs/defesa/`: guia por parte + perguntas prováveis + sessão de 30 min com o time.
  🔎 cada integrante explicou uma parte que não fez.

**✅ Aceite F12:** README, PDF de Cyber, ZIP, entrevista e defesa prontos.

### 8.14 F13 — Vídeos e pitch 🔑

**Spec:** 10. **Por D18:** primeiro os 5 vídeos obrigatórios (F13.4 e F13.6), capturados por script numa rodada só do
`make demo` e cortados por matéria; o pitch (F13.1–F13.3, F13.5) só se sobrar tempo, porque vale só para o prêmio.

- [ ] **F13.1** 🤖 Pipeline do HENRY copiado para `pitch/`, `kit.css` do Sombra e kit técnico (template dos vídeos de
  disciplina).
  🔎 render de teste de 10 s (P14).
- [ ] **F13.2** 🤖 Pesquisa com fontes primárias para o ato 1 (calor em SP e saúde da pessoa idosa).
  🔎 cada número tem link.
- [ ] **F13.3** 🧑🤖 Roteiro e storyboard do pitch (fecha a DA4).
  🔎 aprovado pelo operador.
- [ ] **F13.4** 🤖 Capturas em 1080p: Playwright a partir do `make demo`, Wokwi, VM, emulador; fotos do território
  (D14) com a placa desfocada.
  🔎 os clipes cobrem todos os atos.
- [ ] **F13.5** 🧑🤖 Render do pitch (≤ 4:00), mix e legendas.
  🔎 o operador aprova.
- [ ] **F13.6** 🧑🤖 Vídeos de IoT (≤ 4), Mobile (≤ 3), ML (≤ 5), OS (≤ 7) e Cyber.
  🔎 o operador aprova cada um.
- [ ] **F13.7** 🧑 🔑 Upload no YouTube como não listado + links nos READMEs.
  🔎 cada link abre numa aba anônima.

**✅ Aceite F13:** 6 vídeos dentro do limite, aprovados, com os links publicados.

### 8.15 F14 — Entrega

- [ ] **F14.1** 🤖 `tools/checklist-entrega.sh`: confere links (anônimos), arquivos exigidos, durações dos vídeos,
  integrantes em cada entrega.
  🔎 sai com 0.
- [ ] **F14.2** 🧑 🔑 Envio das 8 tarefas no Teams (um integrante por disciplina).
  🔎 print de cada envio em `docs/evidencias/entrega/`.

**✅ Aceite F14:** as 8 tarefas enviadas até 19/11, 23h55.

---

## 9. Paralelismo e delegação

- **Caminho principal:** F0 → F1 → F2 → F3 → F8 → F7 → F9 → F11 → F12 → F13 → F14. É onde ficam as contas, a VM e a
  integração.
- **Bons pontos de partida 🤝** (uma sessão nova só precisa do `docs/PLANO.md` §0 e da spec): a **F4 inteira** (ML,
  depois da F0.15), a **F6 inteira** (App, contra o contrato e o simulador; liga no Core quando a F1.7 existir), a
  **F5.2** (firmware offline) e a **F10.2–F10.3** (YAML de QA). Cada operador escolhe a sua parte e decide dentro da
  spec.
- **Podem correr juntos:** F3 com F1 (serviços e bancos separados) · F4 com qualquer fase · F10.2–F10.3 a partir da F0 ·
  F13.1–F13.3 a partir da F9.
- **Nunca em paralelo:** duas sessões no mesmo serviço · migrações do mesmo banco · duas sessões editando a mesma spec.
- **Ao chegar:** a pessoa abre a sessão com o prompt do `AGENTS.md` (F0.14). O agente dela mostra os checkpoints
  abertos da parte escolhida, segue a §0 e a spec e entrega por PR. Sem issues nem quadro: o plano é o quadro. Para
  abrir PR, a pessoa precisa ser colaboradora do repositório.

---

## 10. Riscos

| Risco | Mitigação |
|---|---|
| Wi-Fi/MQTT do Wokwi instável na gravação | simulador com o mesmo contrato; vídeo de IoT gravado cedo; P9 checada na F5.3 |
| Site do INMET fora do ar | F0.15 roda primeiro; CSVs da A701 versionados com a proveniência |
| Cota da Gemini | cache por assinatura do evento + templates (`fonte: template`) |
| Integração travar | contratos na F0, stub do Clima na F1, simulador, compose e E2E desde cedo |
| Test Plans pedir licença no Azure | Test Cases como work items vinculados (P12); Test Plan é extra |
| Três stacks ao mesmo tempo | uma fase por serviço; contratos isolam as fronteiras; delegação 🤝 |
| Pipeline de vídeo não renderizar | P14 testada na F13.1, antes de gravar qualquer coisa |
| Prazo | primeiro o piso (F1–F10), polimento só na F11; janela-alvo na §8.0 |

---

## 11. Premissas a verificar

| # | Premissa | Verificar em | Status |
|---|---|---|---|
| P1 | Os vetores do índice de calor seguem a equação oficial da NWS/WPC | F0.6 | ✅ 2026-11-17: fórmula, coeficientes e ajustes transcritos da página da NWS/WPC; 20 vetores conferidos. A página deixa ambígua a saída abaixo de 80 °F; a leitura adotada está no contrato |
| P2 | Os históricos anuais do INMET (portal de dados históricos) trazem a A701 de 2010 a 2025, horária, com `-9999` para faltante | F0.15 | ✅ 2026-11-17: os 16 anos baixados de `portal.inmet.gov.br/uploads/dadoshistoricos/{ano}.zip`, todos com a A701 e as 7 colunas usadas; 140.256 horas, 893 sem temperatura (0,64%) |
| P3 | O `Invoke-CodeQuality.ps1` do professor roda no Linux (pwsh) e dá 100 com a estrutura nova | F1.1 | 🟡 deu 100/100 no CP06 (ExpenseHub) |
| P4 | Testcontainers funciona na máquina de desenvolvimento (Docker) | F1.6 | ✅ 2026-11-17: Testcontainers 4.17 sobe o `postgres:16-alpine` de dentro do contêiner do SDK (socket do Docker, grupo e rede do host) |
| P5 | A chave Gemini atual responde, com cota gratuita suficiente para a demo | F2.6 | ✅ 2026-11-18: `models.list` e `generateContent` respondem com a chave (header `x-goog-api-key`); `gemini-3.5-flash-lite` em 1–4 s; os Flash 3.6/3.8 davam 503 ou timeout na hora, daí o padrão Flash-Lite, o timeout de 8 s e o template |
| P6 | O Semgrep CE em modo taint cobre as regras planejadas em C# | F2.7 | ✅ 2026-11-18 (Semgrep 1.177.0): as 3 regras acham os 8 vazamentos do canário, nenhum falso positivo, e o Core de produção sai limpo; limites: o parser não lê os raw strings do C# 11 dos testes (fora da varredura) e parâmetro solto (a senha na query do A7) não é fonte, o que fica com o taint dinâmico |
| P7 | A Open-Meteo dá, sem chave, as variáveis horárias que o modelo usa (temperatura, umidade, pressão, radiação, vento), incluindo horas passadas | F3.5 | ✅ 2026-11-18: chamada real com `past_hours=24`, sem chave; radiação vem em W/m² (× 3,6 = kJ/m² do INMET) |
| P8 | XGBoost → ONNX (onnxmltools) e ONNX Runtime Java dão a mesma probabilidade | F4.6, F9.1 | 🟡 2026-11-18: ONNX Runtime em Python bate com o XGBoost (|Δ| < 1e-6, com ausentes); falta o Java na F9.1 |
| P9 | O ESP32 no Wokwi conecta num broker MQTT público pela rede `Wokwi-GUEST`, e o projeto abre por link público | F5.3, F5.5 | ⬜ |
| P10 | `@microsoft/signalr` funciona no Expo SDK 58 (Expo Go) | F6.3 | ⬜ |
| P11 | Vagrant + VirtualBox sobem Ubuntu 24.04 na máquina de desenvolvimento (Arch) | F8.1 | 🟡 2026-11-17 (doctor): VirtualBox 7.2.22 instalado; **Vagrant ausente** e fora dos repositórios oficiais do Arch (só AUR). Alternativa: VBoxManage + imagem cloud do Ubuntu com cloud-init. Decidir na F8.1 |
| P12 | A API REST do Azure DevOps cria Test Cases com passos e vínculos sem licença de Test Plans | F10.4 | ⬜ |
| P13 | Há uma conta Azure for Students ativa | — | dispensada (D18: sem VM no Azure) |
| P14 | O pipeline do HENRY (HyperFrames, FFmpeg, Chrome headless, voz Gemini TTS) roda de novo, com cota de TTS | F13.1 | 🟡 rodou em out/2026 no Challenge |

Legenda: ⬜ não verificada · 🟡 conferida em documentação ou uso anterior, falta o teste real · ✅ confirmada ·
⚠️ falsa (com o ajuste feito no plano).

---

## 12. Painel de status

**Meta:** as 8 tarefas enviadas até 19/11, 23h55, com o **✅ Aceite** de F0 a F14 passando.

**Agora:** **F1, F2 e F3 concluídas** (Core em C# com 365 testes e quality 100/100; privacidade com a auditoria A1–A8; Clima Service em Java com 144 testes, autoavaliação de SOA 10/10 e o Core consumindo o Clima de verdade). DA6 fechada pela D19 (o preventivo segue o limite pessoal da R3). Pendente do operador: os 🔎 do app no celular junto com a F9 e o que é do operador na F6.7 e na F6.8. Próximo no caminho principal: **F8 (Infra)**, depois F7 (Painel) e F9 (Integração). Em paralelo: ML (F4, Matheus) e, no QA, a F10.1 (Azure DevOps, 🔑).

| Fase | Status | Início | Fim | Notas |
|---|---|---|---|---|
| F0 Fundação | ✅ concluída | 2026-11-17 | 2026-11-17 | aceite assinado: CI verde, `make up`, contratos, direção de arte, simulador, dados do INMET e doctor |
| F1 Core (C#) | ✅ concluída | 2026-11-17 | 2026-11-18 | aceite assinado: POO, Web API, EF Core + LINQ, Async e Qualidade com evidência cada (README da pasta); `make quality` = 100; `make test` verde (140); roteiro de alerta funcionando com o simulador |
| F2 Privacidade (Cyber) | ✅ concluída | 2026-11-18 | 2026-11-18 | aceite assinado: motor de PII com regras em JSON (63 casos, 0 falso negativo); taint dinâmico (entrada, cifra em repouso, guardas de log, resposta, saída externa, MQTT e exceções) e estático (Semgrep com canário e SARIF); IA na remediação (Gemini Flash-Lite, guarda, cache e template); farol do Risco Cidadão; auditoria antes × depois com os 8 cenários; `make quality` = 100 |
| F3 Clima (SOA) | ✅ concluída | 2026-11-18 | 2026-11-18 | aceite assinado: autoavaliação 10/10 com evidência por subitem no README da pasta (144 testes, Testcontainers, ArchUnit, contrato OpenAPI conferido); o Core consome o Clima de verdade (risco e pontos, circuito, mesmo `correlationId`) e abre o preventivo a partir do replay real pelo limite pessoal (D19) |
| F4 ML | ✅ concluída | 2026-11-18 | 2026-11-18 | aceite assinado (Matheus): os 6 critérios com evidência no README da pasta; notebook executado; 2 modelos ONNX com paridade 20/20; 75 testes. No teste, 74 de 75 dias de calor avisados, com 7,8 dias de alarme falso por mês; o XGBoost empata com a persistência na PR-AUC. A F9.1 precisa da ressalva do pico (F4.6) |
| F5 Sentinela (IoT) | 🟨 em andamento | 2026-11-19 | | firmware compilado para o ESP32 e 47 testes no host; integração com o Core provada pelo broker do compose e pelo público (F5.3 ✔); faltam o projeto no Wokwi, os 🔎 visuais e o link (operador) |
| F6 App (Mobile) | 🟨 em andamento | 2026-11-17 | | F6.1, F6.6, F6.9–F6.16, F6.19 e F6.21–F6.26 feitas: o app fala com o Core real (contrato vivo, D17); F6.7 e F6.8 ⏸ (falta o vídeo com TalkBack e os prints, do operador); faltam os 🔎 no celular |
| F7 Painel | ✅ concluída | 2026-11-18 | 2026-11-19 | aceite assinado: as cinco páginas ao vivo pelo hub (o Core passou a emitir o `RiscoCidadaoAtualizado`), E2E verde (6) e 12 testes no Vitest, prints revisados em três larguras; o painel sobe no compose pelo nginx |
| F8 Infra (OS) | ✅ concluída | 2026-11-18 | 2026-11-19 | aceite assinado: a VM vazia vira o `sombra-srv01` com o Sombra inteiro no ar só com scripts (9 etapas idempotentes, validação de 26 pontos), monitor `Type=notify` com watchdog, auditoria JSON e auditd, os 7 testes de falha com print, reprodução do zero provada; o 🔎 da F8.3 (release no GitHub) espera a primeira tag `v*` |
| F9 Integração | ⬜ não iniciada | | | |
| F10 QA | 🟨 em andamento | 2026-11-18 | | F10.2–F10.3 feitas (João): 12 Features, 50 casos (49 automatizados), conferidos por `qa/ferramentas/conferir_especificacao.py`; faltam a F10.1 (Azure, 🔑), a F10.4 e a F10.5 |
| F11 Polimento | ⬜ não iniciada | | | |
| F12 Docs e PDFs | ⬜ não iniciada | | | |
| F13 Vídeos | ⬜ não iniciada | | | |
| F14 Entrega | ⬜ não iniciada | | | |

Legenda: ⬜ não iniciada · 🟨 em andamento · ✅ concluída · ⛔ bloqueada

