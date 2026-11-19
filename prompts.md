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

## Privacidade (Cybersecurity)

1. *"Construa o motor de parsing de PII com as regras como dados (um JSON validado na carga), detectores para CPF, Cartão
   SUS, RG, telefone, e-mail, CEP, saúde e credenciais, e uma tabela de casos positivos, negativos e difíceis com meta de
   zero falso negativo. Depois, desligue um validador de propósito e mostre que a tabela pega."*
2. *"Faça o taint em tempo de execução: marque o que entra em cada requisição, mascare o dado pessoal escrito em campo
   livre antes de gravar e de logar, e prove o cenário A1 com teste de integração."*
3. *"Cifre a PII em repouso com AES-256-GCM e índice cego HMAC, com a chave do ambiente, e confira no psql que as colunas
   aparecem cifradas e a API devolve mascarado."*
4. *"Ponha um guarda em cada sink (log, resposta, saída externa, MQTT e exceções) e escreva endpoints propositalmente
   vulneráveis para provar os cenários A2 a A7. Rode a suíte toda e corrija os falsos positivos que aparecerem."*
5. *"Calcule o farol do Risco Cidadão com pesos por família, desfecho e decaimento no tempo, calibrado para a leitura que
   o app já mostra, e exponha o farol e a trilha de eventos só com metadados."*
6. *"Traduza cada evento para o cidadão, a gestão e o time técnico com a Gemini, sem PII no prompt (com um teste de
   propriedade que prove isso), guarda na resposta, cache e fallback em template. Confira a P5 na API real antes de
   fixar o modelo."*
7. *"Escreva regras do Semgrep em modo taint com um canário que prove cada regra e um job no CI com SARIF."*
8. *"Rode os oito cenários da auditoria com o módulo desligado e ligado e gere o relatório antes × depois."*

## QA (Azure Boards)

1. *"Sou o João. Quero fazer a minha parte do Sombra: o QA, checkpoints F10.2 e F10.3 (features.yaml e
   casos-de-teste.yaml). Leia o AGENTS.md e o docs/PLANO.md e me guie checkpoint por checkpoint."*
2. *"Escreva as 12 Features com Como/Quero/Para e critérios Dado/Quando/Então tirados das regras do Core e do contrato
   de níveis, e de 40 a 50 casos de teste caixa-preta, positivos e negativos, com a técnica de cada um e o teste
   automatizado que já executa o caso."*
3. *"Escreva um conferidor que falhe se faltar campo do padrão, se uma Feature ficar sem caso negativo ou se um teste
   citado não existir no repositório."*

## Clima Service (SOA e Web Services)

1. *"Monte o Clima Service em Spring Boot 3.5 e Java 21 exatamente nas camadas do guia do professor (domínio, aplicação,
   infraestrutura e api), com o tratamento de erros em ProblemDetails pt-BR e aviso de compilação como erro."*
2. *"Escreva o domínio com as regras dentro das entidades: o índice de calor rodando os 20 vetores do contrato, -9999 do
   INMET não é observação, umidade de 0 a 100, região exige estação válida e ponto que abre antes de fechar, cada regra
   com um código estável no 422."*
3. *"Crie as migrações V1 a V4 no Flyway, com a V3 evoluindo o esquema como se a tabela já tivesse linhas, e gere o seed
   por script a partir do CSV real da A701 conferido pelo SHA-256 e de lugares públicos reais do OpenStreetMap, com o
   horário do próprio OSM. Prove num PostgreSQL real do Testcontainers."*
4. *"Documente todos os endpoints no springdoc com resumo, tag, exemplo e os erros com o corpo Problema, e escreva um teste
   que falhe se um endpoint ficar sem isso ou se o contrato versionado ficar desatualizado."*
5. *"Ligue a Open-Meteo com timeout e novas tentativas (confira a P7 na API real) e o replay da onda de calor real de
   nov/2023 num relógio acelerado, testado com um relógio controlado e um servidor HTTP falso."*
6. *"Faça o previsor por regra (persistência do pico, o baseline do ML) e o /risco-calor, com 503 quando não houver
   observação recente, e os logs JSON com a correlação do Core."*
7. *"No Core, troque o stub pelo ClimaClient real com circuit breaker e último risco conhecido, lendo as amostras reais
   que o próprio Clima grava, sem quebrar os testes nem a nota 100 da pipeline de qualidade."*

## Machine Learning

1. *"Leia o plano e a spec de ML e veja o que o Clima Service guarda em produção. Carregue os CSVs reais da A701 conferindo o
   SHA-256, trate -9999, fuso e buracos em funções testadas, e prove que nenhuma amostra enxerga o futuro."*
2. *"Meça nos dados reais qual limiar da tabela da NWS dá para usar como alvo e justifique. Crie features só com as medições
   e as 24 horas que o serviço terá em produção, a partir de um catálogo único."*
3. *"Divida por tempo, com folga de 24 horas entre treino e teste, e compare persistência e regressão logística na validação
   temporal, sem tocar no teste."*
4. *"Treine o XGBoost com GridSearchCV e TimeSeriesSplit em 12 combinações de 3 hiperparâmetros, escolha o limiar para
   recall de 0,90 fora da amostra e aplique uma única vez no teste."*
5. *"Avalie como o cidadão sentiria: dias de calor avisados a tempo e dias de alarme falso por mês. Diga com intervalo de
   confiança se o XGBoost ganhou mesmo dos baselines, e não esconda se empatar."*
6. *"Exporte os modelos para ONNX com o contrato das features e amostras de paridade, inclusive as janelas brutas para o teste
   em Java, e confira no replay de novembro de 2023 o que muda para o preventivo do Seu Joaquim."*

<!-- Cada parte acrescenta aqui os seus prompts principais quando fecha (PLANO §0, regra 16). -->
