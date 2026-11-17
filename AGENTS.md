# Instruções para agentes

Comece **sempre** pelo [`docs/PLANO.md`](docs/PLANO.md), seção **§0 (Protocolo para agentes)**. Ele é a fonte de verdade
do projeto.

Em resumo:

1. Leia o contexto (§1) e as decisões (§2). As decisões fechadas não se re-litigam.
2. Abra o painel de status (§12) e rode o 🔎 do último checkpoint marcado antes de seguir.
3. Execute o próximo checkpoint da fase, com o detalhe da spec correspondente em `docs/specs/`.
4. Um checkpoint vira um commit atômico (Conventional Commits em inglês). Ao concluir, marque `[x]` com data e hora,
   atualize a §12, acrescente uma linha na §13 e registre o pedido principal no [`prompts.md`](prompts.md).
5. Passos 🧑 são do André: não simule, não invente credenciais, entrevistas, fotos ou dados.
6. Nenhum dado pessoal real em código, fixtures, logs, prompts ou serviços externos.
7. Documentação e UI em pt-BR. Código em inglês, exceto a linguagem ubíqua do domínio, que fica em português (D11).
