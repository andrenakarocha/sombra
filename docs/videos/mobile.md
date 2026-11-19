# Vídeo de Mobile Development (até 3 min)

> Entrega: link do GitHub (pasta `mobile/`) com o README e este vídeo. A spec pede o app **falando com o Core de
> verdade**: o alerta do vídeo nasce numa Sentinela (o simulador ou o Wokwi) e chega ao celular ao vivo.

## Preparar

1. `make up` na raiz.
2. O app no celular (Expo Go) ou no emulador, apontando para o Core: veja a tabela de URLs no
   [`mobile/README.md`](../../mobile/README.md) (`EXPO_PUBLIC_API_URL=http://10.0.2.2:5080 npx expo start` no emulador).
   Entre como `ana.filha@demo.sombra` (a filha da Dona Lurdes), senha `Demo__Senha` do `.env`, digitada fora da tela.
3. Um terminal pronto com `make sim CENARIO=sem-resposta` (a casa chega a PERIGO e ninguém responde) e outro com
   `make sim CENARIO=onda-de-calor` (ela aperta "estou bem"). **Carência:** um alerta por casa por hora; ensaie antes da
   última hora.
4. Grave a tela do celular (o gravador do Android, ou o `scrcpy --record video.mp4` no computador).

## Linha do tempo

| Tempo | Tela | Fazer | Fala |
|---|---|---|---|
| 0:00 | Início | — | "O app do Sombra é para a família de uma pessoa idosa que mora sozinha. A Ana cuida da mãe, a Dona Lurdes, de longe." |
| 0:20 | Início → Casa | abrir a casa | "Na Casa ela vê o calor de dentro da casa agora, o risco de hoje e de amanhã e os pontos de resfriamento mais perto, que vêm do serviço de clima." |
| 0:50 | terminal + app | `make sim CENARIO=sem-resposta` | "Agora a Sentinela da casa mede perigo. **O alerta chega ao vivo**, sem recarregar: o Core avisa o app pelo SignalR e por notificação." |
| 1:20 | app, Modo Idoso | abrir o Modo Idoso | "No **Modo Idoso**, a tela é para a própria Dona Lurdes: letra grande, um botão só, e a voz lê o recado." |
| 1:50 | app, alerta | mostrar os degraus e tocar em **Ligar** | "Se ninguém responde no prazo, o alerta **sobe de degrau** e a família é avisada. A Ana liga para a mãe direto do app, ou marca que está a caminho." |
| 2:20 | app em modo avião | ativar o modo avião e tocar em "estou bem" | "**Sem internet**, o app continua: os dados ficam no SQLite e o 'estou bem' entra numa fila com o horário do toque. Quando a rede volta, ele é enviado e vale o horário em que ela tocou." |
| 2:40 | configurações do Android | fonte grande e TalkBack ligados | "E a **acessibilidade**: o app respeita a fonte grande do sistema e funciona com o leitor de tela." |
| 2:55 | README | — | "O código e os prints estão na pasta `mobile`. Integrantes: André, João e Matheus." |
