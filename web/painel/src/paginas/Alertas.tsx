import { useState } from 'react';
import { useAlertas } from '@/api/consultas';
import { NIVEIS, type Alerta } from '@/api/schemas';
import { Cartao, Carregando, ErroNaTela, Vazio } from '@/componentes/Base';
import { SeloDeNivel } from '@/componentes/Nivel';
import { DEGRAU, ESTADO_DO_ALERTA, NIVEL, rotuloDoEstado } from '@/dominio/niveis';
import { formatarDataHora, haQuanto } from '@/dominio/formato';

const ESTADOS = ['AGUARDANDO_CHECKIN', 'ESCALONADO', 'RECONHECIDO', 'RESOLVIDO'] as const;

function LinhaDoTempo({ alerta }: { alerta: Alerta }) {
  return (
    <ol className="mt-3 space-y-2 border-l-2 border-borda pl-4">
      {alerta.linhaDoTempo.map((e, i) => (
        <li key={i} className="relative">
          <span aria-hidden="true" className="absolute -left-[1.4rem] top-1.5 size-2.5 rounded-full bg-marca" />
          <p className="text-sm text-texto-suave">
            {formatarDataHora(e.momento)} · degrau {e.degrau} ({DEGRAU[e.degrau] ?? ''})
          </p>
          <p>{e.descricao}</p>
        </li>
      ))}
    </ol>
  );
}

/** Os alertas, filtráveis, com a linha do tempo dos degraus de cada um. */
export function Alertas() {
  const [estado, setEstado] = useState('');
  const [nivel, setNivel] = useState('');
  const [aberto, setAberto] = useState<string | null>(null);
  const alertas = useAlertas({ estado, nivel });

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-display text-3xl font-semibold">Alertas</h1>
          <p className="text-texto-suave">Do aberto ao resolvido, com cada degrau que a rede de cuidado recebeu.</p>
        </div>
        <div className="flex flex-wrap gap-3">
          <label className="text-sm">
            <span className="mb-1 block font-semibold">Estado</span>
            <select value={estado} onChange={(e) => setEstado(e.target.value)} className="min-h-11 rounded-medio border border-borda bg-superficie px-3">
              <option value="">Todos</option>
              {ESTADOS.map((e) => (
                <option key={e} value={e}>
                  {ESTADO_DO_ALERTA[e]}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm">
            <span className="mb-1 block font-semibold">Nível</span>
            <select value={nivel} onChange={(e) => setNivel(e.target.value)} className="min-h-11 rounded-medio border border-borda bg-superficie px-3">
              <option value="">Todos</option>
              {NIVEIS.map((n) => (
                <option key={n} value={n}>
                  {NIVEL[n].rotulo}
                </option>
              ))}
            </select>
          </label>
        </div>
      </div>

      {alertas.isPending ? (
        <Carregando />
      ) : alertas.isError ? (
        <ErroNaTela erro={alertas.error} />
      ) : alertas.data.itens.length === 0 ? (
        <Vazio>Nenhum alerta com esse filtro.</Vazio>
      ) : (
        <ul className="space-y-3">
          {alertas.data.itens.map((a) => (
            <li key={a.id}>
              <Cartao>
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <p className="text-sm text-texto-suave">{a.tipo === 'PREVENTIVO' ? 'Preventivo (previsão de 24 h)' : 'Sentinela'}</p>
                    <h2 className="font-display text-xl font-semibold">{a.apelidoDaCasa}</h2>
                    <p className="text-texto-suave">
                      {rotuloDoEstado(a.tipo, a.estado)} · aberto {haQuanto(a.abertoEm)} ({formatarDataHora(a.abertoEm)})
                    </p>
                  </div>
                  <div className="flex flex-col items-start gap-2 sm:items-end">
                    <SeloDeNivel nivel={a.nivel} />
                    <span className="text-sm text-texto-suave">
                      Degrau {a.degrau}: {DEGRAU[a.degrau]}
                    </span>
                  </div>
                </div>
                <button
                  type="button"
                  aria-expanded={aberto === a.id}
                  onClick={() => setAberto(aberto === a.id ? null : a.id)}
                  className="mt-3 min-h-11 rounded-medio border border-borda px-3 text-sm font-semibold hover:bg-superficie-alt"
                >
                  {aberto === a.id ? 'Esconder a linha do tempo' : `Ver a linha do tempo (${a.linhaDoTempo.length})`}
                </button>
                {aberto === a.id ? <LinhaDoTempo alerta={a} /> : null}
              </Cartao>
            </li>
          ))}
        </ul>
      )}
      {alertas.data ? <p className="text-sm text-texto-suave">{alertas.data.total} alerta(s) no total.</p> : null}
    </div>
  );
}
