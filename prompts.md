# Uso de IA no Sombra

A GS permite IA **como copiloto**, para aprender e revisar, e pede que os prompts fiquem registrados aqui. Cada
integrante sabe explicar o que entregou.

- **Ferramenta:** Claude Code, no terminal de cada integrante.
- **Como usamos:** a IA propõe e escreve; a gente revisa, testa e aprova cada mudança antes de ela entrar na `main`.
- **Nossas, não da IA:** a escolha do problema, a entrevista, as fotos, o escopo, a direção de arte e as decisões do
  [plano](docs/PLANO.md#2-decisões).

## Planejamento e fundação

1. *"Leia o briefing da GS CitizenTech e os enunciados das oito disciplinas e proponha três CitizenTechs que atravessem
   todas elas, com uma recomendação."*
2. *"Monte um plano mestre com fases, checkpoints verificáveis e critérios de aceite para todas as disciplinas, com uma
   spec por disciplina."*
3. *"A partir da transcrição da entrevista, liste o que muda no design do Sombra."*
4. *"Defina os contratos entre as partes (MQTT, REST e tempo real), com vetores de teste para o índice de calor e para a
   assinatura das mensagens."*
5. *"Proponha uma direção de arte a partir das fotos do território, com tokens e contraste verificado nos dois temas."*
6. *"Monte o ambiente local, o simulador da Sentinela, o CI e o download dos dados reais do INMET."*

## Core API (C#)

1. *"Monte o Core em .NET 10 com cinco camadas (Domínio, Aplicação, Infraestrutura, Privacidade e API) e o pipeline de
   qualidade do professor sem alteração, com todo comando rodando na imagem do SDK, igual ao CI."*
2. *"Modele o domínio do Sombra com POO de verdade: value objects que recusam estado inválido, o agregado Domicilio, os
   fatores de risco que reduzem os limites térmicos e a hierarquia de alertas com a máquina de estados e o escalonamento
   em degraus da entrevista. Cada regra com teste."*
3. *"Escreva os casos de uso e as portas da aplicação, com DTOs validados por DataAnnotations e testes com fakes para os
   caminhos A e B do roteiro."*
4. *"Persista o domínio com EF Core e PostgreSQL sem contaminar o domínio: tipos complexos, conversores, TPH, as
   migrações do plano e as consultas LINQ do painel, com testes de integração em Testcontainers."*
5. *"Exponha a API v1 com ProblemDetails, JWT com papéis, Cuidador restrito às casas dele, limite de requisições e
   OpenAPI com resumo, exemplo e 4xx em todo endpoint, no Scalar."*
6. *"Siga o formato de log da observabilidade, uma linha JSON por evento com correlação de ponta a ponta e nenhum dado
   pessoal, e ponha o Core real no `make up`."*
7. *"Ligue o Core à Sentinela por MQTT assinado, com HMAC e anti-replay, outbox, workers, o lembrete da rotina e o hub
   SignalR. Rode o simulador nos três cenários do roteiro e corrija o que ele revelar."*
8. *"Escreva testes de arquitetura que barrem dependências erradas entre as camadas e um teste que rode o roteiro da
   Dona Lurdes de ponta a ponta; depois, o README da pasta com a rubrica e as evidências."*

## QA (Azure Boards)

1. *"Sou o João. Quero fazer a minha parte do Sombra: o QA, checkpoints F10.2 e F10.3 (features.yaml e
   casos-de-teste.yaml). Leia o AGENTS.md e o docs/PLANO.md e me guie checkpoint por checkpoint."*
2. *"Escreva as 12 Features com Como/Quero/Para e critérios Dado/Quando/Então tirados das regras do Core e do contrato
   de níveis, e de 40 a 50 casos de teste caixa-preta, positivos e negativos, com a técnica de cada um e o teste
   automatizado que já executa o caso."*
3. *"Escreva um conferidor que falhe se faltar campo do padrão, se uma Feature ficar sem caso negativo ou se um teste
   citado não existir no repositório."*

<!-- Cada parte acrescenta aqui os seus prompts principais quando fecha (PLANO §0, regra 16). -->
