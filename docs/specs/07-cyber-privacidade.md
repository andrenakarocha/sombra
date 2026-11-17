# Spec 07 — Cybersecurity · Sombra.Privacidade (PII · Taint · IA · Risco Cidadão)

> **Fase:** F2 (dashboard na F7.3, PDF na F12.3, polimento na F11.7) · **Checkpoints:** `PLANO.md` §8.3
> **Código:** `services/core-api/src/Sombra.Privacidade/` · **Docs e auditoria:** `security/`
> **Entrega (Prof. Vitor Lasse):** **um PDF** com a documentação (prints de código e evidências de Parsing PII · Taint Analysis · IA · Dashboard) **e, separado, um ZIP ou link do GitHub** com o código. **Não misturar.** Mais o vídeo do dashboard.
> **Rubrica:** Robustez do motor de PII **3** · Eficácia do Taint Analysis **3** · Inovação com IA na remediação **2** · Dashboard Executivo de Risco Cidadão **2**

## Por que aqui brilha
O Sombra guarda **dados de saúde de idosos** (condições, remédios, Cartão SUS), que a LGPD trata como **dado pessoal sensível** (art. 5º II, art. 11). O módulo protege dados reais do domínio, não um exemplo de brinquedo.

## 1. Motor de parsing de PII (3 pts)
- **Regras como dados** (`pii-regras.json`): é isso que o professor chama de "configurar novas regras no motor de parsing". Cada regra tem `id`, `categoria` (Identificacao · Saude · Contato · Credencial · Localizacao), `severidade`, `chaves` (nomes de campo como `documento`, `cpf`, `laudo`, `senha`), `padrao` (regex), `validador`, `mascara` e `baseLegal` (artigo da LGPD).
- **Detectores** (polimorfismo, `IDetectorDePii`): `CpfDetector` (regex + **dígito verificador**, para não marcar qualquer número de 11 dígitos), `CnsDetector` (Cartão SUS, 15 dígitos + validação oficial), `RgDetector`, `TelefoneDetector`, `EmailDetector`, `CepDetector`, `SaudeDetector` (códigos **CID-10** + dicionário PT-BR de condições, exames e remédios, além de termos como "laudo" e "diagnóstico"), `CredencialDetector` (campos `senha`/`token`/`secret`, padrão JWT, strings de alta entropia).
- **`PiiScanningMiddleware`**: lê o JSON de **toda** requisição (com buffering), percorre a árvore (`JsonNode`), classifica cada campo por **chave** e por **conteúdo** e registra os valores no **`ContextoDeTaint`** da requisição (marca o dado como PII). Gera o evento `PiiDetectada` só com metadados (categoria, campo, regra), **nunca com o valor**. PII em campo livre (ex.: CPF dentro de "observações") é mascarada automaticamente ou recusada com 422, conforme a política.
- **Mascaramento** (`MascaradorDePii`): CPF `***.456.***-00` (o exemplo do professor), CNS `*** **** **** 1234`, telefone `(11) *****-4321`, e-mail `l***@d***.com`, saúde `[DADO DE SAÚDE PROTEGIDO]`, senha `[REMOVIDO]`.
- **No banco:** value converters do EF com **AES-256-GCM** para CPF, CNS, perfil de saúde e telefones, mais um **índice cego HMAC** para buscar CPF sem descriptografar. A chave vem do ambiente e nunca do código.
- **Testes:** tabela com positivos, negativos e casos difíceis (CPF com dígito errado, CPF sem pontuação, CID no meio de uma frase, JSON aninhado e arrays, unicode, payload enorme), com meta de **0 falso negativo** nos casos da tabela.

## 2. Taint Analysis (3 pts): fonte → sink, com bloqueio
**Taint em tempo de execução**
- **Fontes:** corpo, query e headers da requisição (marcados pelo middleware) + campos sensíveis lidos do banco (descriptografados).
- **Taint como tipo:** `Sensivel<T>` tem `ToString()` que devolve `[PII:CPF]`, e o serializador **recusa** serializar sem `Revelar(finalidade)` explícito em endpoint autorizado. Vazar por descuido vira impossível ou ruidoso.
- **Guardas de sink** (a "camada de validação antes das saídas" que o professor descreveu):
  1. **Log:** um `ILoggerProvider` decorador verifica cada evento de log contra o `ContextoDeTaint` e os detectores, então **bloqueia e redige** antes de escrever, e emite `VazamentoBloqueado{sink: Log}`. Cobre o `logger.info(dados_de_saude)` do enunciado.
  2. **Resposta HTTP:** `TaintResponseGuardMiddleware` verifica o corpo da resposta. Em resposta **não autenticada** ou **de erro** (stack trace, ProblemDetails, exceção do EF que costuma carregar valores), troca por um erro seguro com `correlationId` e emite o evento. PII só sai em endpoint marcado com `[PermitePii(Categoria, Papel)]`.
  3. **Saída externa:** `DelegatingHandler` no HttpClient (Clima, **LLM**) e guarda no publicador MQTT. **Nem a IA vê o dado:** o prompt nunca contém PII.
  4. **Exceções:** o tratador global sanitiza as mensagens.

