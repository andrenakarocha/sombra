import { AlertaDeCalor } from '@/domain/alerta/alerta-de-calor';
import { FixedClock } from '@/domain/compartilhado/clock';
import { type Result, valorOuErro } from '@/domain/compartilhado/result';
import type { NivelDeRisco } from '@/domain/clima/nivel-de-risco';
import { fatoresEfetivos } from '@/domain/morador/fator-de-risco';
import { RiscoPersonalizado } from '@/domain/risco/risco-personalizado';

const INICIO = '2026-11-18T18:00:00.000Z';

function abrir(nivel: NivelDeRisco = 'PERIGO', temVizinho = true, tipo: 'DE_SENTINELA' | 'PREVENTIVO' = 'DE_SENTINELA') {
  const clock = FixedClock.at(INICIO);
  const alerta = valorOuErro(AlertaDeCalor.abrir({ id: 'a1', domicilioId: 'd1', tipo, nivel, temVizinho, clock }));
  return { clock, alerta };
}
const regra = (r: Result<unknown>) => (r.ok ? null : r.erro.codigo);

describe('abrir (R6)', () => {
  it.each([
    ['CAUTELA_EXTREMA', '2026-11-18T18:30:00.000Z'],
    ['PERIGO', '2026-11-18T18:15:00.000Z'],
    ['PERIGO_EXTREMO', '2026-11-18T18:15:00.000Z'],
  ] as const)('%s: prazo do "estou bem"', (nivel, prazo) => {
    const { alerta } = abrir(nivel);
    expect(alerta.dados.estado).toBe('AGUARDANDO_CHECKIN');
    expect(alerta.dados.prazo).toBe(prazo);
    expect(alerta.dados.linhaDoTempo).toHaveLength(1);
  });

  it('abaixo de CAUTELA_EXTREMA a Sentinela não pede "estou bem"', () => {
    const clock = FixedClock.at(INICIO);
    const r = AlertaDeCalor.abrir({ id: 'a', domicilioId: 'd', tipo: 'DE_SENTINELA', nivel: 'ATENCAO', temVizinho: true, clock });
    expect(regra(r)).toBe('R6');
  });

  it('o preventivo abre em qualquer nível, com prazo informativo de 24 h', () => {
    const { alerta } = abrir('ATENCAO', true, 'PREVENTIVO');
    expect(alerta.dados.prazo).toBe('2026-11-19T18:00:00.000Z');
    expect(alerta.dados.linhaDoTempo[0]!.descricao).toMatch(/Previsão/);
  });
});

