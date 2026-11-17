# Spec 04 — Physical Computing: IoT & IoB · Sentinela (ESP32 no Wokwi)

> **Fase:** F5 (polimento na F11.4, vídeo na F13.6) · **Pasta:** `iot/sentinela/` · **Checkpoints:** `PLANO.md` §8.6 · 🔑 Wokwi do André
> **Entrega:** link público do Wokwi + vídeo ≤ 4 min + `integrantes.txt`
> **Regra do professor:** *"o aluno não vai criar um projeto paralelo apenas para cumprir a atividade"*. A Sentinela **é** a porta física do Sombra.
> **Observabilidade:** [`docs/observabilidade.md`](../observabilidade.md) §4 e §5 (log serial e eventos por MQTT)

## Objetivo
Um dispositivo de baixo custo que fica na casa do idoso, **funciona sem internet**, mede o calor real dentro de casa, conversa com o idoso por luz, som, tela e **dois botões grandes**, e se integra ao Core por MQTT nos dois sentidos.

## Circuito (Wokwi)
| Componente | Função | Pino (sugestão) |
|---|---|---|
| ESP32 DevKit v1 | cérebro + Wi-Fi (`Wokwi-GUEST`) | — |
| DHT22 | temperatura e umidade da casa | GPIO 15 |
| OLED SSD1306 128×64 (I²C) | mensagens em fonte grande | SDA 21 · SCL 22 |
| LED RGB (ou 3 LEDs) | farol verde/amarelo/vermelho | 25 · 26 · 27 |
| Buzzer | aviso sonoro com padrões por nível | 4 |
| Botão **verde** grande | "Estou bem" (check-in) | 18 (pull-up) |
| Botão **vermelho** grande | "Preciso de ajuda" (pressão longa de 2 s, para evitar toque acidental) | 19 (pull-up) |

Custo estimado da lista de materiais: pesquisar e publicar no README (ordem de grandeza de dezenas de reais). Esse é um argumento de escala no pitch.

## Firmware (Arduino C++)
- **Máquina de estados local:** `NORMAL → ATENCAO → PERIGO → AGUARDANDO_CHECKIN → (CONFIRMADO | FAMILIA_AVISADA)`, mais `PRE_ALERTA`, `CHECKIN_DIARIO` e `LEMBRETE_AGUA` (comandos vindos do Core).
- **Índice de calor calculado no próprio dispositivo** (Rothfusz, com os mesmos vetores de teste do Core/Clima). **Funciona offline:** se cair a rede, a Sentinela continua alertando localmente e enfileira os eventos (buffer circular) para enviar quando voltar.
- **MQTT** (PubSubClient + ArduinoJson): `telemetria` a cada 10 s (ou a cada mudança de nível), `evento` nos botões e no boot, assinatura de `comando`, **LWT** em `status`.
- **Segurança:** HMAC-SHA256 (mbedtls) em toda mensagem, `seq` persistente, horário via NTP, sem PII no payload. É o mesmo tema do Cyber, aplicado na borda.
- **Discreta por padrão** (D13, pedido da Dona Cida: *"não pode ficar piscando toda hora"*):
  - em `NORMAL`, a luz fica apagada;
  - a luz acende **fixa**, sem piscar, só em risco, no check-in diário ou no lembrete de água;
  - o som é curto (3 bipes), repete no máximo a cada 10 min e não toca entre 22h e 7h, exceto em `PERIGO`.
- **Sempre explica o próximo passo:** o OLED mostra o que acontece se ela não apertar (*"Se não apertar em 30 min, sua
  filha recebe um lembrete"*). No máximo 3 palavras por linha; botões grandes e com cores fixas.
- Código organizado em abas: `sketch.ino`, `indice_calor.h`, `estado.h`, `mqtt_sombra.h`, `hmac.h`, `display.h`, `config.h` (o segredo **de demo** fica num `secrets.h` de exemplo).

## Checkpoints
A ordem de execução e os 🔎 ficam no `PLANO.md` (fase indicada no topo desta spec).

## Nível HENRY (F11)
Ícones bitmap no OLED (sol, gota, casa, coração) · luz que acende e apaga devagar (sem piscar) · `wokwi-cli` no CI rodando um cenário automatizado (se o token gratuito permitir) · versão PlatformIO com teste nativo do índice de calor.

## Roteiro do vídeo (≤ 4 min)
0:00 o problema e a solução em 20 s → 0:20 o circuito, componente por componente → 1:00 a máquina de estados ao vivo mexendo no DHT22 → 1:50 check-in e pedido de ajuda → 2:30 **integração:** o painel e o app reagindo ao mesmo tempo + comando "família avisada" voltando para o OLED → 3:20 offline-first e HMAC → 3:45 fechamento.

## Defesa
Por que a Sentinela calcula o índice de calor localmente · o que é LWT · como o HMAC impede alguém de falsificar um "estou bem" · por que nenhum dado pessoal passa pelo broker público.
