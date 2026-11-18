// Composition root: o único lugar que conhece todas as implementações. Testes trocam as portas por fakes.

import { Barramento } from '@/application/barramento';
import { ServicoDeBloqueio } from '@/application/casos-de-uso/bloqueio';
import { CasaESentinela } from '@/application/casos-de-uso/casa-e-sentinela';
import { CentralDeSincronizacao } from '@/application/casos-de-uso/central-de-sincronizacao';
import { Consultas } from '@/application/casos-de-uso/consultas';
import { Escritas } from '@/application/casos-de-uso/escritas';
import { PrivacidadeLocal } from '@/application/casos-de-uso/privacidade-local';
import { ServicoDeSessao } from '@/application/casos-de-uso/sessao';
import type { Container } from '@/application/container';
import { PARADA, reproducaoStore } from '@/application/estado/reproducao';
import { emDemonstracao } from '@/application/estado/sessao';
import { ProjetorDeEventos } from '@/application/eventos/projetor';
import type { CoreGateway } from '@/application/ports/core-gateway';
import type { GeradorDeId } from '@/application/ports/outbox';
import type {
  AgendadorDeLembretes,
  Biometria,
  CanalDeTempoReal,
  CofreDeSegredos,
  CofreDeSessao,
  Localizacao,
  MonitorDeRede,
  Notificador,
  RepositoriosDaUnidade,
  SincronizacaoEmSegundoPlano,
  UnidadeDeTrabalho,
} from '@/application/ports/servicos';
import { MotorDeSincronizacao } from '@/application/sincronizacao/motor';
import { type Clock, relogioDoSistema } from '@/domain/compartilhado/clock';

import { migrar } from './database/migracoes';
import { AlertaRepositorySqlite } from './database/repositorios/alerta-repository';
import { MoradorRepositorySqlite, RedeRepositorySqlite } from './database/repositorios/casa-detalhe-repositories';
import { CasaRepositorySqlite } from './database/repositorios/casa-repository';
import {
  apagarDadosLocais,
  PreferenciaRepositorySqlite,
  SincronizacaoRepositorySqlite,
} from './database/repositorios/chave-valor-repositories';
import { LeituraRepositorySqlite } from './database/repositorios/leitura-repository';
import { OutboxRepositorySqlite } from './database/repositorios/outbox-repository';
import type { SqlDatabase } from './database/sql-database';
import { CoreDemoGateway } from './demo/core-demo-gateway';
import { passoDoReplay, REPLAY } from './demo/replay-inmet';

const ESPERA_DO_DEGRAU_MS = 8000;
const HORA_MS = 60 * 60 * 1000;

// Sem cofre do aparelho (testes): guarda em memória.
function semSegredos(): CofreDeSegredos {
  const guardados = new Map<string, string>();
  return { guardar: async (d, s) => void guardados.set(d, s), ler: async (d) => guardados.get(d) ?? null };
}

// Os mesmos repositórios, presos ao banco ou a uma transação (Unit of Work).
const repositorios = (db: SqlDatabase): RepositoriosDaUnidade => ({
  casas: new CasaRepositorySqlite(db),
  leituras: new LeituraRepositorySqlite(db),
  alertas: new AlertaRepositorySqlite(db),
  moradores: new MoradorRepositorySqlite(db),
  rede: new RedeRepositorySqlite(db),
  sincronizacao: new SincronizacaoRepositorySqlite(db),
  outbox: new OutboxRepositorySqlite(db),
});

// Strategy: a sessão decide se fala com o Core de verdade ou com a demonstração; o resto do app nem sabe.
function pelaSessao<T extends object>(real: T, demo: T): T {
  return new Proxy(real, {
    get: (_alvo, prop) => {
      const alvo = emDemonstracao() ? demo : real;
      const valor = Reflect.get(alvo, prop, alvo);
      return typeof valor === 'function' ? valor.bind(alvo) : valor;
    },
  });
}

export type Adaptadores = {
  db: SqlDatabase;
  http: CoreGateway;
  cofre: CofreDeSessao;
  notificador: Notificador;
  tempoReal: CanalDeTempoReal;
  rede: MonitorDeRede;
  ids: GeradorDeId;
  worker?: SincronizacaoEmSegundoPlano;
  localizacao?: Localizacao;
  segredos?: CofreDeSegredos;
  agendador?: AgendadorDeLembretes;
  biometria?: Biometria;
  clock?: Clock;
  sorteio?: () => number;
  demo?: CoreDemoGateway;
};

