# Spec 10 — Vídeos das disciplinas + Pitch (prêmio melhor GS)

> **Fase:** F13 🔑 (conta do YouTube) · **Pasta:** `pitch/` + `pitch/tecnicos/` · **Checkpoints:** `PLANO.md` §8.14
> **Base:** pipeline do HENRY em `~/University/year3/challenge/sprint4-video-pitch/` (HyperFrames + GSAP, `build.sh`, sync de tempos, voz Gemini TTS, mix, Playwright para gravar telas)

## Os seis vídeos

| Vídeo | Limite | Onde vai | Formato |
|---|---|---|---|
| **Pitch** | **4:00** | tarefa de **QA** (YouTube) · concorre a melhor GS | produção completa nível HENRY |
| ML | 5:00 | tarefa de IA/ML (YouTube não listado) | kit técnico |
| Mobile | 3:00 | README do Mobile | kit técnico |
| OS | 7:00 | tarefa de OS | kit técnico |
| IoT | 4:00 | tarefa de Physical | kit técnico |
| Cyber (dashboard) | — | tarefa de Cyber + prints no PDF | kit técnico |

## Kit técnico (um template para os cinco vídeos de disciplina)
Uma composição HyperFrames reutilizável: abertura de 4 s (marca Sombra + disciplina + integrantes), **capítulos** com lower-thirds, **gravação de tela embutida** (Playwright para painel e app web, captura do emulador para o app, OBS/asciinema para o terminal da VM, captura do Wokwi), **legendas** sincronizadas, narração (voz Gemini TTS ou voz própria) e encerramento com os links. O conteúdo é técnico; o acabamento é de produto.

## Pitch (4:00), em cinco atos
| Ato | Tempo | Conteúdo |
|---|---|---|
| 1 · Gancho | 0:00–0:30 | dados reais e citados das ondas de calor recentes em SP + a casa que vira forno. A primeira imagem é a rua da entrevista |
| 2 · Quem sofre | 0:30–1:00 | a **Dona Cida** (entrevista real, primeiro nome autorizado): a casa que vira forno às 15h, a tontura na cozinha, a vizinha que percebeu, a filha que trabalha fora e não quer "alerta toda vez que ela esquecer". Por que a pessoa idosa não percebe o calor (sede, remédios) |
| 3 · O Sombra funcionando | 1:00–2:20 | **demo real do roteiro**: Sentinela no Wokwi → alerta → "estou bem" → caminho sem resposta → família liga. Telas reais do app e do painel |
| 4 · Por dentro, tudo conversa | 2:20–3:20 | arquitetura animada (ESP32 → MQTT → Core → Clima/ML → app/painel) · "o modelo previu X de cada 10 dias perigosos" · farol de privacidade · VM se recuperando sozinha |
| 5 · Impacto | 3:20–4:00 | indicador da Ficha, ODS 3/10/11/13, ecossistema (UBS, ACS, CRAS, Defesa Civil), modelo de escala (Sentinela de baixo custo via programa municipal), equipe e chamada final: *"Ninguém enfrenta o calor sozinho."* |

## Checkpoints
A ordem de execução e os 🔎 ficam no `PLANO.md` (fase indicada no topo desta spec).

## Checklist de qualidade (nível HENRY)
- [ ] duração dentro do limite em **todos** os vídeos
- [ ] nomes dos integrantes na abertura ou no encerramento
- [ ] todo número com fonte (rodapé)
- [ ] fotos do território só com a placa desfocada (D14)
- [ ] demo **real** (nada de tela falsa): o que aparece roda no repositório
- [ ] áudio normalizado (−14 LUFS) e legendas revisadas
- [ ] 1080p · 30 fps
