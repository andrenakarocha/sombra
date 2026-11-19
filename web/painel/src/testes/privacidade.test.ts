import { normalizarSink, rotuloDaCategoria, rotuloDoSink, seriePorSink } from '@/dominio/privacidade';
import type { EventoDeSeguranca } from '@/api/schemas';

const evento = (momento: string, sink: string): EventoDeSeguranca => ({
  id: momento + sink, momento, tipo: 'ACESSO_NEGADO', origem: 'x', sink, categoria: 'SAUDE', severidade: 'ALTA',
  regra: 'acesso-negado', bloqueado: true, domicilioId: null, correlationId: null, detalhe: null, traducao: null,
});

describe('Risco Cidadão no painel', () => {
  it('fala o sink do mesmo jeito nas contagens e nos eventos', () => {
    expect(normalizarSink('RespostaHttp')).toBe('RESPOSTA_HTTP');
    expect(normalizarSink('RESPOSTA_HTTP')).toBe('RESPOSTA_HTTP');
    expect(normalizarSink('SaidaExterna')).toBe('SAIDA_EXTERNA');
  });

  it('conta os bloqueios por hora, das últimas 24 h, por sink', () => {
    const agora = Date.parse('2026-11-18T18:30:00Z');
    const series = seriePorSink(
      [evento('2026-11-18T18:05:00Z', 'RESPOSTA_HTTP'), evento('2026-11-18T17:59:00Z', 'RESPOSTA_HTTP'), evento('2026-11-18T10:00:00Z', 'LOG'), evento('2026-11-17T10:00:00Z', 'LOG')],
      agora,
    );
    const http = series.find((s) => s.sink === 'RESPOSTA_HTTP')!;
    expect(http.total).toBe(2);
    expect(http.pontos.at(-1)?.eventos).toBe(1);
    expect(http.pontos.at(-2)?.eventos).toBe(1);
    expect(series.find((s) => s.sink === 'LOG')?.total).toBe(1);
    expect(series).toHaveLength(6);
  });

  it('evento de integridade, sem sink nem família de dado, ganha um rótulo próprio', () => {
    expect(rotuloDoSink(null, 'MQTT_ASSINATURA_INVALIDA')).toBe('Assinatura MQTT inválida');
    expect(rotuloDaCategoria(null)).toBe('Integridade');
    expect(seriePorSink([{ ...evento('2026-11-18T18:05:00Z', 'LOG'), sink: null }], Date.parse('2026-11-18T18:30:00Z')).every((s) => s.total === 0)).toBe(true);
  });
});
