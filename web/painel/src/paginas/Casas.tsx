import { useState } from 'react';
import { useCasas } from '@/api/consultas';
import { NIVEIS } from '@/api/schemas';
import { Carregando, ErroNaTela, Vazio } from '@/componentes/Base';
import { SeloDeNivel } from '@/componentes/Nivel';
import { ESTADO_DO_ALERTA, NIVEL } from '@/dominio/niveis';
import { formatarGraus, haQuanto } from '@/dominio/formato';

/**
 * As casas acompanhadas. Privacidade por padrão: o operador vê o apelido da casa, o bairro e as medições; nome,
 * documento, endereço e saúde dos moradores ficam só com a família (o Core nem entrega isso ao papel Operador).
 */
export function Casas() {
  const [nivel, setNivel] = useState('');
  const casas = useCasas({ nivel });

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-display text-3xl font-semibold">Casas</h1>
          <p className="text-texto-suave">Sem dado pessoal: só o apelido, o bairro e o que a Sentinela mede.</p>
        </div>
        <label className="text-sm">
          <span className="mb-1 block font-semibold">Nível agora</span>
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
      {casas.isPending ? (
        <Carregando />
      ) : casas.isError ? (
        <ErroNaTela erro={casas.error} />
      ) : casas.data.itens.length === 0 ? (
        <Vazio>Nenhuma casa com esse filtro.</Vazio>
      ) : (
        <div className="overflow-x-auto rounded-grande border border-borda bg-superficie">
          <table className="w-full min-w-[44rem] text-left">
            <caption className="sr-only">Casas acompanhadas, com o nível de risco agora</caption>
            <thead className="border-b border-borda text-sm text-texto-suave">
              <tr>
                <th scope="col" className="px-4 py-3 font-semibold">Casa</th>
                <th scope="col" className="px-4 py-3 font-semibold">Agora</th>
                <th scope="col" className="px-4 py-3 font-semibold">Última leitura</th>
                <th scope="col" className="px-4 py-3 font-semibold">Sentinela</th>
                <th scope="col" className="px-4 py-3 font-semibold">Último "estou bem"</th>
                <th scope="col" className="px-4 py-3 font-semibold">Alerta</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-borda">
              {casas.data.itens.map((c) => (
                <tr key={c.id} className={c.monitorado ? '' : 'text-texto-suave'}>
                  <th scope="row" className="px-4 py-3 font-normal">
                    <strong className="block">{c.apelido}</strong>
                    <span className="text-sm text-texto-suave">
                      {c.bairro} · {c.regiaoClimatica}
                      {c.monitorado ? '' : ' · sem monitoramento'}
                    </span>
                  </th>
                  <td className="px-4 py-3">
                    <SeloDeNivel nivel={c.nivelAtual} />
                  </td>
                  <td className="px-4 py-3 tabular-nums">
                    {c.ultimaLeitura ? (
                      <>
                        {formatarGraus(c.ultimaLeitura.indiceDeCalorC)}
                        <span className="block text-sm text-texto-suave">{haQuanto(c.ultimaLeitura.momento)}</span>
                      </>
                    ) : (
                      '—'
                    )}
                  </td>
                  <td className="px-4 py-3">{c.sentinela ? (c.sentinela.online ? 'Respondendo' : 'Sem resposta') : '—'}</td>
                  <td className="px-4 py-3">{haQuanto(c.ultimoCheckIn)}</td>
                  <td className="px-4 py-3">{c.alertaAberto ? ESTADO_DO_ALERTA[c.alertaAberto.estado] ?? c.alertaAberto.estado : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
