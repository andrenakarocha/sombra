# O problema: Definir (etapa 3 da GS)

## Em uma frase

> *Em ondas de calor, idosos que moram sozinhos em São Paulo não percebem o perigo dentro de casa, e a família só fica
> sabendo quando já é tarde.*

## Quem é o cidadão

A **pessoa idosa que mora sozinha** e a **rede de cuidado** dela: a família, que acompanha de longe, e quem está perto,
como a vizinha. Na [entrevista](entrevista.md), a Dona Cida passa "a maior parte do dia sozinha", a filha liga todo dia,
mas "trabalha fora e não consegue acompanhar o tempo todo", e foi a vizinha quem percebeu no dia em que ela passou mal.

## Por que é um problema de verdade

- **O calor de dentro de casa é o que importa, e ninguém mede.** "Lá pelas três, quatro horas, parece que a casa virou
  um forno", e de noite demora a refrescar. A estação do INMET mede a rua; a Sentinela mede a casa.
- **A pessoa idosa não percebe o perigo a tempo.** Esquece de beber água, e o remédio pode piorar a desidratação. No
  Sombra, o limite de perigo é **pessoal**: para quem toma diurético, ele começa 1,5 °C antes do padrão da NWS (R3).
- **A família descobre tarde, ou se preocupa à toa.** Hoje, ela sabe ligando; se a mãe não atende, a preocupação cresce.
  E a filha pediu o oposto de um alarme: "não queria receber um alerta de emergência toda vez que ela esquecesse de
  apertar um botão".
- **O calor forte é frequente em São Paulo.** Nos dados reais da estação A701 (Mirante de Santana), de 2010 a 2025, o
  índice de calor chegou à cautela extrema (32,22 °C) em 75 dias só no período de teste, de 2022 a 2025, e o maior valor
  em 16 anos foi 35,47 °C ([relatório de ML](../ml/RELATORIO_ML.md)). Para uma pessoa de 80 anos com cardiopatia, pela
  R3, o perigo começa em 34,94 °C.

## O que precisa ser verdade para o problema estar resolvido

| Indicador | Meta |
|---|---|
| Tempo mediano entre o alerta e o "estou bem" | menos de 15 min |
| Alertas resolvidos sem chegar ao último contato da rede | acompanhado no painel do operador |
| Dias de calor forte avisados com até 24 h de antecedência | 9,9 de cada 10 (74 de 75 dias no teste de 2022 a 2025) |
| Alarmes desnecessários para a família por semana | perto de zero, o pedido da filha |

## O que o Sombra não é

Não é um dispositivo médico nem faz diagnóstico, e não substitui o SAMU nem a Defesa Civil: ele avisa as pessoas certas
mais cedo. É um protótipo com dados de clima reais e dados de pessoas fictícios.
