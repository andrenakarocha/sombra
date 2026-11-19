# Vídeo de Cybersecurity: o Dashboard de Risco Cidadão (sem limite, mire em 4 min)

> Entrega: o PDF [`security/entrega/cyber.pdf`](../../security/entrega/cyber.pdf), **separado** do código (o ZIP de
> `make entrega-cyber` ou o link do GitHub), mais este vídeo. O PDF já cobre os quatro pilares com código e evidência; o
> vídeo mostra **o dashboard vivo** e o antes × depois.

## Preparar

1. `make up`, painel em <http://localhost:5173/risco-cidadao> logado como operador.
2. Um terminal ao lado, na raiz do repositório.

## Linha do tempo

| Tempo | Tela | Fazer | Fala |
|---|---|---|---|
| 0:00 | painel, Risco Cidadão | — | "O Sombra guarda dados de saúde, CPF e Cartão SUS de pessoas idosas, que a LGPD trata como **dado pessoal sensível**. O módulo Sombra.Privacidade garante que esses dados não saiam por descuido, e este dashboard mostra o risco para quem decide." |
| 0:30 | painel | apontar o farol e as categorias | "O **farol** soma os eventos dos últimos 7 dias, ponderados pela categoria — saúde pesa mais que contato —, pela severidade e pelo desfecho: um vazamento pesa três vezes um bloqueio. A pontuação decai com meia-vida de 72 horas." |
| 1:00 | terminal | `curl -i localhost:5080/api/v1/domicilios/0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10/moradores` | "Vou tentar ler os moradores de uma casa **sem login**. O Core responde 401, sem nenhum dado..." |
| 1:20 | painel | o evento aparece no feed sem recarregar | "...e o evento aparece **ao vivo** no feed, pelo SignalR. A **IA** traduz o evento em três textos: para o cidadão, em duas frases calmas; para a gestão, com o artigo da LGPD; e para o time técnico, a correção. O prompt leva **só metadados**: a IA nunca vê o dado." |
| 2:10 | painel | abrir "Para a gestão" e "Para o time técnico" e a evidência técnica | "A resposta da IA também passa por uma guarda: os mesmos detectores de PII, jargão proibido para o cidadão, tamanho e ortografia. Se reprovar, entra um template." |
| 2:40 | `security/auditoria/relatorio.md` | rolar a tabela A1–A8 | "O **antes e depois**: oito cenários de ataque rodam com o módulo desligado e ligado. Desligado, os oito vazam: o CPF no texto livre, o laudo numa exceção, o Cartão SUS no log, dado pessoal no prompt da IA e no MQTT. Ligado, os oito são bloqueados." |
| 3:20 | terminal | `make semgrep` | "E o **taint estático**: o Semgrep em modo taint pega os vazamentos do arquivo canário e sai limpo no código do Core." |
| 3:45 | PDF | — | "O PDF tem os trechos de código e as evidências de cada pilar. Integrantes: André, João e Matheus." |

> O id da casa no `curl` é o da Dona Lurdes no seed (fictícia). Se o feed não mexer, confira o canto do painel: o
> indicador "Ao vivo" precisa estar verde.
