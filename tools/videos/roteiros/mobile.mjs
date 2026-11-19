// Vídeo de Mobile Development (docs/videos/mobile.md), até 3 min: o app da família no emulador Android, falando com o
// Core de verdade. Antes: `make up` (o Core no broker local, para o simulador da Sentinela), o emulador ligado com o app
// aberto no Expo Go (adb reverse das portas 5080 e 8082) e a Ana logada. A casa não pode ter tido alerta na última hora.
import { spawn, spawnSync } from 'node:child_process';
import { achar, nos, rolar, tocar, voltar } from '../adb.mjs';

const adb = (...a) => spawnSync('adb', a);
const segurar = (x, y, ms) => adb('shell', 'input', 'swipe', String(x), String(y), String(x), String(y), String(ms));
let simulador;

/** Roda um cenário da Sentinela simulada em segundo plano (é a casa da Dona Lurdes). */
function simular(ctx, cenario) {
  return spawn('make', ['-s', 'sim', `CENARIO=${cenario}`, 'INTERVALO=1'], { cwd: ctx.raiz, stdio: 'ignore', detached: true });
}
/** Para o simulador inteiro (o make, o uv e o Python): o grupo de processos. */
function parar(p) {
  try {
    process.kill(-p.pid, 'SIGINT');
  } catch {
    // já tinha terminado
  }
}

