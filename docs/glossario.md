# Glossário (linguagem ubíqua)

Os termos do domínio ficam em **português**, no código também (D11). Os sufixos técnicos ficam em inglês
(`DomicilioRepository`, `AlertasController`). Quando um termo muda de sentido de um serviço para outro, é ali que um
serviço termina e o outro começa: veja **Leitura × Observação**.

## Casa e pessoas (Core)

| Termo | No código | Significado |
|---|---|---|
| Domicílio | `Domicilio` | A casa monitorada. Raiz do agregado: moradores, Sentinelas, rede de cuidado e rotina passam por ela |
| Morador | `Morador` | A pessoa idosa que mora no domicílio |
| Perfil de saúde | `PerfilDeSaude` | Condições e fatores de risco do morador. **Dado pessoal sensível** (LGPD, art. 5º, II): sempre cifrado e mascarado |
| Fator de risco | `FatorDeRisco` | O que deixa a pessoa mais vulnerável ao calor: idade avançada, cardiopatia, uso de diurético. Reduz o limite pessoal |
| Limites térmicos | `LimitesTermicos` | Os limiares de índice de calor daquela pessoa: os da NWS ajustados pelos fatores de risco, com piso de 26,67 °C |
| Rede de cuidado | `RedeDeCuidado` | Os contatos de cuidado em ordem de escalonamento. Precisa de pelo menos um contato |
| Contato de cuidado | `ContatoDeCuidado` | Uma pessoa da rede, com papel `Familiar` (cuida à distância) ou `Vizinho` (está perto e pode "ir até lá") |
| Rotina | `Rotina` | O check-in diário e os lembretes, nos horários escolhidos pela pessoa |

## Dispositivo e alertas (Core e Sentinela)

| Termo | No código | Significado |
|---|---|---|
| Sentinela | `Sentinela` | O dispositivo ESP32 da casa: mede temperatura e umidade, tem luz, som, tela e dois botões |
| Leitura | `Leitura` | Temperatura e umidade **da Sentinela**, dentro da casa, num instante |
| Índice de calor | `IndiceDeCalor` | A temperatura aparente, calculada pela equação da NWS ([contrato](contratos/niveis-de-risco.md)) |
| Nível de risco | `NivelDeRisco` | `NORMAL`, `ATENCAO`, `CAUTELA_EXTREMA`, `PERIGO` ou `PERIGO_EXTREMO` |
| Alerta de calor | `AlertaDeCalor` | Um episódio de risco numa casa, do início ao fim. Abstrato: é **preventivo** ou **de Sentinela** |
| Alerta preventivo | `AlertaPreventivo` | Nasce da **previsão** de perigo nas próximas 24 h, antes de a casa esquentar |
| Alerta de Sentinela | `AlertaDeSentinela` | Nasce de uma **leitura** da casa acima do limite |
| Check-in ("estou bem") | `CheckIn` | A confirmação de que a pessoa está bem: botão verde, app ou um cuidador que foi ver |
| Check-in diário | `CheckInDiario` | O "estou bem" de rotina, uma vez por dia, opcional. Esquecer gera só um lembrete leve |
| Pedido de ajuda | `PedidoDeAjuda` | O botão vermelho (pressão longa). Vai direto ao degrau 3 |
| Escalonamento | `Escalonamento` | A subida pelos degraus quando o "estou bem" não vem no prazo |
| Degrau | `DegrauDeEscalonamento` | 0 · Sentinela lembra · 1 · lembrete leve à família · 2 · quem está perto · 3 · alerta para toda a rede |
| Lembrete leve | `LembreteLeve` | Aviso sem alarme para a família: "ela ainda não confirmou, quer ligar?" |
| Quem está perto | `QuemEstaPerto` | O degrau em que o contato `Vizinho` recebe "pode dar uma olhada?" |
| Lembrete de água | `LembreteDeAgua` | Aviso na Sentinela no pico da tarde, em dias quentes |

## Clima (Clima Service)

| Termo | No código | Significado |
|---|---|---|
| Região climática | `RegiaoClimatica` | Área atendida por uma estação de referência (ex.: `SP-MIRANTE`) |
| Estação | `Estacao` | Estação meteorológica do INMET (ex.: A701, São Paulo – Mirante de Santana) |
| Observação | `Observacao` | Medição horária **da estação**, ao ar livre. O código `-9999` do INMET não é observação |
| Previsão de calor | `PrevisaoDeCalor` | A probabilidade de o índice de calor chegar a `PERIGO` nas próximas 24 h, dada pelo modelo de ML |
| Ponto de resfriamento | `PontoDeResfriamento` | Lugar público com sombra, água ou climatização (biblioteca, CEU, UBS), com horário e acessibilidade |

> **Leitura × Observação:** as duas medem temperatura e umidade, mas a leitura é de dentro da casa (dado do Core) e a
> observação é da rua, numa estação oficial (dado do Clima). A palavra muda de sentido, então são dois serviços.

## Privacidade (Sombra.Privacidade)

| Termo | No código | Significado |
|---|---|---|
| Dado pessoal (PII) | — | Informação que identifica uma pessoa: CPF, RG, Cartão SUS, telefone, e-mail, endereço |
| Dado sensível | — | Dado pessoal de saúde, entre outros (LGPD, art. 5º, II e art. 11) |
| Regra de PII | `RegraDePii` | Uma regra do motor de parsing: categoria, chaves, padrão, validador, máscara e base legal |
| Contexto de taint | `ContextoDeTaint` | Os valores marcados como PII durante uma requisição |
| Fonte (source) | — | Por onde o dado sensível entra: requisição, banco descriptografado |
| Saída (sink) | — | Por onde ele poderia vazar: log, resposta HTTP, MQTT, chamada externa, IA |
| Evento de segurança | `EventoDeSeguranca` | Registro de PII detectada ou de vazamento bloqueado, só com metadados |
| Risco Cidadão | `RiscoCidadao` | O farol de privacidade (verde, amarelo, vermelho) com a mensagem traduzida pela IA |

## Projeto

| Termo | Significado |
|---|---|
| Persona | "Dona Lurdes, 78 anos": pessoa fictícia dos dados de demonstração, inspirada na entrevista com a Dona Cida |
| Roteiro da demonstração | "A onda de calor da Dona Lurdes" (PLANO §3.4), o mesmo em todos os vídeos e no `make demo` |
