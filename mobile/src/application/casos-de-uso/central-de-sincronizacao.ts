// Central de sincronização: o que está esperando a rede, o que o Core recusou, e o controle de "forçar offline"
// para demonstrar o local-first sem desligar o Wi-Fi.

import type { Clock } from '@/domain/compartilhado/clock';

import type { Operacao, OutboxRepository } from '../ports/outbox';
import type { EstadoDoSegundoPlano, SincronizacaoEmSegundoPlano } from '../ports/servicos';
import type { MotorDeSincronizacao, ResumoDaDrenagem } from '../sincronizacao/motor';

const SEMANA_MS = 7 * 24 * 60 * 60 * 1000;

export type ResumoDaCentral = {
  operacoes: Operacao[];
  pendentes: number;
  deadLetter: number;
  offlineForcado: boolean;
  segundoPlano: EstadoDoSegundoPlano | null;
};

export class CentralDeSincronizacao {
  private segundoPlano: EstadoDoSegundoPlano | null = null;

  constructor(
    private readonly d: {
      outbox: OutboxRepository;
      motor: MotorDeSincronizacao;
      worker: SincronizacaoEmSegundoPlano;
      clock: Clock;
    },
  ) {}

  // Ao abrir o app: registra o worker e joga fora o histórico de enviadas com mais de uma semana.
  async iniciar() {
    this.segundoPlano = await this.d.worker.registrar().catch(() => 'INDISPONIVEL' as const);
    await this.d.outbox.compactarEnviadasAntesDe(new Date(this.d.clock.now().getTime() - SEMANA_MS));
  }

  async resumo(): Promise<ResumoDaCentral> {
    const [operacoes, contagem] = await Promise.all([this.d.outbox.listar(), this.d.outbox.contar()]);
    return { operacoes, ...contagem, offlineForcado: this.d.motor.offlineForcado, segundoPlano: this.segundoPlano };
  }

  tentarAgora(): Promise<ResumoDaDrenagem> {
    return this.d.motor.drenar();
  }

  async reenviar(operationId: string) {
    await this.d.outbox.reenviar(operationId, this.d.clock.now());
    return this.d.motor.drenar();
  }

  async descartar(operationId: string) {
    await this.d.outbox.descartar(operationId);
  }

  forcarOffline(valor: boolean) {
    this.d.motor.definirForcadoOffline(valor);
    if (!valor) void this.d.motor.drenar();
  }
}
