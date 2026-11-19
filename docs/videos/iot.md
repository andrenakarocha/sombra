# Vídeo de Physical Computing (até 4 min)

> Entrega: link público do Wokwi + este vídeo + `integrantes.txt`. A spec pede que a Sentinela seja **integração real**,
> não um projeto paralelo: o vídeo mostra o painel (e o app) reagindo ao que acontece no Wokwi.

## Preparar

1. Projeto no Wokwi com os arquivos de [`iot/sentinela/`](../../iot/sentinela/) (o `README` de lá tem o passo a passo).
   Rode uma vez e confira: o QR aparece por 8 s, depois "Tudo bem" com a temperatura.
2. Na raiz: `make demo-wokwi`. O Core passa a ouvir o `broker.hivemq.com` com as chaves de teste do `secrets.h`.
   Confira com `make integrar-sentinela-wokwi`: os ✔ dizem que o caminho está aberto.
3. Painel aberto em **Visão geral** (ou Alertas) ao lado do Wokwi; se for mostrar o app, ele logado como
   `ana.filha@demo.sombra`.
4. **Carência:** se você ensaiou um alerta na última hora, o Core não abre outro na mesma casa (D13). Grave o trecho do
   alerta numa tomada só.

## Linha do tempo

| Tempo | Tela | Fazer | Fala |
|---|---|---|---|
| 0:00 | Wokwi | — | "A Sentinela é a parte física do Sombra: um aparelho simples na casa de uma pessoa idosa que mora sozinha. Ela mede o calor **de dentro da casa**, calcula o índice de calor ali mesmo e fala com o resto do sistema." |
| 0:20 | Wokwi, zoom no circuito | apontar cada peça | "ESP32 com Wi-Fi; **DHT22** para temperatura e umidade; uma tela OLED; um LED RGB com **luz fixa**, que nunca pisca; um buzzer; e dois botões grandes: **verde** para 'estou bem' e **vermelho** para pedir ajuda, com pressão longa de 2 segundos para não disparar sem querer." |
| 1:00 | Wokwi | clicar no DHT22 e subir para 30 °C, depois 33 °C, depois 35 °C (umidade 50%) | "Com 30 graus, 'Quente', beba água. Com 33, cautela extrema: luz laranja, e ela pede o 'estou bem'. Com 35 graus e 50% de umidade, a sensação passa de 40: **perigo**, luz vermelha fixa, três bipes curtos, e a tela explica o próximo passo — em 15 minutos a família é avisada. A tabela de níveis é a mesma do Core, do serviço de clima e do modelo de ML, com os mesmos 20 vetores de teste." |
| 1:50 | Wokwi + painel lado a lado | apertar **V** (verde) | "Ela aperta o verde: a Sentinela manda o check-in, e o painel do operador marca o alerta como resolvido." (num ensaio anterior, ou agora se a carência permitir: segure **A** 2 s e mostre o pedido de ajuda) |
| 2:30 | painel + Wokwi | nova subida para 35 °C sem apertar nada; mostrar o alerta no painel e o comando chegando na tela | "**Integração:** a mesma leitura aparece no painel ao vivo, o Core abre o alerta e manda um comando assinado de volta. Quando o prazo vence, o Core avisa a família, e a Sentinela mostra 'Sua família está sendo avisada'." |
| 3:20 | README ou monitor serial | mostrar o log serial (`telemetria.enviada`, `comando.aceito`) | "Ela é **offline-first**: sem internet, continua alertando com luz, som e tela, e guarda os eventos numa fila. E toda mensagem é assinada com **HMAC-SHA256**, com contador anti-replay e horário do NTP: uma mensagem adulterada no broker público é recusada pelo Core — está provado nos testes de integração. Nenhum dado pessoal passa pelo broker." |
| 3:45 | README | — | "Circuito, código, testes e o link estão no README da pasta `iot/sentinela`. Integrantes: André, João e Matheus." |

## Se algo não reagir no painel

- `make integrar-sentinela-wokwi` de novo: ele diz se o Core está no broker público e se aceita a assinatura.
- No monitor serial do Wokwi: `mqtt.conectado` precisa aparecer; `evento.sem_relogio` quer dizer que o NTP ainda não
  respondeu (espere alguns segundos).
- Depois de gravar, `make up` volta o Core para o broker local e as chaves do `.env`.
