#!/usr/bin/env node
// Gera narração com o Gemini TTS (camada gratuita da API). Lê a chave de .env (GEMINI_API_KEY).
//   node tools/voz_gemini.mjs <voz> <pasta> [ids...] [--estilo="..."] [--modelo=gemini-3.8-flash-tts]
//   node tools/voz_gemini.mjs Charon audio/amostras-gemini s01       # amostra de uma cena
//   node tools/voz_gemini.mjs Charon audio/voz                       # todas as cenas viram a narração do vídeo
// Vozes: Charon, Algieba, Orus, Sadaltager, Kore, Sulafat, Aoede… (30 no total, ver docs de speech-generation)
// Em audio/voz o silêncio das pontas é cortado, igual ao ./gravar.mjs.
import { readFileSync, writeFileSync, mkdirSync, existsSync, rmSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import vm from "node:vm";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const flags = Object.fromEntries(process.argv.slice(2).filter((a) => a.startsWith("--")).map((a) => { const [k, ...v] = a.slice(2).split("="); return [k, v.join("=")]; }));
const [voz, pasta, ...ids] = process.argv.slice(2).filter((a) => !a.startsWith("--"));
if (!voz || !pasta) { console.error("uso: node tools/voz_gemini.mjs <voz> <pasta> [ids...] [--estilo=...] [--modelo=...]"); process.exit(2); }

const envFile = join(ROOT, ".env");
const chave = process.env.GEMINI_API_KEY || (existsSync(envFile) ? (readFileSync(envFile, "utf8").match(/^GEMINI_API_KEY=["']?([^"'\r\n]+)/m) || [])[1] : null);
if (!chave) { console.error("GEMINI_API_KEY não encontrada (.env ou ambiente)."); process.exit(1); }

const MODELO = flags.modelo || "gemini-3.8-flash-tts";
const ESTILO = flags.estilo || "Brazilian Portuguese narrator for a product pitch video. Natural, warm and confident, like a person explaining something they built. Medium pace, clear diction, no radio-announcer tone.";
const CORTE = "silenceremove=start_periods=1:start_silence=0.06:start_threshold=-40dB:detection=peak";

const ctx = {};
vm.runInNewContext(readFileSync(join(ROOT, "shared/roteiro.js"), "utf8"), ctx);
const cenas = ctx.ROTEIRO.filter((s) => !ids.length || ids.includes(s.id));
const amostra = /amostra/.test(pasta);
mkdirSync(join(ROOT, pasta), { recursive: true });
mkdirSync(join(ROOT, "build"), { recursive: true });
const espera = (ms) => new Promise((ok) => setTimeout(ok, ms));

async function gerar(cena) {
  const corpo = {
    model: MODELO,
    input: [{ type: "user_input", content: [{ type: "text", text: cena.texto, annotations: [{ type: "speech_metadata", style: ESTILO }] }] }],
    response_format: { type: "audio" },
    generation_config: { speech_config: [{ voice: voz }] },
  };
  for (let tentativa = 1; ; tentativa++) {
    const r = await fetch("https://generativelanguage.googleapis.com/v1beta/interactions", {
      method: "POST", headers: { "x-goog-api-key": chave, "Content-Type": "application/json" }, body: JSON.stringify(corpo),
    });
    const j = await r.json().catch(() => ({}));
    const erro = Array.isArray(j) ? j[0] && j[0].error : j.error;
    if (r.ok) {
      const audios = (j.steps || []).filter((s) => s.type === "model_output").flatMap((s) => s.content || []).filter((c) => c.type === "audio");
      const dado = audios.length ? audios[audios.length - 1].data : j.output_audio && j.output_audio.data;
      if (dado) return Buffer.from(dado, "base64");
      throw new Error(`resposta sem áudio · chaves: ${Object.keys(j).join(", ")}`);
    }
    const msg = (erro && erro.message) || JSON.stringify(j).slice(0, 300);
    // 503 = pico de demanda, 429 por minuto = aguardar; cota diária esgotada não adianta insistir
    const temporario = r.status === 503 || (r.status === 429 && !/per day|daily|PerDay/i.test(msg));
    if (!temporario || tentativa >= 6) throw new Error(`HTTP ${r.status} · ${msg}`);
    const pausa = r.status === 429 ? 35000 : 6000 * tentativa;
    console.log(`  ${cena.id}: HTTP ${r.status}, nova tentativa em ${pausa / 1000} s`);
    await espera(pausa);
  }
}

let falhas = 0;
for (const cena of cenas) {
  try {
    const wav = await gerar(cena);
    const nome = amostra ? `${voz}-${cena.id}.wav` : `${cena.id}.wav`;
    const destino = join(ROOT, pasta, nome);
    if (amostra) writeFileSync(destino, wav);
    else {
      const bruto = join(ROOT, `build/bruto-${cena.id}.wav`);
      writeFileSync(bruto, wav);
      execFileSync("ffmpeg", ["-v", "error", "-y", "-i", bruto, "-af", `${CORTE},areverse,${CORTE},areverse`, "-ac", "1", "-ar", "48000", destino]);
      rmSync(bruto);
    }
    console.log(cena.id, "ok", join(pasta, nome));
  } catch (e) {
    falhas++;
    console.error(`${cena.id} FALHOU · ${e.message}`);
    if (/per day|daily|PerDay|402/i.test(e.message)) break;
  }
}
process.exit(falhas ? 1 : 0);
