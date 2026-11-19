# Guia de defesa

> F12.4. As perguntas prováveis de cada disciplina (tiradas da seção "Defesa" de cada spec), com a resposta curta e onde
> mostrar no código. A regra do plano: **cada integrante explica uma parte que não fez**. Leiam em voz alta uns para os
> outros; quem ouve pergunta "por quê?" até a resposta chegar no código.

## C# Software Development (`services/core-api/`)

| Pergunta | Resposta curta | Onde mostrar |
|---|---|---|
| Por que `AlertaDeCalor` é abstrato, e onde está o polimorfismo? | Há três alertas com regras diferentes: o da Sentinela (escalona em degraus), o preventivo (aviso da véspera, não pede resposta) e o pedido de ajuda. O worker chama `Escalonar` sem saber qual é; cada subclasse decide. | `Sombra.Dominio/Alertas/AlertaDeCalor.cs` e as três subclasses |
| Como um alerta resolvido não volta a escalonar? | O estado vive no agregado: toda transição passa por `GarantirAberto()`, que lança a regra R5 se o alerta foi encerrado. Não dá para escalonar por fora. | `AlertaDeCalor.GarantirAberto` |
| `Scoped` × `Singleton` nos workers? | O worker é singleton e vive o tempo todo; o `DbContext` é scoped. Por isso o worker cria um escopo por volta com `IServiceScopeFactory`, em vez de segurar um contexto para sempre. | `Sombra.Infraestrutura/Workers/Workers.cs` |
| Como o EF traduz o `GroupBy`? | A consulta LINQ vira um `GROUP BY` no PostgreSQL: a agregação roda no banco, não em memória. | `Persistencia/ConsultasDoPainel.cs` |
| O que o `CancellationToken` faz? | Quando a requisição é cancelada ou o app desliga, o token avisa a consulta e o worker, que param na hora em vez de terminar trabalho que ninguém vai usar. | qualquer `…Async(…, cancellationToken)` |
| Por que o domínio não referencia o EF? | Para a regra de negócio não depender de banco nem de framework. Um teste de arquitetura falha se o domínio ou a aplicação importarem EF, Npgsql, ASP.NET, MQTTnet ou Serilog. | `Sombra.ArchitectureTests/ArquiteturaTests.cs` |

## Cybersecurity (`Sombra.Privacidade` + `security/`)

| Pergunta | Resposta curta | Onde mostrar |
|---|---|---|
| Taint estático × dinâmico? | O dinâmico marca o dado na entrada e confere cada saída em tempo de execução (pega o que acontece de verdade, inclusive dado vindo do banco). O estático (Semgrep) lê o código e pega o vazamento antes de rodar. Um cobre o ponto cego do outro. | `Taint/ContextoDeTaint.cs` · `security/semgrep/` |
| Por que validar o dígito do CPF? | Sem isso, qualquer número de 11 dígitos vira CPF e o motor mascara à toa (falso positivo). Com o verificador, 0 falso negativo e 0 falso positivo nos 63 casos. | `Deteccao/Validadores.cs` · `security/privacidade/relatorio-de-acertos.md` |
| Por que a IA nunca recebe PII? | Mandar o dado para um provedor externo seria o próprio vazamento. O prompt leva só metadados (tipo, sink, categoria, regra), e a saída da IA passa pela guarda. | `Traducao/PromptDeRisco.cs` · `GuardaDaTraducao.cs` |
| E se a IA errar? | A guarda confere forma e coerência com o evento; reprovou, vale o template. Revisando as traduções reais, achamos erros que passavam e viraram regra e teste. | §3 do `security/entrega/cyber.pdf` |
| Como o farol é calculado? | Soma dos eventos de 7 dias, ponderada por categoria, severidade e desfecho (vazou pesa 3×), com meia-vida de 72 h; verde < 30 ≤ amarelo < 70 ≤ vermelho. | `Risco/RiscoCidadao.cs` |
| O que diz o art. 11 da LGPD? | Dado de saúde é dado pessoal sensível: só pode ser tratado em hipóteses específicas e exige proteção reforçada. Por isso saúde pesa mais no farol. | — |

## SOA e Web Services (`services/clima-service/`)

| Pergunta | Resposta curta | Onde mostrar |
|---|---|---|
| Por que o Clima é outro serviço? | Clima é outro vocabulário (estação, observação, previsão) e outro ritmo de mudança; o Core fala de casas e alertas. O serviço termina onde a palavra muda de sentido. | `README` do Clima |
| E se o Clima cair? | O `ClimaClient` do Core tem timeout de 3 s, abre o circuito depois de 3 falhas por 30 s e usa o último risco conhecido de até 30 min. | `Sombra.Infraestrutura/Clima/ClimaClient.cs` |
| Por que a V3 é uma migração nova? | Migração aplicada é história: editar a V1 quebraria todo banco que já rodou. A V3 evolui o esquema como se a tabela já tivesse linhas. | `db/migration/V3__…sql` |
| Como o modelo do Python roda no Java? | Exportado em ONNX, que o Java lê com o ONNX Runtime, com as features definidas em `features.json` (F9.1). | `ml/` · spec 02 |

