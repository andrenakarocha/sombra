// Check-in com fila offline: grava primeiro no SQLite e só tira da fila quando o Core confirma.

import { onlineManager } from '@tanstack/react-query';
import { useEffect } from 'react';

import { type CheckInNaFila, enfileirar, pendentes, remover } from '@/banco/fila';
import { useSessao } from '@/sessao/sessao';

import { ErroDaApi, requisitar, SemConexao } from './cliente';
import { queryClient } from './consultas';
import type { CheckIn, SituacaoDoCheckIn } from '@/application/modelos';

type Fila = {
  pendentes: () => Promise<CheckInNaFila[]>;
  remover: (id: number) => Promise<void>;
  enviar: (item: CheckInNaFila) => Promise<void>;
};

const filaReal: Fila = {
  pendentes,
  remover,
  enviar: ({ domicilioId, situacao, momento }) =>
    requisitar<void>(`/domicilios/${domicilioId}/check-ins`, {
      metodo: 'POST',
      corpo: { origem: 'APP', situacao, momento } satisfies CheckIn,
    }),
};

// Vale tentar de novo depois: sem rede, sessão vencida, limite de requisições ou Core fora do ar.
function tentarDeNovo(erro: unknown) {
  return erro instanceof SemConexao || (erro instanceof ErroDaApi && [401, 408, 429].includes(erro.status)) ||
    (erro instanceof ErroDaApi && erro.status >= 500);
}

let emAndamento: Promise<number> | null = null;

// Envia em ordem e para no primeiro erro que vale repetir. Devolve quantos continuam na fila.
export function sincronizarFila(fila: Fila = filaReal): Promise<number> {
  emAndamento ??= (async () => {
    try {
      const itens = await fila.pendentes();
      for (const [i, item] of itens.entries()) {
        try {
          await fila.enviar(item);
        } catch (erro) {
          if (tentarDeNovo(erro)) return itens.length - i;
          // Erro definitivo (ex.: casa removida): repetir para sempre não ajuda ninguém.
        }
        await fila.remover(item.id);
      }
      if (itens.length) queryClient.invalidateQueries();
      return 0;
    } finally {
      emAndamento = null;
    }
  })();
  return emAndamento;
}

export async function registrarCheckIn(domicilioId: string, situacao: SituacaoDoCheckIn) {
  await enfileirar(domicilioId, situacao, new Date().toISOString());
  const restantes = await sincronizarFila();
  return restantes === 0 ? 'ENVIADO' : 'NA_FILA';
}

// Tenta ao abrir o app e toda vez que a rede volta.
export function useSincronizacaoDaFila() {
  const token = useSessao((s) => s.token);
  useEffect(() => {
    if (!token) return;
    sincronizarFila();
    return onlineManager.subscribe((online) => {
      if (online) sincronizarFila();
    });
  }, [token]);
}
