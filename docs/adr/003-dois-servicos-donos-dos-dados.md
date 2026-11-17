# ADR-003 · Dois serviços, cada um dono do seu banco

**Status:** aceito · 2026-11-17 · decisão D4

## Contexto

As disciplinas de C# e de SOA pedem, cada uma, "a camada de backend da solução". Entregar o mesmo repositório duas vezes
seria frágil, e a SOA ensina que *"o serviço termina onde a palavra muda de sentido"*. No Sombra, isso acontece de
verdade: uma **leitura** é o calor de dentro da casa, medido pela Sentinela; uma **observação** é o calor da rua, medido
por uma estação oficial.

## Decisão

- **Core API** (C#/.NET 10): casas, moradores, rede de cuidado, Sentinelas, leituras, alertas, check-ins, privacidade.
- **Clima Service** (Java/Spring Boot): regiões, estações, observações, previsão de calor (modelo de ML) e pontos de
  resfriamento.
- **Cada serviço é dono do seu banco** (`sombra_core`, `sombra_clima`), com um papel próprio no PostgreSQL e o `CONNECT`
  público revogado: um serviço não consegue nem se conectar ao banco do outro. Eles conversam só pelo contrato REST.
- O app e o painel falam **só com o Core**; o Core consome o Clima com resiliência (timeout, retry, circuit breaker).

## Consequências

- Duas stacks no mesmo projeto; o contrato (OpenAPI) isola as duas.
- Se o Clima cair, o Core continua com o último risco conhecido e com os alertas da Sentinela, que não dependem dele.
- O isolamento é verificado no CI (`ambiente-local`).

## Alternativas consideradas

- **Um backend só para as duas disciplinas:** menos trabalho, mas a mesma entrega duas vezes e sem SOA de verdade.
- **Banco compartilhado:** acoplaria os esquemas e contrariaria o que a disciplina de SOA avalia.