## IA & Machine Learning (`ml/`)

O Matheus explica com os números do relatório dele. Os conceitos que caem: **não embaralhar** (é série temporal: o
treino vem antes da validação no tempo) · **vazamento de futuro** (nenhuma feature pode usar dado depois do momento da
previsão) · **validação cruzada só no treino** (o teste fica intocado até o fim) · **recall acima de precisão** (perder
um dia perigoso custa mais que um alarme a mais) · **o que o SHAP mostrou** (quais features pesaram).

## Physical Computing (`iot/sentinela/`)

| Pergunta | Resposta curta | Onde mostrar |
|---|---|---|
| Por que o índice é calculado na Sentinela? | Para alertar sem internet. A fórmula e os 20 vetores são os mesmos do Core, do Clima e do ML. | `indice_calor.h` · `make test-sentinela` |
| O que é LWT? | A "última vontade": a Sentinela deixa com o broker uma mensagem `offline` retida, que o broker publica sozinho se ela cair. O Core fica sabendo sem precisar perguntar. | `sketch.ino` (`mqtt.connect`) |
| Como o HMAC impede um "estou bem" falso? | Toda mensagem leva o HMAC-SHA256 da forma canônica com o segredo da Sentinela, um `seq` sempre crescente e o horário do NTP. Sem o segredo, não dá para assinar; repetir uma mensagem cai no replay. | `hmac.h` · `make integrar-sentinela` |
| Por que nenhum dado pessoal no broker público? | Só trafegam o `deviceId`, medições e códigos. Quem é a pessoa fica no Core. | `docs/contratos/mqtt.asyncapi.yaml` |

## Mobile Development (`mobile/`)

| Pergunta | Resposta curta | Onde mostrar |
|---|---|---|
| Por que o app só fala com o Core? | Um ponto só de autenticação, de regra e de privacidade; o app nunca vê o broker nem o Clima direto. | `mobile/src/infrastructure/http/cliente-http.ts` |
| O que fica no SQLite? | O que a família precisa ver sem rede (a casa, o último risco, os alertas) e a fila de ações ainda não enviadas. | `README` do app |
| Como a fila offline não perde um "estou bem"? | O toque entra na fila com o horário em que aconteceu e uma `Idempotency-Key`; quando a rede volta, ele é enviado, vale o horário do toque (R14), e o reenvio não repete o efeito. | spec 01 (R14) |
| Acessibilidade para pessoas idosas? | Modo Idoso com uma ação por tela, letra grande, voz, fonte do sistema respeitada e leitor de tela. | `mobile/README.md#acessibilidade` |

## Operating Systems (`infra/`)

| Pergunta | Resposta curta | Onde mostrar |
|---|---|---|
| O que `Restart=always` + `WatchdogSec` garantem? | Se o monitor morrer, o systemd sobe de novo em 5 s; se ele travar sem morrer, para de avisar o watchdog e o systemd o reinicia depois de 30 s. | `infra/systemd/sombra-monitor.service` · `falha-01` |
| Por que um usuário de serviço sem shell? | Se o serviço for invadido, o invasor não ganha um terminal; e o sudo do operador só deixa mexer nos serviços do Sombra. | `infra/steps/40-usuarios.sh` |
| Como provar que o script é idempotente? | Rodar de novo: a segunda execução grava 0 arquivos (cada escrita registra `config.alterada`). | `infra/evidencias/reproducao.txt` |
| Por que conferir o SHA256 do release? | Para instalar exatamente o artefato publicado, e não um corrompido ou trocado no caminho. | `infra/steps/70-aplicacao.sh` |

## Testing, Compliance & QA (`qa/`)

| Pergunta | Resposta curta | Onde mostrar |
|---|---|---|
| Critério de aceite × caso de teste? | O critério diz **o que** precisa ser verdade (Dado/Quando/Então); o caso diz **como verificar**, com entradas, passos e resultado esperado. | `features.yaml` × `casos-de-teste.yaml` |
| Por que negativos e valor-limite? | Os erros moram nas bordas e nas entradas erradas: 38,9 °C não pode abrir alerta de perigo; CPF com dígito errado tem que dar 422. | TC de F03 e F06 |
| O que é caixa-preta? | Testar pelo comportamento visível (entrada e saída), sem olhar o código. | os passos dos casos |
| Como a matriz liga o cidadão ao código? | Feature (a necessidade) → critério → Test Case → o teste automatizado que roda no CI. Um script confere que cada teste citado existe. | `qa/entrega/qa.pdf` · `conferir_especificacao.py` |
