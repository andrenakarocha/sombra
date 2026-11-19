# Privacidade e auditoria (Cyber)

> Fase **F2** do [plano](../docs/PLANO.md) · spec [`07-cyber-privacidade.md`](../docs/specs/07-cyber-privacidade.md) ·
> código em [`services/core-api/sources/Sombra.Privacidade`](../services/core-api/sources/Sombra.Privacidade)

| Evidência | Onde | Como regenerar |
|---|---|---|
| Relatório de acertos do motor de PII (63 casos, 0 falso negativo) | [`privacidade/relatorio-de-acertos.md`](privacidade/relatorio-de-acertos.md) | `make -C services/core-api pii-relatorio` |
| Taint estático: regras, canário e SARIF | [`semgrep/`](semgrep/) | `make semgrep` |
| **Entrega:** o PDF com os quatro pilares e, à parte, o ZIP do código | [`entrega/`](entrega/) | `make entrega-cyber` |
| Auditoria antes × depois (A1–A8) | [`auditoria/relatorio.md`](auditoria/relatorio.md) e [`relatorio.json`](auditoria/relatorio.json) | `security/auditoria/rodar-auditoria.sh` |

- **Regras de PII como dados:** [`pii-regras.json`](../services/core-api/sources/Sombra.Privacidade/Regras/pii-regras.json),
  com o schema validado na carga.
- **Modo inseguro da auditoria:** `Privacidade__Aplicacao=Desligada`, aceito só com `ASPNETCORE_ENVIRONMENT=Demo`. Os
  guardas detectam e registram como vazamento, mas deixam passar. Os endpoints vulneráveis de propósito
  (`/api/v1/auditoria/*`) só existem em `Demo` e `Test`.
- **Dados:** todos sintéticos (CPF e Cartão SUS gerados por semente); a senha do A7 é gerada a cada execução e sai
  redigida no relatório.