describe('escalonar (R8, D13)', () => {
  it('não sobe antes do prazo', () => {
    const { alerta, clock } = abrir();
    clock.avancarMinutos(14);
    expect(regra(alerta.escalonar(clock))).toBe('R8');
  });

  it('com perigo e vizinho: 1 família → 2 vizinha → 3 rede, a cada 10 min', () => {
    const { alerta, clock } = abrir('PERIGO');
    clock.avancarMinutos(15);
    const d1 = valorOuErro(alerta.escalonar(clock));
    expect([d1.alerta.dados.degrau, d1.destinatario]).toEqual([1, 'FAMILIAR']);
    expect(d1.alerta.minutosAteProximoDegrau(clock)).toBe(10);

    clock.avancarMinutos(10);
    const d2 = valorOuErro(d1.alerta.escalonar(clock));
    expect([d2.alerta.dados.degrau, d2.destinatario]).toEqual([2, 'VIZINHO']);

    clock.avancarMinutos(10);
    const d3 = valorOuErro(d2.alerta.escalonar(clock));
    expect([d3.alerta.dados.degrau, d3.destinatario]).toEqual([3, 'REDE']);
    expect(d3.alerta.minutosAteProximoDegrau(clock)).toBeNull();

    clock.avancarMinutos(10);
    expect(regra(d3.alerta.escalonar(clock))).toBe('R8');
    expect(d3.alerta.dados.linhaDoTempo.map((e) => e.degrau)).toEqual([0, 1, 2, 3]);
  });

  it('sem vizinho, pula do 1 direto para o 3', () => {
    const { alerta, clock } = abrir('PERIGO', false);
    clock.avancarMinutos(15);
    const d1 = valorOuErro(alerta.escalonar(clock)).alerta;
    clock.avancarMinutos(10);
    expect(valorOuErro(d1.escalonar(clock)).destinatario).toBe('REDE');
  });

  it('sem perigo (CAUTELA_EXTREMA), nunca passa do lembrete leve', () => {
    const { alerta, clock } = abrir('CAUTELA_EXTREMA');
    clock.avancarMinutos(30);
    const d1 = valorOuErro(alerta.escalonar(clock)).alerta;
    expect(d1.degrauMaximo).toBe(1);
    expect(d1.minutosAteProximoDegrau(clock)).toBeNull();
    clock.avancarMinutos(60);
    const r = d1.escalonar(clock);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.erro.mensagem).toMatch(/lembrete leve/);
  });

  it('depois que alguém reconhece, o escalonamento para', () => {
    const { alerta, clock } = abrir();
    const reconhecido = valorOuErro(alerta.reconhecer('ESTOU_INDO', clock));
    expect(reconhecido.dados.estado).toBe('RECONHECIDO');
    expect(reconhecido.minutosAteProximoDegrau(clock)).toBeNull();
    clock.avancarMinutos(60);
    expect(regra(reconhecido.escalonar(clock))).toBe('R8');
  });
});

describe('check-in e reconhecimento (R5, R7)', () => {
  it('"estou bem" resolve e mede o tempo até o check-in', () => {
    const { alerta, clock } = abrir();
    clock.avancarMinutos(6);
    const resolvido = valorOuErro(alerta.checkIn('ESTOU_BEM', 'APP', clock));
    expect(resolvido.dados.resolvidoPor).toBe('CHECKIN_APP');
    expect(resolvido.minutosAteResolver()).toBe(6);
    expect(resolvido.minutosAteProximoDegrau(clock)).toBeNull();
  });

  it('o botão da Sentinela também resolve', () => {
    const { alerta, clock } = abrir();
    expect(valorOuErro(alerta.checkIn('ESTOU_BEM', 'SENTINELA', clock)).dados.resolvidoPor).toBe('CHECKIN_SENTINELA');
  });

  it('pedido de ajuda vai direto ao degrau 3, mesmo sem perigo', () => {
    const { alerta, clock } = abrir('CAUTELA_EXTREMA');
    const ajuda = valorOuErro(alerta.checkIn('PRECISO_DE_AJUDA', 'APP', clock));
    expect([ajuda.dados.degrau, ajuda.dados.estado, ajuda.degrauMaximo]).toEqual([3, 'ESCALONADO', 3]);
  });

  it.each(['VOU_LIGAR', 'ESTOU_INDO'] as const)('%s marca como reconhecido', (acao) => {
    const { alerta, clock } = abrir();
    expect(valorOuErro(alerta.reconhecer(acao, clock)).dados.linhaDoTempo.at(-1)!.descricao).toMatch(/família/);
  });

  it('"ela está bem" resolve pelo cuidador', () => {
    const { alerta, clock } = abrir();
    expect(valorOuErro(alerta.reconhecer('ELA_ESTA_BEM', clock)).dados.resolvidoPor).toBe('CUIDADOR');
  });

  it('um alerta resolvido não aceita mais nada', () => {
    const { alerta, clock } = abrir();
    const resolvido = valorOuErro(alerta.checkIn('ESTOU_BEM', 'APP', clock));
    expect(regra(resolvido.checkIn('ESTOU_BEM', 'APP', clock))).toBe('R5');
    expect(regra(resolvido.reconhecer('VOU_LIGAR', clock))).toBe('R5');
    expect(regra(resolvido.escalonar(clock))).toBe('R5');
    expect(regra(resolvido.atualizarNivel('PERIGO_EXTREMO', clock))).toBe('R5');
    expect(alerta.minutosAteResolver()).toBeNull();
  });
});

