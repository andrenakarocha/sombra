# Auditoria do protótipo: antes × depois do Sombra.Privacidade

> Gerado por `security/auditoria/rodar-auditoria.sh` (teste `OitoCenariosVazamDesligadoESaoBloqueadosLigado`, spec 07 §5).
> "Antes" é o módulo desligado (`Privacidade:Aplicacao=Desligada`, aceito só no ambiente `Demo`): os guardas detectam e
> registram, mas deixam passar. "Depois" é o módulo ligado. Todos os dados são sintéticos (CPF e Cartão SUS gerados por semente).

**8 de 8 cenários vazam antes · 8 de 8 são bloqueados depois**

| # | Cenário | Esperado com o módulo | Antes (desligado) | Depois (ligado) |
|---|---|---|---|---|
| A1 | POST de morador com CPF dentro do texto livre de saúde | mascarado antes de gravar e logar | 201 · ❌ vazou | 201 · ✅ bloqueado |
| A2 | POST que provoca exceção com o laudo no payload | resposta de erro segura, sem o laudo | 422 · ❌ vazou | 422 · ✅ bloqueado |
| A3 | GET não autenticado do perfil de saúde | 401 sem nenhum dado, evento registrado | 200 · ❌ vazou | 401 · ✅ bloqueado |
| A4 | Endpoint de depuração que loga o Cartão SUS | log redigido e VazamentoBloqueado{Log} | 204 · ❌ vazou | 204 · ✅ bloqueado |
| A5 | PII no prompt da LLM | bloqueada no handler de saída | 200 · ❌ vazou | 422 · ✅ bloqueado |
| A6 | PII publicada no MQTT | bloqueada no publicador | 202 · ❌ vazou | 422 · ✅ bloqueado |
| A7 | Senha na query string | removida do log | 200 · ❌ vazou | 200 · ✅ bloqueado |
| A8 | Telemetria MQTT com HMAC falso | descartada, evento de segurança | 202 · ❌ vazou | 401 · ✅ bloqueado |

## A1 · POST de morador com CPF dentro do texto livre de saúde

Requisição: `POST /api/v1/domicilios/{Lurdes}/moradores {"nome":"Iracema Duarte","idade":86,"cpf":"30336123779","cartaoSus":"730336123724804","perfilDeSaude":"Hipertens\u00E3o. Em caso de urg\u00EAncia, CPF do filho 60336813201","fatoresDeRisco":[]}`

**Antes (desligado)** · HTTP 201 · vazou

- Evidência: texto livre gravado no banco (decifrado): Hipertensão. Em caso de urgência, CPF do filho 60336813201
- Resposta: `{"id":"01a1f058-4dc0-727c-83f0-768d62b2b2cf","nome":"Iracema Duarte","idade":86,"telefone":null,"cpf":"***.361.***-79","cartaoSus":"*** **** **** 4804","perfilDeSaude":"[DADO DE SAÚDE PROTEGIDO]","fatoresDeRisco":["IDADE_AVANCADA"]}`
- Log: `privacidade.vazamento_permitido: Vazamento NÃO bloqueado (modo inseguro da auditoria) no sink Entrada em api/v1/domicilios/{id:guid}/moradores: Identificacao (regra cpf)`
- Evento de segurança: VAZAMENTO_PERMITIDO · Entrada · Identificacao · regra cpf · vazou

**Depois (ligado)** · HTTP 201 · não vazou

- Evidência: texto livre gravado no banco (decifrado): Hipertensão. Em caso de urgência, CPF do filho ***.368.***-01
- Resposta: `{"id":"01a1f058-5140-79a5-951e-c359bc5f57a9","nome":"Iracema Duarte","idade":86,"telefone":null,"cpf":"***.361.***-79","cartaoSus":"*** **** **** 4804","perfilDeSaude":"[DADO DE SAÚDE PROTEGIDO]","fatoresDeRisco":["IDADE_AVANCADA"]}`
- Log: `privacidade.vazamento_bloqueado: Vazamento bloqueado no sink Entrada em api/v1/domicilios/{id:guid}/moradores: Identificacao (regra cpf, pelo conteudo)`
- Evento de segurança: PII_FORA_DO_LUGAR · Entrada · Identificacao · regra cpf · bloqueado

