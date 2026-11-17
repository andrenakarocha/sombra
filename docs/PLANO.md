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
| D3 | **Escopo máximo** nas 8 disciplinas, tudo integrado; "não se preocupar com prazo" | equipe, 2026-11-17 |
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

As decisões de arquitetura têm ADR em [`docs/adr/`](adr/README.md): 001 (D11), 002 (D2), 003 (D4), 004 (MQTT assinado e sem PII), 005 (D7) e 006 (índice de calor replicado com vetores).

### 2.2 Decisões abertas

| # | Decisão | Padrão até lá | Fecha em |
|---|---|---|---|
| DA2 | Bairro da Ficha · persona fictícia do sistema | cidade **São Paulo** confirmada (a A701 continua valendo); bairro "a preencher"; persona "Dona Lurdes, 78 anos", inspirada na Dona Cida | F12.1 (não bloqueia nada) |
| DA3 | Subir a VM pública no Azure | sim, se a F8 fechar com folga | F11.8 |
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
2. O Clima entra em **replay** da onda de calor de nov/2023 (INMET real). O modelo prevê **perigo em 24 h** → alerta
   preventivo → OLED: *"Amanhã: calor perigoso. Encha sua garrafa de água."* → notificação no app da filha.
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
- [ ] **F0.12** 🤖 CI: um workflow só (`ci.yml`) com filtro por pasta, mais gitleaks, shellcheck e actionlint, e um job
  agregador `ci-ok`, que vira o check obrigatório da `main` (D15). Actions fixadas por SHA. O `release.yml` nasce na F8.3,
  quando houver artefatos.
  🔎 CI verde no PR; a proteção da `main` lista o `ci-ok`.
  ⏸ 2026-11-17 16:50: `ci.yml` implementado e validado localmente (actionlint, gitleaks); falta rodar no PR #3.
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

- [ ] **F1.1** 🤖 Scaffold .NET 10 (`.slnx`): `Sombra.Dominio`, `Sombra.Aplicacao`, `Sombra.Infraestrutura`,
  `Sombra.Privacidade`, `Sombra.Api` + projetos de teste; props, editorconfig e StyleCop do CP06; Central Package
  Management; `Invoke-CodeQuality.ps1` e workflow do professor.
  🔎 `make build` sem warnings; `make quality` = 100 (P3).
- [ ] **F1.2** 🤖 Value objects com testes: `Cpf`, `CartaoSus`, `Telefone`, `Endereco`, `Temperatura`, `UmidadeRelativa`,
  `IndiceDeCalor`, `LimitesTermicos`.
  🔎 testes verdes; os vetores compartilhados passam.
- [ ] **F1.3** 🤖 Agregados `Domicilio`, `Morador` (+ `PerfilDeSaude`, fatores de risco polimórficos), `RedeDeCuidado`,
  `Sentinela`, `Leitura`; regras R1, R3, R8 e R9.
  🔎 testes de cada regra, inclusive as violações.
- [ ] **F1.4** 🤖 `AlertaDeCalor` abstrato → `AlertaPreventivo` e `AlertaDeSentinela`; máquina de estados; `CheckIn`; eventos
  de domínio; regras R4–R7 e R10.
  🔎 testes de todas as transições válidas e inválidas; cobertura do domínio ≥ 90%.
- [ ] **F1.5** 🤖 Aplicação: casos de uso, portas (`IDomicilioRepository`, `IAlertaRepository`, `IClimaGateway`,
  `INotificador`, `IRelogio`, `IUnitOfWork`), DTOs e validação.
  🔎 testes dos casos de uso com fakes.
- [ ] **F1.6** 🤖 Infraestrutura EF Core + Npgsql: configurações, owned types, migrações **Inicial**, **AlertasECheckIns**,
  **RedeDeCuidadoEIndices**, **SeedDemo** (personas fictícias); repositórios; consultas LINQ do painel.
  🔎 `make db-reset` cria do zero; testes de integração dos repositórios com Testcontainers (P4).
- [ ] **F1.7** 🤖 API v1: controllers, ProblemDetails, JWT com papéis, rate limiting, `/health/*`, `/version`, OpenAPI +
  Scalar com resumo, tag, exemplo e erros em todo endpoint.
  🔎 o Scalar lista todos os endpoints com os 4xx; testes de API conferem status e corpo.
