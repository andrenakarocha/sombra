// O composition root de verdade, sobre SQLite em memória (sql.js). O "Core" é o gateway da demonstração, sem
// latência, atrás de um interruptor de rede: `rede.offline = true` é o modo avião.

import { SemConexao } from '@/application/erros';
import { preferenciasStore } from '@/application/estado/preferencias';
import { sessaoStore } from '@/application/estado/sessao';
import type { CoreGateway } from '@/application/ports/core-gateway';
import type {
  AgendadorDeLembretes,
  Biometria,
  CofreDeSessao,
  Localizacao,
  MonitorDeRede,
  Notificacao,
  Notificador,
} from '@/application/ports/servicos';
import { FixedClock } from '@/domain/compartilhado/clock';
import { montarContainer } from '@/infrastructure/container';
import { CoreDemoGateway } from '@/infrastructure/demo/core-demo-gateway';

import { SqlJsDatabase } from './sql-js-database';

export const AGORA = '2026-11-18T18:00:00.000Z';

export async function containerDeTeste({
  agora = AGORA,
  logado = true,
  localizacao,
  agendador,
  biometria,
}: {
  agora?: string;
  logado?: boolean;
  localizacao?: Localizacao;
  agendador?: AgendadorDeLembretes;
  biometria?: Biometria;
} = {}) {
  sessaoStore.setState({ token: logado ? 'token-de-teste' : null, carregada: true });
  preferenciasStore.setState({ modoIdosoCasaId: null, escalaDoTexto: 'NORMAL', carregada: true });

  const clock = FixedClock.at(agora);
  const db = await SqlJsDatabase.abrir();
  const core = new CoreDemoGateway(clock, 0);
  const rede = { offline: false, recebidas: 0 };
  const http = new Proxy(core, {
    get(alvo, prop) {
      const valor = Reflect.get(alvo, prop, alvo);
      if (typeof valor !== 'function') return valor;
      return (...args: unknown[]) => {
        if (rede.offline) return Promise.reject(new SemConexao());
        if (prop === 'enviar') rede.recebidas++;
        return valor.apply(alvo, args);
      };
    },
  }) as unknown as CoreGateway;

  const notificacoes: Notificacao[] = [];
  const notificador: Notificador = { configurar: async () => {}, notificar: async (n) => void notificacoes.push(n) };
  let guardado: string | null = null;
  const cofre: CofreDeSessao = {
    ler: async () => guardado,
    salvar: async (t) => void (guardado = t),
    apagar: async () => void (guardado = null),
  };
  const ouvintesDaRede = new Set<(online: boolean) => void>();
  const monitor: MonitorDeRede = {
    aoMudar: (o) => {
      ouvintesDaRede.add(o);
      return () => ouvintesDaRede.delete(o);
    },
  };
  let n = 0;
  const container = await montarContainer({
    db,
    http,
    cofre,
    notificador,
    tempoReal: core.tempoReal,
    rede: monitor,
    ids: { novo: () => `op-${++n}` },
    clock,
    sorteio: () => 0.5,
    demo: new CoreDemoGateway(clock, 0),
    localizacao,
    agendador,
    biometria,
  });
  const mudarRede = (online: boolean) => {
    rede.offline = !online;
    for (const o of ouvintesDaRede) o(online);
  };
  return { container, core, rede, mudarRede, notificacoes, clock, db };
}
