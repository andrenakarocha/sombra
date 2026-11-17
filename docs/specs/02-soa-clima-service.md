# Spec 02 — SOA e Web Services · Clima Service

> **Fase:** F3 (ONNX na F9.1, polimento na F11.2) · **Pasta:** `services/clima-service/` · **Checkpoints:** `PLANO.md` §8.4
> **Entrega:** link do GitHub (pasta) · **Consumido por:** Core (C#) · **Consome:** Open-Meteo, CSVs do INMET, modelo ONNX do `ml/`
> **Observabilidade:** [`docs/observabilidade.md`](../observabilidade.md) §1–§7 (formatter próprio do Spring; saúde pelo actuator)

## Objetivo
Um serviço **dono do conhecimento climático**: regiões, estações, observações, risco de calor atual, **previsão de 24 h feita pelo modelo de ML** e pontos de resfriamento perto de casa. Segue **exatamente** a estrutura do guia do professor (domínio · aplicação · infraestrutura · api), em Java/Spring Boot, a stack da disciplina. O Core conversa com ele só pelo contrato OpenAPI, nunca pelo banco.

## Rubrica (autoavaliação do guia do professor) → como → evidência

| Subitem | Pts | Como |
|---|---|---|
| Entidades e nomes vêm do problema | 0,5 | `RegiaoClimatica`, `Estacao`, `Observacao`, `IndiceDeCalor`, `NivelDeRisco`, `PrevisaoDeCalor`, `PontoDeResfriamento` |
| Camadas domínio · aplicação · infraestrutura · api | 0,75 | pacotes `dominio/`, `aplicacao/`, `infraestrutura/`, `api/` + teste de arquitetura (ArchUnit) |
| ≥ 2 regras no domínio | 0,75 | índice de calor (Rothfusz) e classificação · **`-9999` do INMET não é observação** · umidade entre 0 e 100 · ponto de resfriamento com `abre < fecha` · região exige estação válida |
| DTOs com validação | 0,5 | `record`s de entrada e saída com Bean Validation. A entidade nunca vaza no JSON |
| ≥ 1 relacionamento | 0,5 | `Estacao 1—N Observacao` · `RegiaoClimatica N—1 Estacao` |
| Banco relacional de verdade | 0,75 | PostgreSQL 16 (`sombra_clima`) |
| Conexão por variável de ambiente + `.env.example` | 0,5 | `SPRING_DATASOURCE_*` vindos do ambiente |
| CRUD de ≥ 1 entidade | 0,75 | CRUD completo de `PontoDeResfriamento` **e** de `RegiaoClimatica` |
| Relacionamento com FK | 0,5 | `observacao.estacao_codigo → estacao.codigo` |
| ≥ 1 consulta além do CRUD | 0,5 | observações por período (`?de=&ate=`) com paginação · pontos próximos ordenados por distância |
| Ferramenta de migração (sem auto-DDL) | 0,5 | Flyway + `spring.jpa.hibernate.ddl-auto=validate` |
| ≥ 2 migrações (criação + evolução) | 0,5 | V1 regiões/estações/observações · V2 pontos de resfriamento · V3 evolução (`acessivel`, `horario`, `fonte`) |
| Dados carregados por migração | 0,25 | V4 seed: região SP-MIRANTE, estação A701, **observações reais da onda de calor de nov/2023** e pontos públicos |
| README com o comando que cria o banco | 0,25 | `docker compose up -d db && ./mvnw flyway:migrate` |
| OpenAPI gerada numa URL | 0,5 | springdoc → `/swagger-ui.html` + `/v3/api-docs` |
| Resumo e tag em todo endpoint | 0,25 | `@Operation(summary)` + `@Tag` |
| Modelos com exemplo | 0,25 | `@Schema(example=…)` em todos os DTOs |
| Erros documentados (400/404/422) | 0,25 | `@ApiResponse` com o corpo `Erro` em cada endpoint |
| README com link e print da doc | 0,25 | `docs/evidencias/swagger.png` |
| Testes de unidade do domínio | 0,4 | JUnit 5, sem banco e sem HTTP |
| ≥ 1 teste de API (status + corpo) | 0,3 | MockMvc + Testcontainers |
| Um comando roda tudo, no README, com print | 0,3 | `./mvnw verify` |

## Endpoints
- `GET|POST|PUT|DELETE /api/v1/regioes[/{codigo}]`
- `POST /api/v1/estacoes/{codigo}/observacoes` (ingestão) · `GET /api/v1/estacoes/{codigo}/observacoes?de=&ate=&pagina=&tamanho=`
- `GET /api/v1/regioes/{codigo}/risco-calor` → `{ agora: {indiceDeCalorC, nivel, observadoEm}, previsao24h: {probabilidadePerigo, nivelPrevisto, modelo: "xgb-calor-v1", fonte: "onnx|regra"}, fonteDados: "open-meteo|replay-inmet" }`
- `GET|POST|PUT|DELETE /api/v1/pontos-de-resfriamento[/{id}]` · `GET /api/v1/pontos-de-resfriamento/proximos?lat=&lon=&raioKm=`
- `POST /api/v1/replay` `{ regiao, inicio: "2023-11-12T00:00-03:00", velocidade: 60 }` · `DELETE /api/v1/replay` (modo de demonstração, documentado como tal)
- `GET /actuator/health`

## Previsão (integração com o ML)
- Porta `PrevisorDeCalor` com duas implementações: `OnnxPrevisorDeCalor` (carrega `modelo-calor-v1.onnx` + `features.json` gerados no `ml/`) e `RegraPrevisorDeCalor` (fallback por regras). Se o modelo falhar, a resposta diz `fonte: "regra"`.
- A janela de features (últimas 24 h) vem do repositório de observações. As features são calculadas **com a mesma especificação do notebook** (`features.json`), e um **teste de paridade** compara 20 previsões do Java com as do Python.
- `FonteMeteorologica`: `OpenMeteoClient` (ao vivo, a cada 10 min, sem chave) e `ReplayInmet` (toca os CSVs reais da A701 acelerados).

## Checkpoints
A ordem de execução e os 🔎 ficam no `PLANO.md` (fase indicada no topo desta spec).

## Nível HENRY (F11)
Cache de risco por região (Caffeine) · versionamento do contrato (`/api/v1` + changelog) · métricas Micrometer · paridade ONNX × Python publicada no README · página de evidências com prints de cada subitem da rubrica.

## Defesa
Por que o Clima é outro serviço e não uma tabela do Core ("o serviço termina onde a palavra muda de sentido") · o que acontece no Core se o Clima cair (circuit breaker + último risco conhecido) · por que V3 é uma migração nova e não a edição da V1 · como o modelo do Python roda dentro do Java (ONNX).
