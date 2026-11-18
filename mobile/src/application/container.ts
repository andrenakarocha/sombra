// O que a camada de apresentação pode usar. Quem monta é a infraestrutura (composition root).

import type { Clock } from '@/domain/compartilhado/clock';

import type { Barramento } from './barramento';
import type { CentralDeSincronizacao } from './casos-de-uso/central-de-sincronizacao';
import type { Consultas } from './casos-de-uso/consultas';
import type { Escritas } from './casos-de-uso/escritas';
import type { ServicoDeSessao } from './casos-de-uso/sessao';
import type { ProjetorDeEventos } from './eventos/projetor';
import type { OutboxRepository } from './ports/outbox';
import type { CanalDeTempoReal, MonitorDeRede, Notificador } from './ports/servicos';
import type { MotorDeSincronizacao } from './sincronizacao/motor';

export type Demonstracao = {
  simularOndaDeCalor(): Promise<void>;
  // Toca as horas reais do INMET (nov/2023), uma a cada `intervaloMs`; o estado vai para o reproducaoStore.
  reproduzirOndaReal(intervaloMs?: number): void;
  pararReproducao(): void;
  recomecar(): Promise<void>;
};

export type Container = {
  consultas: Consultas;
  escritas: Escritas;
  sessao: ServicoDeSessao;
  eventos: ProjetorDeEventos;
  sincronizacao: { motor: MotorDeSincronizacao; outbox: OutboxRepository };
  central: CentralDeSincronizacao;
  barramento: Barramento;
  tempoReal: CanalDeTempoReal;
  rede: MonitorDeRede;
  notificador: Notificador;
  demonstracao: Demonstracao;
  clock: Clock;
};