- [ ] **F1.8** 🤖 Logs estruturados (§6): Serilog JSON, middleware de correlação, log de requisição sem corpo, versão no log.
  🔎 uma requisição gera uma linha JSON com `correlationId` e nenhum dado pessoal.
- [ ] **F1.9** 🤖 Workers e tempo real:
  - `IngestaoMqttWorker`: MQTTnet → HMAC + anti-replay → `Channel<T>` → `RegistrarLeitura`;
  - `EscalonadorDeAlertasWorker`: a cada 15 s;
  - `MonitorDePrevisaoWorker`: contra um stub do contrato do Clima até a F3.9;
  - `PublicadorDeComandosWorker`: outbox → MQTT;
  - hub SignalR `/hubs/sombra`.

  🔎 com o simulador: `onda-de-calor` abre o alerta e emite no hub; `sem-resposta` escalona; `payload-adulterado` é
  descartado e gera evento de segurança.
- [ ] **F1.10** 🤖 Testes de arquitetura (NetArchTest) e teste de integração dos caminhos A e B do roteiro.
  🔎 `make test` verde; relatório de cobertura gerado.
- [ ] **F1.11** 🤖 README da pasta (Ficha, diagrama de classes em Mermaid, como rodar, comando que cria o banco, link do
  Scalar, integrantes) + `docs/evidencias/` (Scalar, migrações, testes, quality 100/100).
  🔎 todos os itens da tabela de rubrica da spec 01 com evidência.

**✅ Aceite F1:** POO, Web API, EF Core + LINQ, Async e Qualidade com evidência cada · `make quality` = 100 · `make test`
verde · roteiro de alerta funcionando com o simulador.

### 8.3 F2 — Privacidade (Cyber), backend

**Objetivo:** os quatro pilares do prof. Vitor no backend; o dashboard visual fecha na F7.3. **Spec:** 07.

- [ ] **F2.1** 🤖 `Sombra.Privacidade`: regras em `pii-regras.json` (com schema validado na carga); detectores CPF, CNS,
  RG, telefone, e-mail, CEP, saúde (CID-10 + dicionário) e credenciais; testes em tabela (positivos, negativos, difíceis).
  🔎 zero falso negativo na tabela; relatório de acertos gerado.
- [ ] **F2.2** 🤖 `PiiScanningMiddleware` + `ContextoDeTaint` + `MascaradorDePii` + política de campo livre.
  🔎 cenário A1 como teste de integração.
- [ ] **F2.3** 🤖 Criptografia em repouso: converters AES-256-GCM, índice cego HMAC para CPF, chave por ambiente,
  migração **CamposSensiveisCifrados**.
  🔎 no `psql`, as colunas aparecem cifradas; a API devolve mascarado.
- [ ] **F2.4** 🤖 `Sensivel<T>` + guardas de sink: log (decorador do `ILoggerProvider`), resposta HTTP (middleware),
  saída externa (`DelegatingHandler` + publicador MQTT) e exceções.
  🔎 cenários A2–A7 como testes de integração.
- [ ] **F2.5** 🤖 Eventos de segurança (outbox), cálculo do `RiscoCidadao` (pesos e decaimento) e endpoints
  `/privacidade/eventos`, `/privacidade/risco-cidadao`, `/domicilios/{id}/privacidade`.
  🔎 testes do cálculo do farol.
- [ ] **F2.6** 🤖 🔑 `TradutorDeRiscoWorker`: prompt sem PII, três audiências (cidadão, gestão, time técnico com sugestão
  de correção), guardas na saída da IA, cache e fallback.
  🔎 com a chave, tradução real registrada; sem chave ou com timeout, cai no template (`fonte: template`); um teste
  prova que o prompt nunca tem PII (P5).
- [ ] **F2.7** 🤖 Semgrep em modo taint (`security/semgrep/`) + canário + job no CI com SARIF.
  🔎 o Semgrep acha o canário e nenhum vazamento no código real (P6).
- [ ] **F2.8** 🤖 Auditoria antes × depois (`security/auditoria/rodar-auditoria.sh`; modo inseguro **só** no ambiente
  `Demo`) + `relatorio.md` e JSON.
  🔎 os 8 cenários: vazam com o módulo desligado e são bloqueados com ele ligado.

**✅ Aceite F2:** motor de PII, taint (dinâmico e estático), IA na remediação e API do farol prontos, com a auditoria
provando o antes e o depois.

### 8.4 F3 — Clima Service (SOA)

**Objetivo:** a autoavaliação do guia do professor em 10/10, com o Core consumindo o serviço. **Spec:** 02.

