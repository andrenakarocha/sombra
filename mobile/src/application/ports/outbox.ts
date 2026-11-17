// Outbox única: toda escrita que pode esperar a rede passa por aqui, com um operationId que o Core usa como
// Idempotency-Key. Assim uma operação reenviada depois de uma resposta perdida nunca vale duas vezes.

import type { AcaoDoCuidador } from '@/domain/alerta/alerta-de-calor';

export type PayloadDe = {
  CHECK_IN: { domicilioId: string; situacao: 'ESTOU_BEM' | 'PRECISO_DE_AJUDA'; momento: string };
  RECONHECER_ALERTA: { alertaId: string; acao: AcaoDoCuidador; momento: string };
};
export type TipoDeOperacao = keyof PayloadDe;

export type EstadoDaOperacao = 'PENDENTE' | 'ENVIADO' | 'DEAD_LETTER';

export type Operacao<T extends TipoDeOperacao = TipoDeOperacao> = {
  [K in T]: {
    operationId: string;
    tipo: K;
    payload: PayloadDe[K];
    criadoEm: string;
    tentativas: number;
    proximaTentativaEm: string;
    estado: EstadoDaOperacao;
    ultimoErro: string | null;
  };
}[T];

export interface OutboxRepository {
  enfileirar(op: Pick<Operacao, 'operationId' | 'tipo' | 'payload' | 'criadoEm'>): Promise<void>;
  // Pendentes cujo horário de tentativa já chegou, na ordem em que foram criadas.
  prontas(agora: Date): Promise<Operacao[]>;
  marcarEnviada(operationId: string): Promise<void>;
  reagendar(operationId: string, tentativas: number, proxima: Date, erro: string): Promise<void>;
  moverParaDeadLetter(operationId: string, tentativas: number, erro: string): Promise<void>;
  reenviar(operationId: string, agora: Date): Promise<void>;
  descartar(operationId: string): Promise<void>;
  listar(): Promise<Operacao[]>;
  contar(): Promise<{ pendentes: number; deadLetter: number }>;
  compactarEnviadasAntesDe(limite: Date): Promise<number>;
}

export interface EnviadorDeOperacoes {
  enviar(op: Operacao): Promise<void>;
}

export interface GeradorDeId {
  novo(): string;
}
