#!/usr/bin/env node
/**
 * Publica a Sentinela no Wokwi (F5.1): abre o Chrome na tela, espera você entrar na sua conta do Wokwi, monta o projeto
 * com os arquivos de iot/sentinela/ (código, circuito, cabeçalhos e bibliotecas), salva e imprime o link público.
 *
 *   node tools/wokwi/publicar.mjs
 */
import { createRequire } from 'node:module';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { homedir } from 'node:os';

const RAIZ = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const require = createRequire(resolve(RAIZ, 'web/painel/package.json'));
const { chromium } = require('@playwright/test');
const PASTA = join(RAIZ, 'iot/sentinela');

const contexto = await chromium.launchPersistentContext(join(homedir(), '.cache/sombra-wokwi-perfil'), {
  channel: 'chrome',
  headless: false,
  viewport: { width: 1600, height: 950 },
  // o Chrome segue desenhando mesmo quando a janela fica atrás de outra
  args: ['--disable-backgrounding-occluded-windows', '--disable-renderer-backgrounding', '--disable-background-timer-throttling'],
});
const p = contexto.pages()[0] ?? (await contexto.newPage());
await p.goto('https://wokwi.com/projects/new/esp32', { waitUntil: 'domcontentloaded' });

const logado = () => p.getByRole('button', { name: /sign in/i }).count().then((n) => n === 0);
if (!(await logado())) {
  console.log('Entre na sua conta do Wokwi na janela que abriu (SIGN IN). Espero até 5 min…');
  const inicio = Date.now();
  while (!(await logado())) {
    if (Date.now() - inicio > 5 * 60 * 1000) throw new Error('sem login no Wokwi');
    await p.waitForTimeout(2000);
  }
  await p.goto('https://wokwi.com/projects/new/esp32', { waitUntil: 'domcontentloaded' });
}

await p.waitForFunction(() => window.monaco?.editor?.getModels().some((m) => m.uri.toString().includes('sketch.ino')), null, { timeout: 60000 });
await p.waitForTimeout(2000);
for (const nome of ['diagram.json', 'sketch.ino']) {
  await p.getByText(nome, { exact: true }).first().click();
  await p.waitForFunction((n) => window.monaco.editor.getModels().some((m) => m.uri.toString().endsWith(n)), nome);
  await p.evaluate(([n, t]) => window.monaco.editor.getModels().find((m) => m.uri.toString().endsWith(n)).setValue(t), [nome, readFileSync(join(PASTA, nome), 'utf8')]);
}
await p.locator('button:right-of(:text("Library Manager"))').first().click();
await p.waitForTimeout(600);
const [seletor] = await Promise.all([p.waitForEvent('filechooser'), p.getByText('Upload file(s)...').click()]);
await seletor.setFiles(readdirSync(PASTA).filter((f) => /\.h$|^libraries\.txt$/.test(f)).map((f) => join(PASTA, f)));
await p.waitForTimeout(2500);

await p.getByRole('button', { name: /^save$/i }).first().click();
await p.waitForURL(/\/projects\/\d+/, { timeout: 60000 });
console.log(`\nProjeto salvo: ${p.url()}\nConfira numa aba anônima e cole o link em iot/sentinela/README.md (a F5.1).`);
await p.waitForTimeout(3000);
await contexto.close();
