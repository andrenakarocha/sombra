# ADR-005 · LLM para traduzir falhas, sem dado pessoal e com fallback

**Status:** aceito · 2026-11-17 · decisão D7

## Contexto

O professor de Cybersecurity pede que uma IA generativa traduza falhas técnicas em mensagens que um cidadão entende, e
alimente o farol de Risco Cidadão. Mandar o evento de segurança para uma API externa cria dois riscos: vazar o próprio
dado que se quer proteger, e o painel ficar sem mensagem quando a API cair ou a cota acabar.

## Decisão

- Provedor: **Gemini Flash**, porque a equipe já tem a chave. O modelo exato é fixado na F2.6, depois de conferir a P5.
- **O prompt nunca leva dado pessoal:** só metadados do evento (categoria, saída, rota sem valores, severidade). A guarda
  de saída externa do `Sombra.Privacidade` bloqueia qualquer tentativa.
- **A resposta da IA também é conferida:** os mesmos detectores de PII, uma lista de jargões proibidos e um limite de
  tamanho. Se reprovar, vale o template.
- **Fallback em templates** por categoria e cache por assinatura do evento. A mensagem sempre diz de onde veio
  (`fonte: ia` ou `fonte: template`).

## Consequências

- O farol nunca fica vazio, mesmo sem rede ou sem cota.
- "Nem a IA vê o dado" vira um argumento de segurança demonstrável na auditoria (cenário A5).
- Trocar de provedor é trocar um adaptador; o contrato da tradução não muda.

## Alternativas consideradas

- **Só templates:** não cumpre o pilar "IA na remediação" da rubrica.
- **Modelo local:** pesado para a VM da GS e sem ganho para a demonstração.
