# QA · Azure Boards

> Fase **F10** do [plano](../docs/PLANO.md) · spec [`09-qa-azure-boards.md`](../docs/specs/09-qa-azure-boards.md)

As Features e os Test Cases vivem aqui como código, revisados por PR, e sobem para o Azure Boards pelo script de
sincronização (F10.4).

| Arquivo | O que tem |
|---|---|
| [`especificacao/features.yaml`](especificacao/features.yaml) | Epic e 12 Features (F01–F12): *Como / quero / para*, critérios *Dado / Quando / Então*, prioridade, valor de negócio, tags e regras cobertas |
| [`especificacao/casos-de-teste.yaml`](especificacao/casos-de-teste.yaml) | 50 Test Cases caixa-preta (22 positivos, 28 negativos): objetivo, preparação, entradas, passos, resultado esperado, técnica e os testes automatizados que executam cada caso |
| [`ferramentas/conferir_especificacao.py`](ferramentas/conferir_especificacao.py) | Confere as regras da spec antes de subir |
| [`ferramentas/sincronizar_azure.py`](ferramentas/sincronizar_azure.py) | Sobe o Epic, as Features e os Test Cases para o Azure Boards, com os passos e os vínculos; idempotente pelo mapa `azure/mapa.json` |
| [`ferramentas/testar_sincronizacao.py`](ferramentas/testar_sincronizacao.py) | Testa a sincronização contra um Azure DevOps falso: cria tudo, a segunda execução não muda nada |

## Conferir

```bash
uv run qa/ferramentas/conferir_especificacao.py
```

O script falha se faltar um campo do padrão (D9), se alguma Feature ficar sem caso negativo, se algum critério de
aceite ficar sem caso, ou se um teste automatizado citado não existir no repositório. Assim a rastreabilidade não
aponta para o vazio.

## Subir para o Azure Boards

```bash
make test-qa                                   # confere a especificação e testa a sincronização (também no CI)
uv run qa/ferramentas/sincronizar_azure.py --simular   # o que seria criado, sem rede
AZURE_DEVOPS_ORG=https://dev.azure.com/<organizacao> AZURE_DEVOPS_PROJETO=<projeto> AZURE_DEVOPS_PAT=<pat> make qa-azure
```

O PAT precisa do escopo **Work Items: Read & write** e só passa pelo ambiente. A primeira execução cria 1 Epic, 12
Features (filhas do Epic) e 50 Test Cases (cada um ligado à sua Feature por **Tested By**, com os passos em
ação → resultado esperado) e grava o `qa/azure/mapa.json`, que vai para o Git. Rodar de novo só atualiza o que mudou no
YAML e nunca duplica um item ou um vínculo. O processo do projeto precisa ter o tipo **Epic** (Agile, Scrum ou CMMI).

## Rastreabilidade

Cada caso aponta para os testes que o executam: **MSTest** no Core (`services/core-api`), **Jest** no app (`mobile`) e
**pytest** no simulador da Sentinela (`tools/sentinela-sim`). São 49 casos automatizados e 1 manual: a fonte em 200%
com TalkBack (TC36). O CPF em campo livre (TC39) passou a ser automatizado com a F2.
