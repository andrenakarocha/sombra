// Efeito local (otimista) de uma operação da outbox. Roda ao enfileirar e de novo depois de cada retrato do
// servidor, enquanto a operação estiver pendente: é um "rebase" das escritas locais sobre o estado do Core.

import { FixedClock } from '@/domain/compartilhado/clock';

import type { Operacao } from '../ports/outbox';
import type { AlertaRepository, CasaRepository } from '../ports/repositorios';

type Repos = { casas: CasaRepository; alertas: AlertaRepository };

// Devolve o que foi tocado, para avisar as telas certas.
export type Afetados = { domicilioId: string | null; alertaId: string | null };

export async function aplicarOperacao(op: Operacao, { casas, alertas }: Repos): Promise<Afetados> {
  // O relógio da operação é o do toque: reaplicar gera o mesmo evento, e assim dá para saber se já foi aplicada.
  const clock = FixedClock.at(op.payload.momento);
  const jaAplicada = (linha: { momento: string }[]) => linha.some((e) => e.momento === clock.now().toISOString());

  if (op.tipo === 'CHECK_IN') {
    const { domicilioId, situacao, momento } = op.payload;
    await casas.registrarCheckIn(domicilioId, momento);
    const alertaId = (await casas.buscar(domicilioId))?.alertaAberto?.id;
    const salvo = alertaId ? await alertas.buscar(alertaId) : null;
    if (!salvo || jaAplicada(salvo.alerta.dados.linhaDoTempo)) return { domicilioId, alertaId: alertaId ?? null };
    const r = salvo.alerta.checkIn(situacao, 'APP', clock);
    if (r.ok) await alertas.salvar(r.valor, salvo.apelidoDaCasa);
    return { domicilioId, alertaId: alertaId ?? null };
  }

  const salvo = await alertas.buscar(op.payload.alertaId);
  const afetados = { domicilioId: salvo?.alerta.dados.domicilioId ?? null, alertaId: op.payload.alertaId };
  if (!salvo || jaAplicada(salvo.alerta.dados.linhaDoTempo)) return afetados;
  const r = salvo.alerta.reconhecer(op.payload.acao, clock);
  if (r.ok) await alertas.salvar(r.valor, salvo.apelidoDaCasa);
  return afetados;
}
