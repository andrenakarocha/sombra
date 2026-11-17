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
| 2026-11-17 | F0.5 | "Público agora; PR obrigatório para todos." Depois: "um PR por lote". | Criou o repositório, as labels, os templates e a proteção da `main` pelo `gh`; propôs o fluxo por lote quando o hook mostrou que o push é manual | André escolheu a visibilidade, a proteção e o fluxo de PR |
| 2026-11-17 | F0.6 | "Pode seguir." | Conferiu a equação do índice de calor na página oficial da NWS, escreveu a implementação de referência, os 20 vetores de teste, o contrato de níveis e o glossário | Contrato e glossário entram no PR do lote para revisão do André |
| 2026-11-17 | F0.7 | "Pode seguir." | Escreveu o AsyncAPI do MQTT, a assinatura HMAC com vetores, os contratos REST do Core e do Clima, os eventos do SignalR e o `.env.example`; achou e corrigiu um exemplo que contrariava a fórmula do índice de calor | Contratos entram no PR do lote para revisão do André |
| 2026-11-17 | F0.8 | "Pode seguir." | Escreveu o padrão de observabilidade e logs (campos, correlação, catálogo de eventos, como fazer em cada stack, saúde e auditoria) e ligou cada spec à seção que lhe cabe | Entra no PR do lote para revisão do André |
| 2026-11-17 | F0.9 | "Pode seguir." | Propôs a direção de arte a partir das fotos e da entrevista: tokens, verificador de contraste e página de amostra, revisados em capturas reais | Aguardando a aprovação do André |
| 2026-11-17 | F0.9 | "Direção de arte aprovada." | Registrou a aprovação como D16 e tirou a marca de proposta da amostra | André aprovou a amostra no navegador |
| 2026-11-17 | F0.10 | "Pode seguir." | Escreveu o compose (um banco e um papel por serviço, Mosquitto, placeholders), o Makefile e o gerador de `.env`; achou a porta 5432 ocupada pelo Postgres local e mudou o banco do compose para 55432 no host | `make up` rodado na máquina do André, com tudo saudável |
| 2026-11-17 | F0.11 | "Pode seguir." | Escreveu o simulador da Sentinela (contrato MQTT, cenários do roteiro, comandos verificados) com 45 testes, incluindo a integração com o broker | Rodado na máquina do André contra o `make up` |
| 2026-11-17 | F0.12 | "Pode seguir." | Escreveu o CI com filtro por pasta e o agregador `ci-ok`; fixou as actions por SHA depois de ler o `action.yml` de cada uma; trocou a gitleaks-action (licença comercial) pelo binário MIT | Falta ver o CI rodar no PR |
| 2026-11-17 | F0.13 | "Pode seguir." | Escreveu os seis ADRs a partir das decisões do plano e dos contratos | Entra no PR do lote para revisão do André |