/** Entra como a Ana do zero (o reset do banco troca os IDs do seed, e a sessão antiga não acha mais a casa). */
async function entrarComoAna(ctx) {
  const modoFamilia = await achar('Modo família (segure)', { timeout: 1500 }).catch(() => null);
  if (modoFamilia) segurar(modoFamilia.x, modoFamilia.y, 3800);
  if (!(await achar('Entrar', { timeout: 1500 }).catch(() => null))) {
    for (let i = 0; i < 3 && !(await achar(/^Ajustes$/, { timeout: 1200 }).catch(() => null)); i++) voltar();
    await aba('Ajustes', 'Sair da conta');
    await tocar('Texto normal').catch(() => {});
    await tocar('Sair da conta');
    await ctx.esperar(2);
  }
  // O campo é o nó com o rótulo de acessibilidade (o texto "E-mail" acima dele é só o rótulo visual).
  const preencher = async (rotulo, valor) => {
    const campo = nos().find((n) => n.rotulo === rotulo);
    if (!campo) throw new Error(`sem o campo ${rotulo}`);
    adb('shell', 'input', 'tap', String(Math.round(campo.x)), String(Math.round(campo.y)));
    await ctx.esperar(0.7);
    adb('shell', 'input', 'keycombination', '113', '29');
    adb('shell', 'input', 'keyevent', '67');
    adb('shell', 'input', 'text', `'${valor.replace(/'/g, `'\\''`)}'`);
    await ctx.esperar(0.4);
  };
  await achar('Entrar', { timeout: 5000 });
  await preencher('E-mail', 'ana.filha@demo.sombra');
  await preencher('Senha', ctx.env.Demo__Senha ?? '');
  adb('shell', 'input', 'keyevent', 'KEYCODE_BACK');
  await ctx.esperar(0.5);
  await tocar('Entrar');
  await achar('Suas casas', { timeout: 20000 });
}

/** Toca na aba e confere que a tela certa abriu (um toque durante a transição se perde); tenta até 3 vezes. */
async function aba(nome, esperado) {
  for (let i = 0; i < 3; i++) {
    await tocar(new RegExp(`^${nome}$`));
    if (await achar(esperado, { timeout: 3500 }).catch(() => null)) return;
  }
  throw new Error(`a aba ${nome} não abriu ${esperado}`);
}

async function inicio() {
  for (let i = 0; i < 4; i++) {
    const naInicio = await achar('Suas casas', { timeout: 1500 }).catch(() => null);
    if (naInicio) return;
    voltar();
  }
  await aba('Início', 'Suas casas');
}

export default {
  nome: 'mobile',
  limite: 180,
  voz: 'pf_dora',

  async preparar(ctx) {
    await ctx.slide({
      titulo: 'O app da família',
      sub: 'Mobile Development · Expo e React Native, falando com o Core de verdade',
      itens: ['O calor de dentro da casa, ao vivo', 'Alerta com degraus para a rede de cuidado', 'Modo Idoso', 'Offline-first e acessível'],
    });
    adb('shell', 'settings', 'put', 'system', 'font_scale', '1.0');
    await entrarComoAna(ctx);
    // Uma leitura amena para a casa ter o calor de agora (o simulador para antes de chegar ao perigo).
    const p = simular(ctx, 'sem-resposta');
    await ctx.esperar(3.2);
    parar(p);
    await inicio();
    await ctx.esperar(2);
  },

  cenas: [
    {
      id: 'abertura',
      tela: 'palco',
      fala: 'O app do Sombra é para a família de uma pessoa idosa que mora sozinha. A Ana cuida da mãe, a Dona Lurdes, de longe, e o app mostra a casa dela em tempo real.',
      async acao() {},
    },
    {
      id: 'casa',
      tela: 'celular',
      fala: 'Na tela da casa, ela vê o índice de calor de dentro da casa agora, medido pela Sentinela, e o gráfico das últimas 24 horas. E vê por que a mãe corre mais risco: ela toma diurético, então o perigo começa um grau e meio antes do padrão. O limite é pessoal.',
      async acao(ctx) {
        await ctx.esperar(1.5);
        await tocar('Ver a casa');
        await ctx.esperar(ctx.restante() * 0.45);
        rolar(1800, 900, 900);
      },
    },
    {
      id: 'perto',
      tela: 'celular',
      fala: 'Em "Perto de mim", os lugares frescos e públicos mais perto da casa, para passar a hora mais quente do dia, com o horário de funcionamento. Eles vêm do serviço de clima, a partir do OpenStreetMap.',
      async acao(ctx) {
        voltar();
        await ctx.esperar(0.8);
        await aba('Perto de mim', /Lugares frescos/);
        await ctx.esperar(ctx.restante() * 0.55);
        rolar(1700, 1000, 900);
      },
    },
    {
      id: 'alerta-ao-vivo',
      tela: 'celular',
      acelerar: 2,
      rotulo: 'acelerado 2x',
      fala: 'Agora, a Sentinela da casa começa a medir calor perigoso. O alerta chega ao vivo, sem recarregar: o Core avisa o app pelo SignalR, e a casa passa para perigo, com o prazo para ela responder.',
      folga: 1,
      async acao(ctx) {
        await aba('Início', 'Suas casas');
        await ctx.esperar(0.8);
        simulador = simular(ctx, 'sem-resposta');
        await achar('Perigo', { timeout: 40000 }).catch(() => {});
        await ctx.esperar(2.5);
      },
    },
    {
      id: 'degraus',
      tela: 'celular',
      fala: 'No alerta, a linha do tempo e os degraus: se ninguém responde no prazo, o alerta sobe de degrau e o próximo contato da rede de cuidado é avisado. A Ana pode ligar para a mãe direto do app ou avisar que está a caminho.',
      async acao(ctx) {
        await aba('Alertas', 'Casa da Dona Lurdes');
        await ctx.esperar(1.2);
        await tocar('Casa da Dona Lurdes');
        await ctx.esperar(ctx.restante() * 0.6);
        await tocar('Estou indo');
      },
    },
    {
      id: 'idoso',
      tela: 'celular',
      fala: 'E o Modo Idoso, para o celular da própria Dona Lurdes: letra bem grande, um botão verde para "estou bem", um vermelho, com pressão longa, para pedir ajuda, e a voz que lê o recado em voz alta.',
      async acao(ctx) {
        voltar();
        await ctx.esperar(0.8);
        await aba('Ajustes', 'Central de sincronização');
        await ctx.esperar(1);
        await tocar('Usar neste celular: Casa da Dona Lurdes');
      },
    },
    {
      id: 'offline',
      tela: 'celular',
      acelerar: 3,
      rotulo: 'acelerado 3x',
      fala: 'Sem internet, o app continua. Simulo a queda da rede e ela aperta "estou bem": o toque entra numa fila, no SQLite, com o horário em que ela tocou. Quando a rede volta, ele é enviado, e vale o horário do toque, não o do envio.',
      folga: 1,
      async acao(ctx) {
        segurar(540, 2211, 3800);
        await ctx.esperar(1.5);
        await aba('Ajustes', 'Central de sincronização');
        await tocar('Central de sincronização');
        await ctx.esperar(1);
        await tocar('Simular sem internet');
        await ctx.esperar(1.5);
        voltar();
        await ctx.esperar(0.8);
        await tocar('Usar neste celular: Casa da Dona Lurdes');
        await ctx.esperar(1.5);
        await tocar('ESTOU BEM');
        await ctx.esperar(2.5);
        segurar(540, 2211, 3800);
        await ctx.esperar(1.5);
        await aba('Ajustes', 'Central de sincronização');
        await tocar('Central de sincronização');
        await ctx.esperar(2);
        await tocar('Simular sem internet');
        await ctx.esperar(3);
      },
    },
    {
      id: 'acessibilidade',
      tela: 'celular',
      fala: 'E a acessibilidade: cada botão tem rótulo para o leitor de tela, e o app respeita a fonte grande do sistema, somada ao tamanho de texto do próprio app.',
      async acao(ctx) {
        voltar();
        await ctx.esperar(0.8);
        await tocar('Texto maior');
        adb('shell', 'settings', 'put', 'system', 'font_scale', '1.3');
        await ctx.esperar(1.5);
        await aba('Início', 'Suas casas');
      },
    },
    {
      id: 'fechamento',
      tela: 'palco',
      fala: 'O código, os testes e os prints estão na pasta mobile do repositório. Integrantes: André, João e Matheus. Obrigado!',
      folga: 1.2,
      async acao(ctx) {
        if (simulador) parar(simulador);
        adb('shell', 'settings', 'put', 'system', 'font_scale', '1.0');
        await ctx.slide({
          titulo: 'mobile/',
          sub: 'Expo Router, Clean Architecture, SQLite e SignalR',
          itens: ['Contrato vivo com o Core', 'Limites pessoais de calor (R3)', 'Fila offline com o horário do toque', 'Acessível por padrão'],
        });
      },
    },
  ],
};
