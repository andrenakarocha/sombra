# Core API · contrato REST

> **Fonte da verdade desde a F1.7:** [`core-api.openapi.json`](core-api.openapi.json), gerado pelo próprio Core
> (`make -C services/core-api openapi`). O teste de API falha se a API e esse arquivo divergirem, e o Scalar do Core
> (`/scalar/v1`) mostra o mesmo documento. Esta página fica como visão geral. As rotas de `/privacidade` chegam na F2.5
> e a de pontos de resfriamento na F3.9. Em `Development` e `Demo`, `POST /demo/relogio/avancar` (Admin) adianta o
> relógio do Core para o caminho B caber num vídeo.

## Convenções

- **Base:** `/api/v1` · JSON · propriedades em camelCase, com a linguagem ubíqua em português (`indiceDeCalorC`).
- **Tempo:** ISO-8601 em UTC (`2026-11-18T18:00:00Z`). A tela converte para America/Sao_Paulo.
- **Enums como texto.** Os níveis seguem `niveis-de-risco.md`.
- **Autenticação:** `POST /api/v1/auth/login` devolve um JWT (`Authorization: Bearer …`). Papéis:
  - `Cuidador`: só as casas da sua rede de cuidado;
  - `Operador`: todas as casas, sempre com PII mascarada;
  - `Admin`: cadastros e operação;
  - `Servico`: token do monitor da VM, só para `/ops/alertas`.
- **Erros:** ProblemDetails (RFC 9457) com `type`, `title`, `status`, `detail` (nunca com PII) e `correlationId`.
  `400` validação (com `errors`) · `401` · `403` · `404` · `409` conflito · `422` regra de domínio (com `regra`, ex.:
  `"R5"`) · `429` limite de requisições.
- **Paginação:** `?pagina=1&tamanho=20` (máximo 100) → `{ "itens": [], "pagina": 1, "tamanho": 20, "total": 57 }`.
- **Correlação:** o header `X-Correlation-Id` é aceito e devolvido. Sem ele, o Core gera um (§6 do plano).
- **PII:** CPF, Cartão SUS, telefone e saúde saem **sempre mascarados**, exceto em endpoints marcados para o `Cuidador`
  da própria casa (spec 07). Máscara do CPF: `***.456.***-00` (o bloco do meio e os verificadores).

## Endpoints

| Método | Rota | Papel | O que faz |
|---|---|---|---|
| POST | `/auth/login` | público | entra e recebe o token |
| GET | `/domicilios?nivel=&bairro=&pagina=&tamanho=` | Cuidador, Operador | lista as casas (o Cuidador vê só as suas) |
| POST | `/domicilios` | Cuidador, Admin | cadastra uma casa |
| GET · PUT · DELETE | `/domicilios/{id}` | Cuidador, Admin | consulta, edita, remove |
| GET · POST | `/domicilios/{id}/moradores` | Cuidador, Admin | lista e cadastra moradores |
| GET · PUT · DELETE | `/domicilios/{id}/moradores/{moradorId}` | Cuidador, Admin | consulta, edita, remove |
| GET · PUT | `/domicilios/{id}/rede-de-cuidado` | Cuidador | contatos em ordem, com papel `Familiar` ou `Vizinho` |
| GET · PUT | `/domicilios/{id}/rotina` | Cuidador | check-in diário e lembretes (D13) |
| POST | `/domicilios/{id}/sentinelas` | Cuidador, Admin | vincula uma Sentinela e devolve o segredo HMAC **uma única vez** |
| GET | `/domicilios/{id}/sentinelas` | Cuidador, Operador | Sentinelas da casa e se estão online |
| GET | `/domicilios/{id}/leituras?de=&ate=&pagina=` | Cuidador, Operador | leituras num período, da mais recente para a mais antiga |
| POST | `/sentinelas/{deviceId}/leituras` | assinatura HMAC | alternativa por HTTP ao MQTT, com o mesmo payload assinado |
| GET | `/alertas?estado=&nivel=&pagina=` | Cuidador, Operador | alertas (o Cuidador vê só os seus) |
| GET | `/alertas/{id}` | Cuidador, Operador | alerta com a linha do tempo dos degraus |
| POST | `/alertas/{id}/reconhecer` | Cuidador | `{"acao": "VOU_LIGAR" \| "ESTOU_INDO" \| "ELA_ESTA_BEM"}` |
| POST | `/domicilios/{id}/check-ins` | Cuidador | `{"origem": "APP" \| "CUIDADOR", "situacao": "ESTOU_BEM" \| "PRECISO_DE_AJUDA"}` |
| GET | `/domicilios/{id}/pontos-de-resfriamento?raioKm=` | Cuidador | pontos perto da casa, via Clima, com fallback |
| GET | `/painel/resumo` | Operador | números do painel e indicadores de impacto |
| GET | `/privacidade/risco-cidadao` | Operador | farol global e por categoria |
| GET | `/privacidade/eventos?pagina=` | Operador | eventos de segurança, só com metadados |
| GET | `/domicilios/{id}/privacidade` | Cuidador | farol da casa e as mensagens traduzidas |
| POST | `/ops/alertas` | Servico | alerta de infraestrutura vindo do monitor da VM |
| GET | `/health/live` · `/health/ready` · `/version` | público | saúde e versão (commit e data do build) |

