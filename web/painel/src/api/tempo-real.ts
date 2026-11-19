import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { useSyncExternalStore } from 'react';
import { CHAVES, consultas } from '@/app/consultas';
import { sessao } from './sessao';

/**
 * O tempo real do painel (docs/contratos/signalr.md): uma conexão com o hub do Core, no grupo dos operadores. Cada
 * evento invalida as consultas que ele muda e entra no feed ao vivo; ao reconectar, o painel busca tudo de novo (o Core
 * não reenvia eventos perdidos).
 */
export const EVENTOS = [
  'LeituraRegistrada',
  'SentinelaStatus',
  'AlertaAberto',
  'AlertaEscalonado',
  'AvisoParaARede',
  'AlertaResolvido',
  'CheckInRegistrado',
  'RiscoCidadaoAtualizado',
  'AlertaDeInfra',
] as const;
export type NomeDoEvento = (typeof EVENTOS)[number];

export interface EventoAoVivo {
  nome: NomeDoEvento;
  dados: Record<string, unknown>;
  recebidoEm: number;
}

const INVALIDA: Record<NomeDoEvento, readonly (readonly string[])[]> = {
  LeituraRegistrada: [CHAVES.casas, CHAVES.resumo],
  SentinelaStatus: [CHAVES.casas, CHAVES.resumo],
  AlertaAberto: [CHAVES.alertas, CHAVES.resumo, CHAVES.casas],
  AlertaEscalonado: [CHAVES.alertas, CHAVES.resumo, CHAVES.casas],
  AvisoParaARede: [CHAVES.alertas],
  AlertaResolvido: [CHAVES.alertas, CHAVES.resumo, CHAVES.casas],
  CheckInRegistrado: [CHAVES.resumo, CHAVES.casas, CHAVES.alertas],
  RiscoCidadaoAtualizado: [CHAVES.risco],
  AlertaDeInfra: [],
};

const LIMITE = 60;

interface Estado {
  conectado: boolean;
  eventos: readonly EventoAoVivo[];
}

let estado: Estado = { conectado: false, eventos: [] };
const ouvintes = new Set<() => void>();
let conexao: HubConnection | null = null;

function publicar(novo: Partial<Estado>) {
  estado = { ...estado, ...novo };
  ouvintes.forEach((o) => o());
}

function receber(nome: NomeDoEvento, dados: Record<string, unknown>) {
  publicar({ eventos: [{ nome, dados, recebidoEm: Date.now() }, ...estado.eventos].slice(0, LIMITE) });
  INVALIDA[nome].forEach((chave) => void consultas.invalidateQueries({ queryKey: chave }));
}

/** Liga a conexão (uma só para o painel inteiro). Sem sessão, não liga. */
export function conectar(): void {
  if (conexao || !sessao.atual()) return;
  conexao = new HubConnectionBuilder()
    .withUrl('/hubs/sombra', { accessTokenFactory: () => sessao.atual()?.token ?? '' })
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
    .configureLogging(LogLevel.Warning)
    .build();
  EVENTOS.forEach((nome) => conexao?.on(nome, (dados: Record<string, unknown>) => receber(nome, dados)));
  conexao.onreconnecting(() => publicar({ conectado: false }));
  conexao.onreconnected(() => {
    publicar({ conectado: true });
    void consultas.invalidateQueries();
  });
  conexao.onclose(() => publicar({ conectado: false }));
  conexao
    .start()
    .then(() => publicar({ conectado: conexao?.state === HubConnectionState.Connected }))
    .catch(() => publicar({ conectado: false }));
}

export function desconectar(): void {
  void conexao?.stop();
  conexao = null;
  publicar({ conectado: false, eventos: [] });
}

/** Só para os testes: entrega um evento como se viesse do hub. */
export function simularEvento(nome: NomeDoEvento, dados: Record<string, unknown>): void {
  receber(nome, dados);
}

export function useTempoReal(): Estado {
  return useSyncExternalStore(
    (ouvinte) => {
      ouvintes.add(ouvinte);
      conectar();
      return () => ouvintes.delete(ouvinte);
    },
    () => estado,
  );
}
