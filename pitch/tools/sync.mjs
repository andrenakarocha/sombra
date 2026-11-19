#!/usr/bin/env node
// Sincroniza tempo: lê shared/roteiro.js, mede os áudios disponíveis e gera
// shared/timing.js, ROTEIRO.md e os data-duration dos atos.
// Prioridade de áudio por cena: audio/voz/<id>.* (gravação do grupo) > audio/guia/<id>.wav (voz sintética).
import { readFileSync, writeFileSync, existsSync } from "node:fs";
import { execFileSync, spawnSync } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import vm from "node:vm";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const ctx = {};
vm.runInNewContext(readFileSync(join(ROOT, "shared/roteiro.js"), "utf8"), ctx);
const { ROTEIRO, RITMO } = ctx;

const EXTS = ["wav", "mp3", "m4a", "ogg", "flac"];
function findAudio(id) {
  for (const dir of ["audio/voz", "audio/guia"]) {
    for (const ext of EXTS) {
      const rel = `${dir}/${id}.${ext}`;
      if (existsSync(join(ROOT, rel))) return rel;
    }
  }
  return null;
}
function probe(rel) {
  const out = execFileSync("ffprobe", ["-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", join(ROOT, rel)]);
  return parseFloat(out.toString().trim());
}
const words = (t) => t.trim().split(/\s+/).length;
const ceil1 = (n) => Math.ceil(n * 10 - 1e-9) / 10;
const round2 = (n) => Math.round(n * 100) / 100;

// ---------- alinhamento fala → palavras, medido no próprio áudio (sem transcrição) ----------
// Detecta as pausas reais, casa cada frase do roteiro com um trecho de fala e distribui as
// palavras da frase só sobre o tempo falado. É o que mantém a tela em sincronia com a voz.
function pausas(rel, dur) {
  const r = spawnSync("ffmpeg", ["-hide_banner", "-i", join(ROOT, rel), "-af", "silencedetect=noise=-38dB:d=0.13", "-f", "null", "-"], { encoding: "utf8" });
  const sil = []; let ini = null;
  for (const ln of r.stderr.split("\n")) {
    let m;
    if ((m = ln.match(/silence_start: (-?[\d.]+)/))) ini = Math.max(0, parseFloat(m[1]));
    else if ((m = ln.match(/silence_end: ([\d.]+)/)) && ini !== null) { sil.push([ini, parseFloat(m[1])]); ini = null; }
  }
  if (ini !== null) sil.push([ini, dur]);
  return sil;
}
const peso = (w) => w.replace(/[^\p{L}\p{N}]/gu, "").length + 1.2;
// Reparte os trechos de fala [ini, fim) entre unidades consecutivas (frases ou orações) de pesos dados.
// Minimiza o erro de duração e prefere pausas longas como fronteira. Devolve os cortes, ou null se faltar pausa.
function casar(pesos, ini, fim, fala, acum) {
  const F = pesos.length, V = fim - ini;
  if (F === 1) return [ini, fim];
  if (V < F) return null;
  const total = pesos.reduce((a, b) => a + b, 0), falado = acum[fim] - acum[ini];
  const INF = 1e9;
  const dp = Array.from({ length: F + 1 }, () => new Array(V + 1).fill(INF));
  const de = Array.from({ length: F + 1 }, () => new Array(V + 1).fill(0));
  dp[0][0] = 0;
  for (let i = 1; i <= F; i++) for (let j = i; j <= V - (F - i); j++) for (let k = i - 1; k < j; k++) {
    if (dp[i - 1][k] >= INF) continue;
    const real = acum[ini + j] - acum[ini + k];
    const gap = j < V ? fala[ini + j][0] - fala[ini + j - 1][1] : 0;
    const custo = dp[i - 1][k] + (real - falado * pesos[i - 1] / total) ** 2 - 2.5 * Math.min(gap, 0.9);
    if (custo < dp[i][j]) { dp[i][j] = custo; de[i][j] = k; }
  }
  const corte = new Array(F + 1); corte[F] = V;
  for (let i = F; i > 0; i--) corte[i - 1] = de[i][corte[i]];
  return corte.map((c) => ini + c);
}
function alinhar(rel, texto, dur) {
  const fala = []; let c = 0;
  for (const [a, b] of pausas(rel, dur)) { if (a - c > 0.03) fala.push([c, a]); c = Math.max(c, b); }
  if (dur - c > 0.03) fala.push([c, dur]);
  if (!fala.length) return null;
  const pal = texto.trim().split(/\s+/);
  const durFala = fala.map(([a, b]) => b - a);
  const acum = [0]; durFala.forEach((d, i) => acum.push(acum[i] + d));
  const grupos = (idx, fim) => { const out = []; let cur = []; idx.forEach((i) => { cur.push(i); if (fim.test(pal[i])) { out.push(cur); cur = []; } }); if (cur.length) out.push(cur); return out; };
  const soma = (idx) => idx.reduce((n, i) => n + peso(pal[i]), 0);
  // instante a `frac` do tempo falado entre os trechos [ini, fim)
  const pos = (ini, fim, frac) => {
    let alvo = (acum[fim] - acum[ini]) * frac;
    for (let v = ini; v < fim; v++) {
      if (alvo <= durFala[v] || v === fim - 1) return fala[v][0] + Math.min(alvo, durFala[v]);
      alvo -= durFala[v];
    }
    return fala[fim - 1][1];
  };
  const tempos = new Array(pal.length).fill(0);
  const espalhar = (idx, ini, fim) => { const total = soma(idx); let ac = 0; idx.forEach((w) => { tempos[w] = pos(ini, fim, ac / total); ac += peso(pal[w]); }); };
  const todas = pal.map((_, i) => i);
  const frases = grupos(todas, /[.?!:]$/);
  const cf = casar(frases.map(soma), 0, fala.length, fala, acum);
  if (!cf) { espalhar(todas, 0, fala.length); return tempos.map(round2); }
  frases.forEach((f, i) => {
    const oracoes = grupos(f, /[,;]$/);
    const co = oracoes.length > 1 ? casar(oracoes.map(soma), cf[i], cf[i + 1], fala, acum) : null;
    if (co) oracoes.forEach((o, k) => espalhar(o, co[k], co[k + 1])); else espalhar(f, cf[i], cf[i + 1]);
  });
  return tempos.map(round2);
}

