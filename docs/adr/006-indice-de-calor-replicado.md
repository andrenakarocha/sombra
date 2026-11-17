# ADR-006 · Fórmula do índice de calor replicada de propósito, com vetores compartilhados

**Status:** aceito · 2026-11-17

## Contexto

Quatro partes calculam o índice de calor: a **Sentinela** (precisa alertar mesmo sem rede), o **Core** (confere a
leitura que chega), o **Clima** (classifica a observação da estação) e o **ML** (monta o alvo do modelo). Cada uma está
numa linguagem: C++, C#, Java e Python. Centralizar o cálculo num serviço tiraria da Sentinela a capacidade de alertar
offline e acoplaria serviços que, pelo ADR-003, devem ser independentes.

## Decisão

- Cada parte tem **a sua implementação**, que segue a mesma leitura da equação oficial da NWS/WPC, descrita no
  [contrato de níveis](../contratos/niveis-de-risco.md).
- A consistência é garantida por **vetores de teste compartilhados**
  ([`indice-de-calor.vetores.json`](../contratos/indice-de-calor.vetores.json): 20 casos, os cinco níveis e os dois
  ajustes), gerados por uma [implementação de referência](../../tools/indice-de-calor/vetores.py) e rodados nos testes
  de **todas** as implementações.
- Os limiares dos níveis são os da NWS convertidos de °F com duas casas: 26,67 / 32,22 / 39,44 / 51,67 °C.

## Consequências

- Uma fórmula pública e estável é copiada em quatro linguagens, mas qualquer divergência quebra um teste.
- O Core trata o nível enviado pela Sentinela como informativo e recalcula para decidir.
- Mudar a fórmula é mudar o contrato e os vetores no mesmo PR.

## Alternativas consideradas

- **Um serviço de cálculo:** a Sentinela perderia o alerta offline, que veio da entrevista ("se eu tivesse caído
  sozinha, não sei quem ia perceber").
- **Uma biblioteca compartilhada:** não existe uma que sirva a C++, C#, Java e Python ao mesmo tempo.
