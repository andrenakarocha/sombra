import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { entrarComoOperador } from './ambiente';

/**
 * Acessibilidade (F11.6, D18): o axe confere as cinco páginas e a tela de login contra as regras WCAG 2.2 A e AA, no
 * claro e no escuro, em 1440 e 390 px. Falha com qualquer violação séria ou crítica, e lista o que achou.
 */
const PAGINAS = [null, 'Alertas', 'Casas', 'Risco Cidadão', 'Operação'] as const;
const REGRAS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

async function conferir(page: import('@playwright/test').Page, onde: string) {
  const { violations } = await new AxeBuilder({ page }).withTags(REGRAS).analyze();
  const graves = violations.filter((v) => v.impact === 'serious' || v.impact === 'critical');
  const resumo = graves.map((v) => `${v.id} (${v.impact}): ${v.help} — ${v.nodes.map((n) => n.target.join(' ')).slice(0, 3).join(' | ')}`);
  expect(resumo, `${onde}\n${resumo.join('\n')}`).toEqual([]);
}

for (const tema of ['light', 'dark'] as const) {
  for (const largura of [1440, 390] as const) {
    test(`sem violação séria de WCAG 2.2 AA · ${tema} · ${largura} px`, async ({ page }) => {
      await page.emulateMedia({ colorScheme: tema, reducedMotion: 'reduce' });
      await page.setViewportSize({ width: largura, height: 900 });
      await page.goto('/entrar');
      await conferir(page, `login · ${tema} · ${largura}`);
      await entrarComoOperador(page);
      for (const link of PAGINAS) {
        if (link) {
          const alvo = page.getByRole('link', { name: link });
          await alvo.scrollIntoViewIfNeeded();
          await alvo.click();
        }
        await page.waitForLoadState('networkidle');
        await page.waitForTimeout(500);
        await conferir(page, `${link ?? 'Visão geral'} · ${tema} · ${largura}`);
      }
    });
  }
}