describe('atualizarNivel (R4)', () => {
  it('subir de nível encurta o prazo; descer não muda nada', () => {
    const { alerta, clock } = abrir('CAUTELA_EXTREMA');
    clock.avancarMinutos(5);
    const subiu = valorOuErro(alerta.atualizarNivel('PERIGO', clock));
    expect(subiu.dados.nivel).toBe('PERIGO');
    expect(subiu.dados.prazo).toBe('2026-11-18T18:20:00.000Z');
    expect(valorOuErro(subiu.atualizarNivel('ATENCAO', clock))).toBe(subiu);
  });

  it('subir perto do fim não estica o prazo', () => {
    const { alerta, clock } = abrir('CAUTELA_EXTREMA');
    clock.avancarMinutos(25);
    expect(valorOuErro(alerta.atualizarNivel('PERIGO', clock)).dados.prazo).toBe('2026-11-18T18:30:00.000Z');
  });

  it('restaurar reconstitui sem compartilhar a linha do tempo', () => {
    const { alerta, clock } = abrir();
    const copia = AlertaDeCalor.restaurar(alerta.dados);
    valorOuErro(copia.reconhecer('VOU_LIGAR', clock));
    expect(alerta.dados.linhaDoTempo).toHaveLength(1);
  });
});

describe('risco personalizado (R3)', () => {
  it('sem fatores, os limiares são os da NWS', () => {
    const risco = RiscoPersonalizado.para([], 70);
    expect(risco.limiares).toEqual({ ATENCAO: 26.67, CAUTELA_EXTREMA: 32.22, PERIGO: 39.44, PERIGO_EXTREMO: 51.67 });
    expect(risco.antecipacaoDoPerigoC).toBe(0);
    expect(risco.contribuicoes).toEqual([]);
  });

  it('cada fator explica quanto baixou o limiar, e 80 anos conta sozinho', () => {
    const risco = RiscoPersonalizado.para(['USO_DE_DIURETICO'], 84);
    expect(risco.contribuicoes.map((c) => [c.fator, c.reducaoC])).toEqual([
      ['IDADE_AVANCADA', 2],
      ['USO_DE_DIURETICO', 1.5],
    ]);
    expect(risco.limiares.PERIGO).toBe(35.94);
    expect(risco.antecipacaoDoPerigoC).toBe(3.5);
    expect(risco.contribuicoes.every((c) => c.porque.length > 10)).toBe(true);
  });

  it('o mesmo calor é PERIGO para quem tem fatores e só CAUTELA_EXTREMA para quem não tem', () => {
    const avaliacao = RiscoPersonalizado.para(['CARDIOPATIA', 'USO_DE_DIURETICO'], 84).avaliar(36.5);
    expect(avaliacao).toEqual({ padrao: 'CAUTELA_EXTREMA', personalizado: 'PERIGO', folgaAtePerigoC: -3.1 });
  });

  it('o piso é o início de ATENCAO (26,67 °C)', () => {
    const risco = RiscoPersonalizado.para(['IDADE_AVANCADA', 'CARDIOPATIA', 'USO_DE_DIURETICO'], 90);
    expect(risco.limiares.CAUTELA_EXTREMA).toBe(26.67);
    expect(risco.limiares.PERIGO).toBe(33.44);
  });

  it('fatoresEfetivos não duplica e mantém a ordem', () => {
    expect(fatoresEfetivos(['USO_DE_DIURETICO', 'IDADE_AVANCADA'], 85)).toEqual(['IDADE_AVANCADA', 'USO_DE_DIURETICO']);
  });
});
