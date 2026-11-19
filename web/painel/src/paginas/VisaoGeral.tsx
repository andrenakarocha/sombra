import { useCasas, useResumo } from '@/api/consultas';
import { NIVEIS, type Nivel } from '@/api/schemas';
import { Cartao, Carregando, ErroNaTela, Kpi } from '@/componentes/Base';
import { FeedAoVivo } from '@/componentes/FeedAoVivo';
import { SeloDeNivel } from '@/componentes/Nivel';
import { DEGRAU } from '@/dominio/niveis';
import { formatarNumero } from '@/dominio/formato';

/** A visão geral: os números da operação, o indicador de impacto e o que está acontecendo agora. */
export function VisaoGeral() {
  const resumo = useResumo();
  const casas = useCasas();

  if (resumo.isPending) return <Carregando />;
  if (resumo.isError) return <ErroNaTela erro={resumo.error} />;
  const r = resumo.data;
  const metaAtingida = r.tempoMedianoAteCheckInMin !== null && r.tempoMedianoAteCheckInMin < 15;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-display text-3xl font-semibold">Visão geral</h1>
        <p className="text-texto-suave">Os números mudam sozinhos quando chega um evento das Sentinelas ou do Core.</p>
      </div>

      <section aria-label="Indicadores" className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <Kpi
          rotulo='Tempo mediano até o "estou bem"'
          valor={r.tempoMedianoAteCheckInMin === null ? '—' : `${formatarNumero(r.tempoMedianoAteCheckInMin, 1)} min`}
          detalhe={r.tempoMedianoAteCheckInMin === null ? 'Ainda sem alerta resolvido' : metaAtingida ? 'Dentro da meta de 15 min' : 'Acima da meta de 15 min'}
          destaque
        />
        <Kpi rotulo="Casas monitoradas" valor={formatarNumero(r.casasMonitoradas)} />
        <Kpi rotulo="Alertas ativos" valor={formatarNumero(r.alertasAtivos)} />
        <Kpi
          rotulo="Resolvidos antes de chamar toda a rede"
          valor={r.alertasResolvidosSemUltimoDegrauPct === null ? '—' : `${formatarNumero(r.alertasResolvidosSemUltimoDegrauPct, 0)}%`}
        />
        <Kpi rotulo="Alarmes desnecessários para a família (semana)" valor={formatarNumero(r.alarmesDesnecessariosSemana)} detalhe="Pedido da entrevista: nada de alarme à toa" />
        <Kpi rotulo="Sentinelas sem responder" valor={formatarNumero(r.sentinelasOffline)} />
      </section>

      <div className="grid gap-6 lg:grid-cols-5">
        <div className="space-y-6 lg:col-span-2">
          <Cartao titulo="Alertas ativos por nível">
            <ul className="space-y-2">
              {[...NIVEIS].reverse().filter((n) => (r.alertasAtivosPorNivel[n] ?? 0) > 0).map((n) => (
                <li key={n} className="flex items-center justify-between">
                  <SeloDeNivel nivel={n as Nivel} />
                  <span className="font-display text-2xl font-semibold tabular-nums">{r.alertasAtivosPorNivel[n]}</span>
                </li>
              ))}
              {Object.values(r.alertasAtivosPorNivel).every((v) => !v) ? <li className="text-texto-suave">Nenhum alerta ativo agora.</li> : null}
            </ul>
          </Cartao>
          <Cartao titulo="Escalonamentos hoje">
            <ol className="grid grid-cols-3 gap-2 text-center">
              {[1, 2, 3].map((d) => (
                <li key={d} className="rounded-medio border border-borda bg-fundo p-3">
                  <p className="font-display text-2xl font-semibold tabular-nums">{r.escalonamentosHojePorDegrau[String(d)] ?? 0}</p>
                  <p className="text-sm text-texto-suave">
                    Degrau {d}
                    <br />
                    {DEGRAU[d]}
                  </p>
                </li>
              ))}
            </ol>
            <p className="mt-3 text-sm text-texto-suave">Os degraus 2 e 3 só acontecem em perigo ou com pedido de ajuda (D13).</p>
          </Cartao>
          <Cartao titulo="Alertas da semana por bairro">
            <ul className="space-y-1">
              {Object.entries(r.alertasDaSemanaPorBairro).map(([bairro, total]) => (
                <li key={bairro} className="flex justify-between border-b border-borda py-1 last:border-0">
                  <span>{bairro}</span>
                  <span className="tabular-nums">{total}</span>
                </li>
              ))}
            </ul>
            <p className="mt-2 text-sm text-texto-suave">Só o agregado por bairro: nenhum endereço sai do Core.</p>
          </Cartao>
        </div>
        <div className="space-y-6 lg:col-span-3">
          <Cartao titulo="Agora, ao vivo">
            <FeedAoVivo />
          </Cartao>
          <Cartao titulo="Casas em risco">
            {casas.isPending ? (
              <Carregando />
            ) : casas.isError ? (
              <ErroNaTela erro={casas.error} />
            ) : (
              <ul className="divide-y divide-borda">
                {casas.data.itens
                  .filter((c) => c.monitorado)
                  .sort((a, b) => NIVEIS.indexOf(b.nivelAtual) - NIVEIS.indexOf(a.nivelAtual))
                  .slice(0, 6)
                  .map((c) => (
                    <li key={c.id} className="flex items-center justify-between gap-3 py-2">
                      <span>
                        <strong className="block">{c.apelido}</strong>
                        <span className="text-sm text-texto-suave">{c.bairro}</span>
                      </span>
                      <SeloDeNivel nivel={c.nivelAtual} />
                    </li>
                  ))}
              </ul>
            )}
          </Cartao>
        </div>
      </div>
    </div>
  );
}
