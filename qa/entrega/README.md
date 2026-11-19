# Entrega de QA

> Spec [`09-qa-azure-boards.md`](../../docs/specs/09-qa-azure-boards.md). A tarefa pede **um PDF** com o link do projeto
> no Azure, prints de todas as telas de Features e de Test Cases, o **professor como Admin** e o link do pitch.

O PDF sai do repositório: os números e a matriz de rastreabilidade vêm de [`../especificacao/`](../especificacao/), e os
prints são tirados por script, um por work item. Os prints, o `qa.pdf` e a sessão do navegador ficam fora do Git,
porque mostram o professor e a conta de quem entrou: o PDF vai só para o Teams.

## Passo a passo (operador)

1. **Azure DevOps** (F10.1): crie a organização e o projeto (processo Agile) e adicione o professor em *Project
   settings → Permissions → Project Administrators*. Crie um PAT com o escopo **Work Items: Read & write**.
2. **Subir tudo:** na raiz,
   `AZURE_DEVOPS_ORG=https://dev.azure.com/<organizacao> AZURE_DEVOPS_PROJETO=<projeto> AZURE_DEVOPS_PAT=<pat> make qa-azure`.
   Cria 63 itens e grava o `qa/azure/mapa.json` (commite esse arquivo). Rodar de novo não duplica nada.
3. **Prints:** `make qa-prints`. Abre um Chrome; na primeira vez, faça o login na janela. O script tira 66 prints
   (a tela de permissões, as duas listas, o Epic, as 12 Features e os 50 Test Cases) em `prints/`. `--de-novo` refaz.
4. **PDF:** `PITCH_URL=<link do pitch, se houver> make qa-pdf` → `qa/entrega/qa.pdf`. Um print que falta aparece em
   vermelho no PDF; confira antes de enviar.
