#!/usr/bin/env bash
# Monta o vídeo pitch do Sombra (pipeline do pitch HENRY).
#   ./build.sh            sincroniza tempos, renderiza os 5 atos, junta e mixa o áudio
#   ./build.sh sync       só recalcula tempos e o ROTEIRO.md
#   ./build.sh ato3       renderiza só um ato (e remonta o final)
#   ./build.sh join       só junta os atos já renderizados e remixa o áudio
# Variáveis: QUALITY=draft|looks|delivery (padrão looks) · LEGENDAS=0 para sair sem legendas
set -euo pipefail
cd "$(dirname "$0")"

HF=(npx --yes hyperframes@0.8.143)
QUALITY="${QUALITY:-looks}"
LEGENDAS="${LEGENDAS:-1}"
ACTS=(ato1 ato2 ato3 ato4 ato5)
OUT="renders/sombra-pitch.mp4"
mkdir -p build renders audio/voz audio/guia

sync() { node tools/sync.mjs; }

render() {
  local vars='{"legendas":true}'
  [[ "$LEGENDAS" == "0" ]] && vars='{"legendas":false}'
  for a in "$@"; do
    echo "▶ renderizando $a ($QUALITY)"
    "${HF[@]}" render -c "$a.html" -o "renders/$a.mp4" -q "$QUALITY" --variables "$vars" --quiet 2>&1 \
      | grep -iE 'error|fail|não encontrada|sem alvo' || true
    [[ -s "renders/$a.mp4" ]] || { echo "falha ao renderizar $a" >&2; exit 1; }
  done
}

join() {
  : > build/concat.txt
  for a in "${ACTS[@]}"; do
    [[ -s "renders/$a.mp4" ]] || { echo "falta renders/$a.mp4 (rode ./build.sh)" >&2; exit 1; }
    echo "file '../renders/$a.mp4'" >> build/concat.txt
  done
  ffmpeg -v error -y -f concat -safe 0 -i build/concat.txt -c copy -an build/video.mp4
  node tools/mix.mjs
  ffmpeg -v error -y -i build/video.mp4 -i build/narracao.wav -map 0:v -map 1:a -c:v copy -c:a aac -b:a 192k -movflags +faststart "$OUT"
  local dur; dur=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$OUT")
  printf '✔ %s · %s s · %s\n' "$OUT" "$dur" "$(du -h "$OUT" | cut -f1)"
}

case "${1:-all}" in
  sync) sync ;;
  join) sync; join ;;
  all)  sync; render "${ACTS[@]}"; join ;;
  ato*) sync; render "$@"; join ;;
  *) echo "uso: ./build.sh [sync|join|all|atoN...]" >&2; exit 2 ;;
esac
