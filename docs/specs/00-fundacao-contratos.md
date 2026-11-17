# Spec 00 — Fundação e contratos

> **Fase:** F0 · **Execução:** operador (D10) · **Checkpoints:** `PLANO.md` §8.1
> Detalhe da fundação: contratos, níveis de risco, design, simulador e roteiro da entrevista.

## Objetivo
Deixar o terreno pronto para qualquer frente começar **em paralelo sem se bloquear**: repositório, contratos entre as partes, linguagem ubíqua, identidade visual, ambiente local de um comando, CI e simulador da Sentinela.

## Detalhamento (a ordem e os 🔎 ficam no PLANO §8.1)

1. **Repositório** `sombra` (público, decisão D3): `main` protegida (PR + CI verde), `CODEOWNERS` por pasta, templates de PR/issue, labels por disciplina (`c#`, `soa`, `ml`, `iot`, `mobile`, `os`, `cyber`, `qa`, `pitch`).
   → verificar: o link abre em aba anônima.
2. **Esqueleto** das pastas (PLANO §3.3) + `README.md` com a Ficha v0 + `prompts.md` + `LICENSE` (MIT) + `.gitignore` + `.editorconfig` raiz + `.env.example`.
   → verificar: `tree -L 2` bate com o plano.
3. **Linguagem ubíqua** em `docs/glossario.md`: Domicílio · Morador · Perfil de saúde · Rede de cuidado · Contato de cuidado · Sentinela · Leitura · Índice de calor · Nível de risco · Alerta de calor (preventivo / de Sentinela) · Check-in ("estou bem") · Pedido de ajuda · Escalonamento · Região climática · Estação · Observação · Previsão de calor · Ponto de resfriamento · Evento de segurança · Risco Cidadão.
4. **Níveis de risco e índice de calor:** contrato único em `docs/contratos/niveis-de-risco.md` (os cinco níveis da
   NWS em °C, o comportamento de cada nível conforme a D13 e a leitura adotada da equação oficial). Implementação de
   referência e vetores em `tools/indice-de-calor/vetores.py` e `docs/contratos/indice-de-calor.vetores.json`, rodados
   pelos testes de C#, Java, C++ e Python.
5. **Contratos**
   - `docs/contratos/mqtt.asyncapi.yaml` (AsyncAPI 3), com tópicos `sombra/v1/{deviceId}/{telemetria|evento|comando|status}`:
     ```json
     // telemetria (Sentinela → Core)
     {"v":1,"ts":1763400000,"seq":42,"t":33.4,"rh":40.0,"hi":34.4,"nivel":"CAUTELA_EXTREMA","rssi":-60,"sig":"<hmac-sha256-hex>"}
     // evento (Sentinela → Core)
     {"v":1,"ts":1763400060,"seq":43,"tipo":"CHECKIN_ESTOU_BEM|PEDIDO_DE_AJUDA|BOOT","sig":"…"}
     // comando (Core → Sentinela)
     {"v":1,"ts":1763400070,"seq":7,"tipo":"PRE_ALERTA|ALERTA|FAMILIA_AVISADA|LEMBRETE_AGUA|CHECKIN_DIARIO|NORMAL","mensagem":"Amanhã: calor perigoso. Encha sua garrafa de água.","sig":"…"}
     // status (LWT, retido): "online" | "offline"
     ```
     O HMAC-SHA256 é calculado sobre uma **string canônica por tipo** (com o `deviceId` dentro, para a mensagem não
     servir em outra Sentinela), com segredo por dispositivo; vetores em `docs/contratos/mqtt-assinatura.vetores.json`.
     O anti-replay usa `seq` crescente e janela de ±5 min em `ts`. **Nenhuma PII no MQTT.** Detalhe no AsyncAPI.
   - `docs/contratos/core-api.md` e `docs/contratos/clima-service.md`: tabela de endpoints + exemplos JSON (rascunho). Depois, o OpenAPI **gerado pelo código** vira a fonte da verdade e o CI exporta para `docs/contratos/*.openapi.json`.
   - `docs/contratos/signalr.md`: eventos do hub `/hubs/sombra` (`LeituraRegistrada`, `AlertaAberto`, `AlertaEscalonado`, `AlertaResolvido`, `CheckInRegistrado`, `RiscoCidadaoAtualizado`, `AlertaDeInfra`).
   - **Portas e hosts:** core `5080` · clima `8081` · painel `5173` (dev) · postgres `5432` · mosquitto `1883` (dev) · VM `192.168.56.10` `sombra-srv01` · `api|clima|painel.sombra.local`.
   - **Variáveis de ambiente:** lista única em `.env.example`, com o prefixo do serviço (`CORE__…`, `CLIMA_…`).
6. **Design** em `design/tokens.json` + `design/README.md`: paleta (sol/âmbar → vermelho para o risco, azul-petróleo para a "sombra" e a calma, neutros quentes), escala tipográfica com **Atkinson Hyperlegible** (UI) + uma display para títulos, farol acessível (cor + ícone + texto, contraste ≥ 4,5:1 nos dois temas). Esses tokens alimentam o Tailwind do painel, o `theme.ts` do app, o `kit.css` do pitch e os PDFs.
7. **Ambiente local:** `compose.yaml` (postgres com 2 bancos via script de init, mosquitto, core, clima, painel) + `Makefile` (`up`, `down`, `logs`, `test`, `demo`, `lint`).
   → verificar: `make up` e os três `/health` respondem 200.
8. **Simulador da Sentinela** em `tools/sentinela-sim/` (Python): publica telemetria e eventos assinados conforme o contrato e obedece comandos. Cenários: `normal`, `onda-de-calor`, `sem-resposta`, `pedido-de-ajuda`, `esqueceu-checkin-diario`, `payload-adulterado`.
9. **CI** em `.github/workflows/`: um workflow por componente com `paths:` + `gitleaks` + `release.yml` (em tag: publica binário do core, jar do clima e dist do painel com `SHA256SUMS`, consumidos pelo `infra/`).
10. **ADRs** em `docs/adr/`: 001 linguagem ubíqua PT + sufixos técnicos EN · 002 monorepo · 003 dois serviços, cada um dono do seu banco · 004 MQTT público com HMAC e sem PII · 005 LLM com fallback · 006 fórmula do índice de calor replicada de propósito (física pública, contextos independentes, vetores de teste compartilhados).
11. **Quadro:** issues criadas a partir das etapas dos specs (`gh issue create`) + GitHub Project em Kanban.

## Roteiro da entrevista (F0.16, tarefa humana · ver PLANO §7)
Consentimento verbal gravado no início, só com o primeiro nome ou apelido.
1. Como é o seu dia quando faz muito calor? Em que hora a casa fica mais quente?
2. O que a senhora/o senhor faz para se refrescar? Lembra de beber água?
3. Já passou mal com calor? O que aconteceu e quem ficou sabendo?
4. Quem costuma ver como a senhora/o senhor está? Com que frequência?
5. Usa celular? Para quê? O que é difícil nele?
6. Se uma luz e um botão grande pedissem "aperte se está bem", isso ajudaria ou incomodaria?
7. Toma algum remédio que o médico disse para ter cuidado no calor? (**não registrar o nome do remédio**, só "sim/não")
8. Para quem cuida: como você sabe hoje se ela está bem? O que te dá medo?

Saída: `docs/entrevista.md` (achados + 3 citações curtas anonimizadas + o que mudou no projeto por causa da conversa) e fotos em `docs/territorio/`.

