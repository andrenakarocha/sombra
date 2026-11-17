// Hub SignalR do Core (docs/contratos/signalr.md): o Core empurra os eventos e o app atualiza o cache sem recarregar.

import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import type { QueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';

import { API_URL } from '@/config';
import type { NivelDeRisco } from '@/domain/clima/nivel-de-risco';
import { notificacaoDoEvento, notificar } from '@/notificacoes/notificacoes';
import { usePreferencias } from '@/sessao/preferencias';
import { TOKEN_DEMO, useSessao } from '@/sessao/sessao';

import { queryClient, type Resultado } from './consultas';
import type { Domicilio } from '@/application/modelos';

const NOVA_TENTATIVA_MS = 10_000;

type LeituraRegistrada = {
  domicilioId: string;
  momento: string;
  temperaturaC: number;
  umidadePct: number;
  indiceDeCalorC: number;
  nivel: NivelDeRisco;
};
type SentinelaStatus = { domicilioId: string; deviceId: string; online: boolean };
type CheckInRegistrado = { domicilioId: string; momento: string };
type EventoDeAlerta = { domicilioId: string; alertaId: string };

function atualizarCasa(cliente: QueryClient, id: string, mudar: (casa: Domicilio) => Domicilio) {
  cliente.setQueryData<Resultado<Domicilio[]>>(
    ['casas'],
    (atual) => atual && { ...atual, dados: atual.dados.map((c) => (c.id === id ? mudar(c) : c)) },
  );
  cliente.setQueryData<Resultado<Domicilio>>(['casa', id], (atual) => atual && { ...atual, dados: mudar(atual.dados) });
}

// Função pura sobre o cache do TanStack Query, para testar sem hub.
export function aplicarEvento(cliente: QueryClient, evento: string, payload: unknown) {
  switch (evento) {
    case 'LeituraRegistrada': {
      const { domicilioId, nivel, ...leitura } = payload as LeituraRegistrada;
      atualizarCasa(cliente, domicilioId, (c) => ({ ...c, nivelAtual: nivel, ultimaLeitura: leitura }));
      cliente.invalidateQueries({ queryKey: ['leituras', domicilioId] });
      break;
    }
    case 'SentinelaStatus': {
      const { domicilioId, deviceId, online } = payload as SentinelaStatus;
      atualizarCasa(cliente, domicilioId, (c) => ({ ...c, sentinela: { deviceId, online } }));
      break;
    }
    case 'CheckInRegistrado': {
      const { domicilioId, momento } = payload as CheckInRegistrado;
      atualizarCasa(cliente, domicilioId, (c) => ({ ...c, ultimoCheckIn: momento }));
      break;
    }
    case 'RiscoCidadaoAtualizado': {
      const { domicilioId } = payload as { domicilioId: string | null };
      if (domicilioId) cliente.invalidateQueries({ queryKey: ['privacidade', domicilioId] });
      break;
    }
    case 'AlertaAberto':
    case 'AlertaEscalonado':
    case 'AlertaResolvido': {
      const { domicilioId, alertaId } = payload as EventoDeAlerta;
      cliente.invalidateQueries({ queryKey: ['casas'] });
      cliente.invalidateQueries({ queryKey: ['casa', domicilioId] });
      cliente.invalidateQueries({ queryKey: ['alertas'] });
      cliente.invalidateQueries({ queryKey: ['alerta', alertaId] });
      break;
    }
  }
}

// Todo evento do hub (ou da simulação da demonstração) passa por aqui: atualiza o cache e, se for o caso, notifica.
export function receberEvento(evento: string, payload: Record<string, unknown>) {
  aplicarEvento(queryClient, evento, payload);
  const modoIdoso = !!usePreferencias.getState().modoIdosoCasaId;
  const casas = queryClient.getQueryData<Resultado<Domicilio[]>>(['casas'])?.dados ?? [];
  const apelido = (id: string) => casas.find((c) => c.id === id)?.apelido ?? 'Sombra';
  const n = notificacaoDoEvento(evento, payload, { modoIdoso, apelido });
  if (n) notificar(n, modoIdoso).catch(() => {});
}

export const EVENTOS = [
  'LeituraRegistrada',
  'SentinelaStatus',
  'CheckInRegistrado',
  'AlertaAberto',
  'AlertaEscalonado',
  'AlertaResolvido',
  'RiscoCidadaoAtualizado',
] as const;

export function useTempoReal() {
  const token = useSessao((s) => s.token);

  useEffect(() => {
    if (!token || token === TOKEN_DEMO) return;
    const conexao = new HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/sombra`, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build();

    for (const evento of EVENTOS) conexao.on(evento, (payload) => receberEvento(evento, payload));
    // Eventos perdidos não são reenviados: ao reconectar, busca tudo de novo.
    conexao.onreconnected(() => queryClient.invalidateQueries());

    let ativo = true;
    let espera: ReturnType<typeof setTimeout>;
    const conectar = () =>
      conexao.start().catch(() => {
        // Sem hub, o app segue com REST e cache; tenta de novo depois.
        if (ativo) espera = setTimeout(conectar, NOVA_TENTATIVA_MS);
      });
    conectar();

    return () => {
      ativo = false;
      clearTimeout(espera);
      conexao.stop();
    };
  }, [token]);
}