- [ ] **F3.1** 🤖 Projeto Spring Boot 3.5 / Java 21 com os pacotes `dominio/`, `aplicacao/`, `infraestrutura/` e `api/`,
  Dockerfile, tratamento de erros e padrão de testes reaproveitados do Orbital Guardian (GS 1).
  🔎 `./mvnw verify` verde.
- [ ] **F3.2** 🤖 Domínio com testes: índice de calor (vetores), classificação, `-9999` não é observação, umidade 0–100,
  ponto com `abre < fecha`, região exige estação válida.
  🔎 testes de domínio verdes, sem banco e sem HTTP.
- [ ] **F3.3** 🤖 Flyway V1 (regiões, estações, observações), V2 (pontos de resfriamento), V3 (evolução), V4 (seed: região
  SP-MIRANTE, A701, observações reais de nov/2023 extraídas da F0.15, pontos públicos com fonte); repositórios e consultas
  (período paginado, proximidade); `ddl-auto=validate`.
  🔎 `docker compose down -v && up` + migrate num banco vazio; testes de repositório.
- [ ] **F3.4** 🤖 API, DTOs com Bean Validation, erros (400, 404, 422) e springdoc completo (resumo, tag, exemplo, erros).
  🔎 Swagger mostra os 4xx em todos os endpoints.
- [ ] **F3.5** 🤖 `FonteMeteorologica`: `OpenMeteoClient` agendado e resiliente + `ReplayInmet` (`POST`/`DELETE /replay`).
  🔎 o replay de nov/2023 avança hora a hora; o modo ao vivo traz dado atual (P7).
- [ ] **F3.6** 🤖 `PrevisorDeCalor` com `RegraPrevisorDeCalor` (o ONNX entra na F9.1) e `/risco-calor` completo.
  🔎 teste de API do risco, com status e corpo.
- [ ] **F3.7** 🤖 Logs JSON (§6) aceitando o `X-Correlation-Id` do Core + actuator health.
  🔎 uma chamada do Core aparece nos dois logs com o mesmo `correlationId`.
- [ ] **F3.8** 🤖 Testes de API (MockMvc + Testcontainers) + ArchUnit + README (Ficha, como rodar, comando do banco,
  link e print da doc, endpoints, fonte dos dados, integrantes) + evidências.
  🔎 autoavaliação do guia preenchida com 10/10 e evidência por subitem.
- [ ] **F3.9** 🤖 No Core: o `ClimaClient` real (HttpClient + resiliência) substitui o stub; teste de contrato.
  🔎 o `MonitorDePrevisaoWorker` abre alerta preventivo a partir do replay real.

**✅ Aceite F3:** autoavaliação 10/10 com evidência · o Core consome o Clima de verdade.

### 8.5 F4 — ML 🤝

**Objetivo:** os 6 critérios da rubrica de ML, com o modelo pronto para produção. **Spec:** 03.

- [ ] **F4.1** 🤖 `requirements.txt` fixado, carga dos CSVs da F0.15, limpeza (`-9999`, fuso, buracos) em funções testadas.
  🔎 `pytest` verde (sem vazamento de futuro, `-9999` tratado).
- [ ] **F4.2** 🤖 EDA, alvo (limiar ancorado na tabela NWS e no balanceamento real) e features.
  🔎 seções 1–4 do notebook executadas; limiar justificado no texto.
- [ ] **F4.3** 🤖 Divisão temporal (treino 2010–2021, teste 2022–2025) e baselines (persistência, regressão logística).
  🔎 métricas dos baselines registradas.
- [ ] **F4.4** 🤖 XGBoost com `GridSearchCV` + `TimeSeriesSplit(5)` só no treino: 3 hiperparâmetros, 12 combinações.
  🔎 tabela das 12 combinações, com média e desvio.
- [ ] **F4.5** 🤖 Treino × teste lado a lado, gráficos, overfitting, limiar de decisão escolhido no treino, SHAP.
  🔎 figuras em `ml/figuras/`.
- [ ] **F4.6** 🤖 Conclusão para o cidadão + exportação ONNX + `features.json` + `paridade.csv`.
  🔎 o ONNX (onnxruntime) bate com o XGBoost em 20 amostras (|Δ| < 1e-5) (P8).
- [ ] **F4.7** 🤖 `RELATORIO_ML.md` (2–4 páginas), `dados/README.md` e README da pasta.
  🔎 checklist da rubrica de ML marcado; `jupyter nbconvert --execute` roda do início ao fim sem erro.

