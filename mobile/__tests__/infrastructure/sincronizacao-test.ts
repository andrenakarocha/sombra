import { SqlJsDatabase } from '@test/sql-js-database';

import { ErroDaApi, SemConexao } from '@/application/erros';
import type { EnviadorDeOperacoes, Operacao } from '@/application/ports/outbox';
import { atrasoDaTentativa, BASE_MS, MAXIMO_DE_TENTATIVAS, TETO_MS } from '@/application/sincronizacao/backoff';
import { MotorDeSincronizacao, transitorio } from '@/application/sincronizacao/motor';
import { FixedClock } from '@/domain/compartilhado/clock';
import { migrar } from '@/infrastructure/database/migracoes';
import { OutboxRepositorySqlite } from '@/infrastructure/database/repositorios/outbox-repository';

// Um Core de mentira que só conta o que recebeu e falha do jeito que o teste mandar.
class CoreFalso implements EnviadorDeOperacoes {
  recebidas: string[] = [];
  falha: unknown = null;

  async enviar(op: Operacao) {
    if (this.falha) throw this.falha;
    this.recebidas.push(op.operationId);
  }
}

async function montar() {
  const db = await SqlJsDatabase.abrir();
  await migrar(db);
  const outbox = new OutboxRepositorySqlite(db);
  const clock = FixedClock.at('2026-11-18T18:00:00Z');
  const core = new CoreFalso();
  const motor = new MotorDeSincronizacao(outbox, core, clock, () => 0.5);
  const checkIn = (id: string) =>
    outbox.enfileirar({
      operationId: id,
      tipo: 'CHECK_IN',
      payload: { domicilioId: 'd1', situacao: 'ESTOU_BEM', momento: clock.now().toISOString() },
      criadoEm: clock.now().toISOString(),
    });
  return { outbox, clock, core, motor, checkIn };
}

describe('backoff exponencial com jitter', () => {
  it('dobra a cada tentativa, metade fixa e metade sorteada', () => {
    expect(atrasoDaTentativa(1, () => 0)).toBe(BASE_MS / 2);
    expect(atrasoDaTentativa(1, () => 1)).toBe(BASE_MS);
    expect(atrasoDaTentativa(3, () => 0.5)).toBe(6_000);
  });

  it('para no teto de 5 minutos', () => {
    expect(atrasoDaTentativa(30, () => 1)).toBe(TETO_MS);
  });

  it('sem sorteio injetado usa Math.random dentro da faixa', () => {
    const atraso = atrasoDaTentativa(2);
    expect(atraso).toBeGreaterThanOrEqual(2_000);
    expect(atraso).toBeLessThanOrEqual(4_000);
  });
});

describe('o que vale repetir', () => {
  it.each([
    [new SemConexao(), true],
    [new ErroDaApi(503, 'Indisponível', 'x'), true],
    [new ErroDaApi(429, 'Limite', 'x'), true],
    [new ErroDaApi(401, 'Sessão vencida', 'x'), true],
    [new ErroDaApi(422, 'Regra', 'x'), false],
    [new ErroDaApi(404, 'Não achou', 'x'), false],
    [new Error('bug'), false],
  ])('%p → %p', (erro, esperado) => {
    expect(transitorio(erro)).toBe(esperado);
  });
});

