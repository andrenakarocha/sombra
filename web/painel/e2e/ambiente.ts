import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { spawn } from 'node:child_process';
import { expect, type Page } from '@playwright/test';

export const RAIZ = resolve(import.meta.dirname, '../../..');
export const CORE = process.env.CORE_URL ?? 'http://localhost:5080';

/** O .env da raiz (o mesmo do make up): a senha das contas da demo e o token do monitor. Nada disso fica no código. */
function lerEnv(): Record<string, string> {
  const env: Record<string, string> = {};
  for (const linha of readFileSync(resolve(RAIZ, '.env'), 'utf8').split('\n')) {
    const m = linha.match(/^([A-Za-z_][A-Za-z0-9_]*)=(.*)$/);
    if (m && m[1]) env[m[1]] = m[2] ?? '';
  }
  return { ...env, ...process.env } as Record<string, string>;
}

export const ENV = lerEnv();

export async function entrar(page: Page, email = 'operador@demo.sombra') {
  await page.goto('/entrar');
  await page.getByLabel('E-mail').fill(email);
  await page.getByLabel('Senha').fill(ENV.Demo__Senha ?? '');
  await page.getByRole('button', { name: 'Entrar' }).click();
}

let tokenDoOperador: Promise<string> | null = null;

/**
 * Um login do operador pela API, reaproveitado em todos os testes: o Core limita o login a 5 por minuto por origem
 * (a proteção contra força bruta), e o E2E respeita isso. A tela de login tem o teste dela.
 */
function token(): Promise<string> {
  tokenDoOperador ??= fetch(`${CORE}/api/v1/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: 'operador@demo.sombra', senha: ENV.Demo__Senha }),
  }).then(async (r) => {
    if (!r.ok) throw new Error(`login do operador falhou: HTTP ${r.status}`);
    return ((await r.json()) as { token: string }).token;
  });
  return tokenDoOperador;
}

export async function entrarComoOperador(page: Page) {
  const t = await token();
  await page.addInitScript((valor) => sessionStorage.setItem('sombra.painel.token', valor), t);
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Visão geral' })).toBeVisible();
  await expect(page.getByText('Ao vivo', { exact: true })).toBeVisible();
}

/** Roda um cenário do simulador da Sentinela (tools/sentinela-sim) e devolve quando ele termina. */
export function simular(cenario: string): Promise<number> {
  return new Promise((ok) => {
    const p = spawn('make', ['-s', 'sim', `CENARIO=${cenario}`, 'INTERVALO=1'], { cwd: RAIZ, stdio: 'ignore' });
    p.on('exit', (codigo) => ok(codigo ?? 1));
  });
}