## A2 · POST que provoca exceção com o laudo no payload

Requisição: `POST /api/v1/auditoria/excecao {"laudo":"Paciente com diabetes, CID E11.9, toma metformina"}`

**Antes (desligado)** · HTTP 422 · vazou

- Evidência: o laudo aparece na mensagem de erro?
- Resposta: `{"type":"https://github.com/andrenakarocha/sombra/blob/main/docs/contratos/core-api.md#erros","title":"Regra de negócio violada","status":422,"detail":"Laudo recusado: Paciente com diabetes, CID E11.9, toma metformina","regra":"A2","correlationId":"auditoria-A2-desligado"}`
- Log: `privacidade.vazamento_permitido: Vazamento NÃO bloqueado (modo inseguro da auditoria) no sink Excecao em api/v1/auditoria/excecao: Saude (regra saude)`
- Log: `privacidade.vazamento_permitido: Vazamento NÃO bloqueado (modo inseguro da auditoria) no sink Excecao em api/v1/auditoria/excecao: Saude (regra saude)`
- Log: `privacidade.vazamento_permitido: Vazamento NÃO bloqueado (modo inseguro da auditoria) no sink RespostaHttp em (sem rota): Saude (regra saude)`
- Log: `privacidade.vazamento_permitido: Vazamento NÃO bloqueado (modo inseguro da auditoria) no sink RespostaHttp em (sem rota): Saude (regra saude)`
- Evento de segurança: VAZAMENTO_PERMITIDO · Excecao · Saude · regra saude · vazou
- Evento de segurança: VAZAMENTO_PERMITIDO · RespostaHttp · Saude · regra saude · vazou

**Depois (ligado)** · HTTP 422 · não vazou

- Evidência: o laudo aparece na mensagem de erro?
- Resposta: `{"type":"https://github.com/andrenakarocha/sombra/blob/main/docs/contratos/core-api.md#erros","title":"Regra de negócio violada","status":422,"detail":"[PII:saude] recusado: [PII:saude]","regra":"A2","correlationId":"auditoria-A2-ligado"}`
- Log: `privacidade.vazamento_bloqueado: Vazamento bloqueado no sink Excecao em api/v1/auditoria/excecao: Saude (regra saude, pelo taint)`
- Log: `privacidade.vazamento_bloqueado: Vazamento bloqueado no sink Excecao em api/v1/auditoria/excecao: Saude (regra saude, pelo conteudo)`
- Evento de segurança: VAZAMENTO_BLOQUEADO · Excecao · Saude · regra saude · bloqueado

## A3 · GET não autenticado do perfil de saúde

Requisição: `GET /api/v1/auditoria/perfil/{Lurdes}/{moradora} (sem login)`

**Antes (desligado)** · HTTP 200 · vazou

- Evidência: o CPF da Dona Lurdes aparece na resposta?
- Resposta: `{"nome":"Lurdes A.","cpf":"34875763689","observacoesDeSaude":null}`
- Log: `privacidade.vazamento_permitido: Vazamento NÃO bloqueado (modo inseguro da auditoria) no sink RespostaHttp em api/v1/auditoria/perfil/{domicilioId:guid}/{moradorId:guid}: Identificacao (regra cpf)`
- Evento de segurança: VAZAMENTO_PERMITIDO · RespostaHttp · Identificacao · regra cpf · vazou

**Depois (ligado)** · HTTP 401 · não vazou

