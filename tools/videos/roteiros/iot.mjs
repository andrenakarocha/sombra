// Vídeo de Physical Computing (docs/videos/iot.md), até 4 min: a Sentinela no Wokwi integrada ao Sombra de verdade.
// Antes: `make demo-wokwi` (o Core ouve o broker público com as chaves de teste do secrets.h). O projeto é montado num
// editor novo do Wokwi com os arquivos de iot/sentinela/ (sem login), e a simulação roda no navegador.
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

const CORE = 'http://localhost:5080';
const PAINEL = 'http://localhost:5173';
// Close na simulação: o editor do DHT22, a OLED, o ESP32 e o LED (a metade direita, ampliada 2×).
const CLOSE = { x: 960, y: 140, w: 960, h: 540 };
const BOTAO_VERDE = { x: 1455, y: 857 };

async function montarNoWokwi(ctx) {
  const p = ctx.paginas.wokwi;
  const pasta = join(ctx.raiz, 'iot/sentinela');
  await p.goto('https://wokwi.com/projects/new/esp32', { waitUntil: 'domcontentloaded' });
  await p.waitForFunction(() => window.monaco?.editor?.getModels().some((m) => m.uri.toString().includes('sketch.ino')), null, { timeout: 60000 });
  await p.waitForTimeout(2000);
  for (const nome of ['diagram.json', 'sketch.ino']) {
    await p.getByText(nome, { exact: true }).first().click();
    await p.waitForFunction((n) => window.monaco.editor.getModels().some((m) => m.uri.toString().endsWith(n)), nome);
    await p.evaluate(([n, t]) => window.monaco.editor.getModels().find((m) => m.uri.toString().endsWith(n)).setValue(t), [nome, readFileSync(join(pasta, nome), 'utf8')]);
  }
  await p.locator('button:right-of(:text("Library Manager"))').first().click();
  await p.waitForTimeout(600);
  const [seletor] = await Promise.all([p.waitForEvent('filechooser'), p.getByText('Upload file(s)...').click()]);
  await seletor.setFiles(readdirSync(pasta).filter((f) => /\.h$|^libraries\.txt$/.test(f)).map((f) => join(pasta, f)));
  await p.waitForTimeout(2500);
  await p.getByText('sketch.ino', { exact: true }).first().click();
  await p.getByRole('button', { name: 'Start the simulation' }).click();
  await p.getByText('Compiling project...').waitFor({ timeout: 30000 }).catch(() => {});
  await p.getByText('Compiling project...').waitFor({ state: 'hidden', timeout: 400000 });
  await p.waitForFunction(() => document.body.innerText.includes('mqtt.conectado'), null, { timeout: 180000 });
}

/** Move o slider do DHT22 aos poucos até o valor (a OLED e o LED reagem enquanto ele anda). */
async function ajustar(ctx, qual, alvo, segundos = 1.6) {
  const p = ctx.paginas.wokwi;
  const slider = p.locator('input[type=range]').nth(qual === 'temperatura' ? 0 : 1);
  const caixa = await slider.boundingBox();
  const atual = Number(await slider.inputValue());
  const passos = 16;
  for (let i = 1; i <= passos; i++) {
    const v = atual + ((alvo - atual) * i) / passos;
    if (caixa) {
      const min = qual === 'temperatura' ? -40 : 0;
      const max = qual === 'temperatura' ? 80 : 100;
      await p.mouse.move(caixa.x + ((v - min) / (max - min)) * caixa.width, caixa.y + caixa.height / 2);
    }
    await slider.evaluate((el, valor) => {
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(el, String(valor));
      el.dispatchEvent(new Event('input', { bubbles: true }));
      el.dispatchEvent(new Event('change', { bubbles: true }));
    }, Math.round(v * 10) / 10);
    await ctx.esperar(segundos / passos);
  }
}

