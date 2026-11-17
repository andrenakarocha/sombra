# Uso de IA no Sombra

A GS permite IA **como copiloto**, para aprender e revisar, e pede que os prompts fiquem registrados aqui e que cada
integrante saiba explicar cada linha do que entregou.

**Ferramenta:** Claude Code (modelo Claude Opus), em sessões no terminal dos integrantes.

**Como usamos:** cada checkpoint do [plano](docs/PLANO.md) começa com um pedido nosso. A IA propõe e escreve, e o
integrante revisa, testa e aprova antes do commit. Decisões de produto, a entrevista e as fotos são nossas.

## Registro

| Data | Checkpoint | Pedido à IA (resumo) | O que a IA fez | Revisão humana |
|---|---|---|---|---|
| 2026-11-17 | Planejamento | "Entra na pasta da segunda GS, analisa o documento e o site oficial e sugere soluções; depois monta um plano com todas as matérias, production ready." | Leu os enunciados (PDF geral, PDF de Cyber, instruções de OS, guia de SOA), propôs três ideias e, depois da escolha do Sombra, escreveu o plano mestre e as specs | André escolheu a ideia, o monorepo e o escopo; definiu a divisão do time; pediu o formato do plano do projeto Kyber |
| 2026-11-17 | F0.16 | "Coloquei a entrevista e as fotos em docs." | Leu a transcrição e propôs ajustes de design (escalonamento em degraus, Sentinela discreta, check-in diário) | André confirmou a moradia (casa) e o uso das fotos; os ajustes viraram a decisão D13 |
| 2026-11-17 | F0.2 | "Pode seguir." | Escreveu `tools/doctor.sh`, que confere as ferramentas de cada fase | Rodado na máquina do André, saiu com 0 |
| 2026-11-17 | F0.4 | "Pode seguir." | Criou a estrutura do monorepo, o README com a Ficha, este arquivo e os arquivos de base | André revisa no primeiro push (F0.5) |
