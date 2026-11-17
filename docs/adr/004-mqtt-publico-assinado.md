# ADR-004 · MQTT em broker público, com mensagens assinadas e sem dado pessoal

**Status:** aceito · 2026-11-17

## Contexto

A Sentinela roda no Wokwi, que sai para a internet pela rede `Wokwi-GUEST`. O jeito prático de ela falar com o Core é um
broker MQTT público, mas qualquer pessoa pode ler e publicar nele. Uma mensagem falsa de "estou bem" poderia esconder
uma emergência.

## Decisão

- **Nenhum dado pessoal** trafega no MQTT: só o `deviceId` (sem relação com a pessoa), medições e códigos.
- **Toda mensagem é assinada** com HMAC-SHA256 e um segredo de 32 bytes por Sentinela, gerado pelo Core no vínculo e
  guardado cifrado.
- A assinatura cobre uma **string canônica por tipo com o `deviceId` dentro**, e não o JSON: assim C++, C# e Python
  assinam igual sem canonicalizar JSON, e uma mensagem não serve para outra Sentinela.
- **Anti-replay** por `seq` crescente (persistido na Sentinela) e janela de 300 s em `ts`.
- Contrato em [`mqtt.asyncapi.yaml`](../contratos/mqtt.asyncapi.yaml), com
  [vetores de assinatura](../contratos/mqtt-assinatura.vetores.json) rodados por todas as implementações.

## Consequências

- Ler o broker não revela nada sobre a pessoa; publicar nele não engana o Core.
- A Sentinela precisa de relógio (NTP) e de memória não volátil para o `seq`.
- Confidencialidade (TLS) fica fora: o conteúdo não é sensível. Pode entrar depois sem mudar o contrato.

## Alternativas consideradas

- **TLS com certificado no ESP32:** protege o transporte, mas não impede alguém de publicar no broker público.
- **Broker próprio exposto na internet:** mais infraestrutura para manter durante a GS, sem ganho de segurança sobre a
  assinatura.
