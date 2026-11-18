# Observabilidade e logs

> **Tudo o que roda deixa rastro, e nenhum rastro carrega dado pessoal.** Detalha a §6 do [plano](PLANO.md). Todo
> checkpoint que cria ou muda um serviço só fecha com o [checklist](#7-checklist-de-pronto) desta página.

## 1. Formato

Uma linha JSON por evento no **stdout**: na VM vira journald, no compose vira `docker compose logs`. Campos obrigatórios:

| Campo | Exemplo | Regra |
|---|---|---|
| `timestamp` | `2026-11-18T18:00:00.123Z` | ISO-8601 em UTC, com milissegundos |
| `level` | `Information` | `Debug`, `Information`, `Warning`, `Error` ou `Critical` |
| `service` | `core-api` | `core-api`, `clima-service`, `sentinela-sim`, `sombra-monitor`, `provision` |
| `version` | `9571ead` | commit do build (o mesmo do `/version`) |
| `environment` | `Development` | `Development`, `Demo`, `Production` ou `Test` |
| `correlationId` | `4f1c2b7e9a0d4e6f` | §3 |
| `event` | `alerta.escalonado` | nome estável do [catálogo](#4-catálogo-de-eventos) |
| `message` | `Alerta escalonado para o degrau 1` | frase legível, **sem dado pessoal** |

Campos de domínio vêm a mais, em camelCase, **sempre por ID** (`domicilioId`, `alertaId`, `deviceId`), nunca por nome,
documento, telefone ou endereço.

```json
{"timestamp":"2026-11-18T18:15:02.481Z","level":"Information","service":"core-api","version":"9571ead","environment":"Demo","correlationId":"snt-7f3a9c:57","event":"alerta.escalonado","message":"Alerta escalonado para o degrau 1","alertaId":"5d2e9a40-1b7c-4f0e-8e1a-3c9b2d7f6a55","domicilioId":"0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10","degrau":1}
```

## 2. Níveis

| Nível | Quando | Exemplo |
|---|---|---|
| `Information` | evento de negócio ou de ciclo de vida | `alerta.aberto`, `servico.iniciado` |
| `Warning` | falha **recuperável**: retry, fallback, circuito aberto, entrada recusada | `clima.indisponivel`, `ia.fallback_template`, `mqtt.assinatura_invalida` |
| `Error` | falha que pede intervenção ou quebrou uma requisição | `banco.indisponivel`, exceção não tratada |
| `Critical` | o serviço não consegue continuar | `servico.encerrado_por_falha` |
| `Debug` | só em `Development`; nunca ligado na VM | — |

**Nunca no log:** corpo de requisição ou resposta, headers de autenticação, tokens, segredos, PII. No Core, a guarda de
log do `Sombra.Privacidade` (F2.4) **bloqueia e redige** o que escapar, e a auditoria (F2.8) prova isso. Nos outros
serviços, a regra é verificada em revisão e pelo gitleaks.

## 3. Correlação ponta a ponta

- **HTTP:** o header `X-Correlation-Id` é aceito, propagado e devolvido. Sem ele, o serviço gera 16 caracteres hex.
- **MQTT:** uma mensagem da Sentinela vira o `correlationId` `{deviceId}:{seq}` (ex.: `snt-7f3a9c:57`), e um
  comando do Core vira `{deviceId}:c{seq}`. Assim dá para achar no log exatamente a mensagem que o botão gerou.
- **Atravessa:** Core → Clima (header) · Core → SignalR (campo `correlationId` do evento) · Core → outbox (coluna) →
  comando MQTT e tradução por IA (log com o mesmo ID).
- **Formato aceito:** até 64 caracteres em `[A-Za-z0-9:_-]`. Fora disso, o serviço descarta e gera um novo.
- **Na tela:** quando algo falha, o app e o painel mostram *"código do erro: 4f1c2b7e"* (os 8 primeiros caracteres),
  para cruzar com o log sem expor nada.
- Na F11.1, o OpenTelemetry passa a carregar o mesmo ID como atributo dos traces.

## 4. Catálogo de eventos

Nome `<contexto>.<fato>` em português, no particípio. Evento novo entra aqui no mesmo PR.

| Serviço | Eventos |
|---|---|
| todos | `servico.iniciado` (com versão e configuração **sem segredos**), `servico.encerrado`, `requisicao.concluida` (método, rota-modelo, status, duração; sem corpo), `requisicao.falhou` (exceção inesperada, com o tipo e o stack trace só no log) |
| Core | `leitura.registrada`, `leitura.recusada`, `mqtt.assinatura_invalida`, `mqtt.replay_descartado`, `sentinela.online`, `sentinela.offline`, `alerta.aberto`, `alerta.agravado`, `alerta.escalonado`, `alerta.reconhecido`, `alerta.resolvido`, `alerta.expirado`, `aviso.registrado`, `sms.simulado`, `outbox.falhou`, `worker.volta_falhou`, `seguranca.evento_registrado`, `mqtt.conectado`, `mqtt.desconectado`, `mqtt.processamento_falhou`, `infra.alerta_recebido`, `checkin.registrado`, `pedido_de_ajuda.recebido`, `lembrete.enviado`, `comando.publicado`, `clima.indisponivel`, `clima.recuperado` |
| Core · Privacidade | `privacidade.pii_detectada`, `privacidade.vazamento_bloqueado`, `privacidade.saida_externa_bloqueada`, `ia.traducao_gerada`, `ia.fallback_template`, `risco_cidadao.atualizado` |
| Clima | `observacao.registrada`, `observacao.recusada`, `previsao.calculada` (com `fonte`: `onnx` ou `regra`), `modelo.carregado`, `modelo.indisponivel`, `fonte_meteorologica.falhou`, `replay.iniciado`, `replay.encerrado` |
| Sentinela (serial) e simulador | `boot`, `wifi.conectado`, `mqtt.conectado`, `mqtt.desconectado`, `telemetria.enviada`, `nivel.mudou`, `checkin.enviado`, `evento.enviado`, `comando.aceito`, `comando.recusado`, `fila.descarregada`; só no simulador: `telemetria.adulterada_enviada`, `comando.publicado`, `simulador.ouvindo` |
| VM | `provision.etapa_iniciada`, `provision.etapa_concluida`, `provision.etapa_falhou`, `monitor.verificacao`, `monitor.alerta`, `monitor.servico_inativo` |

## 5. Por stack

| Stack | Como |
|---|---|
| **.NET (Core)** | Serilog com `Serilog.Expressions` num template que gera exatamente os campos da §1. `version` e `environment` entram como propriedades globais. A correlação é um middleware que abre um `BeginScope`. O `Sombra.Privacidade` decora o provedor de log (F2.4) |
| **Java (Clima)** | logging estruturado do Spring Boot com um formatter próprio (`StructuredLogFormatter`) que gera os mesmos campos. A correlação é um filtro que põe o ID no MDC |
| **Python (simulador, scripts)** | `logging` da biblioteca padrão com um formatter JSON de poucas linhas, sem dependência |
| **Bash (VM)** | `infra/lib/log.sh` gera a linha com `jq -cn` (escapa tudo certo) no stdout e em `/var/log/sombra/*.log`. A trilha de auditoria vai em `/var/log/sombra/auditoria.jsonl` (§6) |
| **ESP32** | Serial legível no monitor do Wokwi: `[INFO] nivel.mudou de=ATENCAO para=PERIGO seq=57`. Os eventos importantes também vão por MQTT |
| **App e painel** | Nada de dado pessoal no `console`. Erros mostram o código curto (§3). Sem envio de telemetria do cliente |

## 6. Saúde, versão e auditoria

| Serviço | Vivo | Pronto | Versão |
|---|---|---|---|
| Core | `GET /health/live` (processo) | `GET /health/ready` (banco e broker) | `GET /version` → `{ "versao", "commit", "construidoEm" }` |
| Clima | `GET /actuator/health/liveness` | `GET /actuator/health/readiness` (banco; o modelo aparece como detalhe, porque o fallback por regra mantém o serviço útil) | `GET /actuator/info` (commit e data do build) |
| Painel | o nginx serve `/healthz` | — | o rodapé mostra o commit |

O `sombra-monitor` da VM consulta esses endpoints a cada ciclo (spec 08).

**Trilhas de auditoria** (ninguém apaga; rotação só por tempo):
- **eventos de segurança** do Core, numa tabela própria via outbox, só com metadados (spec 07);
- **provisionamento e operação** da VM em `auditoria.jsonl`, com o quê, quando, quem e resultado (código de saída);
- **`auditd`** nos diretórios `/etc/sombra` e `/opt/sombra`.

**Retenção na VM:** logrotate diário, 7 arquivos, comprimidos. O journald fica com o limite padrão.

## 7. Checklist de pronto

Um serviço novo ou alterado só fecha o checkpoint com:

- [ ] uma linha JSON por evento, com os 8 campos obrigatórios;
- [ ] `correlationId` aceito, propagado e devolvido;
- [ ] eventos com nome do catálogo (§4), e eventos novos acrescentados lá;
- [ ] nenhum corpo de requisição, segredo ou dado pessoal no log (conferido com uma requisição de teste);
- [ ] erro com o tipo da exceção e o `correlationId`; stack trace só no log, nunca na resposta;
- [ ] `servico.iniciado` com versão e configuração, sem segredos;
- [ ] endpoints de vivo, pronto e versão (§6) respondendo.
