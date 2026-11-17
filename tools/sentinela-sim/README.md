# Simulador da Sentinela

Uma Sentinela de mentira que fala **exatamente o contrato** da de verdade ([`mqtt.asyncapi.yaml`](../../docs/contratos/mqtt.asyncapi.yaml)):
telemetria e eventos assinados com HMAC, `seq` que continua crescendo entre execuções, status com Last Will e
verificação dos comandos recebidos (assinatura, anti-replay, janela de 300 s). Serve para desenvolver e testar o
Core, o app e o painel sem depender do Wokwi, e para o E2E do roteiro da demonstração (F9.2).

## Uso

Com o ambiente no ar (`make up`) e o `.env` gerado (`make env`):

```bash
make sim CENARIO=onda-de-calor          # roda um cenário (INTERVALO=1 s entre mensagens; OUVIR=s ouvindo comandos no fim)
make test-sim                           # testes (a integração usa o Mosquitto do compose)
```

Direto pelo `uv`, dentro desta pasta:

```bash
uv run sentinela-sim cenarios                                   # lista os cenários
uv run sentinela-sim rodar --cenario sem-resposta --intervalo 1 # roda um cenário
uv run sentinela-sim ouvir --segundos 60                        # só ouve e verifica comandos
uv run sentinela-sim comando --tipo PRE_ALERTA --mensagem "Amanhã: calor perigoso."   # publica como o Core faria
```

As opções caem nas variáveis `SENTINELA_DEVICE_ID`, `SENTINELA_SEGREDO_HEX`, `MQTT_HOST` e `MQTT_PORTA` do `.env`.

## Cenários

| Cenário | O que acontece | Para que serve |
|---|---|---|
| `normal` | dia comum, tudo `NORMAL` | fumaça |
| `onda-de-calor` | a casa sobe até `PERIGO` e ela aperta **ESTOU BEM** | caminho A do roteiro |
| `sem-resposta` | a casa chega a `PERIGO` e ninguém responde | caminho B (degraus) |
| `pedido-de-ajuda` | o botão vermelho é pressionado | degrau 3 imediato |
| `esqueceu-checkin-diario` | dia ameno sem check-in | prova de que esquecer não vira emergência (D13) |
| `payload-adulterado` | uma telemetria alterada depois de assinada | o Core precisa recusar (R10) |

## Logs

Uma linha JSON por evento, no padrão de [`docs/observabilidade.md`](../../docs/observabilidade.md). O `correlationId`
de uma mensagem enviada é `{deviceId}:{seq}`; o de um comando recebido é `{deviceId}:c{seq}`.

## Testes

- **Contrato:** os 20 vetores do índice de calor e os 6 de assinatura, os mesmos do ESP32, do Core e do Clima.
- **Verificação:** assinatura, outra Sentinela, replay, janela de tempo, campos faltando.
- **Cenários:** cada um faz o que o roteiro precisa.
- **Integração:** com o Mosquitto do compose, recebe e obedece um comando assinado, recusa replay e adulteração, e a
  telemetria publicada tem assinatura válida. Pulado se não houver broker em `localhost:1883`.

O estado (`seq`) fica em `.estado/`, fora do Git.
