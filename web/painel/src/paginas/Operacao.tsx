import type { ReactNode } from 'react';
import { useSaudeDoClima, useSaudeDoCore } from '@/api/consultas';
import { useTempoReal } from '@/api/tempo-real';
import { Cartao, Vazio } from '@/componentes/Base';
import { Icone } from '@/componentes/Icone';
import { formatarDataHora, formatarHora } from '@/dominio/formato';

function Situacao({ ok, carregando }: { ok: boolean; carregando: boolean }) {
  if (carregando) return <span className="text-texto-suave">conferindo…</span>;
  return ok ? (
    <span className="inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-full border border-normal-borda bg-normal-fundo px-2.5 py-0.5 text-sm font-semibold text-normal-texto">
      <Icone nome="normal" /> No ar
    </span>
  ) : (
    <span className="inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-full border border-perigo-borda bg-perigo-fundo px-2.5 py-0.5 text-sm font-semibold text-perigo-texto">
      <Icone nome="perigo" /> Fora do ar
    </span>
  );
}

function Servico({ nome, papel, situacao, linhas }: { nome: string; papel: string; situacao: ReactNode; linhas: [string, ReactNode][] }) {
  return (
    <Cartao>
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="font-display text-xl font-semibold">{nome}</h2>
          <p className="text-sm text-texto-suave">{papel}</p>
        </div>
        {situacao}
      </div>
      <dl className="mt-4 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 text-sm">
        {linhas.map(([rotulo, valor]) => (
          <div key={rotulo} className="contents">
            <dt className="text-texto-suave">{rotulo}</dt>
            <dd className="font-mono">{valor}</dd>
          </div>
        ))}
      </dl>
    </Cartao>
  );
}

const ESTADO: Record<string, string> = { Healthy: 'ok', Degraded: 'degradado', Unhealthy: 'fora', UP: 'ok', DOWN: 'fora' };

/** A saúde da operação: cada serviço, a versão que está no ar e os alertas de infraestrutura do monitor da VM. */
export function Operacao() {
  const core = useSaudeDoCore();
  const clima = useSaudeDoClima();
  const { eventos } = useTempoReal();
  const alertas = eventos.filter((e) => e.nome === 'AlertaDeInfra');

  const verificacoes = core.data?.pronto?.verificacoes ?? {};
  const componentes = clima.data?.pronto?.components ?? {};
  const previsor = componentes.previsor?.details as { fonte?: string; modelo?: string } | undefined;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-display text-3xl font-semibold">Operação</h1>
        <p className="text-texto-suave">Os serviços conferidos a cada 15 segundos, e os alertas do monitor da VM chegando ao vivo.</p>
      </div>
      <section aria-label="Serviços" className="grid gap-4 lg:grid-cols-3">
        <Servico
          nome="Core"
          papel="API, alertas, escalonamento e privacidade (C#)"
          situacao={<Situacao ok={core.data?.pronto?.status === 'Healthy'} carregando={core.isPending} />}
          linhas={[
            ['banco', ESTADO[verificacoes.banco ?? ''] ?? '—'],
            ['broker MQTT', ESTADO[verificacoes.broker ?? ''] ?? '—'],
            ['versão', core.data?.versao ? `${core.data.versao.versao} · ${core.data.versao.commit}` : '—'],
            ['build', core.data?.versao?.construidoEm ? formatarDataHora(core.data.versao.construidoEm) : '—'],
          ]}
        />
        <Servico
          nome="Clima"
          papel="Risco de calor, previsão de 24 h e pontos (Java)"
          situacao={<Situacao ok={clima.data?.pronto?.status === 'UP'} carregando={clima.isPending} />}
          linhas={[
            ['banco', ESTADO[componentes.db?.status ?? ''] ?? '—'],
            ['previsor', previsor ? `${previsor.fonte} · ${previsor.modelo}` : '—'],
            ['versão', clima.data?.info?.commit ?? '—'],
            ['build', clima.data?.info?.construidoEm ? formatarDataHora(clima.data.info.construidoEm) : '—'],
          ]}
        />
        <Servico
          nome="Painel"
          papel="Esta central (React), servida pelo nginx"
          situacao={<Situacao ok carregando={false} />}
          linhas={[
            ['versão', __COMMIT__],
            ['tempo real', alertas.length || eventos.length ? 'recebendo eventos' : 'conectado, sem eventos ainda'],
          ]}
        />
      </section>
      <Cartao titulo="Alertas de infraestrutura">
        {alertas.length === 0 ? (
          <Vazio>Nenhum alerta do monitor desde que você abriu o painel. CPU, memória, disco e serviços parados aparecem aqui na hora.</Vazio>
        ) : (
          <ol className="space-y-2" aria-live="polite">
            {alertas.map((a) => {
              const d = a.dados as { host?: string; recurso?: string; severidade?: string; mensagem?: string; momento?: string };
              const critico = d.severidade === 'CRITICO';
              return (
                <li key={a.recebidoEm} className="flex flex-wrap items-start gap-3 rounded-medio border border-borda bg-fundo px-3 py-2">
                  <span
                    className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-sm font-semibold ${critico ? 'border-perigo-borda bg-perigo-fundo text-perigo-texto' : 'border-atencao-borda bg-atencao-fundo text-atencao-texto'}`}
                  >
                    <Icone nome={critico ? 'perigo' : 'atencao'} /> {critico ? 'Crítico' : 'Aviso'}
                  </span>
                  <span className="flex-1">
                    <span className="font-mono">{d.host}</span> · {d.mensagem}
                  </span>
                  <time className="font-mono text-sm text-texto-suave">{d.momento ? formatarHora(d.momento) : formatarHora(a.recebidoEm)}</time>
                </li>
              );
            })}
          </ol>
        )}
      </Cartao>
    </div>
  );
}