export default {
  nome: 'iot',
  limite: 240,
  voz: 'pf_dora',
  telas: { wokwi: { zoom: 1 }, painel: { zoom: 1.25 } },

  async preparar(ctx) {
    await ctx.slide({
      titulo: 'A Sentinela',
      sub: 'Physical Computing · o ESP32 dentro de casa, integrado ao Sombra de verdade',
      itens: ['DHT22, OLED, LED RGB, buzzer e dois botões', 'Índice de calor calculado no aparelho', 'MQTT assinado com HMAC-SHA256', 'Offline-first'],
    });
    const r = await fetch(`${CORE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'operador@demo.sombra', senha: ctx.env.Demo__Senha }),
    });
    if (!r.ok) throw new Error(`login do operador: HTTP ${r.status}`);
    const { token } = await r.json();
    await ctx.painel.addInitScript((t) => sessionStorage.setItem('sombra.painel.token', t), token);
    await ctx.painel.goto(`${PAINEL}/alertas`);
    await ctx.painel.getByText('Ao vivo', { exact: true }).waitFor({ timeout: 30000 });
    await montarNoWokwi(ctx);
    // Fecha o editor do DHT22, se estiver aberto, e deixa o ponteiro no meio do circuito.
    await ctx.paginas.wokwi.mouse.move(1440, 560);
  },

  cenas: [
    {
      id: 'abertura',
      tela: 'palco',
      fala: 'A Sentinela é a parte física do Sombra: um aparelho simples na casa de uma pessoa idosa que mora sozinha. Ela mede o calor de dentro da casa, calcula o índice de calor ali mesmo e fala com o resto do sistema.',
      async acao() {},
    },
    {
      id: 'circuito',
      tela: 'wokwi',
      fala: 'Este é o circuito no Wokwi, com o código do repositório. Um ESP32 com Wi-Fi; o DHT22, para temperatura e umidade; uma tela OLED; um LED RGB de luz fixa, que nunca pisca; um buzzer; e dois botões grandes: verde, para "estou bem", e vermelho, para pedir ajuda, com pressão longa de dois segundos para não disparar sem querer.',
      async acao(ctx) {
        const p = ctx.paginas.wokwi;
        const pontos = [[1441, 520], [1203, 340], [1612, 330], [1715, 670], [1208, 742], [1455, 857], [1612, 857]];
        for (const [i, [x, y]] of pontos.entries()) {
          await p.mouse.move(x, y, { steps: 25 });
          await ctx.esperar(Math.max(0.8, (ctx.restante() - 0.5) / (pontos.length - i)));
        }
      },
    },
    {
      id: 'niveis',
      tela: 'wokwi',
      recorte: CLOSE,
      fala: 'Agora, o calor sobe. Com 30 graus, a tela diz "Quente": beba água. Com 33, cautela extrema, e a luz fica laranja. Com 35 graus e 50% de umidade, a sensação passa de 40: perigo. Luz vermelha fixa, bipes curtos, e a tela explica o próximo passo: em 15 minutos, a família é avisada. A tabela de níveis é a mesma do Core, do serviço de clima e do modelo de ML, com os mesmos vinte vetores de teste.',
      async acao(ctx) {
        const p = ctx.paginas.wokwi;
        await p.locator('wokwi-dht22').first().click();
        await ctx.esperar(1);
        await ajustar(ctx, 'temperatura', 30);
        await ctx.esperar(ctx.restante() * 0.25);
        await ajustar(ctx, 'temperatura', 33);
        await ctx.esperar(ctx.restante() * 0.3);
        await ajustar(ctx, 'umidade', 50, 0.8);
        await ajustar(ctx, 'temperatura', 35);
        await p.mouse.move(1612, 330, { steps: 20 });
      },
    },
    {
      id: 'alerta-no-painel',
      tela: 'painel',
      fala: 'E a integração. A leitura chega ao Core pelo MQTT, e o painel do operador mostra o alerta da casa da Dona Lurdes, aberto agora, ao vivo. O Core manda de volta um comando assinado, e a Sentinela confere a assinatura antes de mostrar qualquer coisa.',
      async acao(ctx) {
        const p = ctx.painel;
        await p.getByText(/degrau 0/i).first().waitFor({ timeout: 60000 }).catch(() => {});
        const item = p.getByText(/degrau 0/i).first();
        const c = await item.boundingBox().catch(() => null);
        if (c) await p.mouse.move(c.x + 80, c.y + 8, { steps: 30 });
      },
    },
    {
      id: 'prazo',
      tela: 'wokwi',
      recorte: CLOSE,
      acelerar: 40,
      rotulo: '15 minutos, acelerado 40x',
      fala: 'Ninguém respondeu. Quinze minutos depois, o Core sobe o alerta de degrau, avisa a família pelo app e manda o comando FAMILIA_AVISADA: a tela da Sentinela passa a dizer que a família está sendo avisada.',
      async acao(ctx) {
        const p = ctx.paginas.wokwi;
        await p.mouse.move(1612, 330, { steps: 10 });
        await p.waitForFunction(() => /comando\.aceito[^\n]*FAMILIA_AVISADA/.test(document.body.innerText), null, { timeout: 20 * 60 * 1000 });
        await ctx.esperar(3);
      },
    },
    {
      id: 'degrau-no-painel',
      tela: 'painel',
      fala: 'No painel, o mesmo alerta já está no degrau 1: a família foi avisada, com o horário de cada passo.',
      async acao(ctx) {
        const p = ctx.painel;
        const item = p.getByText(/degrau 1/i).first();
        await item.waitFor({ timeout: 30000 }).catch(() => {});
        const c = await item.boundingBox().catch(() => null);
        if (c) await p.mouse.move(c.x + 80, c.y + 8, { steps: 30 });
      },
    },
    {
      id: 'estou-bem',
      tela: 'wokwi',
      fala: 'Agora ela aperta o botão verde. A Sentinela manda o check-in, assinado, e o Core resolve o alerta.',
      async acao(ctx) {
        const p = ctx.paginas.wokwi;
        await p.locator('input[type=range]').first().waitFor({ state: 'visible', timeout: 2000 }).catch(() => {});
        await p.mouse.move(BOTAO_VERDE.x, BOTAO_VERDE.y, { steps: 25 });
        await ctx.esperar(0.6);
        await p.mouse.down();
        await ctx.esperar(0.35);
        await p.mouse.up();
        await p.mouse.move(1612, 330, { steps: 25 });
      },
    },
    {
      id: 'resolvido',
      tela: 'painel',
      fala: 'E o painel marca o alerta como resolvido, com o "estou bem" na linha do tempo.',
      async acao(ctx) {
        const p = ctx.painel;
        const item = p.getByText(/resolvido/i).first();
        await item.waitFor({ timeout: 30000 }).catch(() => {});
        const c = await item.boundingBox().catch(() => null);
        if (c) await p.mouse.move(c.x + 60, c.y + 8, { steps: 30 });
      },
    },
    {
      id: 'seguranca',
      tela: 'wokwi',
      fala: 'No monitor serial, cada telemetria enviada e cada comando aceito. Ela é offline-first: sem internet, continua alertando com luz, som e tela, e guarda os eventos numa fila. Toda mensagem é assinada com HMAC-SHA256, com contador anti-replay e horário do NTP. Uma mensagem adulterada no broker público é recusada, e nenhum dado pessoal passa por ele.',
      async acao(ctx) {
        const p = ctx.paginas.wokwi;
        await p.mouse.move(1300, 960, { steps: 30 });
        await ctx.esperar(ctx.restante() * 0.5);
        await p.getByText('hmac.h', { exact: true }).first().click();
        await p.mouse.move(400, 400, { steps: 30 });
      },
    },
    {
      id: 'fechamento',
      tela: 'palco',
      fala: 'O circuito, o código, os testes e o link do Wokwi estão no README da pasta iot/sentinela. Integrantes: André, João e Matheus. Obrigado!',
      folga: 1.2,
      async acao(ctx) {
        await ctx.slide({
          titulo: 'iot/sentinela',
          sub: 'Circuito, firmware, 47 testes no host e o link do Wokwi',
          itens: ['Integração real com o Core, ida e volta', 'Comandos assinados e verificados', 'Offline-first', 'Nenhum dado pessoal no broker'],
        });
      },
    },
  ],
};
