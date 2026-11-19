# Pitch do Sombra (Prêmio melhor GS)

Vídeo de até 4 minutos com a solução e as entregas que mais somaram, enviado na disciplina de Testing. Feito em código
com [HyperFrames](https://hyperframes.heygen.com) (HTML + GSAP renderizado para MP4), no mesmo pipeline do pitch HENRY
do Challenge, com a identidade do Sombra ([`design.md`](design.md)).

**Saída:** `renders/sombra-pitch.mp4` (1920 × 1080, 30 fps, 3:01).

```bash
./build.sh                  # sincroniza os tempos, renderiza os 5 atos, junta e mixa o áudio
QUALITY=draft ./build.sh    # rascunho rápido
LEGENDAS=0 ./build.sh       # sem legendas
./build.sh ato3             # renderiza só um ato e remonta
```

## Narração

A voz **Charon** do Gemini TTS, uma cena por arquivo em `audio/voz/` (FLAC no repositório). Para refazer uma cena, com
`GEMINI_API_KEY` no `pitch/.env` (fora do Git): `node tools/voz_gemini.mjs Charon audio/voz s05`.

O texto, a ordem e a duração mínima de cada cena ficam em [`shared/roteiro.js`](shared/roteiro.js); o `tools/sync.mjs`
mede a fala e ancora as animações nas palavras (`s.w("palavra")`), então trocar a voz não quebra a sincronia.

| Ato | Cenas | Conteúdo |
|---|---|---|
| 1 | s01–s03 | A onda de calor de novembro de 2023 (a série real da A701), a entrevista e o problema |
| 2 | s04–s07 | A Sentinela (o clipe real do Wokwi), a previsão, os degraus e o app |
| 3 | s08–s10 | A arquitetura, o motor de privacidade e a VM |
| 4 | s11 | As entregas em números |
| 5 | s12 | Assinatura |

## De onde vêm os números e as imagens

- A curva do Ato 1 é o índice de calor hora a hora da estação A701 de 5 a 19/11/2023, calculado a partir de
  `ml/dados/brutos/A701_2023.csv` (pico de 35,47 °C em 15/11, às 15h).
- 74 de 75 dias: `ml/RELATORIO_ML.md`. Os 8 ataques: `security/auditoria/relatorio.md`. 63/63: o relatório de acertos
  do motor de PII. 363 e 144 testes: os READMEs do Core e do Clima. 50 casos e 7 testes de falha: `qa/` e `infra/`.
- O clipe do Wokwi, os prints do app e o painel são capturas reais do projeto rodando. As falas da entrevista estão em
  [`docs/entrevista.md`](../docs/entrevista.md), sem nomes; a Dona Lurdes e a filha do app são fictícias.
- Música: "Deliberate Thought", Kevin MacLeod (incompetech.com), CC BY 4.0.
