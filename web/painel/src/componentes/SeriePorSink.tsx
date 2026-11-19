import { useState } from 'react';
import { Bar, BarChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import type { EventoDeSeguranca } from '@/api/schemas';
import { formatarHora } from '@/dominio/formato';
import { SINK, seriePorSink } from '@/dominio/privacidade';

const horaCurta = (t: number) => formatarHora(t).slice(0, 5);

/**
 * Bloqueios por sink nas últimas 24 h, em small multiples: um gráfico por sink, todos no matiz da marca e na mesma
 * escala, com o nome e o total escritos. A identidade vem do rótulo, nunca da cor (o vermelho, o âmbar e o verde são do
 * farol). Também há a tabela, para quem não lê o gráfico.
 */
export function SeriePorSink({ eventos }: { eventos: readonly EventoDeSeguranca[] }) {
  const [tabela, setTabela] = useState(false);
  const series = seriePorSink(eventos);
  const maximo = Math.max(1, ...series.flatMap((s) => s.pontos.map((p) => p.eventos)));

  return (
    <div>
      <div className="mb-3 flex justify-end">
        <button type="button" onClick={() => setTabela(!tabela)} className="min-h-11 rounded-medio border border-borda px-3 text-sm font-semibold hover:bg-superficie-alt">
          {tabela ? 'Ver como gráfico' : 'Ver como tabela'}
        </button>
      </div>
      {tabela ? (
        <table className="w-full text-left">
          <caption className="sr-only">Bloqueios por sink nas últimas 24 horas</caption>
          <thead className="text-sm text-texto-suave">
            <tr>
              <th scope="col" className="py-2">Sink</th>
              <th scope="col" className="py-2 text-right">Últimas 24 h</th>
              <th scope="col" className="py-2 text-right">Hora de pico</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-borda">
            {series.map((s) => {
              const pico = s.pontos.reduce((a, b) => (b.eventos > a.eventos ? b : a));
              return (
                <tr key={s.sink}>
                  <th scope="row" className="py-2 font-normal">{SINK[s.sink]}</th>
                  <td className="py-2 text-right tabular-nums">{s.total}</td>
                  <td className="py-2 text-right tabular-nums">{s.total ? `${horaCurta(pico.hora)} (${pico.eventos})` : '—'}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {series.map((s) => (
            <figure key={s.sink} className="rounded-medio border border-borda bg-fundo p-3">
              <figcaption className="mb-1">
                <span className="block font-semibold">{SINK[s.sink]}</span>
                <span className="text-sm tabular-nums text-texto-suave">{s.total} bloqueio(s) em 24 h</span>
              </figcaption>
              <div className="h-24" role="img" aria-label={`${SINK[s.sink]}: ${s.total} bloqueio(s) nas últimas 24 horas`}>
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={s.pontos} margin={{ top: 4, right: 12, bottom: 0, left: 12 }} barCategoryGap={2}>
                    <XAxis dataKey="hora" tickFormatter={horaCurta} interval={5} tick={{ fontSize: 11, fill: 'var(--texto-suave)' }} axisLine={{ stroke: 'var(--borda)' }} tickLine={false} />
                    <YAxis hide domain={[0, maximo]} allowDecimals={false} />
                    <Tooltip
                      cursor={{ fill: 'var(--marca-suave)' }}
                      labelFormatter={(h) => `${horaCurta(Number(h))} às ${horaCurta(Number(h) + 3_600_000)}`}
                      formatter={(v) => [`${v} bloqueio(s)`, SINK[s.sink]]}
                      contentStyle={{ background: 'var(--superficie)', border: '1px solid var(--borda)', borderRadius: 8, color: 'var(--texto)' }}
                    />
                    <Bar dataKey="eventos" fill="var(--marca)" radius={[4, 4, 0, 0]} isAnimationActive={false} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </figure>
          ))}
        </div>
      )}
    </div>
  );
}
