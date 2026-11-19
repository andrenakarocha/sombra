import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { test } from '@playwright/test';
import { entrarComoOperador } from './ambiente';

/**
 * Os prints de evidência das cinco páginas em 1440, 768 e 390 px (§0.13 do plano), gravados em docs/evidencias/.
 * Só roda com EVIDENCIAS=1 (make evidencias-painel), para o E2E comum não regravar imagem.
 */
const PAGINAS = [
  { link: null, arquivo: 'visao-geral' },
  { link: 'Alertas', arquivo: 'alertas' },
  { link: 'Casas', arquivo: 'casas' },
  { link: 'Risco Cidadão', arquivo: 'risco-cidadao' },
  { link: 'Operação', arquivo: 'operacao' },
] as const;
const LARGURAS = [1440, 768, 390] as const;
const PASTA = resolve(import.meta.dirname, '../docs/evidencias');

test.skip(!process.env.EVIDENCIAS, 'só com EVIDENCIAS=1');

for (const largura of LARGURAS) {
  test(`prints em ${largura} px`, async ({ page }) => {
    mkdirSync(PASTA, { recursive: true });
    await page.setViewportSize({ width: largura, height: 900 });
    await entrarComoOperador(page);
    for (const p of PAGINAS) {
      if (p.link) {
        const link = page.getByRole('link', { name: p.link });
        await link.scrollIntoViewIfNeeded();
        await link.click();
      }
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(800);
      await page.screenshot({ path: `${PASTA}/${p.arquivo}-${largura}.png`, fullPage: true });
    }
  });
}
