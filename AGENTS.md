# Instruções para agentes

Comece **sempre** pelo [`docs/PLANO.md`](docs/PLANO.md), seção **§0 (Protocolo para agentes)**. Ele é a fonte de verdade
do projeto.

**Operador** é a pessoa com quem você está trabalhando nesta sessão. Cada integrante é operador da sua parte (D10):
escolhe o que vai fazer, decide dentro da spec e entrega por PR. O dono do repositório revisa e faz o merge.

Em resumo:

1. Leia o contexto (§1) e as decisões (§2). As decisões fechadas não se re-litigam.
2. Abra o painel de status (§12) e rode o 🔎 do último checkpoint marcado antes de seguir.
3. Execute o próximo checkpoint da parte escolhida, com o detalhe da spec correspondente em `docs/specs/`.
4. Um checkpoint vira um commit atômico (Conventional Commits em inglês). Ao concluir, marque `[x]` com a data; ao
   fechar uma fase, assine-a na §12. Sem log de execução. Ao fechar uma parte, acrescente os prompts principais dela no
   [`prompts.md`](prompts.md) (exigência da GS).
5. Passos 🧑 são do operador: não simule, não invente credenciais, entrevistas, fotos ou dados.
6. Nenhum dado pessoal real em código, fixtures, logs, prompts ou serviços externos.
7. Documentação e UI em pt-BR. Código em inglês, exceto a linguagem ubíqua do domínio, que fica em português (D11).

## Quando alguém chega para fazer uma parte

Se a pessoa abrir a sessão dizendo algo como *"sou o João, quero fazer a minha parte"* ou *"quero fazer o ML"*:

1. Leia o `docs/PLANO.md` (§0, §2 e §12) e mostre os checkpoints abertos da parte pedida. Os marcados 🤝 são bons
   pontos de partida.
2. Combine com a pessoa o que ela vai fazer e leia a spec correspondente em `docs/specs/`.
3. Siga o protocolo da §0: branch `fN-parte-nome` (ex.: `f4-ml-matheus`), um commit por checkpoint, checkpoints
   marcados no plano, PR para a `main` no fim.
4. Passo 🔑 (conta, chave, VM, Wokwi, Azure, YouTube): combine com a pessoa. Se for uma conta do projeto, ela pede ao
   dono do repositório.

Prompt para mandar no grupo:

```
Clona github.com/andrenakarocha/sombra, abre o Claude Code na pasta e manda:
"Sou o <seu nome>. Quero fazer a minha parte do Sombra: <a parte, ex.: o ML>.
Leia o AGENTS.md e o docs/PLANO.md e me guie checkpoint por checkpoint."
```

Para abrir PR, a pessoa precisa ser colaboradora do repositório.
