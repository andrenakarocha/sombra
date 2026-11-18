# Spec 01 — C# Software Development · Core API

> **Fase:** F1 (polimento na F11.1) · **Pasta:** `services/core-api/` · **Checkpoints:** `PLANO.md` §8.2
> **Entrega:** link do GitHub (pasta) · **Consumido por:** app, painel, Sentinela, monitor da VM
> **Observabilidade:** [`docs/observabilidade.md`](../observabilidade.md) §1–§7 (o Core é o serviço mais instrumentado; a guarda de log é da spec 07)

## Objetivo
O **coração do Sombra**: domicílios, moradores, rede de cuidado, Sentinelas, leituras, alertas, check-ins e escalonamento, com POO de verdade, Web API completa, EF Core com LINQ, assincronia ponta a ponta e qualidade medida pelo **pipeline do próprio professor**.

## Rubrica → como a gente crava → evidência

| Critério (pts) | Como | Evidência |
|---|---|---|
| **POO (3,0)** | Agregados encapsulados (setters privados, construtores que recusam estado inválido, métodos com nome de negócio). **Herança e polimorfismo:** `AlertaDeCalor` abstrato → `AlertaPreventivo` (vem da previsão) e `AlertaDeSentinela` (vem da leitura), cada um com `PrazoDeCheckIn()` e `MensagemAoMorador()` próprios. `ICanalDeNotificacao` → `CanalApp`, `CanalSentinela`, `CanalSmsSimulado`. `IFatorDeRisco` → `IdadeAvancada`, `Cardiopatia`, `UsoDeDiuretico`. Value objects: `Cpf`, `CartaoSus`, `Telefone`, `Endereco`, `Temperatura`, `UmidadeRelativa`, `IndiceDeCalor`, `LimitesTermicos`. Eventos de domínio. | diagrama de classes no README + testes de domínio |
| **Web API (2,5)** | Controllers em `/api/v1`, status corretos (201 + Location, 204, 400, 401, 403, 404, 409, 422), ProblemDetails (RFC 9457), validação de DTOs, paginação e filtros, JWT com papéis (`Cuidador`, `Operador`, `Admin`), rate limiting, health checks, `/version`, **OpenAPI + Scalar** com exemplos e erros documentados | print do Scalar + testes de API |
| **EF Core + LINQ (1,5)** | Npgsql, `IEntityTypeConfiguration`, owned types para VOs, value converters (criptografia de PII), **4 migrações** (Inicial · Alertas e check-ins · Rede de cuidado e índices · Seed da demo). LINQ: agregações do painel (`GroupBy` por nível, tempo mediano até o check-in, alertas por bairro), projeções `Select` para DTO, `AsNoTracking`, paginação | prints das migrações + consultas no código |
| **Async/Await (1,0)** | Async ponta a ponta com `CancellationToken`. `BackgroundService`s (ingestão MQTT, escalonador, monitor de previsão, publicador de comandos via outbox, tradutor de risco). `Channel<T>` entre ingestão e processamento, `Task.WhenAll` para chamadas paralelas e `IAsyncEnumerable` no export de leituras | código + testes dos workers |
| **Qualidade (2,0)** | Camadas (Domínio · Aplicação · Infraestrutura · Privacidade · API) com **testes de arquitetura (NetArchTest)**, StyleCop + analyzers + `.editorconfig` do CP06, warnings como erro, **`Invoke-CodeQuality.ps1` do professor = 100/100**, cobertura ≥ 90% no domínio, XML docs, README e ADRs | print do score + relatório de cobertura |

## Estrutura

```
services/core-api/
├── Sombra.Core.slnx · Directory.Build.props · Directory.Packages.props · .editorconfig
├── sources/                       (layout do pipeline do professor; os testes também ficam aqui)
│   ├── Sombra.Dominio/          entidades, VOs, regras, eventos (zero dependências)
│   ├── Sombra.Aplicacao/        casos de uso, portas (interfaces), DTOs, validação
│   ├── Sombra.Infraestrutura/   EF Core, repositórios, MQTTnet, ClimaClient, notificadores, outbox
│   ├── Sombra.Privacidade/      módulo de Cyber (spec 07)
│   └── Sombra.Api/              controllers, middlewares, hub SignalR, Program.cs
├── sources/ (testes, MSTest)
│   ├── Sombra.Dominio.Tests/ · Sombra.Aplicacao.Tests/ · Sombra.Privacidade.Tests/
│   ├── Sombra.Api.IntegrationTests/   (WebApplicationFactory + Testcontainers Postgres e Mosquitto)
│   └── Sombra.ArchitectureTests/
├── scripts/Invoke-CodeQuality.ps1   (do professor, inalterado)
└── Makefile · Dockerfile · README.md
```