**✅ Aceite F4:** os 6 critérios com evidência · notebook executado · modelo e paridade publicados.

### 8.6 F5 — Sentinela (IoT)

**Objetivo:** a porta física do Sombra, integrada de verdade. **Spec:** 04.

- [ ] **F5.1** 🧑🤖 🔑 Projeto no Wokwi (conta do projeto) + circuito + `diagram.json` versionado.
  🔎 o OLED mostra a temperatura do DHT22.
- [ ] **F5.2** 🤖 🤝 Índice de calor (vetores), máquina de estados offline, LED, buzzer e botões.
  🔎 mexendo no DHT22, os estados mudam conforme a tabela de níveis.
- [ ] **F5.3** 🤖 Wi-Fi, NTP, MQTT, HMAC, `seq` e LWT.
  🔎 o Core aceita a telemetria; uma mensagem adulterada é rejeitada (P9).
- [ ] **F5.4** 🤖 Comandos do Core no OLED + fila offline de eventos.
  🔎 roteiro, passos 2–5, com a Sentinela real; um check-in feito offline chega depois.
- [ ] **F5.5** 🤖 README (circuito, pinagem, lista de materiais com custo pesquisado, estados em Mermaid, contrato, link)
  + `integrantes.txt`.
  🔎 o link do Wokwi abre deslogado.

**✅ Aceite F5:** integração real demonstrável (não é projeto paralelo) · link público · `.txt` com os integrantes.

### 8.7 F6 — App (Mobile) 🤝

**Objetivo:** os 6 critérios da rubrica de Mobile. **Spec:** 05.

- [ ] **F6.1** 🤖 Scaffold Expo SDK 58 + Expo Router + TS estrito + tema dos tokens + `Farol`, `CartaoCasa`,
  `BotaoGigante` + lint, typecheck e Jest.
  🔎 `npx expo-doctor` ok; o app abre no Expo Go.
- [ ] **F6.2** 🤖 Cliente da API (TanStack Query), login (secure-store) e SQLite com migrações para cache.
  🔎 abre offline com os últimos dados.
- [ ] **F6.3** 🤖 Início, Casa (gráfico de 24 h), Alertas (com reconhecer) e SignalR.
  🔎 o simulador deixa o card vermelho sem recarregar (P10).
- [ ] **F6.4** 🤖 Modo Idoso (gigante, voz, háptico) + fila offline de check-in.
  🔎 check-in feito em modo avião sincroniza ao voltar a rede.
- [ ] **F6.5** 🤖 Perto de mim, Privacidade (farol) e cadastro de morador com máscaras.
  🔎 CPF aparece mascarado; o farol reflete `/risco-cidadao`.
- [ ] **F6.6** 🤖 Modo demonstração (fixtures quando a API não responde) + notificações locais.
  🔎 sem backend, o app abre com dados de exemplo e um aviso claro.
- [ ] **F6.7** 🧑🤖 Acessibilidade (checklist da spec) + testes Jest.
  🔎 checklist marcado com prints; vídeo curto com TalkBack gravado no celular do operador.
- [ ] **F6.8** 🤖 README (Ficha, como rodar, variáveis, prints de todas as telas).
  🔎 todos os critérios da rubrica de Mobile com evidência.

**✅ Aceite F6:** os 6 critérios com evidência · app roda com e sem backend.

### 8.8 F7 — Painel web

**Objetivo:** central do operador + Dashboard Executivo de Risco Cidadão. **Spec:** 06.

- [ ] **F7.1** 🤖 Scaffold Vite, React 19, TS, Tailwind v4 com os tokens, layout e login de operador.
  🔎 `npm run build` ok.
- [ ] **F7.2** 🤖 Visão geral (KPIs + indicador de impacto), Alertas e Casas (PII mascarada) com SignalR.
  🔎 os KPIs mudam ao vivo com o simulador.
- [ ] **F7.3** 🤖 Risco Cidadão: farol global e por categoria, feed com as três traduções, série por sink, evidência
  técnica. Fecha o pilar "Dashboard" do Cyber.
  🔎 rodando a auditoria (F2.8), o farol muda ao vivo.
- [ ] **F7.4** 🤖 Operação: saúde e versão dos serviços, alertas de infra recebidos em `/ops/alertas`.
  🔎 um `POST` manual em `/ops/alertas` aparece na tela.
