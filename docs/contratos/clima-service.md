# Clima Service · contrato REST

> **Rascunho da F0.7.** Na F3.4, o OpenAPI gerado pelo springdoc vira a fonte da verdade e o CI o exporta para
> `clima-service.openapi.json`, nesta pasta. O Core é o único cliente: o app e o painel falam só com o Core.

## Convenções

As mesmas do Core: base `/api/v1`, JSON em camelCase com a linguagem ubíqua, tempo em ISO-8601 UTC, ProblemDetails
(RFC 9457) nos erros (`400`, `404`, `422` com `regra`), paginação `?pagina=&tamanho=` e o header `X-Correlation-Id`
aceito e repassado aos logs. O Clima não guarda dado pessoal.

## Endpoints

| Método | Rota | O que faz |
|---|---|---|
| GET · POST | `/regioes` | lista e cadastra regiões climáticas |
| GET · PUT · DELETE | `/regioes/{codigo}` | consulta, edita, remove |
| GET | `/regioes/{codigo}/risco-calor` | risco agora e previsão de 24 h |
| POST | `/estacoes/{codigo}/observacoes` | ingere uma observação (o código `-9999` do INMET é recusado com `422`) |
| GET | `/estacoes/{codigo}/observacoes?de=&ate=&pagina=&tamanho=` | observações num período |
| GET · POST | `/pontos-de-resfriamento` | lista e cadastra pontos |
| GET · PUT · DELETE | `/pontos-de-resfriamento/{id}` | consulta, edita, remove |
| GET | `/pontos-de-resfriamento/proximos?lat=&lon=&raioKm=` | pontos dentro do raio, do mais perto ao mais longe |
| POST · DELETE | `/replay` | liga e desliga o replay de uma onda de calor real do INMET (modo de demonstração) |
| GET | `/actuator/health` | saúde |

## Exemplos

`GET /api/v1/regioes/SP-MIRANTE/risco-calor` (valores ilustrativos):

```json
{
  "regiao": "SP-MIRANTE",
  "agora": {
    "observadoEm": "2023-11-14T18:00:00Z",
    "temperaturaC": 35.0,
    "umidadePct": 50.0,
    "indiceDeCalorC": 40.68,
    "nivel": "PERIGO"
  },
  "previsao24h": {
    "probabilidadePerigo": 0.83,
    "nivelPrevisto": "PERIGO",
    "horizonteHoras": 24,
    "modelo": "xgb-calor-v1",
    "fonte": "onnx"
  },
  "fonteDados": "replay-inmet"
}
```

`fonte` vale `onnx` (modelo de ML) ou `regra` (fallback, se o modelo não carregar). `fonteDados` vale `open-meteo`
(ao vivo) ou `replay-inmet` (demonstração).

`POST /api/v1/replay`:

```json
{ "regiao": "SP-MIRANTE", "inicio": "2023-11-12T00:00:00-03:00", "velocidade": 60 }
```

`velocidade: 60` toca uma hora de dados por minuto.

`GET /api/v1/pontos-de-resfriamento/proximos?lat=-23.4964&lon=-46.6200&raioKm=2` (exemplo):

```json
[
  {
    "id": "8e4b2c1a-3d5f-4a6b-9c7d-1e2f3a4b5c6d",
    "nome": "Biblioteca do bairro (exemplo)",
    "tipo": "BIBLIOTECA",
    "distanciaKm": 0.8,
    "horario": { "abre": "09:00", "fecha": "18:00" },
    "acessivel": true,
    "fonte": "dados abertos da Prefeitura de São Paulo"
  }
]
```
