// Tempo injetável: o domínio nunca chama `new Date()` sozinho, assim os testes controlam o relógio.

export interface Clock {
  now(): Date;
}

export const relogioDoSistema: Clock = { now: () => new Date() };

export class FixedClock implements Clock {
  private constructor(private atual: Date) {}

  static at(momento: Date | string) {
    return new FixedClock(new Date(momento));
  }

  now() {
    return new Date(this.atual);
  }

  avancarMinutos(minutos: number) {
    this.atual = new Date(this.atual.getTime() + minutos * 60_000);
  }
}

const horaEmSaoPaulo = new Intl.DateTimeFormat('pt-BR', { hour: 'numeric', hourCycle: 'h23', timeZone: 'America/Sao_Paulo' });

export function horaLocal(momento: Date): number {
  return Number(horaEmSaoPaulo.format(momento));
}

// D13: lembretes nunca entre 22h e 7h (horário de São Paulo).
export function ehNoite(momento: Date): boolean {
  const hora = horaLocal(momento);
  return hora >= 22 || hora < 7;
}

const horaEMinuto = new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit', timeZone: 'America/Sao_Paulo' });

// "15:30": o Core fala em UTC; as mensagens para a pessoa, no horário de São Paulo.
export function horarioEmSaoPaulo(momento: Date | string): string {
  return horaEMinuto.format(new Date(momento));
}
