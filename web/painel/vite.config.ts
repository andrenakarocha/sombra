import { execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// O commit vai no rodapé (observabilidade §6). No CI e no release vem de SOMBRA_COMMIT; local, do git.
function commit(): string {
  if (process.env.SOMBRA_COMMIT) return process.env.SOMBRA_COMMIT;
  try {
    return execSync('git rev-parse --short HEAD', { stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim();
  } catch {
    return 'dev';
  }
}

// No desenvolvimento, o Vite repassa /api e /hubs ao Core e /clima ao Clima: o navegador vê uma origem só, como na VM
// (onde quem faz isso é o nginx). Assim o Core não precisa abrir CORS.
const core = process.env.SOMBRA_CORE_URL ?? 'http://localhost:5080';
const clima = process.env.SOMBRA_CLIMA_URL ?? 'http://localhost:8081';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  define: { __COMMIT__: JSON.stringify(commit()) },
  server: {
    port: 5173,
    proxy: {
      '/api': core,
      '/health': core,
      '/version': core,
      '/hubs': { target: core, ws: true },
      '/clima': { target: clima, rewrite: (caminho) => caminho.replace(/^\/clima/, '') },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/testes/preparar.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
  },
});
