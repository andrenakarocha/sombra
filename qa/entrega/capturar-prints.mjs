// Sombra · QA · os prints do Azure Boards para o PDF da F10.5, tirados pelo id de cada work item (qa/azure/mapa.json).
// Abre um Chrome de verdade com uma sessão só desta pasta (qa/entrega/.sessao, fora do Git): na primeira vez, faça o login
// no Azure na janela que abrir; depois o script segue sozinho. Nenhuma senha passa pelo script.
//
// Uso (na raiz, depois do make qa-azure):  make qa-prints
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const AQUI = dirname(fileURLToPath(import.meta.url));
const RAIZ = join(AQUI, '..', '..');
const { chromium } = createRequire(join(RAIZ, 'web/painel/package.json'))('@playwright/test');

const mapa = JSON.parse(readFileSync(join(RAIZ, 'qa/azure/mapa.json'), 'utf8'));
const base = `${mapa.organizacao.replace(/\/$/, '')}/${encodeURIComponent(mapa.projeto)}`;
const PRINTS = join(AQUI, 'prints');
mkdirSync(PRINTS, { recursive: true });

const consulta = (tipo) =>
  `${base}/_queries/query/?wiql=${encodeURIComponent(
    `SELECT [System.Id], [System.Title], [System.State], [System.Tags] FROM WorkItems WHERE [System.TeamProject] = @project AND [System.WorkItemType] = '${tipo}' ORDER BY [System.Title]`,
  )}`;

const telas = [
  ['administradores', `${base}/_settings/permissions`],
  ['lista-features', consulta('Feature')],
  ['lista-casos', consulta('Test Case')],
  ['epico', `${base}/_workitems/edit/${mapa.epico}`],
  ...Object.entries(mapa.features).map(([id, n]) => [id, `${base}/_workitems/edit/${n}`]),
  ...Object.entries(mapa.casos).map(([id, n]) => [id, `${base}/_workitems/edit/${n}`]),
];

const contexto = await chromium.launchPersistentContext(join(AQUI, '.sessao'), {
  channel: 'chrome',
  headless: false,
  viewport: { width: 1440, height: 1500 },
  locale: 'pt-BR',
});
const pagina = contexto.pages()[0] ?? (await contexto.newPage());

await pagina.goto(`${base}/_workitems/edit/${mapa.epico}`);
if (!pagina.url().startsWith(mapa.organizacao)) {
  console.log('Faça o login no Azure DevOps na janela do Chrome; o script espera até 5 minutos.');
}
await pagina.waitForURL((url) => url.href.startsWith(mapa.organizacao), { timeout: 300_000 });

let feitos = 0;
for (const [nome, url] of telas) {
  const arquivo = join(PRINTS, `${nome}.png`);
  if (existsSync(arquivo) && !process.argv.includes('--de-novo')) continue;
  await pagina.goto(url, { waitUntil: 'networkidle' });
  await pagina.waitForTimeout(1500); // o Boards termina de desenhar o formulário depois do networkidle
  await pagina.screenshot({ path: arquivo });
  feitos += 1;
  console.log(`${nome}.png`);
}
console.log(`${feitos} prints novos em qa/entrega/prints (de ${telas.length}); --de-novo refaz todos`);
await contexto.close();