- [ ] **F7.5** 🤖 Vitest + Playwright E2E do roteiro com o simulador.
  🔎 E2E verde.

**✅ Aceite F7:** as cinco páginas funcionando ao vivo · E2E verde · screenshots revisados (§0.13).

### 8.9 F8 — Infra (OS) 🔑

**Objetivo:** os 6 critérios da rubrica de OS, com a VM reproduzível do zero. **Spec:** 08.

- [ ] **F8.1** 🤖 `lib/log.sh` + `lib/auditoria.sh` (o quê, quando, quem, resultado; JSONL; `trap ERR`), `provision.sh`
  orquestrador idempotente e `Vagrantfile`.
  🔎 `vagrant up` grava `provision.log` e `auditoria.jsonl` (P11).
- [ ] **F8.2** 🤖 Etapas 10 (host), 20 (rede), 30 (DNS com dnsmasq, zona `sombra.local`) e 40 (usuários e permissões).
  🔎 `hostnamectl`, `ip a`, `dig api.sombra.local`, `id sombra`.
- [ ] **F8.3** 🤖 Workflow de release (`release.yml`, adiado da F0.12): core linux-x64, jar do clima, dist do painel e
  `SHA256SUMS`, com a mesma fixação de actions por SHA do `ci.yml`.
  🔎 um release de teste no GitHub com os checksums.
- [ ] **F8.4** 🤖 Etapas 50 (dependências), 60 (banco) e 70 (aplicação: download + SHA256, diretórios, env 0640, units
  endurecidas, migrações, nginx).
  🔎 `systemctl status sombra-*` ativos; `curl` nos `/health` pelos nomes respondem 200.
- [ ] **F8.5** 🤖 Monitor (CPU, RAM, disco e serviços; limites em `monitor.conf`; alerta em log, journald e `POST` no Core)
  + `sombra-monitor.service` (`Type=notify`, `WatchdogSec`, `Restart=always`) + logrotate + regras do auditd.
  🔎 `restart`, `stop` e `start` funcionam; um alerta aparece no painel.
- [ ] **F8.6** 🤖 Etapa 90 (validação) + scripts de falha 01–07 + evidências.
  🔎 cada falha gera recuperação ou registro, com print em `infra/evidencias/`.
- [ ] **F8.7** 🧑🤖 Prova de reprodutibilidade: `vagrant destroy -f && vagrant up` sem nenhum passo manual; uma segunda
  execução do `provision.sh` não muda nada.
  🔎 os logs das duas execuções registrados nas evidências.
- [ ] **F8.8** 🤖 shellcheck no CI + README (arquitetura, como reproduzir, como operar, evidências, integrantes).
  🔎 os 6 critérios da rubrica de OS com evidência.

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
- [ ] **F10.2** 🤖 🤝 `features.yaml` (F01–F12, Como/Quero/Para + Dado/Quando/Então).
  🔎 revisado pelo operador.
- [ ] **F10.3** 🤖 🤝 `casos-de-teste.yaml` (40–50, positivos e negativos, técnica de cada um, vínculo com o teste
  automatizado).
  🔎 cada Feature tem pelo menos 1 negativo.
- [ ] **F10.4** 🤖 `sincronizar_azure.py` idempotente (REST + PAT).
  🔎 o Boards mostra tudo, com passos e vínculos; uma segunda execução não duplica nada (P12).
- [ ] **F10.5** 🧑🤖 Prints (Playwright com sessão ou manual), matriz de rastreabilidade e PDF.
  🔎 o PDF tem o link do projeto e prints de todas as telas exigidas.

**✅ Aceite F10:** Features e Test Cases completos no Azure · professor como Admin · PDF pronto.

### 8.12 F11 — Polimento nível HENRY

**Objetivo:** o acabamento que diferencia, sem tocar no que já garante a nota. Cada item vem da seção "Nível HENRY" da
spec. Um checkpoint por superfície.

