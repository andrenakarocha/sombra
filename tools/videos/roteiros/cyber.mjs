// Vídeo de Cybersecurity: o Dashboard de Risco Cidadão (docs/videos/cyber.md). Sem limite; mira em ~4 min.
import { spawn, spawnSync } from 'node:child_process';
import { mkdirSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const CORE = 'http://localhost:5080';
const PAINEL = 'http://localhost:5173';
const CASA = '0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10'; // a casa da Dona Lurdes no seed (fictícia)
const CURL = `curl -i -s ${CORE}/api/v1/domicilios/${CASA}/moradores`;

export default {
  nome: 'cyber',
  limite: null,
  voz: 'pf_dora',

  async preparar(ctx) {
    const r = await fetch(`${CORE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'operador@demo.sombra', senha: ctx.env.Demo__Senha }),
    });
    if (!r.ok) throw new Error(`login do operador: HTTP ${r.status}`);
    const { token } = await r.json();
    await ctx.painel.addInitScript((t) => sessionStorage.setItem('sombra.painel.token', t), token);
    await ctx.painel.goto(`${PAINEL}/risco-cidadao`);
    await ctx.painel.getByRole('heading', { name: 'Risco Cidadão' }).waitFor();
    await ctx.painel.getByText('Ao vivo', { exact: true }).waitFor();

    const pasta = join(ctx.raiz, 'tools/videos/saida/cyber/pdf');
    mkdirSync(pasta, { recursive: true });
    spawnSync('pdftoppm', ['-r', '110', '-png', join(ctx.raiz, 'security/entrega/cyber.pdf'), join(pasta, 'p')]);
    ctx.dados.paginas = readdirSync(pasta).filter((f) => f.endsWith('.png')).sort().map((f) => join(pasta, f));

    await ctx.slide({
      titulo: 'Dashboard de Risco Cidadão',
      sub: 'Cybersecurity · o motor de privacidade que protege os dados de saúde das famílias',
      itens: ['Parsing de PII', 'Taint dinâmico e estático', 'IA na remediação', 'Dashboard executivo'],
    });
  },

  cenas: [
    {
      id: 'abertura',
      tela: 'palco',
      fala: 'Olá! Este é o Sombra, o alerta de calor extremo para pessoas idosas que moram sozinhas. Ele guarda dados de saúde, CPF e Cartão SUS, que a LGPD trata como dado pessoal sensível. O módulo Sombra.Privacidade garante que esses dados não saiam por descuido, e o dashboard de Risco Cidadão mostra esse risco para quem decide.',
      async acao() {},
    },
    {
      id: 'farol',
      tela: 'painel',
      fala: 'Este é o dashboard. O farol soma os eventos de segurança dos últimos sete dias, ponderados pela família do dado, em que saúde pesa mais que contato, pela severidade e pelo desfecho: um vazamento pesa três vezes mais que um bloqueio. A pontuação decai com meia-vida de setenta e duas horas, então um problema antigo vai saindo do farol aos poucos.',
      async acao(ctx) {
        const p = ctx.painel;
        await ctx.esperar(1);
        await p.mouse.move(420, 300, { steps: 30 });
        await ctx.esperar(ctx.restante() * 0.4);
        const familia = p.getByText('Por família de dado');
        await familia.scrollIntoViewIfNeeded();
        const caixa = await familia.boundingBox();
        if (caixa) await p.mouse.move(caixa.x + 120, caixa.y + 90, { steps: 30 });
        await ctx.esperar(ctx.restante() * 0.5);
        const sink = p.getByText('Bloqueios por sink, últimas 24 h');
        const c2 = await sink.boundingBox();
        if (c2) await p.mouse.move(c2.x + 300, c2.y + 120, { steps: 30 });
      },
    },
    {
      id: 'sem-login',
      tela: 'palco',
      fala: 'Agora, um ataque simples. Vou tentar ler os moradores de uma casa sem login. O Core responde 401, sem nenhum dado no corpo da resposta, e registra um evento de segurança.',
      async acao(ctx) {
        await ctx.terminal({ titulo: 'operador@sombra: ~/sombra' });
        await ctx.esperar(0.6);
        await ctx.comentario('sem token: o Core não pode devolver nenhum dado');
        await ctx.rodar(CURL, { filtro: (t) => t.replace(/\r/g, '') });
      },
    },
    {
      id: 'ao-vivo',
      tela: 'painel',
      fala: 'De volta ao painel, sem recarregar a página. Repito a tentativa, e o evento chega ao vivo, pelo SignalR, no topo do feed: a família do dado, onde foi barrado e o desfecho, bloqueado. Em seguida, a IA traduz o evento em três textos: para o cidadão, em duas frases calmas; para a gestão, com o artigo da LGPD; e para o time técnico, com a correção. O prompt leva só metadados: a IA nunca vê o dado pessoal.',
      folga: 1,
      async acao(ctx) {
        const p = ctx.painel;
        const feed = p.getByText('Eventos de segurança, traduzidos');
        const caixa = await feed.boundingBox();
        const topo = await p.evaluate(() => window.scrollY);
        if (caixa) await ctx.rolarPainel(topo + caixa.y - 90, 1500);
        await ctx.esperar(1.2);
        spawn('bash', ['-c', `${CURL} > /dev/null`], { stdio: 'ignore' });
        const primeiro = p.getByRole('listitem').filter({ has: p.getByRole('tablist') }).first();
        const c = await primeiro.boundingBox();
        if (c) await p.mouse.move(c.x + 260, c.y + 30, { steps: 30 });
        await ctx.esperar(Math.min(10, ctx.restante() * 0.5));
        const c3 = await primeiro.boundingBox();
        if (c3) await p.mouse.move(c3.x + 360, c3.y + 110, { steps: 30 });
      },
    },
    {
      id: 'publicos',
      tela: 'painel',
      fala: 'Este é o texto para a gestão, e este, o do time técnico. A resposta da IA também passa por uma guarda: os mesmos detectores de PII, jargão proibido no texto do cidadão, tamanho e ortografia. Se reprovar, entra um template revisado. E a evidência técnica mostra a regra, a severidade, a origem e o correlationId, para rastrear o caso nos logs.',
      async acao(ctx) {
        const p = ctx.painel;
        const primeiro = p.getByRole('listitem').filter({ has: p.getByRole('tablist') }).first();
        await ctx.clicar(primeiro.getByRole('tab', { name: 'Para a gestão' }));
        await ctx.esperar(ctx.restante() * 0.25);
        await ctx.clicar(primeiro.getByRole('tab', { name: 'Para o time técnico' }));
        await ctx.esperar(ctx.restante() * 0.45);
        await ctx.clicar(primeiro.getByRole('button', { name: 'Ver evidência técnica' }));
        await ctx.esperar(0.5);
        const dl = primeiro.locator('dl');
        const c = await dl.boundingBox();
        const topo = await p.evaluate(() => window.scrollY);
        if (c) await ctx.rolarPainel(topo + c.y - 300, 1200);
      },
    },
    {
      id: 'antes-depois',
      tela: 'painel',
      fala: 'Agora, o antes e depois. Oito cenários de ataque rodam com o módulo desligado e, depois, ligado. Desligado, os oito vazam: o CPF no texto livre, o laudo numa exceção, o perfil de saúde sem login, o Cartão SUS no log, dado pessoal no prompt da IA e no MQTT, a senha na URL e uma telemetria com assinatura falsa. Ligado, os oito são bloqueados. Este relatório é gerado por um teste automatizado, não à mão.',
      async acao(ctx) {
        const p = ctx.painel;
        await p.goto('https://github.com/andrenakarocha/sombra/blob/main/security/auditoria/relatorio.md');
        const tabela = p.locator('article table').first();
        await tabela.waitFor({ timeout: 20000 });
        const c = await tabela.boundingBox();
        if (c) await ctx.rolarPainel(c.y - 160, 1600);
        const linhas = tabela.locator('tbody tr');
        const n = await linhas.count();
        for (let i = 0; i < n; i++) {
          const b = await linhas.nth(i).boundingBox();
          if (b) await p.mouse.move(b.x + b.width - 140, b.y + b.height / 2, { steps: 14 });
          await ctx.esperar(Math.max(0.6, (ctx.restante() - 2) / (n - i + 1)));
        }
      },
    },
    {
      id: 'semgrep',
      tela: 'palco',
      fala: 'E o taint estático: o Semgrep em modo taint segue o dado desde a fonte até os sinks. Ele acha os oito vazamentos plantados no arquivo canário e sai limpo nos mais de duzentos arquivos do Core. O resultado vai em SARIF para o Code Scanning do GitHub.',
      async acao(ctx) {
        await ctx.terminal();
        await ctx.esperar(0.5);
        await ctx.rodar('make semgrep');
      },
    },
    {
      id: 'pdf',
      tela: 'palco',
      fala: 'Tudo isso está no PDF da entrega, com os trechos de código tirados direto do repositório e a evidência de cada pilar: parsing de PII, taint, IA e o dashboard. O código vai separado, no ZIP e no GitHub. Integrantes: André, João e Matheus. Obrigado!',
      folga: 1.2,
      async acao(ctx) {
        await ctx.imagens(ctx.dados.paginas.slice(0, 4), { segundos: Math.max(6, ctx.duracao - 1), fim: 1 });
      },
    },
  ],
};