**Taint estático (complemento):** regras **Semgrep em modo taint** (`security/semgrep/sombra-taint.yaml`) com fontes (DTOs com `Cpf`/`Cns`/`Laudo`/`Senha`, `[FromBody]`), sinks (`ILogger.Log*`, `Console.Write*`, `Problem(detail:…)`) e sanitizers (`MascaradorDePii.Mascarar`, `Sensivel<T>`). Roda no CI com SARIF no GitHub Code Scanning. Um arquivo **canário** em `security/semgrep/canario/` prova que a regra pega o vazamento.

**Modo inseguro de demonstração:** `Privacidade__Aplicacao=Desligada`, **só** no ambiente `Demo`, para gerar o **antes × depois** da auditoria.

## 3. IA na remediação (2 pts)
- `TradutorDeRiscoWorker` consome os eventos de segurança (outbox) e monta um **prompt dinâmico sem PII** (categoria, sink, rota sem valores, severidade, contagem, se foi bloqueado). Gera **três traduções**:
  - **Cidadão** (60+ anos, 2 frases, sem jargão, tom calmo, o que aconteceu e o que fazer): *"Tentaram acessar o laudo de saúde da Dona Lurdes, mas o Sombra bloqueou para proteger a privacidade dela."*
  - **Gestão** (impacto e risco LGPD em 1 linha).
  - **Time técnico:** a **sugestão de correção** (qual sink, qual padrão usar, ex.: "use `Sensivel<T>`" ou "mova para `MascaradorDePii`"). Esta é a parte de **remediação**.
- **Guardas na saída da IA:** a resposta passa pelos **mesmos detectores de PII**, por uma lista de jargões proibidos e por limite de tamanho. Se reprovar, cai num template.
- **Resiliência:** cache por assinatura do evento, timeout, fallback em templates (`fonte: "template"`). Provedor conforme a decisão D1 (Gemini Flash por padrão).

## 4. Dashboard Executivo de Risco Cidadão (2 pts)
- **Cálculo do farol** (`RiscoCidadao`): pontuação dos eventos recentes ponderada por **categoria** (saúde > identificação > contato), **desfecho** (bloqueado × vazado) e **severidade**, com decaimento no tempo. Verde < 30 ≤ amarelo < 70 ≤ vermelho (calibrar).
- **Painel web** (spec 06): farol global e por categoria, feed com as três traduções, série temporal por sink, top regras disparadas, evidência técnica sob demanda.
- **App** (spec 05): o cidadão vê o farol **da sua casa** e a mensagem traduzida. É a visão "do usuário" que o professor descreveu.
- Atualização ao vivo via SignalR (`RiscoCidadaoAtualizado`).

## 5. Auditoria do protótipo (evidência do taint em ação)
`security/auditoria/rodar-auditoria.sh` executa os cenários com o módulo **desligado** e depois **ligado**, salva requisição e resposta, trechos de log e eventos, e gera `security/auditoria/relatorio.md` + JSON:

| # | Cenário | Esperado com o módulo |
|---|---|---|
| A1 | `POST` de morador com CPF dentro de "observações" | mascarado antes de gravar e logar |
| A2 | `POST` que provoca exceção com laudo no payload | resposta de erro segura, sem o laudo |
| A3 | `GET` não autenticado do perfil de saúde | 401 sem nenhum dado, evento registrado |
| A4 | endpoint de debug (só `Demo`) que loga o Cartão SUS | log redigido e evento `VazamentoBloqueado{Log}` |
| A5 | tentativa de mandar PII no prompt da LLM | bloqueada no handler de saída |
| A6 | tentativa de publicar PII no MQTT | bloqueada no publicador |
| A7 | senha na query string | removida de log e trace |
| A8 | telemetria MQTT com HMAC falso | descartada, evento de segurança |

## Checkpoints
A ordem de execução e os 🔎 ficam no `PLANO.md` (fase indicada no topo desta spec).

## Nível HENRY (F11)
Mapa de calor regra × sink · relatório de conformidade LGPD gerado (arts. 6º, 11, 46 e 48) · métrica "tempo até o bloqueio" · comparação lado a lado do log sem e com o módulo, no próprio dashboard.

## Defesa
Diferença entre taint estático e dinâmico (e por que fizemos os dois) · por que validar o dígito do CPF · por que a IA nunca recebe PII · como o farol é calculado · o que diz o art. 11 da LGPD.