- [ ] **F11.1** 🤖 Core: Stryker no domínio, OpenTelemetry, C4 no README, arquivos `.http` da demonstração.
- [ ] **F11.2** 🤖 Clima: cache por região, métricas Micrometer, página de evidências.
- [ ] **F11.3** 🤖 ML: figuras com os tokens, validação na A771, gráfico do replay hora a hora.
- [ ] **F11.4** 🤖 Sentinela: ícones no OLED, luz que acende e apaga devagar (sem piscar, D13), `wokwi-cli` no CI.
- [ ] **F11.5** 🤖 App: animações com Reanimated, ilustrações, tema escuro, ícone, splash, onboarding.
- [ ] **F11.6** 🤖 Painel: transições do farol, modo apresentação, mapa por bairro (agregado).
- [ ] **F11.7** 🤖 Cyber: mapa regra × sink, relatório LGPD gerado, antes e depois lado a lado no dashboard.
- [ ] **F11.8** 🧑🤖 🔑 VM no Azure com o mesmo `provision.sh` e HTTPS (fecha a DA3) (P13).
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

**Spec:** 10.

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
| P4 | Testcontainers funciona na máquina de desenvolvimento (Docker) | F1.6 | ⬜ |
| P5 | A chave Gemini atual responde, com cota gratuita suficiente para a demo | F2.6 | 🟡 a chave existe no `.env` do HENRY; a cota atual não foi conferida |
| P6 | O Semgrep CE em modo taint cobre as regras planejadas em C# | F2.7 | ⬜ |
| P7 | A Open-Meteo dá, sem chave, as variáveis horárias que o modelo usa (temperatura, umidade, pressão, radiação, vento), incluindo horas passadas | F3.5 | ⬜ |
| P8 | XGBoost → ONNX (onnxmltools) e ONNX Runtime Java dão a mesma probabilidade | F4.6, F9.1 | ⬜ |
| P9 | O ESP32 no Wokwi conecta num broker MQTT público pela rede `Wokwi-GUEST`, e o projeto abre por link público | F5.3, F5.5 | ⬜ |
| P10 | `@microsoft/signalr` funciona no Expo SDK 58 (Expo Go) | F6.3 | ⬜ |
| P11 | Vagrant + VirtualBox sobem Ubuntu 24.04 na máquina de desenvolvimento (Arch) | F8.1 | 🟡 2026-11-17 (doctor): VirtualBox 7.2.22 instalado; **Vagrant ausente** e fora dos repositórios oficiais do Arch (só AUR). Alternativa: VBoxManage + imagem cloud do Ubuntu com cloud-init. Decidir na F8.1 |
| P12 | A API REST do Azure DevOps cria Test Cases com passos e vínculos sem licença de Test Plans | F10.4 | ⬜ |
| P13 | Há uma conta Azure for Students ativa | F11.8 | ⬜ |
| P14 | O pipeline do HENRY (HyperFrames, FFmpeg, Chrome headless, voz Gemini TTS) roda de novo, com cota de TTS | F13.1 | 🟡 rodou em out/2026 no Challenge |

Legenda: ⬜ não verificada · 🟡 conferida em documentação ou uso anterior, falta o teste real · ✅ confirmada ·
⚠️ falsa (com o ajuste feito no plano).

---

## 12. Painel de status

**Meta:** as 8 tarefas enviadas até 19/11, 23h55, com o **✅ Aceite** de F0 a F14 passando.

**Agora:** F0 quase fechada; falta o 🔎 da F0.12 (CI verde no PR do lote `f0-fundacao-3`). Depois vem a **F1** (Core em C#).

| Fase | Status | Início | Fim | Notas |
|---|---|---|---|---|
| F0 Fundação | 🟨 em andamento | 2026-11-17 | | F0.1–F0.11 e F0.13–F0.16 feitas; F0.12 espera o CI no PR do lote `f0-fundacao-3` |
| F1 Core (C#) | ⬜ não iniciada | | | |
| F2 Privacidade (Cyber) | ⬜ não iniciada | | | |
| F3 Clima (SOA) | ⬜ não iniciada | | | |
| F4 ML | ⬜ não iniciada | | | 🤝 |
| F5 Sentinela (IoT) | ⬜ não iniciada | | | |
| F6 App (Mobile) | ⬜ não iniciada | | | 🤝 |
| F7 Painel | ⬜ não iniciada | | | |
| F8 Infra (OS) | ⬜ não iniciada | | | |
| F9 Integração | ⬜ não iniciada | | | |
| F10 QA | ⬜ não iniciada | | | |
| F11 Polimento | ⬜ não iniciada | | | |
| F12 Docs e PDFs | ⬜ não iniciada | | | |
| F13 Vídeos | ⬜ não iniciada | | | |
| F14 Entrega | ⬜ não iniciada | | | |

Legenda: ⬜ não iniciada · 🟨 em andamento · ✅ concluída · ⛔ bloqueada

