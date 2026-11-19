# Roteiro de narração — HENRY · Ford ZeroTouch

> Gerado por `tools/sync.mjs` a partir de `shared/roteiro.js`. Edite o texto lá e rode `./build.sh sync`.

**Duração total:** 3:01 (181.3 s) · limite do pitch: 4:00

## Como gravar

1. Grave **um arquivo por cena** com o nome da cena: `audio/voz/s01.wav`, `audio/voz/s02.wav`… (aceita wav, mp3, m4a, ogg, flac).
2. Use a coluna **Janela** como alvo: é o tempo de fala que cabe na cena sem alterar o vídeo. Se passar um pouco, a cena estica sozinha.
3. Rode `./build.sh`. O script mede os áudios, reajusta os tempos, renderiza os atos e entrega `renders/sombra-pitch.mp4`.
4. Cenas sem gravação usam a voz-guia sintética de `audio/guia/` (só para referência de ritmo).

## Ato 1 · O problema — 0:00 a 0:45

| Cena | Início | Duração | Janela | Narrador | Texto |
|---|---|---|---|---|---|
| `s01` | 0:00 | 14.0 s | 12.3 s (voz) | undefined | Novembro de 2023. São Paulo atravessa uma onda de calor. Numa casa que pega sol a tarde inteira, a Dona Lurdes, de setenta e oito anos, mora sozinha. E esquece de beber água. |
| `s02` | 0:14 | 18.5 s | 16.6 s (voz) | undefined | Fomos conversar com quem vive isso. Uma senhora que mora sozinha contou: lá pelas três, quatro horas, parece que a casa virou um forno. Um dia, ela passou mal na cozinha, e foi a vizinha quem percebeu. A filha pediu uma coisa só: não quero receber alerta à toa. |
| `s03` | 0:32 | 12.5 s | 9.4 s (voz) | undefined | Em ondas de calor, idosos que moram sozinhos não percebem o perigo dentro de casa. E a família só fica sabendo quando já é tarde. Por isso criamos o Sombra. |

## Ato 2 · A solução — 0:45 a 1:43

| Cena | Início | Duração | Janela | Narrador | Texto |
|---|---|---|---|---|---|
| `s04` | 0:45 | 15.5 s | 13.7 s (voz) | undefined | Na casa, a Sentinela: um ESP32 que mede o calor de dentro e calcula o índice de calor ali mesmo. A luz é fixa e nunca pisca. E são dois botões grandes: estou bem, e preciso de ajuda. |
| `s05` | 1:00 | 14.4 s | 12.7 s (voz) | undefined | Antes de o calor chegar, um modelo treinado com dezesseis anos de dados reais do INMET prevê as próximas vinte e quatro horas. No teste, avisou setenta e quatro de setenta e cinco dias de calor forte. |
| `s06` | 1:14 | 15.0 s | 13.0 s (voz) | undefined | Quando o perigo chega, ela só precisa apertar o verde. Se não aperta, o Sombra sobe um degrau por vez: um lembrete para ela, depois a família, depois quem está perto. Alarme de verdade, só em perigo. |
| `s07` | 1:29 | 13.2 s | 11.6 s (voz) | undefined | A filha acompanha a casa pelo app, ao vivo: o calor de agora, o prazo para responder e um botão para ligar. E, no celular da Dona Lurdes, o Modo Idoso: letra gigante e um botão só. |

## Ato 3 · Por dentro — 1:43 a 2:30

| Cena | Início | Duração | Janela | Narrador | Texto |
|---|---|---|---|---|---|
| `s08` | 1:43 | 17.5 s | 15.7 s (voz) | undefined | Por dentro, um Core em C sharp orquestra os alertas e a rede de cuidado. Um serviço de clima em Java, com Spring, é dono dos dados do INMET. A Sentinela fala MQTT, com cada mensagem assinada. E o app e o painel recebem tudo ao vivo. |
| `s09` | 2:00 | 19.0 s | 17.1 s (voz) | undefined | Os dados de saúde passam por um motor de privacidade: ele acha CPF, Cartão SUS e laudo, segue o dado até a saída e bloqueia o vazamento. Os oito ataques que vazavam com ele desligado são todos barrados. E a IA explica cada evento ao cidadão, sem nunca ver o dado. |
| `s10` | 2:19 | 11.1 s | 9.4 s (voz) | undefined | E tudo sobe numa máquina Linux vazia, só com scripts: rede, DNS, serviços e um monitor que se recupera sozinho das falhas. |

## Ato 4 · As entregas — 2:30 a 2:48

| Cena | Início | Duração | Janela | Narrador | Texto |
|---|---|---|---|---|---|
| `s11` | 2:30 | 17.9 s | 16.3 s (voz) | undefined | Em números: mais de quinhentos testes automatizados, sessenta e três casos de dado pessoal sem nenhum falso negativo, cinquenta casos de teste, quarenta e nove deles automatizados, e sete testes de falha na máquina, cada um com a sua prova. |

## Ato 5 · Fechamento — 2:48 a 3:01

| Cena | Início | Duração | Janela | Narrador | Texto |
|---|---|---|---|---|---|
| `s12` | 2:48 | 12.7 s | 8.0 s (voz) | undefined | Sombra. Porque, numa onda de calor, ninguém deveria enfrentar o calor sozinho. André, João e Matheus. |