export async function montarContainer(a: Adaptadores): Promise<Container & { demoGateway: CoreDemoGateway }> {
  const clock = a.clock ?? relogioDoSistema;
  await migrar(a.db);
  const demo = a.demo ?? new CoreDemoGateway(clock);
  const gateway = pelaSessao<CoreGateway>(a.http, demo);
  const tempoReal = pelaSessao<CanalDeTempoReal>(a.tempoReal, demo.tempoReal);

  const repos = { ...repositorios(a.db), preferencias: new PreferenciaRepositorySqlite(a.db) };
  const unidade: UnidadeDeTrabalho = { executar: (trabalho) => a.db.transaction((tx) => trabalho(repositorios(tx))) };
  const barramento = new Barramento();
  const motor = new MotorDeSincronizacao(repos.outbox, gateway, clock, a.sorteio);
  motor.aoMudar(() => barramento.publicar({ recurso: 'sincronizacao' }));
  const eventos = new ProjetorDeEventos({ casas: repos.casas, barramento, notificador: a.notificador, clock });

  let replay: ReturnType<typeof setInterval> | null = null;
  const pararReproducao = () => {
    if (replay) clearInterval(replay);
    replay = null;
    reproducaoStore.setState(PARADA);
  };

  const sessao = new ServicoDeSessao({
    gateway,
    cofre: a.cofre,
    preferencias: repos.preferencias,
    apagarDadosLocais: () => apagarDadosLocais(a.db, { inclusivePreferencias: false }),
  });

  return {
    consultas: new Consultas({ ...repos, gateway, unidade, clock }),
    escritas: new Escritas({ ...repos, gateway, motor, unidade, ids: a.ids, clock, barramento }),
    casaESentinela: new CasaESentinela({
      gateway,
      casas: repos.casas,
      segredos: a.segredos ?? semSegredos(),
      agendador: a.agendador ?? { programar: async () => 0 },
      barramento,
      clock,
    }),
    sessao,
    bloqueio: new ServicoDeBloqueio({
      preferencias: repos.preferencias,
      biometria: a.biometria ?? { disponivel: async () => false, autenticar: async () => false },
      clock,
    }),
    privacidadeLocal: new PrivacidadeLocal({
      ...repos,
      clock,
      // Apagar tudo: sai da conta e leva junto as preferências (o que sobrar é só o app instalado).
      apagarTudo: async () => {
        await sessao.sair();
        await apagarDadosLocais(a.db, { inclusivePreferencias: true });
      },
    }),
    eventos,
    sincronizacao: { motor, outbox: repos.outbox },
    central: new CentralDeSincronizacao({
      outbox: repos.outbox,
      motor,
      worker: a.worker ?? { registrar: async () => 'INDISPONIVEL' },
      clock,
    }),
    barramento,
    tempoReal,
    rede: a.rede,
    notificador: a.notificador,
    localizacao: a.localizacao ?? { atual: async () => null, observarDirecao: async () => () => {} },
    clock,
    demoGateway: demo,
    demonstracao: {
      async simularOndaDeCalor() {
        const leitura = { momento: clock.now().toISOString(), temperaturaC: 37.2, umidadePct: 45, indiceDeCalorC: 42.1 };
        const alertaId = demo.simularPerigo(leitura);
        setTimeout(() => demo.escalonar(alertaId), ESPERA_DO_DEGRAU_MS);
      },
      reproduzirOndaReal(intervaloMs = 1500) {
        pararReproducao();
        const casa = demo.casaPrincipal;
        const inicio = clock.now().getTime();
        let passo = 0;
        const tocar = () => {
          const hora = REPLAY.horas[passo];
          if (!hora) return pararReproducao();
          // A leitura é real; o horário anda uma hora por passo a partir de agora, para o gráfico e os prazos.
          const p = passoDoReplay(hora, demo.moradorPrincipal(casa.id), new Date(inicio + passo * HORA_MS));
          demo.registrarLeitura(casa.id, p.leitura, p.nivelPersonalizado);
          passo++;
          reproducaoStore.setState({
            ativo: true,
            passo,
            total: REPLAY.horas.length,
            momentoOriginal: hora.momento,
            temperaturaC: hora.temperaturaC,
            indiceC: p.leitura.indiceDeCalorC,
            nivelPadrao: p.nivelPadrao,
            nivelPersonalizado: p.nivelPersonalizado,
            fonte: REPLAY.estacao,
          });
        };
        tocar();
        replay = setInterval(tocar, intervaloMs);
      },
      pararReproducao,
      async recomecar() {
        pararReproducao();
        demo.recomecar();
        await apagarDadosLocais(a.db, { inclusivePreferencias: false });
        barramento.publicar({ recurso: 'casas' }, { recurso: 'alertas' }, { recurso: 'sincronizacao' });
      },
    },
  };
}
