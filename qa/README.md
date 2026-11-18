# QA · Azure Boards

> Fase **F10** do [plano](../docs/PLANO.md) · spec [`09-qa-azure-boards.md`](../docs/specs/09-qa-azure-boards.md)

As Features e os Test Cases vivem aqui como código, revisados por PR, e sobem para o Azure Boards pelo script de
sincronização (F10.4).

| Arquivo | O que tem |
|---|---|
| [`especificacao/features.yaml`](especificacao/features.yaml) | Epic e 12 Features (F01–F12): *Como / quero / para*, critérios *Dado / Quando / Então*, prioridade, valor de negócio, tags e regras cobertas |
| [`especificacao/casos-de-teste.yaml`](especificacao/casos-de-teste.yaml) | 50 Test Cases caixa-preta (22 positivos, 28 negativos): objetivo, preparação, entradas, passos, resultado esperado, técnica e os testes automatizados que executam cada caso |
| [`ferramentas/conferir_especificacao.py`](ferramentas/conferir_especificacao.py) | Confere as regras da spec antes de subir |

## Conferir

```bash
uv run qa/ferramentas/conferir_especificacao.py
```

O script falha se faltar um campo do padrão (D9), se alguma Feature ficar sem caso negativo, se algum critério de
aceite ficar sem caso, ou se um teste automatizado citado não existir no repositório. Assim a rastreabilidade não
aponta para o vazio.

## Rastreabilidade

Cada caso aponta para os testes que o executam: **MSTest** no Core (`services/core-api`), **Jest** no app (`mobile`) e
**pytest** no simulador da Sentinela (`tools/sentinela-sim`). São 49 casos automatizados e 1 manual: a fonte em 200%
com TalkBack (TC36). O CPF em campo livre (TC39) passou a ser automatizado com a F2.