- Evidência: o CPF da Dona Lurdes aparece na resposta?
- Resposta: `{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.2","title":"Resposta bloqueada","status":401,"detail":"A resposta levava dado pessoal e foi bloqueada para proteger o cidadão. Informe o correlationId ao suporte.","correlationId":"auditoria-A3-ligado"}`
- Log: `privacidade.vazamento_bloqueado: Vazamento bloqueado no sink RespostaHttp em api/v1/auditoria/perfil/{domicilioId:guid}/{moradorId:guid}: Identificacao (regra cpf, pelo conteudo)`
- Evento de segurança: VAZAMENTO_BLOQUEADO · RespostaHttp · Identificacao · regra cpf · bloqueado

## A4 · Endpoint de depuração que loga o Cartão SUS

Requisição: `POST /api/v1/auditoria/log {"cartaoSus":"760336813207526"}`

**Antes (desligado)** · HTTP 204 · vazou

- Evidência: linha de log: Depuração: cartão 760336813207526
- Resposta: ``
- Log: `auditoria.log_de_depuracao: Depuração: cartão 760336813207526`
- Log: `privacidade.vazamento_permitido: Vazamento NÃO bloqueado (modo inseguro da auditoria) no sink Log em auditoria.log_de_depuracao: Saude (regra cns)`
- Evento de segurança: VAZAMENTO_PERMITIDO · Log · Saude · regra cns · vazou

**Depois (ligado)** · HTTP 204 · não vazou

- Evidência: linha de log: Depuração: cartão [PII:cns]
- Resposta: ``
- Log: `auditoria.log_de_depuracao: Depuração: cartão [PII:cns]`
- Log: `privacidade.vazamento_bloqueado: Vazamento bloqueado no sink Log em auditoria.log_de_depuracao: Saude (regra cns, pelo taint)`
- Evento de segurança: VAZAMENTO_BLOQUEADO · Log · Saude · regra cns · bloqueado

## A5 · PII no prompt da LLM

Requisição: `POST /api/v1/auditoria/llm {"cpf":"25151596640"}`

**Antes (desligado)** · HTTP 200 · vazou

- Evidência: o provedor de LLM (eco) recebeu o CPF no prompt?
- Resposta: `{"recebido":{"prompt":"Explique o risco do cidadão de CPF 25151596640."}}`
- Log: `privacidade.vazamento_permitido: Vazamento NÃO bloqueado (modo inseguro da auditoria) no sink SaidaExterna em localhost: Identificacao (regra cpf)`
- Log: `privacidade.vazamento_permitido: Vazamento NÃO bloqueado (modo inseguro da auditoria) no sink RespostaHttp em api/v1/auditoria/llm: Identificacao (regra cpf)`
- Evento de segurança: VAZAMENTO_PERMITIDO · RespostaHttp · Identificacao · regra cpf · vazou
- Evento de segurança: VAZAMENTO_PERMITIDO · SaidaExterna · Identificacao · regra cpf · vazou

**Depois (ligado)** · HTTP 422 · não vazou

- Evidência: o provedor de LLM (eco) recebeu o CPF no prompt?
- Resposta: `{"type":"https://github.com/andrenakarocha/sombra/blob/main/docs/contratos/core-api.md#erros","title":"Saída bloqueada","status":422,"detail":"O pedido levaria dado pessoal para fora do Sombra e foi bloqueado.","correlationId":"auditoria-A5-ligado"}`
- Log: `privacidade.saida_externa_bloqueada: Saída SaidaExterna para localhost bloqueada: Identificacao (regra cpf)`
- Evento de segurança: SAIDA_EXTERNA_BLOQUEADA · SaidaExterna · Identificacao · regra cpf · bloqueado

## A6 · PII publicada no MQTT

Requisição: `POST /api/v1/auditoria/mqtt {"telefone":"(11) 97654-3210"}`

**Antes (desligado)** · HTTP 202 · vazou

- Evidência: mensagem no broker: {"aviso":"Ligar para (11) 97654-3210"}
- Resposta: ``
- Log: `privacidade.vazamento_permitido: Vazamento NÃO bloqueado (modo inseguro da auditoria) no sink Mqtt em sombra-teste/d95e86d169b34f56a557a2bb1ab22d66/auditoria/eco: Contato (regra telefone)`
- Evento de segurança: VAZAMENTO_PERMITIDO · Mqtt · Contato · regra telefone · vazou

