#!/usr/bin/env node
// Monta a trilha de áudio do vídeo inteiro: cada narração entra no instante da sua cena.
// Usa audio/voz/<id>.* quando existir, senão audio/guia/<id>.wav. Trilha opcional: audio/trilha.*
import { readFileSync, existsSync } from "node:fs";
import { execFileSync, spawnSync } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const timing = JSON.parse(readFileSync(join(ROOT, "build/timing.json"), "utf8"));
const out = join(ROOT, "build/narracao.wav");

const inputs = []; const filters = []; const labels = [];
for (const [id, s] of Object.entries(timing.scenes)) {
  if (!s.audio) continue;
  const i = inputs.length / 2;
  inputs.push("-i", join(ROOT, s.audio));
  const ms = Math.round((s.abs + s.vo0) * 1000);
  // voz do grupo é normalizada em loudness; a guia já sai nivelada
  const norm = s.fonte === "voz" ? "loudnorm=I=-16:TP=-1.5:LRA=11," : "";
  filters.push(`[${i}:a]${norm}aresample=48000,aformat=channel_layouts=stereo,adelay=${ms}:all=1[a${i}]`);
  labels.push(`[a${i}]`);
}
if (!labels.length) { console.error("nenhum áudio de narração encontrado em audio/voz ou audio/guia"); process.exit(1); }

let mixIn = labels.join(""); let n = labels.length;
const trilha = ["mp3", "wav", "m4a", "ogg", "flac"].map((e) => `audio/trilha.${e}`).find((p) => existsSync(join(ROOT, p)));
if (trilha) {
  // Nível: mede o loudness da faixa e leva para o alvo (padrão -32 LUFS, uns 16 dB abaixo da voz).
  const arq = join(ROOT, trilha);
  const alvo = parseFloat(process.env.TRILHA_LUFS || "-32");
  const med = spawnSync("ffmpeg", ["-hide_banner", "-i", arq, "-af", "loudnorm=print_format=json", "-f", "null", "-"], { encoding: "utf8" }).stderr;
  const lufs = parseFloat((med.match(/"input_i"\s*:\s*"(-?[\d.]+)"/) || [])[1]);
  const ganho = (Number.isFinite(lufs) ? alvo - lufs : -20).toFixed(1);
  // Faixa mais curta que o vídeo: repete com crossfade de 4 s em vez de um corte seco.
  const L = parseFloat(execFileSync("ffprobe", ["-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", arq]).toString());
  const X = 4; const copias = Math.max(1, Math.ceil((timing.total - X) / (L - X)));
  const base = inputs.length / 2;
  for (let c = 0; c < copias; c++) inputs.push("-i", arq);
  let fim = `[${base}:a]`;
  for (let c = 1; c < copias; c++) { filters.push(`${fim}[${base + c}:a]acrossfade=d=${X}:c1=tri:c2=tri[bx${c}]`); fim = `[bx${c}]`; }
  const fadeOut = Math.max(0, timing.total - 3).toFixed(2);
  filters.push(`${fim}aresample=48000,aformat=channel_layouts=stereo,volume=${ganho}dB,afade=t=in:d=2,afade=t=out:st=${fadeOut}:d=3[bgm]`);
  mixIn += "[bgm]"; n += 1;
}
filters.push(`${mixIn}amix=inputs=${n}:normalize=0:duration=longest,apad,atrim=0:${timing.total.toFixed(2)}[out]`);

execFileSync("ffmpeg", ["-v", "error", "-y", ...inputs, "-filter_complex", filters.join(";"), "-map", "[out]", "-ar", "48000", "-ac", "2", out], { stdio: "inherit" });
const voz = Object.values(timing.scenes).filter((s) => s.fonte === "voz").length;
const guia = Object.values(timing.scenes).filter((s) => s.fonte === "guia").length;
console.log(`narração: ${voz} cena(s) com voz do grupo, ${guia} com voz-guia${trilha ? " · trilha: " + trilha : ""}`);