describe('motor de sincronização', () => {
  it('modo avião: fica pendente com backoff; a rede volta: sai uma vez só', async () => {
    const { outbox, clock, core, motor, checkIn } = await montar();
    await checkIn('op-1');
    core.falha = new SemConexao();
    expect(await motor.drenar()).toEqual({ enviadas: 0, reagendadas: 1, deadLetter: 0, semRede: true });
    const [pendente] = await outbox.listar();
    expect([pendente!.tentativas, pendente!.ultimoErro, pendente!.proximaTentativaEm]).toEqual([
      1,
      'sem conexão',
      '2026-11-18T18:00:01.500Z',
    ]);

    core.falha = null;
    expect((await motor.drenar()).enviadas).toBe(0); // ainda não chegou a hora da próxima tentativa
    clock.avancarMinutos(1);
    expect((await motor.drenar()).enviadas).toBe(1);
    expect(await motor.drenar()).toEqual({ enviadas: 0, reagendadas: 0, deadLetter: 0, semRede: false });
    expect(core.recebidas).toEqual(['op-1']);
    expect(await outbox.contar()).toEqual({ pendentes: 0, deadLetter: 0 });
  });

  it('sem rede, para no primeiro e não gasta tentativa dos outros', async () => {
    const { outbox, core, motor, checkIn } = await montar();
    await checkIn('op-1');
    await checkIn('op-2');
    core.falha = new SemConexao();
    await motor.drenar();
    const ops = await outbox.listar();
    expect(ops.map((o) => [o.operationId, o.tentativas]).sort()).toEqual([
      ['op-1', 1],
      ['op-2', 0],
    ]);
  });

  it('erro definitivo vai direto para a dead-letter, e os seguintes continuam', async () => {
    const { outbox, core, motor, checkIn } = await montar();
    await checkIn('op-1');
    await checkIn('op-2');
    let primeira = true;
    core.enviar = async (op) => {
      if (primeira) {
        primeira = false;
        throw new ErroDaApi(422, 'Alerta já resolvido', 'abcdef12');
      }
      core.recebidas.push(op.operationId);
    };
    expect(await motor.drenar()).toEqual({ enviadas: 1, reagendadas: 0, deadLetter: 1, semRede: false });
    const [dead] = await outbox.listar();
    expect([dead!.estado, dead!.ultimoErro]).toEqual(['DEAD_LETTER', '422 Alerta já resolvido (abcdef12)']);
    expect(await outbox.contar()).toEqual({ pendentes: 0, deadLetter: 1 });
  });

  it(`depois de ${MAXIMO_DE_TENTATIVAS} tentativas transitórias, desiste para a dead-letter`, async () => {
    const { outbox, clock, core, motor, checkIn } = await montar();
    await checkIn('op-1');
    core.falha = new ErroDaApi(503, 'Indisponível', 'x');
    for (let i = 0; i < MAXIMO_DE_TENTATIVAS; i++) {
      await motor.drenar();
      clock.avancarMinutos(10);
    }
    const [op] = await outbox.listar();
    expect([op!.estado, op!.tentativas]).toEqual(['DEAD_LETTER', MAXIMO_DE_TENTATIVAS]);
  });

  it('reenviar tira da dead-letter; descartar apaga', async () => {
    const { outbox, clock, core, motor, checkIn } = await montar();
    await checkIn('op-1');
    await checkIn('op-2');
    core.falha = new ErroDaApi(404, 'Não achou', 'x');
    await motor.drenar();
    await outbox.reenviar('op-1', clock.now());
    await outbox.descartar('op-2');
    core.falha = null;
    expect((await motor.drenar()).enviadas).toBe(1);
    expect(core.recebidas).toEqual(['op-1']);
    expect(await outbox.listar()).toEqual([]);
  });

  it('duas drenagens ao mesmo tempo enviam uma vez só', async () => {
    const { core, motor, checkIn } = await montar();
    await checkIn('op-1');
    await Promise.all([motor.drenar(), motor.drenar(), motor.drenar()]);
    expect(core.recebidas).toEqual(['op-1']);
  });

  it('o mesmo operationId enfileirado de novo é ignorado (idempotência local)', async () => {
    const { outbox, checkIn } = await montar();
    await checkIn('op-1');
    await checkIn('op-1');
    expect(await outbox.contar()).toEqual({ pendentes: 1, deadLetter: 0 });
  });

  it('forçar offline segura tudo na fila e avisa quem está ouvindo', async () => {
    const { core, motor, checkIn } = await montar();
    const ouvinte = jest.fn();
    const parar = motor.aoMudar(ouvinte);
    motor.definirForcadoOffline(true);
    expect(motor.offlineForcado).toBe(true);
    await checkIn('op-1');
    expect(await motor.drenar()).toEqual({ enviadas: 0, reagendadas: 0, deadLetter: 0, semRede: true });
    motor.definirForcadoOffline(false);
    await motor.drenar();
    expect(core.recebidas).toEqual(['op-1']);
    expect(ouvinte).toHaveBeenCalledTimes(3);
    parar();
  });

  it('compacta as enviadas antigas', async () => {
    const { outbox, clock, motor, checkIn } = await montar();
    await checkIn('op-1');
    await motor.drenar();
    clock.avancarMinutos(60 * 24 * 8);
    expect(await outbox.compactarEnviadasAntesDe(new Date(clock.now().getTime() - 7 * 24 * 60 * 60_000))).toBe(1);
  });
});
