#!/usr/bin/env node
/**
 * Grava um vídeo de disciplina inteiro por script (F13.4): narra as falas com voz sintética (narrar.py), executa as
 * cenas do roteiro gravando o painel e o palco (terminal com a saída real dos comandos, slides e imagens) com o
 * Playwright em 1920 × 1080, e monta com o ffmpeg: cada cena com a sua fala e a legenda, tudo concatenado.
 *
 *   node tools/videos/gravar.mjs <roteiro>            # roteiros/<roteiro>.mjs → saida/<roteiro>/<roteiro>.mp4
 *   node tools/videos/gravar.mjs <roteiro> --montar   # só remonta a partir da última gravação
 */
import { spawn, spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const AQUI = dirname(fileURLToPath(import.meta.url));
export const RAIZ = resolve(AQUI, '../..');
const require = createRequire(resolve(RAIZ, 'web/painel/package.json'));
const { chromium } = require('@playwright/test');

const nome = process.argv[2];
if (!nome) {
  console.error('uso: node tools/videos/gravar.mjs <roteiro> [--montar]');
  process.exit(2);
}
const soMontar = process.argv.includes('--montar');
const roteiro = (await import(pathToFileURL(join(AQUI, 'roteiros', `${nome}.mjs`)).href)).default;
const SAIDA = join(AQUI, 'saida', nome);
const AUDIO = join(SAIDA, 'audio');
const BRUTO = join(SAIDA, 'bruto');
mkdirSync(AUDIO, { recursive: true });

const dormir = (ms) => new Promise((r) => setTimeout(r, Math.max(0, ms)));
const log = (...a) => console.log(`[${nome}]`, ...a);

/** O .env da raiz (o mesmo do make up): a senha das contas da demo. Nada disso vai para a tela nem para a fala. */
export function lerEnv() {
  const env = {};
  const arquivo = resolve(RAIZ, '.env');
  if (!existsSync(arquivo)) return env;
  for (const linha of readFileSync(arquivo, 'utf8').split('\n')) {
    const m = linha.match(/^([A-Za-z_][A-Za-z0-9_]*)=(.*)$/);
    if (m) env[m[1]] = m[2];
  }
  return env;
}

// 1. A narração: um wav por cena e a duração de cada um.
writeFileSync(
  join(SAIDA, 'falas.json'),
  JSON.stringify({ voz: roteiro.voz ?? 'pf_dora', velocidade: roteiro.velocidade ?? 1.0, cenas: roteiro.cenas.map(({ id, fala }) => ({ id, fala })) }, null, 2),
);
log('narrando…');
const narracao = spawnSync('uv', ['run', '-q', join(AQUI, 'narrar.py'), join(SAIDA, 'falas.json'), AUDIO], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'] });
if (narracao.status !== 0) process.exit(narracao.status ?? 1);
const duracoes = JSON.parse(narracao.stdout.trim().split('\n').pop());
const totalFala = Object.values(duracoes).reduce((a, b) => a + b, 0);
log(`fala: ${totalFala.toFixed(1)} s em ${roteiro.cenas.length} cenas (limite ${roteiro.limite ?? '—'} s)`);

const ATRASO_DA_FALA = 0.25;

// 2. A gravação.
if (!soMontar) {
  rmSync(BRUTO, { recursive: true, force: true });
  const browser = await chromium.launch({ channel: 'chrome', args: roteiro.chromeArgs ?? [] });
  const env = lerEnv();
  const ctx = { env, raiz: RAIZ, duracao: 0, fimDaFala: 0, dados: {} };
  const t0 = {};
  // Cada tela que não é o palco (o painel, o Wokwi…) é um contexto do navegador com a sua gravação. O zoom de cada uma
  // vem de roteiro.telas[nome].zoom (padrão 1,25: o texto do painel fica legível no vídeo).
  // 'celular' é a tela do emulador Android, gravada por cena com o adb screenrecord (não é página do navegador).
  const telas = [...new Set(roteiro.cenas.map((c) => c.tela).filter((t) => t !== 'palco' && t !== 'celular'))];

  const contextoPalco = await browser.newContext({ viewport: { width: 1920, height: 1080 }, recordVideo: { dir: join(BRUTO, 'palco'), size: { width: 1920, height: 1080 } } });
  ctx.palco = await contextoPalco.newPage();
  t0.palco = Date.now();
  await ctx.palco.goto(pathToFileURL(join(AQUI, 'palco.html')).href);
  await ctx.palco.evaluate(() => document.fonts.ready);

  const contextos = [contextoPalco];
  ctx.paginas = {};
  for (const tela of telas) {
    const contexto = await browser.newContext({
      viewport: { width: 1920, height: 1080 },
      locale: 'pt-BR',
      timezoneId: 'America/Sao_Paulo',
      colorScheme: 'light',
      ignoreHTTPSErrors: true,
      recordVideo: { dir: join(BRUTO, tela), size: { width: 1920, height: 1080 } },
    });
    // Como o cursor não aparece na gravação headless, um ponteiro desenhado fora da área com zoom segue o mouse.
    await contexto.addInitScript((zoom) => {
      addEventListener('DOMContentLoaded', () => {
        if (zoom !== 1) document.body.style.zoom = String(zoom);
        const p = document.createElement('div');
        p.style.cssText = 'position:fixed;z-index:2147483647;left:0;top:0;width:22px;height:22px;margin:-4px 0 0 -4px;pointer-events:none;transition:transform .12s;' +
          "background:url(\"data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'><path d='M3 2l7.5 19 2.6-7.9L21 10.5z' fill='%23142224' stroke='white' stroke-width='1.5'/></svg>\") no-repeat;";
        document.documentElement.append(p);
        addEventListener('mousemove', (e) => { p.style.left = `${e.clientX}px`; p.style.top = `${e.clientY}px`; }, true);
        addEventListener('mousedown', () => { p.style.transform = 'scale(.8)'; }, true);
        addEventListener('mouseup', () => { p.style.transform = ''; }, true);
      });
    }, roteiro.telas?.[tela]?.zoom ?? 1.25);
    contextos.push(contexto);
    ctx.paginas[tela] = await contexto.newPage();
    t0[tela] = Date.now();
  }
  ctx.painel = ctx.paginas.painel;

  // Ajudantes das cenas.
  const P = (fn, arg) => ctx.palco.evaluate(fn, arg);
  Object.assign(ctx, {
    restante: () => Math.max(0, (ctx.fimDaFala - Date.now()) / 1000),
    esperar: (s) => dormir(s * 1000),
    ateOFimDaFala: (folga = 0) => dormir(ctx.fimDaFala - Date.now() + folga * 1000),
    slide: (s) => P((x) => window.palco.slide(x), s),
    terminal: (o = {}) => P((x) => window.palco.terminal(x), o),
    comentario: (t) => P((x) => window.palco.comentario(x), t),
    selo: (t) => P((x) => window.palco.selo(x), t ?? ''),
    digitar: (cmd, o = {}) => P(([c, op]) => window.palco.digitar(c, op), [cmd, o]),
    escrever: (t) => P((x) => window.palco.escrever(x), t),
    figura: (arquivo, titulo) => P(([s, t]) => window.palco.figura(s, t), [pathToFileURL(resolve(RAIZ, arquivo)).href, titulo ?? '']),
    imagens: (arquivos, o = {}) => P(([s, op]) => window.palco.imagens(s, op), [arquivos.map((a) => pathToFileURL(resolve(RAIZ, a)).href), o]),
    /** Digita o comando e roda de verdade, com a saída aparecendo no palco enquanto sai. */
    async rodar(cmd, { mostrar = cmd, cwd = RAIZ, env: extra = {}, prompt = '$', msPorTecla = 30, timeout = 240, filtro = (t) => t } = {}) {
      await ctx.digitar(mostrar, { prompt, msPorTecla });
      return new Promise((ok) => {
        const p = spawn('bash', ['-c', cmd], { cwd, env: { ...process.env, ...extra } });
        let saida = '';
        let fila = Promise.resolve();
        const enviar = (b) => {
          const t = b.toString();
          saida += t;
          const visivel = filtro(t);
          if (visivel) fila = fila.then(() => ctx.escrever(visivel));
        };
        p.stdout.on('data', enviar);
        p.stderr.on('data', enviar);
        const relogio = setTimeout(() => p.kill('SIGTERM'), timeout * 1000);
        p.on('close', async (codigo) => {
          clearTimeout(relogio);
          await fila;
          await P(() => window.palco.fimDoComando());
          ok({ codigo, saida });
        });
      });
    },
    /** Reproduz um log real já capturado, espalhado em `segundos` (o selo avisa que está acelerado). */
    async reproduzir(texto, segundos) {
      const linhas = texto.split('\n');
      const lotes = Math.max(1, Math.round(segundos * 8));
      const porLote = Math.ceil(linhas.length / lotes);
      for (let i = 0; i < linhas.length; i += porLote) {
        await ctx.escrever(linhas.slice(i, i + porLote).join('\n') + '\n');
        await dormir((segundos * 1000) / lotes);
      }
      await P(() => window.palco.fimDoComando());
    },
    /** Move o ponteiro até o elemento, devagar, e clica. */
    async clicar(locator, { espera = 0.35 } = {}) {
      await locator.scrollIntoViewIfNeeded();
      const caixa = await locator.boundingBox();
      if (caixa) await locator.page().mouse.move(caixa.x + caixa.width / 2, caixa.y + caixa.height / 2, { steps: 28 });
      await dormir(espera * 1000);
      await locator.click();
    },
    async rolarPainel(y, ms = 1200, pagina = ctx.painel) {
      await pagina.evaluate((alvo) => window.scrollTo({ top: alvo, behavior: 'smooth' }), y);
      await dormir(ms);
    },
  });

  if (roteiro.preparar) await roteiro.preparar(ctx);

  const linhaDoTempo = [];
  for (const cena of roteiro.cenas) {
    const fala = duracoes[cena.id];
    let gravadorDoCelular;
    if (cena.tela === 'celular') {
      mkdirSync(join(BRUTO, 'celular'), { recursive: true });
      gravadorDoCelular = spawn('adb', ['shell', 'screenrecord', '--bit-rate', '10000000', `/sdcard/sombra-${cena.id}.mp4`], { stdio: 'ignore' });
      await dormir(900);
      t0.celular = Date.now();
    }
    const inicio = Date.now();
    ctx.duracao = fala;
    ctx.fimDaFala = inicio + (fala + ATRASO_DA_FALA) * 1000;
    log(`cena ${cena.id} (${cena.tela}, ${fala.toFixed(1)} s)`);
    await cena.acao(ctx);
    // Segura o estado final na tela (1,5 s no vídeo, já contando a aceleração): o resultado de um comando costuma
    // chegar no fim, e a captura de quadros tem um pequeno atraso.
    const segurar = (cena.segurar ?? 1.5) * (cena.acelerar ?? 1) * 1000;
    await dormir(Math.max(ctx.fimDaFala + (cena.folga ?? 0.6) * 1000 - Date.now(), segurar));
    const fim = Date.now();
    let fonte = null;
    if (gravadorDoCelular) {
      spawnSync('adb', ['shell', 'pkill', '-INT', 'screenrecord']);
      await new Promise((r) => (gravadorDoCelular.exitCode !== null ? r() : gravadorDoCelular.on('exit', r)));
      await dormir(800);
      fonte = join(BRUTO, 'celular', `${cena.id}.mp4`);
      spawnSync('adb', ['pull', `/sdcard/sombra-${cena.id}.mp4`, fonte], { stdio: 'ignore' });
    }
    linhaDoTempo.push({ id: cena.id, tela: cena.tela, fonte, inicio: (inicio - t0[cena.tela]) / 1000, fim: (fim - t0[cena.tela]) / 1000, fala, acelerar: cena.acelerar ?? 1, recorte: cena.recorte ?? null, rotulo: cena.rotulo ?? null });
  }

  const videos = { palco: ctx.palco.video() };
  for (const tela of telas) videos[tela] = ctx.paginas[tela].video();
  for (const c of contextos) await c.close();
  await browser.close();
  const fontes = {};
  for (const [tela, v] of Object.entries(videos)) fontes[tela] = await v.path();
  writeFileSync(join(SAIDA, 'linha-do-tempo.json'), JSON.stringify({ fontes, cenas: linhaDoTempo }, null, 2));
}

// 3. A montagem: cena a cena (vídeo cortado + fala + legenda) e a concatenação.
const { fontes, cenas } = JSON.parse(readFileSync(join(SAIDA, 'linha-do-tempo.json'), 'utf8'));
const SEG = join(SAIDA, 'segmentos');
rmSync(SEG, { recursive: true, force: true });
mkdirSync(SEG, { recursive: true });

const relogio = (s) => {
  const ms = Math.round(s * 1000);
  const p = (n, w = 2) => String(n).padStart(w, '0');
  return `${p(Math.floor(ms / 3600000))}:${p(Math.floor(ms / 60000) % 60)}:${p(Math.floor(ms / 1000) % 60)},${p(ms % 1000, 3)}`;
};

/** A legenda de uma cena: blocos de até ~84 caracteres, com o tempo proporcional ao tamanho na duração da fala. */
function legenda(fala, duracao) {
  const limpa = fala.replace(/[`*_]/g, '').replace(/\s+/g, ' ').trim();
  const blocos = [];
  for (const frase of limpa.split(/(?<=[.!?…])\s+/)) {
    let atual = '';
    for (const palavra of frase.split(' ')) {
      if ((atual + ' ' + palavra).trim().length > 84 && atual) {
        blocos.push(atual);
        atual = palavra;
      } else atual = (atual + ' ' + palavra).trim();
    }
    if (atual) blocos.push(atual);
  }
  const total = blocos.reduce((a, b) => a + b.length, 0);
  let t = ATRASO_DA_FALA;
  return blocos
    .map((b, i) => {
      const d = (b.length / total) * duracao;
      const linha = `${i + 1}\n${relogio(t)} --> ${relogio(t + d)}\n${b}\n`;
      t += d;
      return linha;
    })
    .join('\n');
}

function ffmpeg(args) {
  const r = spawnSync('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-y', ...args], { stdio: 'inherit' });
  if (r.status !== 0) throw new Error(`ffmpeg falhou: ${args.join(' ')}`);
}

const ESTILO = "FontName=DejaVu Sans,FontSize=12,PrimaryColour=&H00FFFFFF,BackColour=&H30142224,BorderStyle=4,Outline=0,Shadow=0,MarginV=22,MarginL=60,MarginR=60";
const lista = [];
for (const [i, c] of cenas.entries()) {
  // Uma cena acelerada (ex.: o vagrant up, 4×) ocupa o tempo real dividido pelo fator, nunca menos que a fala.
  const f = c.acelerar ?? 1;
  // O conteúdo da tela chega uns décimos antes do relógio: o corte termina 0,35 s antes para não pegar a cena seguinte.
  const real = Math.max(0.5, c.fim - c.inicio - 0.35).toFixed(3);
  const dur = Math.max((c.fim - c.inicio) / f, c.fala + ATRASO_DA_FALA + 0.4).toFixed(3);
  const srt = join(SEG, `${i}.srt`);
  const fala = roteiro.cenas.find((x) => x.id === c.id).fala;
  writeFileSync(srt, legenda(fala, c.fala));
  const saida = join(SEG, `${String(i).padStart(2, '0')}-${c.id}.mp4`);
  const atraso = Math.round(ATRASO_DA_FALA * 1000);
  ffmpeg([
    '-ss', c.inicio.toFixed(3), '-t', real, '-i', c.fonte ?? fontes[c.tela],
    '-i', join(AUDIO, `${c.id}.wav`),
    '-filter_complex',
    // recorte: {x, y, w, h} da tela gravada, ampliado para 1080p (o close na OLED do Wokwi, por exemplo);
    // rotulo: um selo no canto, desenhado na montagem (para telas que não são o palco).
    `[0:v]fps=30,setpts=(PTS-STARTPTS)/${f},fps=30,${c.recorte ? `crop=${c.recorte.w}:${c.recorte.h}:${c.recorte.x}:${c.recorte.y},` : ''}${c.tela === 'celular' ? 'scale=-2:1000:flags=lanczos,pad=iw+16:ih+16:8:8:color=0x142224,pad=1920:1080:(ow-iw)/2:(oh-ih)/2:color=0xF5EEDF' : 'scale=1920:1080:flags=lanczos'},` +
      `${c.rotulo ? `drawtext=fontfile=/usr/share/fonts/liberation/LiberationSans-Bold.ttf:text='${c.rotulo.replace(/[':]/g, '')}':fontsize=34:fontcolor=white:box=1:boxcolor=0xB04F27:boxborderw=16:x=w-tw-48:y=40,` : ''}tpad=stop_mode=clone:stop_duration=${dur},trim=0:${dur},setpts=PTS-STARTPTS,format=yuv420p,subtitles=${srt}:force_style='${ESTILO}'[v];` +
      `[1:a]aresample=48000,aformat=channel_layouts=stereo,adelay=${atraso}|${atraso},apad,atrim=0:${dur}[a]`,
    '-map', '[v]', '-map', '[a]',
    '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '20', '-r', '30',
    '-c:a', 'aac', '-b:a', '160k', '-ar', '48000',
    saida,
  ]);
  lista.push(`file '${saida}'`);
}
writeFileSync(join(SEG, 'lista.txt'), lista.join('\n') + '\n');
const final = join(SAIDA, `${nome}.mp4`);
ffmpeg(['-f', 'concat', '-safe', '0', '-i', join(SEG, 'lista.txt'), '-c', 'copy', '-movflags', '+faststart', final]);
// A prévia leve, para revisar no celular.
ffmpeg(['-i', final, '-vf', 'scale=1280:-2', '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '31', '-c:a', 'aac', '-b:a', '96k', '-movflags', '+faststart', join(SAIDA, `${nome}-previa.mp4`)]);

const dur = Number(spawnSync('ffprobe', ['-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', final], { encoding: 'utf8' }).stdout.trim());
const passou = roteiro.limite && dur > roteiro.limite;
log(`${final}: ${Math.floor(dur / 60)}:${String(Math.round(dur % 60)).padStart(2, '0')}${roteiro.limite ? ` (limite ${roteiro.limite / 60} min)` : ''}${passou ? ' ✘ PASSOU DO LIMITE' : ' ✔'}`);
process.exit(passou ? 1 : 0);
