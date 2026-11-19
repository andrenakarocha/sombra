import { useState } from 'react';
import { useEventosDeSeguranca, useRiscoCidadao } from '@/api/consultas';
import type { EventoDeSeguranca } from '@/api/schemas';
import { Cartao, Carregando, ErroNaTela, Vazio } from '@/componentes/Base';
import { FarolGrande, SeloDeFarol } from '@/componentes/Farol';
import { SeriePorSink } from '@/componentes/SeriePorSink';
import { formatarDataHora, haQuanto } from '@/dominio/formato';
import { rotuloDaCategoria, rotuloDoSink } from '@/dominio/privacidade';

const PUBLICOS = [
  { chave: 'cidadao', rotulo: 'Para o cidadão' },
  { chave: 'gestao', rotulo: 'Para a gestão' },
  { chave: 'tecnico', rotulo: 'Para o time técnico' },
] as const;

function EventoComTraducao({ evento }: { evento: EventoDeSeguranca }) {
  const [publico, setPublico] = useState<(typeof PUBLICOS)[number]['chave']>('cidadao');
  const [evidencia, setEvidencia] = useState(false);
  const texto = evento.traducao?.[publico];

  return (
    <li className="rounded-grande border border-borda bg-superficie p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="font-semibold">
          {rotuloDaCategoria(evento.categoria)} · {rotuloDoSink(evento.sink, evento.tipo)}
          <span className="font-normal text-texto-suave"> · {haQuanto(evento.momento)}</span>
        </p>
        <span
          className={`rounded-full border px-2.5 py-0.5 text-sm font-semibold ${evento.bloqueado ? 'border-normal-borda bg-normal-fundo text-normal-texto' : 'border-perigo-borda bg-perigo-fundo text-perigo-texto'}`}
        >
          {evento.bloqueado ? 'Bloqueado' : 'Só registrado'}
        </span>
      </div>
      <div role="tablist" aria-label="Tradução para cada público" className="mt-3 flex flex-wrap gap-1">
        {PUBLICOS.map((p) => (
          <button
            key={p.chave}
            type="button"
            role="tab"
            aria-selected={publico === p.chave}
            onClick={() => setPublico(p.chave)}
            className="min-h-10 rounded-medio border border-borda px-3 text-sm aria-selected:border-marca aria-selected:bg-marca-suave aria-selected:font-semibold"
          >
            {p.rotulo}
          </button>
        ))}
      </div>
      <p className="mt-3" role="tabpanel">
        {texto ?? 'Tradução ainda na fila: a IA traduz em segundos, ou o template cobre.'}
        {evento.traducao?.fonte ? <span className="ml-2 text-sm text-texto-suave">({evento.traducao.fonte === 'IA' ? 'IA' : 'template'})</span> : null}
      </p>
      <button
        type="button"
        aria-expanded={evidencia}
        onClick={() => setEvidencia(!evidencia)}
        className="mt-3 min-h-11 rounded-medio border border-borda px-3 text-sm font-semibold hover:bg-superficie-alt"
      >
        {evidencia ? 'Esconder a evidência técnica' : 'Ver evidência técnica'}
      </button>
      {evidencia ? (
        <dl className="mt-3 grid gap-x-4 gap-y-1 rounded-medio bg-fundo p-3 font-mono text-sm sm:grid-cols-[max-content_1fr]">
          <dt className="text-texto-suave">quando</dt>
          <dd>{formatarDataHora(evento.momento)}</dd>
          <dt className="text-texto-suave">tipo</dt>
          <dd>{evento.tipo}</dd>
          <dt className="text-texto-suave">regra</dt>
          <dd>{evento.regra ?? '—'}</dd>
          <dt className="text-texto-suave">severidade</dt>
          <dd>{evento.severidade ?? '—'}</dd>
          <dt className="text-texto-suave">origem</dt>
          <dd className="break-all">{evento.origem}</dd>
          <dt className="text-texto-suave">detalhe</dt>
          <dd className="break-all">{evento.detalhe ?? '—'}</dd>
          <dt className="text-texto-suave">correlationId</dt>
          <dd>{evento.correlationId ?? '—'}</dd>
        </dl>
      ) : null}
    </li>
  );
}

/** O Dashboard Executivo de Risco Cidadão (Cyber): o farol, o porquê e o que cada público precisa saber. */
export function RiscoCidadao() {
  const risco = useRiscoCidadao();
  const eventos = useEventosDeSeguranca();

  if (risco.isPending) return <Carregando />;
  if (risco.isError) return <ErroNaTela erro={risco.error} />;
  const r = risco.data;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-display text-3xl font-semibold">Risco Cidadão</h1>
        <p className="text-texto-suave">
          O quanto os dados das famílias estiveram em risco, e o que o Sombra barrou. Só metadados: nenhum dado pessoal aparece aqui.
        </p>
      </div>
      <FarolGrande farol={r.farol} pontuacao={r.pontuacao} eventos={r.eventosNaJanela} />
      <div className="grid gap-6 lg:grid-cols-3">
        <Cartao titulo="Por família de dado">
          {r.porCategoria.length === 0 ? (
            <Vazio>Nenhum evento na janela.</Vazio>
          ) : (
            <ul className="space-y-3">
              {r.porCategoria.map((c) => (
                <li key={c.categoria}>
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-semibold">{rotuloDaCategoria(c.categoria)}</span>
                    <SeloDeFarol farol={c.farol} />
                  </div>
                  <div className="mt-1 flex items-center gap-2">
                    <div className="h-2 flex-1 rounded-full bg-superficie-alt" aria-hidden="true">
                      <div className="h-2 rounded-full bg-marca" style={{ width: `${Math.min(100, c.pontuacao)}%` }} />
                    </div>
                    <span className="whitespace-nowrap text-right text-sm tabular-nums text-texto-suave">
                      {Math.round(c.pontuacao)}/100 · {c.eventos} evento(s)
                    </span>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Cartao>
        <Cartao titulo="Bloqueios por sink, últimas 24 h" className="lg:col-span-2">
          {eventos.isPending ? <Carregando /> : eventos.isError ? <ErroNaTela erro={eventos.error} /> : <SeriePorSink eventos={eventos.data.itens} />}
        </Cartao>
      </div>
      <div className="grid gap-6 lg:grid-cols-3">
        <Cartao titulo="Regras que mais dispararam">
          <ol className="space-y-1">
            {r.topRegras.map((t) => (
              <li key={t.nome} className="flex justify-between border-b border-borda py-1 last:border-0">
                <span className="font-mono text-sm">{t.nome}</span>
                <span className="tabular-nums">{t.eventos}</span>
              </li>
            ))}
            {r.topRegras.length === 0 ? <li className="text-texto-suave">Nenhuma regra disparou.</li> : null}
          </ol>
        </Cartao>
        <Cartao titulo="Eventos de segurança, traduzidos" className="lg:col-span-2">
          {eventos.isPending ? (
            <Carregando />
          ) : eventos.isError ? (
            <ErroNaTela erro={eventos.error} />
          ) : eventos.data.itens.length === 0 ? (
            <Vazio>Nenhum evento de segurança registrado.</Vazio>
          ) : (
            <ul className="space-y-3">
              {eventos.data.itens.slice(0, 8).map((e) => (
                <EventoComTraducao key={e.id} evento={e} />
              ))}
            </ul>
          )}
        </Cartao>
      </div>
      <p className="text-sm text-texto-suave">Calculado {haQuanto(r.calculadoEm)} · pesos e decaimento na spec 07 §4.</p>
    </div>
  );
}