**Depois (ligado)** · HTTP 422 · não vazou

- Evidência: mensagem no broker: (nada publicado)
- Resposta: `{"type":"https://github.com/andrenakarocha/sombra/blob/main/docs/contratos/core-api.md#erros","title":"Saída bloqueada","status":422,"detail":"O pedido levaria dado pessoal para fora do Sombra e foi bloqueado.","correlationId":"auditoria-A6-ligado"}`
- Log: `privacidade.saida_externa_bloqueada: Saída Mqtt para sombra-teste/e5751a5a33804a83acc5dda947b32036/auditoria/eco bloqueada: Contato (regra telefone)`
- Evento de segurança: SAIDA_EXTERNA_BLOQUEADA · Mqtt · Contato · regra telefone · bloqueado

## A7 · Senha na query string

Requisição: `GET /api/v1/auditoria/login?usuario=ana&senha=…`

**Antes (desligado)** · HTTP 200 · vazou

- Evidência: linha de log: Login recebido em /api/v1/auditoria/login?usuario=ana&senha=‹senha sintética›
- Resposta: `{"recebido":true}`
- Log: `auditoria.login_pela_url: Login recebido em /api/v1/auditoria/login?usuario=ana&senha=‹senha sintética›`
- Log: `privacidade.vazamento_permitido: Vazamento NÃO bloqueado (modo inseguro da auditoria) no sink Log em auditoria.login_pela_url: Credencial (regra credencial)`
- Evento de segurança: VAZAMENTO_PERMITIDO · Log · Credencial · regra credencial · vazou

**Depois (ligado)** · HTTP 200 · não vazou

- Evidência: linha de log: Login recebido em /api/v1/auditoria/login?usuario=ana&senha=[PII:credencial]
- Resposta: `{"recebido":true}`
- Log: `auditoria.login_pela_url: Login recebido em /api/v1/auditoria/login?usuario=ana&senha=[PII:credencial]`
- Log: `privacidade.vazamento_bloqueado: Vazamento bloqueado no sink Log em auditoria.login_pela_url: Credencial (regra credencial, pelo taint)`
- Evento de segurança: VAZAMENTO_BLOQUEADO · Log · Credencial · regra credencial · bloqueado

## A8 · Telemetria MQTT com HMAC falso

Requisição: `POST /api/v1/sentinelas/snt-7f3a9c/leituras (temperatura alterada depois de assinar)`

**Antes (desligado)** · HTTP 202 · vazou

- Evidência: leituras da casa: 0 → 1
- Resposta: ``
- Log: `leitura.registrada: Leitura da Sentinela snt-7f3a9c na casa 0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10: nível Perigo`
- Log: `alerta.aberto: Alerta 01a1f058-4edd-7c18-8841-adc7dd78f602 (DE_SENTINELA) aberto na casa 0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10 com nível Perigo`
- Evento de segurança: MQTT_ASSINATURA_INVALIDA · MQTT · Credencial · regra hmac · vazou

**Depois (ligado)** · HTTP 401 · não vazou

- Evidência: leituras da casa: 0 → 0
- Resposta: `{"type":"https://github.com/andrenakarocha/sombra/blob/main/docs/contratos/core-api.md#erros","title":"Telemetria recusada","status":401,"detail":"Assinatura da Sentinela inválida.","traceId":"00-42f006eed56c91355cf73c2754669f33-8afc42504361d72e-00","correlationId":"auditoria-A8-ligado"}`
- Log: `mqtt.assinatura_invalida: Assinatura inválida na mensagem telemetria da Sentinela snt-7f3a9c (seq 1): descartada`
- Log: `seguranca.evento_registrado: Evento de segurança MQTT_ASSINATURA_INVALIDA da origem snt-7f3a9c`
- Evento de segurança: MQTT_ASSINATURA_INVALIDA · MQTT · Credencial · regra hmac · bloqueado
