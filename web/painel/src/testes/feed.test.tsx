import { descrever } from '@/componentes/FeedAoVivo';
import { rotuloDoEstado } from '@/dominio/niveis';
import type { EventoAoVivo } from '@/api/tempo-real';

const evento = (nome: EventoAoVivo['nome'], dados: Record<string, unknown>): EventoAoVivo => ({ nome, dados, recebidoEm: 0 });

describe('frases do feed ao vivo', () => {
  it('descreve cada evento do hub sem dado pessoal', () => {
    expect(descrever(evento('AlertaAberto', { tipo: 'PREVENTIVO' }))).toMatch(/preventivo/);
    expect(descrever(evento('AlertaEscalonado', { degrau: 2, destinatario: 'VIZINHO' }))).toBe(
      'Alerta escalonado para o degrau 2 (Quem está perto): avisando quem está perto.',
    );
    expect(descrever(evento('AlertaResolvido', { minutosAteCheckIn: 6.5 }))).toBe('Alerta resolvido: "estou bem" em 6,5 min.');
    expect(descrever(evento('CheckInRegistrado', { situacao: 'PRECISO_DE_AJUDA' }))).toBe('Pedido de ajuda recebido.');
    expect(descrever(evento('LeituraRegistrada', { indiceDeCalorC: 40.68 }))).toBe('Leitura da Sentinela: índice de calor 40,7 °C.');
  });
});

describe('estado do alerta no painel', () => {
  it('o preventivo é o aviso da véspera: não pede "estou bem" (R5)', () => {
    expect(rotuloDoEstado('PREVENTIVO', 'AGUARDANDO_CHECKIN')).toBe('Aviso da véspera (não pede resposta)');
    expect(rotuloDoEstado('DE_SENTINELA', 'AGUARDANDO_CHECKIN')).toBe('Aguardando o "estou bem"');
  });
});
