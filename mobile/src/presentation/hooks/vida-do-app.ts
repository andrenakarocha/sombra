// Liga o container ao ciclo de vida do app: mudanças locais → consultas; rede → sincronização; hub → eventos.

import { onlineManager, type QueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';

import type { Container } from '@/application/container';

import { chaves } from './consultas';
import { useSessao } from './estado';

export function useVidaDoApp(container: Container, queryClient: QueryClient) {
  const token = useSessao((s) => s.token);
  const tokenAnterior = useRef(token);

  // Cada recurso local que muda invalida a consulta correspondente: a tela relê do SQLite.
  useEffect(
    () =>
      container.barramento.assinar(({ recurso, id }) => {
        if (recurso === 'casas') queryClient.invalidateQueries({ queryKey: chaves.casas });
        if (recurso === 'casa' && id) queryClient.invalidateQueries({ queryKey: chaves.casa(id) });
        if (recurso === 'alertas') queryClient.invalidateQueries({ queryKey: chaves.alertas });
        if (recurso === 'alerta' && id) queryClient.invalidateQueries({ queryKey: chaves.alerta(id) });
        if (recurso === 'privacidade') queryClient.invalidateQueries({ queryKey: ['privacidade'] });
        if (recurso === 'sincronizacao') queryClient.invalidateQueries({ queryKey: chaves.sincronizacao });
      }),
    [container, queryClient],
  );

  // A rede volta: o TanStack Query refaz as consultas e a outbox drena.
  useEffect(() => {
    onlineManager.setEventListener((definirOnline) =>
      container.rede.aoMudar((online) => {
        definirOnline(online);
        if (online) container.sincronizacao.motor.drenar();
      }),
    );
  }, [container]);

  // Trocou de pessoa (ou saiu): nada da sessão anterior fica no cache da tela. Na primeira montagem não há o que
  // limpar, e limpar mataria as consultas que acabaram de começar.
  useEffect(() => {
    if (tokenAnterior.current !== token) queryClient.clear();
    tokenAnterior.current = token;
    if (!token) return;
    container.notificador.configurar().catch(() => {});
    container.sincronizacao.motor.drenar();
    return container.tempoReal.conectar(
      token,
      (evento, payload) => void container.eventos.receber(evento, payload),
      () => queryClient.invalidateQueries(),
    );
  }, [container, queryClient, token]);
}

// networkMode 'always': offline, a consulta roda mesmo assim e responde do SQLite em vez de ficar pausada.
export const opcoesDoQueryClient = { defaultOptions: { queries: { networkMode: 'always' as const, retry: 1, staleTime: 30_000 } } };
