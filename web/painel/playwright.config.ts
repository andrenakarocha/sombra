import { defineConfig } from '@playwright/test';

/**
 * O E2E do roteiro contra o ambiente de verdade (make up): o painel do compose em :5173, o Core, o Clima e o simulador
 * da Sentinela. Local, usa o Chrome instalado; no CI, o Chromium do Playwright.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 120_000,
  expect: { timeout: 20_000 },
  fullyParallel: false,
  workers: 1,
  reporter: [['list']],
  use: {
    baseURL: process.env.PAINEL_URL ?? 'http://localhost:5173',
    locale: 'pt-BR',
    timezoneId: 'America/Sao_Paulo',
    trace: 'retain-on-failure',
    ...(process.env.CI ? {} : { channel: 'chrome' }),
  },
});