const acts = {};
const scenes = {};
let abs = 0;
for (const s of ROTEIRO) {
  const audio = findAudio(s.id);
  const voDur = audio ? probe(audio) : words(s.texto) / RITMO.wps;
  const dur = ceil1(Math.max(s.min || 0, RITMO.lead + voDur + (s.tail ?? RITMO.tail)));
  const act = (acts[s.ato] ||= { dur: 0, offset: abs, scenes: [] });
  scenes[s.id] = {
    ato: s.ato, sec: s.sec, quem: s.quem, texto: s.texto,
    t0: round2(act.dur), dur, abs: round2(abs),
    vo0: RITMO.lead, voDur: round2(voDur), audio, fonte: audio ? (audio.startsWith("audio/voz") ? "voz" : "guia") : "estimado",
    palavras: audio ? alinhar(audio, s.texto, voDur) : null,
  };
  act.scenes.push(s.id);
  act.dur = round2(act.dur + dur);
  abs = round2(abs + dur);
}
const timing = { total: abs, acts, scenes };

writeFileSync(join(ROOT, "shared/timing.js"),
  "// Gerado por tools/sync.mjs. Não editar à mão.\nwindow.TIMING = " + JSON.stringify(timing, null, 1) + ";\n");
writeFileSync(join(ROOT, "build/timing.json"), JSON.stringify(timing, null, 1));

// data-duration de cada ato
for (const [ato, a] of Object.entries(acts)) {
  const file = join(ROOT, `${ato}.html`);
  if (!existsSync(file)) continue;
  const html = readFileSync(file, "utf8");
  const re = new RegExp(`(data-composition-id="${ato}"[^>]*?data-duration=")[^"]*(")`);
  if (!re.test(html)) { console.warn(`! ${ato}.html: data-duration não encontrado`); continue; }
  writeFileSync(file, html.replace(re, `$1${a.dur}$2`));
}

// ROTEIRO.md
const tc = (t) => `${Math.floor(t / 60)}:${String(Math.floor(t % 60)).padStart(2, "0")}`;
const nomes = { ato1: "Ato 1 · O problema", ato2: "Ato 2 · A solução", ato3: "Ato 3 · Por dentro", ato4: "Ato 4 · As entregas", ato5: "Ato 5 · Fechamento" };
let md = `# Roteiro de narração — Sombra\n\n`;
md += `> Gerado por \`tools/sync.mjs\` a partir de \`shared/roteiro.js\`. Edite o texto lá e rode \`./build.sh sync\`.\n\n`;
md += `**Duração total:** ${tc(abs)} (${abs.toFixed(1)} s) · limite do pitch: 4:00\n\n`;
md += `## Como gravar\n\n`;
md += `1. Grave **um arquivo por cena** com o nome da cena: \`audio/voz/s01.wav\`, \`audio/voz/s02.wav\`… (aceita wav, mp3, m4a, ogg, flac).\n`;
md += `2. Use a coluna **Janela** como alvo: é o tempo de fala que cabe na cena sem alterar o vídeo. Se passar um pouco, a cena estica sozinha.\n`;
md += `3. Rode \`./build.sh\`. O script mede os áudios, reajusta os tempos, renderiza os atos e entrega \`renders/sombra-pitch.mp4\`.\n`;
md += `4. Cenas sem gravação usam a voz-guia sintética de \`audio/guia/\` (só para referência de ritmo).\n\n`;
for (const [ato, a] of Object.entries(acts)) {
  md += `## ${nomes[ato] || ato} — ${tc(a.offset)} a ${tc(a.offset + a.dur)}\n\n`;
  md += `| Cena | Início | Duração | Janela | Narrador | Texto |\n|---|---|---|---|---|---|\n`;
  for (const id of a.scenes) {
    const s = scenes[id];
    md += `| \`${id}\` | ${tc(s.abs)} | ${s.dur.toFixed(1)} s | ${s.voDur.toFixed(1)} s (${s.fonte}) | ${s.quem} | ${s.texto} |\n`;
  }
  md += `\n`;
}
writeFileSync(join(ROOT, "ROTEIRO.md"), md);

const nWords = ROTEIRO.reduce((n, s) => n + words(s.texto), 0);
console.log(`total ${tc(abs)} (${abs.toFixed(1)} s) · ${ROTEIRO.length} cenas · ${nWords} palavras`);
for (const [ato, a] of Object.entries(acts)) console.log(`  ${ato}  offset ${tc(a.offset)}  dur ${a.dur.toFixed(1)} s  [${a.scenes.join(" ")}]`);
if (abs > 240) { console.error("! ACIMA DE 4:00"); process.exitCode = 2; }
