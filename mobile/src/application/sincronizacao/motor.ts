// Motor de sincronização: drena a outbox em ordem, com backoff, dead-letter e uma drenagem por vez.

import type { Clock } from '@/domain/compartilhado/clock';

import { ErroDaApi, SemConexao } from '../erros';
import type { EnviadorDeOperacoes, OutboxRepository } from '../ports/outbox';
import { atrasoDaTentativa, MAXIMO_DE_TENTATIVAS } from './backoff';

export type ResumoDaDrenagem = { enviadas: number; reagendadas: number; deadLetter: number; semRede: boolean };

// Vale tentar de novo: sem rede, tempo esgotado, sessão vencida (o usuário entra de novo), limite ou Core fora do ar.
export function transitorio(erro: unknown) {
  if (erro instanceof SemConexao) return true;
  return erro instanceof ErroDaApi && (erro.status >= 500 || [401, 408, 425, 429].includes(erro.status));
}

const descrever = (erro: unknown) =>
  erro instanceof ErroDaApi ? `${erro.status} ${erro.titulo} (${erro.codigo})` : erro instanceof SemConexao ? 'sem conexão' : 'erro inesperado';

export class MotorDeSincronizacao {
  private emAndamento: Promise<ResumoDaDrenagem> | null = null;
  private repetir: Promise<ResumoDaDrenagem> | null = null;
  private forcadoOffline = false;
  private readonly ouvintes = new Set<() => void>();

  constructor(
    private readonly outbox: OutboxRepository,
    private readonly enviador: EnviadorDeOperacoes,
    private readonly clock: Clock,
    private readonly sorteio: () => number = Math.random,
  ) {}

  // Para a Central de sincronização simular uma queda sem desligar o Wi-Fi.
  definirForcadoOffline(valor: boolean) {
    this.forcadoOffline = valor;
    this.avisar();
  }

  get offlineForcado() {
    return this.forcadoOffline;
  }

  aoMudar(ouvinte: () => void) {
    this.ouvintes.add(ouvinte);
    return () => this.ouvintes.delete(ouvinte);
  }

  private avisar() {
    for (const ouvinte of this.ouvintes) ouvinte();
  }

  // Uma drenagem por vez. Pedir outra durante uma em andamento agenda mais uma rodada logo depois (compartilhada por
  // todos que pediram): uma operação enfileirada no meio não fica esperando a próxima rede.
  drenar(): Promise<ResumoDaDrenagem> {
    if (this.emAndamento) {
      this.repetir ??= this.emAndamento.then(() => {
        this.repetir = null;
        return this.drenar();
      });
      return this.repetir;
    }
    this.emAndamento = this.executar().finally(() => {
      this.emAndamento = null;
    });
    return this.emAndamento;
  }

  private async executar(): Promise<ResumoDaDrenagem> {
    const resumo: ResumoDaDrenagem = { enviadas: 0, reagendadas: 0, deadLetter: 0, semRede: this.forcadoOffline };
    if (this.forcadoOffline) return resumo;
    for (const op of await this.outbox.prontas(this.clock.now())) {
      try {
        await this.enviador.enviar(op);
        await this.outbox.marcarEnviada(op.operationId);
        resumo.enviadas++;
      } catch (erro) {
        const tentativas = op.tentativas + 1;
        if (!transitorio(erro) || tentativas >= MAXIMO_DE_TENTATIVAS) {
          await this.outbox.moverParaDeadLetter(op.operationId, tentativas, descrever(erro));
          resumo.deadLetter++;
        } else {
          const proxima = new Date(this.clock.now().getTime() + atrasoDaTentativa(tentativas, this.sorteio));
          await this.outbox.reagendar(op.operationId, tentativas, proxima, descrever(erro));
          resumo.reagendadas++;
        }
        // Sem rede, as próximas também vão falhar: para e espera a rede voltar.
        if (erro instanceof SemConexao) {
          resumo.semRede = true;
          break;
        }
      }
    }
    if (resumo.enviadas + resumo.reagendadas + resumo.deadLetter > 0) this.avisar();
    return resumo;
  }
}
