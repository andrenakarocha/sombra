/**
 * Ajudantes para dirigir o app no emulador Android pelo adb (o vídeo de Mobile): acha um elemento pelo texto ou pelo
 * rótulo de acessibilidade (o content-desc, que é o accessibilityLabel do React Native) e toca no centro dele.
 */
import { spawnSync } from 'node:child_process';

const adb = (...args) => spawnSync('adb', args, { encoding: 'utf8', maxBuffer: 32 * 1024 * 1024 });
const dormir = (ms) => new Promise((r) => setTimeout(r, ms));

/** Os nós da tela atual: texto, rótulo e o centro de cada um. */
export function nos() {
  adb('shell', 'uiautomator', 'dump', '/sdcard/sombra-ui.xml');
  const xml = adb('exec-out', 'cat', '/sdcard/sombra-ui.xml').stdout;
  const lista = [];
  for (const m of xml.matchAll(/<node [^>]*>/g)) {
    const no = m[0];
    const attr = (n) => (no.match(new RegExp(`${n}="([^"]*)"`)) ?? [])[1] ?? '';
    const b = attr('bounds').match(/\[(\d+),(\d+)\]\[(\d+),(\d+)\]/);
    if (!b) continue;
    const [x1, y1, x2, y2] = b.slice(1).map(Number);
    lista.push({ texto: attr('text'), rotulo: attr('content-desc'), clicavel: attr('clickable') === 'true', x: (x1 + x2) / 2, y: (y1 + y2) / 2, area: (x2 - x1) * (y2 - y1) });
  }
  return lista;
}

/** Espera um elemento cujo texto ou rótulo case com `padrao` (string ou RegExp) e devolve o nó. */
export async function achar(padrao, { timeout = 20000 } = {}) {
  const casa = (s) => (padrao instanceof RegExp ? padrao.test(s) : s === padrao);
  const ate = Date.now() + timeout;
  while (Date.now() < ate) {
    const achados = nos().filter((n) => casa(n.texto) || casa(n.rotulo));
    if (achados.length) return achados.sort((a, b) => a.area - b.area)[0];
    await dormir(600);
  }
  throw new Error(`não achei na tela: ${padrao}`);
}

export async function tocar(padrao, opcoes) {
  const n = await achar(padrao, opcoes);
  adb('shell', 'input', 'tap', String(Math.round(n.x)), String(Math.round(n.y)));
  return n;
}

export function digitar(texto) {
  adb('shell', 'input', 'text', texto.replace(/ /g, '%s'));
}

export function rolar(de = 1600, para = 700, ms = 500) {
  adb('shell', 'input', 'swipe', '540', String(de), '540', String(para), String(ms));
}

export function voltar() {
  adb('shell', 'input', 'keyevent', 'KEYCODE_BACK');
}
