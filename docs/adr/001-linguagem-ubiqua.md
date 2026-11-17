# ADR-001 · Linguagem ubíqua em português, sufixos técnicos em inglês

**Status:** aceito · 2026-11-17 · decisão D11

## Contexto

O Sombra existe para pessoas que falam português: a Dona Cida, a filha, o agente de saúde. As palavras do problema
("domicílio", "estou bem", "rede de cuidado", "escalonamento") não têm tradução fiel, e traduzi-las no código cria duas
línguas para a mesma ideia. O guia de SOA do professor usa o mesmo padrão (`Estacao`, `LeiturasController`).

## Decisão

- Os **termos do domínio** ficam em português no código, nos contratos e na UI, como estão no
  [glossário](../glossario.md): `Domicilio`, `AlertaDeCalor`, `CheckIn`, `RedeDeCuidado`.
- Os **termos técnicos** ficam em inglês: `Repository`, `Controller`, `Middleware`, `DbContext`, e tudo o que vem de
  framework.
- **Commits** em inglês (Conventional Commits). **Documentação e UI** em pt-BR.

## Consequências

- O professor encontra no código as mesmas palavras da Ficha e da entrevista.
- Nomes mistos (`DomicilioRepository`) são esperados e consistentes.
- Acento não entra em identificador (`Domicilio`, `Observacao`); entra em texto.

## Alternativas consideradas

- **Tudo em inglês:** perde a ponte com o problema e com a avaliação ("nomes vêm do problema da CitizenTech").
- **Tudo em português:** brigaria com frameworks e bibliotecas.
