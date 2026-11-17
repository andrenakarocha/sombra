import { ehNoite, FixedClock, horaLocal, relogioDoSistema } from '@/domain/compartilhado/clock';
import { valorOuErro } from '@/domain/compartilhado/result';
import { CartaoSus } from '@/domain/morador/cartao-sus';
import { Cpf } from '@/domain/morador/cpf';
import { Telefone } from '@/domain/morador/telefone';

// Documentos sintéticos: os dígitos verificadores são calculados aqui, nenhum número é de alguém.
function cpfSintetico(base: string) {
  const dv = (d: string) => (([...d].reduce((s, c, i) => s + Number(c) * (d.length + 1 - i), 0) * 10) % 11) % 10;
  const primeiro = base + dv(base);
  return primeiro + dv(primeiro);
}
function cnsSintetico(prefixo11: string) {
  // Procura o sufixo de 4 dígitos que fecha a soma ponderada em múltiplo de 11.
  for (let s = 0; s < 10000; s++) {
    const d = prefixo11 + String(s).padStart(4, '0');
    if ([...d].reduce((t, c, i) => t + Number(c) * (15 - i), 0) % 11 === 0) return d;
  }
  throw new Error('sem CNS');
}

const erro = <T>(r: { ok: boolean; erro?: { codigo: string } } & T) => (r.ok ? null : r.erro!.codigo);

describe('Cpf', () => {
  const valido = cpfSintetico('123456789');

  it('aceita com ou sem pontuação e só mostra o meio', () => {
    expect(valorOuErro(Cpf.criar(valido)).mascarado).toBe('***.456.789-**');
    expect(valorOuErro(Cpf.criar('123.456.789-09')).paraEnvio()).toBe('12345678909');
  });

  it.each([
    ['123', 'CPF_TAMANHO'],
    ['111.111.111-11', 'CPF_REPETIDO'],
    ['123.456.789-00', 'CPF_DIGITO'],
  ])('%s → %s', (texto, codigo) => {
    expect(erro(Cpf.criar(texto))).toBe(codigo);
  });
});

describe('CartaoSus', () => {
  it.each(['1', '2', '7', '8', '9'])('aceita o CNS que começa com %s e mascara', (inicio) => {
    const cns = cnsSintetico(`${inicio}0000000000`);
    const cartao = valorOuErro(CartaoSus.criar(cns));
    expect(cartao.mascarado).toBe(`*** **** **** ${cns.slice(-4)}`);
    expect(cartao.paraEnvio()).toBe(cns);
  });

  it.each([
    ['1234', 'CNS_TAMANHO'],
    ['300000000000000', 'CNS_INICIO'],
    ['700000000000001', 'CNS_DIGITO'],
  ])('%s → %s', (texto, codigo) => {
    expect(erro(CartaoSus.criar(texto))).toBe(codigo);
  });
});

describe('Telefone', () => {
  it('celular e fixo com DDD', () => {
    const cel = valorOuErro(Telefone.criar('(11) 98765-4321'));
    expect(cel.mascarado).toBe('(11) *****-4321');
    expect(cel.discavel).toBe('11987654321');
    expect(valorOuErro(Telefone.criar('1132654321')).paraEnvio()).toBe('1132654321');
  });

  it.each([
    ['98765', 'TELEFONE_TAMANHO'],
    ['(01) 98765-4321', 'TELEFONE_DDD'],
    ['(20) 98765-4321', 'TELEFONE_DDD'],
    ['(11) 88765-4321', 'TELEFONE_CELULAR'],
  ])('%s → %s', (texto, codigo) => {
    expect(erro(Telefone.criar(texto))).toBe(codigo);
  });
});

describe('Clock', () => {
  it('FixedClock avança e devolve cópias', () => {
    const relogio = FixedClock.at('2026-11-18T18:00:00Z');
    const agora = relogio.now();
    agora.setFullYear(2000);
    relogio.avancarMinutos(30);
    expect(relogio.now().toISOString()).toBe('2026-11-18T18:30:00.000Z');
  });

  it('hora de São Paulo e a regra da noite (22h–7h)', () => {
    expect(horaLocal(new Date('2026-11-18T18:00:00Z'))).toBe(15);
    expect(ehNoite(new Date('2026-11-19T01:00:00Z'))).toBe(true); // 22h
    expect(ehNoite(new Date('2026-11-19T09:59:00Z'))).toBe(true); // 6h59
    expect(ehNoite(new Date('2026-11-19T10:00:00Z'))).toBe(false); // 7h
  });

  it('o relógio do sistema é o de agora', () => {
    expect(Math.abs(relogioDoSistema.now().getTime() - Date.now())).toBeLessThan(1000);
  });
});