## Regras de negócio (no domínio, cada uma com teste)
- **R1** · CPF e Cartão SUS (CNS) válidos pelo dígito verificador.
- **R2** · Índice de calor pela regressão de Rothfusz (NOAA), com os vetores de teste compartilhados.
- **R3** · Limites térmicos **personalizados** pelos fatores de risco do morador (≥ 80 anos, cardiopatia, uso de diurético reduzem o limiar de perigo), com piso de 26,67 °C (início de `ATENCAO`).
- **R4** · Um alerta aberto por domicílio por vez. Novas leituras **atualizam** o alerta em vez de duplicá-lo.
- **R5** · Máquina de estados do alerta: `Aberto → AguardandoCheckIn → (Confirmado | Escalonado(n)) → Resolvido | Expirado`. Uma transição inválida lança `RegraVioladaException` (vira 422).
- **R6** · O "estou bem" é pedido a partir de `CAUTELA_EXTREMA`, com prazo por nível (`PERIGO` e acima: 15 min ·
  `CAUTELA_EXTREMA`: 30 min; `ATENCAO` só tem o lembrete de água), conforme o
  [contrato de níveis](../contratos/niveis-de-risco.md). Relógio injetável (`IRelogio`) para acelerar na demo.
- **R7** · "Pedido de ajuda" notifica **toda** a rede de cuidado na hora.
- **R8** · **Escalonamento em degraus** (D13, da entrevista):
  - (0) a Sentinela lembra a pessoa e explica o próximo passo;
  - (1) **lembrete leve** ao primeiro familiar;
  - (2) **"quem está perto"**: contato com papel `Vizinho`, ação "ir até lá";
  - (3) **alerta** para toda a rede.

  Os degraus 2 e 3 só acontecem com nível ≥ `PERIGO` ou com pedido de ajuda. Esquecer o check-in num dia sem risco
  **nunca** passa do degrau 1. O monitoramento só ativa com ≥ 1 contato.
- **R9** · Leitura fora da faixa física é recusada. A Sentinela fica offline depois de 3 heartbeats perdidos.
- **R10** · Telemetria com HMAC inválido ou `seq` repetido é descartada e vira **evento de segurança**.
- **R11** · **Check-in diário** opcional, no horário escolhido pela pessoa; esquecer gera só lembrete leve. **Lembrete de
  água** no pico da tarde (15h–16h) em dias ≥ `ATENCAO`, no máximo 2 por dia.
- **R12** · Lembretes (água, check-in diário) nunca entre 22h e 7h. Alertas de perigo funcionam a qualquer hora.
- **R13** · **Carência depois do "estou bem"** (achada no 🔎 da F1.9, com o simulador): por 1 h depois de a pessoa
  confirmar, uma leitura no mesmo nível ou abaixo não abre outro alerta; só um nível mais alto reabre. Sem ela, a casa
  ainda quente pediria o botão de novo a cada 10 s.
- **R14** · **Check-in da fila offline** (F6.23, D17): o app manda o horário do toque (`momento`), e é ele que vale para
  encerrar o alerta e medir o tempo até o check-in. Nunca no futuro (tolerância de 2 min para o relógio do celular) e até
  24 h atrás; um toque de antes de o alerta abrir não responde a ele. Com a `Idempotency-Key` (o `operationId` da fila),
  o reenvio devolve a mesma resposta sem repetir o efeito, no check-in e no reconhecer.

## Endpoints (rascunho)
`POST /auth/login` · CRUD `/domicilios` (filtros `nivel`, `bairro`, paginação) · CRUD `/domicilios/{id}/moradores` · `GET|PUT /domicilios/{id}/rede-de-cuidado` · `GET|PUT /domicilios/{id}/rotina` (check-in diário e lembretes) · `POST /domicilios/{id}/sentinelas` · `GET /domicilios/{id}/leituras?de=&ate=` · `GET /alertas?estado=&nivel=` · `POST /alertas/{id}/reconhecer` · `POST /domicilios/{id}/check-ins` · `GET /domicilios/{id}/pontos-de-resfriamento` (proxy resiliente para o Clima) · `GET /painel/resumo` · `GET /privacidade/risco-cidadao` · `GET /privacidade/eventos` · `POST /ops/alertas` (monitor da VM) · `GET /health/live|ready` · `GET /version` · hub `/hubs/sombra`

## Checkpoints
A ordem de execução e os 🔎 ficam no `PLANO.md` (fase indicada no topo desta spec).

## Nível HENRY (F11)
Teste de mutação (Stryker) no domínio · `IAsyncEnumerable` com export CSV de leituras · OpenTelemetry (traces de MQTT → alerta → SignalR) · diagrama C4 renderizado no README · demonstração HTTP gravada (scripts `.http`).

## Defesa (o que cada um precisa saber explicar)
Por que `AlertaDeCalor` é abstrato e onde está o polimorfismo · como a máquina de estados impede um alerta resolvido de voltar a escalonar · ciclo de vida da DI (`Scoped` × `Singleton` nos workers, `IServiceScopeFactory`) · como o EF traduz o `GroupBy` para SQL · o que o `CancellationToken` faz quando o app fecha · por que o domínio não referencia o EF.
