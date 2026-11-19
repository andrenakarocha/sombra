# Entrega de Cybersecurity

> Prof. Vitor Lasse · spec [`07-cyber-privacidade.md`](../../docs/specs/07-cyber-privacidade.md). O professor pediu **um
> PDF** com a documentação e, **separado**, o código (ZIP ou link do GitHub), sem misturar, mais o vídeo do dashboard.

**Vídeo do dashboard:** https://youtu.be/O-JLswyySPU · roteiro em [`docs/videos/cyber.md`](../../docs/videos/cyber.md)

| Arquivo | O que é |
|---|---|
| [`cyber.pdf`](cyber.pdf) | a documentação: capa com os integrantes, o mapa da rubrica e uma seção por pilar (Parsing de PII, Taint Analysis, IA na remediação, Dashboard de Risco Cidadão), com os trechos de código e as evidências |
| `sombra-codigo.zip` | o código, gerado na hora (`git archive` do commit atual), entregue à parte e fora do Git |
| [`cyber.html`](cyber.html) | a fonte do PDF |
| [`traducoes.json`](traducoes.json) · [`semgrep.txt`](semgrep.txt) | as traduções reais da IA e a última execução do Semgrep, guardadas para o PDF poder ser refeito sem o Core no ar |

```bash
make entrega-cyber    # na raiz, com o make up no ar: refaz o PDF e o ZIP
```

Nada no PDF é escrito à mão: o [`gerar.py`](gerar.py) copia os trechos dos arquivos-fonte (com o caminho e as linhas), lê
as tabelas dos relatórios que os testes geram, roda o Semgrep de novo e busca no Core os eventos reais já traduzidos.
