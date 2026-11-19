# Vídeos gravados por script (F13.4)

Cada vídeo de disciplina é um roteiro em código (`roteiros/<vídeo>.mjs`), com a fala e o que acontece na tela em cada
cena. O gravador narra as falas com voz sintética pt-BR (Kokoro, local, sem chave), executa as cenas gravando o painel e
o palco (terminal com a saída **real** dos comandos, slides e imagens) com o Playwright em 1920 × 1080, e monta tudo com o
`ffmpeg`, com a fala e a legenda de cada cena.

```bash
make up                                   # o ambiente no ar (o painel e o Core)
node tools/videos/gravar.mjs cyber        # → tools/videos/saida/cyber/cyber.mp4 (e cyber-previa.mp4, leve)
node tools/videos/gravar.mjs cyber --montar   # só remonta a última gravação (mudou a legenda, por exemplo)
```

- `narrar.py` (rodado pelo `uv`): um `.wav` por cena; refaz só a cena cuja fala mudou. A tabela `PRONUNCIA` troca
  siglas pelo jeito de falar (a legenda mantém a grafia certa). O modelo (~340 MB) é baixado na primeira vez.
- `palco.html`: os slides com a direção de arte do Sombra, o terminal e as imagens que rolam (PDF, relatório).
- A saída (`saida/`) fica fora do Git; os vídeos vão para o YouTube como não listados (F13.7).
- A duração final é conferida com o `ffprobe` contra o limite do roteiro; o gravador sai com erro se passar.
- Carência de 1 h por casa (D13): um roteiro que dispara alerta usa uma casa que não teve alerta na última hora.
