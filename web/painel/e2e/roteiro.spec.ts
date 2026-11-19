import { expect, test } from '@playwright/test';
import { CORE, ENV, entrar, entrarComoOperador, simular } from './ambiente';

const LURDES = '0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10';
const MORADORA = 'c1a8e2f4-7b3d-4e9a-8f2c-6d5b4a3e2f10';

test('o operador entra pela tela de login e cai na visão geral', async ({ page }) => {
  await entrar(page);
  await expect(page.getByRole('heading', { name: 'Visão geral' })).toBeVisible();
  await expect(page.getByText('Operação do Sombra')).toBeVisible();
});

test('o painel é de quem acompanha muitas casas: a família é mandada para o app', async ({ page }) => {
  await entrar(page, 'ana.filha@demo.sombra');
  await expect(page.getByText('Esta central é para operadores. A família acompanha pelo app do Sombra.')).toBeVisible();
  await expect(page).toHaveURL(/\/entrar$/);
});

test('roteiro: a casa chega a perigo, ela aperta "estou bem" e o painel acompanha ao vivo', async ({ page }) => {
  await entrarComoOperador(page);

  const fim = simular('onda-de-calor');
  await expect(page.getByText(/Casa da Dona Lurdes: Leitura da Sentinela/).first()).toBeVisible({ timeout: 60_000 });
  await expect(page.getByText(/Casa da Dona Lurdes: "Estou bem" recebido/).first()).toBeVisible({ timeout: 90_000 });
  expect(await fim).toBe(0);
  // Logo depois de um alerta fechado, a casa fica em carência e não abre outro (D13, nada de alarme à toa): por isso o
  // teste confere o alerta resolvido na lista, que vale tanto na primeira rodada quanto nas seguintes.

  await page.getByRole('link', { name: 'Alertas' }).click();
  await page.getByLabel('Estado').selectOption('RESOLVIDO');
  const alerta = page.getByRole('listitem').filter({ hasText: 'Casa da Dona Lurdes' }).first();
  await expect(alerta).toBeVisible();
  await alerta.getByRole('button', { name: /Ver a linha do tempo/ }).click();
  await expect(alerta.getByText(/degrau 0/).first()).toBeVisible();
});

test('as casas aparecem sem dado pessoal', async ({ page }) => {
  await entrarComoOperador(page);
  await page.getByRole('link', { name: 'Casas' }).click();
  await expect(page.getByRole('rowheader').filter({ hasText: 'Casa da Dona Lurdes' })).toBeVisible();
  const texto = await page.locator('main').innerText();
  expect(texto).not.toMatch(/\d{3}\.\d{3}\.\d{3}-\d{2}/);
  expect(texto).not.toMatch(/Rua das Flores/);
});

test('o farol de Risco Cidadão muda ao vivo quando o Sombra barra um acesso', async ({ page }) => {
  await entrarComoOperador(page);
  await page.getByRole('link', { name: 'Risco Cidadão' }).click();
  const contagem = page.getByText(/de 100 · \d+ evento\(s\) na janela/);
  const antes = await contagem.innerText();

  // o cenário de controle de acesso da auditoria: pedir sem login os dados de saúde de uma moradora
  const resposta = await fetch(`${CORE}/api/v1/domicilios/${LURDES}/moradores/${MORADORA}`);
  expect(resposta.status).toBe(401);

  await expect(contagem).not.toHaveText(antes);
  await page.getByRole('button', { name: 'Ver evidência técnica' }).first().click();
  await expect(page.getByText('acesso-negado').first()).toBeVisible();
});

test('a operação recebe o alerta do monitor da VM na hora', async ({ page }) => {
  await entrarComoOperador(page);
  await page.getByRole('link', { name: 'Operação' }).click();
  await expect(page.getByText('No ar').first()).toBeVisible();

  const mensagem = `Disco acima de 90% (teste ${Date.now()})`;
  const resposta = await fetch(`${CORE}/api/v1/ops/alertas`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${ENV.Ops__TokenDoMonitor}` },
    body: JSON.stringify({ host: 'sombra-srv01', recurso: 'DISCO', severidade: 'AVISO', valor: 91, limite: 90, mensagem, momento: new Date().toISOString() }),
  });
  expect(resposta.status).toBe(202);
  await expect(page.getByText(mensagem)).toBeVisible();
});
