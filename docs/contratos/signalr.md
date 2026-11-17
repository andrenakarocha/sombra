# Tempo real · hub SignalR do Core

> Contrato dos eventos que o Core empurra para o app e para o painel, sem polling.

- **Hub:** `/hubs/sombra`, no Core.
- **Autenticação:** o mesmo JWT da API, enviado como `?access_token=` (padrão do SignalR para WebSocket).
- **Grupos:** ao conectar, o Core coloca a conexão nos grupos certos. O cliente não escolhe grupo.
  - `domicilio:{id}`: as casas da rede de cuidado de um `Cuidador`;
  - `operadores`: `Operador` e `Admin`.
- **Sem PII nos eventos:** só IDs, apelido da casa, medições e códigos.
- **Reconexão:** backoff automático no cliente. Eventos não são reenviados, então ao reconectar o cliente busca o
  estado de novo pela API.
- **Correlação:** todo evento leva `correlationId`, o mesmo do log que o originou (§6 do plano).

## Eventos (servidor → cliente)

| Evento | Grupos | Payload |
|---|---|---|
| `LeituraRegistrada` | casa, operadores | `domicilioId`, `momento`, `temperaturaC`, `umidadePct`, `indiceDeCalorC`, `nivel` |
| `SentinelaStatus` | casa, operadores | `domicilioId`, `deviceId`, `online` |
| `AlertaAberto` | casa, operadores | `alertaId`, `domicilioId`, `tipo` (`PREVENTIVO` ou `DE_SENTINELA`), `nivel`, `prazo` |
| `AlertaEscalonado` | casa, operadores | `alertaId`, `domicilioId`, `degrau` (1 a 3), `destinatario` (`FAMILIAR`, `VIZINHO` ou `REDE`), `mensagem` |
| `AlertaResolvido` | casa, operadores | `alertaId`, `domicilioId`, `resolvidoPor` (`CHECKIN_SENTINELA`, `CHECKIN_APP` ou `CUIDADOR`), `minutosAteCheckIn` |
| `CheckInRegistrado` | casa, operadores | `domicilioId`, `momento`, `origem`, `situacao` |
| `RiscoCidadaoAtualizado` | casa (se for da casa), operadores | `domicilioId` (ou `null` para o farol global), `farol`, `mensagemCidadao` |
| `AlertaDeInfra` | operadores | `host`, `recurso`, `severidade`, `mensagem`, `momento` |

Exemplo de `AlertaEscalonado`:

```json
{
  "alertaId": "5d2e9a40-1b7c-4f0e-8e1a-3c9b2d7f6a55",
  "domicilioId": "0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10",
  "degrau": 1,
  "destinatario": "FAMILIAR",
  "mensagem": "A Dona Lurdes ainda não confirmou que está bem. Quer ligar?",
  "correlationId": "4f1c2b7e9a0d4e6f"
}
```

O cliente não chama métodos no hub: toda ação (check-in, reconhecer alerta) vai pela API REST.