## Exemplos

`GET /api/v1/domicilios/{id}` (dados fictícios):

```json
{
  "id": "0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10",
  "apelido": "Casa da Dona Lurdes",
  "bairro": "Santana",
  "regiaoClimatica": "SP-MIRANTE",
  "nivelAtual": "CAUTELA_EXTREMA",
  "ultimaLeitura": { "momento": "2026-11-18T18:00:00Z", "temperaturaC": 33.4, "umidadePct": 40.0, "indiceDeCalorC": 34.4 },
  "sentinela": { "deviceId": "snt-7f3a9c", "online": true },
  "ultimoCheckIn": "2026-11-18T12:02:00Z",
  "alertaAberto": {
    "id": "5d2e9a40-1b7c-4f0e-8e1a-3c9b2d7f6a55",
    "tipo": "DE_SENTINELA",
    "estado": "AGUARDANDO_CHECK_IN",
    "degrau": 0,
    "prazo": "2026-11-18T18:30:00Z"
  }
}
```

`POST /api/v1/domicilios/{id}/moradores` responde com a PII **mascarada**:

```json
{
  "id": "c1a8e2f4-7b3d-4e9a-8f2c-6d5b4a3e2f10",
  "nome": "Lurdes A.",
  "idade": 78,
  "cpf": "***.982.***-10",
  "cartaoSus": "*** **** **** 1234",
  "perfilDeSaude": "[DADO DE SAÚDE PROTEGIDO]",
  "fatoresDeRisco": ["IDADE_AVANCADA", "USO_DE_DIURETICO"]
}
```

`GET /api/v1/painel/resumo`:

```json
{
  "casasMonitoradas": 12,
  "alertasAtivos": 2,
  "tempoMedianoAteCheckInMin": 6.5,
  "alertasResolvidosSemUltimoDegrauPct": 91.7,
  "alarmesDesnecessariosSemana": 1,
  "escalonamentosHojePorDegrau": { "1": 3, "2": 1, "3": 0 },
  "sentinelasOffline": 0
}
```

`POST /api/v1/ops/alertas` (monitor da VM):

```json
{
  "host": "sombra-srv01",
  "recurso": "CPU",
  "severidade": "CRITICO",
  "valor": 96.4,
  "limite": 90,
  "mensagem": "CPU acima de 90% por 3 verificações seguidas",
  "momento": "2026-11-18T18:05:00Z"
}
```

`422` de regra de domínio:

```json
{
  "type": "https://github.com/andrenakarocha/sombra/blob/main/docs/contratos/core-api.md#erros",
  "title": "Regra de negócio violada",
  "status": 422,
  "detail": "Um alerta resolvido não pode ser escalonado.",
  "regra": "R5",
  "correlationId": "4f1c2b7e9a0d4e6f"
}
```
