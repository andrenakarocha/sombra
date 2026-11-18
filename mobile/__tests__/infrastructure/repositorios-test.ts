import { SqlJsDatabase } from '@test/sql-js-database';

import type { Domicilio } from '@/application/modelos';
import { AlertaDeCalor } from '@/domain/alerta/alerta-de-calor';
import { FixedClock } from '@/domain/compartilhado/clock';
import { valorOuErro } from '@/domain/compartilhado/result';
import { MIGRACOES, migrar } from '@/infrastructure/database/migracoes';
import { AlertaRepositorySqlite } from '@/infrastructure/database/repositorios/alerta-repository';
import { MoradorRepositorySqlite, RedeRepositorySqlite } from '@/infrastructure/database/repositorios/casa-detalhe-repositories';
import { CasaRepositorySqlite } from '@/infrastructure/database/repositorios/casa-repository';
import {
  apagarDadosLocais,
  PreferenciaRepositorySqlite,
  SincronizacaoRepositorySqlite,
} from '@/infrastructure/database/repositorios/chave-valor-repositories';
import { LeituraRepositorySqlite } from '@/infrastructure/database/repositorios/leitura-repository';

const casa = (id: string, extra: Partial<Domicilio> = {}): Domicilio => ({
  id,
  apelido: `Casa ${id}`,
  bairro: 'Santana',
  regiaoClimatica: 'SP-MIRANTE',
  nivelAtual: 'ATENCAO',
  ultimaLeitura: { momento: '2026-11-18T17:00:00Z', temperaturaC: 29, umidadePct: 50, indiceDeCalorC: 30.1 },
  sentinela: { deviceId: `snt-${id}`, online: true },
  ultimoCheckIn: '2026-11-18T12:00:00Z',
  alertaAberto: null,
  ...extra,
});

async function banco() {
  const db = await SqlJsDatabase.abrir();
  await migrar(db);
  return db;
}

describe('migrações', () => {
  it('banco novo chega à última versão', async () => {
    const db = await banco();
    expect((await db.first<{ user_version: number }>('PRAGMA user_version'))!.user_version).toBe(MIGRACOES.length);
  });

  it('quem já tinha a versão 3 não perde o "estou bem" que estava na fila', async () => {
    const db = await SqlJsDatabase.abrir();
    for (const [i, sql] of MIGRACOES.slice(0, 3).entries()) {
      await db.exec(sql);
      await db.exec(`PRAGMA user_version = ${i + 1}`);
    }
    await db.run("INSERT INTO fila_checkins (domicilio_id, situacao, momento) VALUES ('d1', 'ESTOU_BEM', '2026-11-18T18:05:00Z')");
    await db.run("INSERT INTO casas (id, dados, atualizado_em) VALUES ('d1', '{}', 'x')");
    await migrar(db);
    const outbox = await db.all<{ tipo: string; payload: string; estado: string }>('SELECT tipo, payload, estado FROM outbox');
    expect(outbox).toHaveLength(1);
    expect(outbox[0]!.tipo).toBe('CHECK_IN');
    expect(JSON.parse(outbox[0]!.payload)).toEqual({ domicilioId: 'd1', situacao: 'ESTOU_BEM', momento: '2026-11-18T18:05:00Z' });
    expect(await db.all('SELECT * FROM casas')).toEqual([]);
  });

  it('rodar de novo não faz nada', async () => {
    const db = await banco();
    await migrar(db);
    expect((await db.first<{ user_version: number }>('PRAGMA user_version'))!.user_version).toBe(MIGRACOES.length);
  });
});

describe('CasaRepository', () => {
  it('guarda e devolve a casa com a última leitura vinda da tabela de leituras', async () => {
    const repo = new CasaRepositorySqlite(await banco());
    await repo.salvar(casa('d1'));
    expect(await repo.buscar('d1')).toEqual(casa('d1'));
    expect(await repo.buscar('nao-existe')).toBeNull();
  });

  it('substituirTodas atualiza, insere e remove quem saiu (com as leituras em cascata)', async () => {
    const db = await banco();
    const repo = new CasaRepositorySqlite(db);
    await repo.substituirTodas([casa('d1'), casa('d2')]);
    await repo.substituirTodas([casa('d2', { apelido: 'Casa do Seu Antônio' }), casa('d3')]);
    expect((await repo.listar()).map((c) => c.apelido)).toEqual(['Casa d3', 'Casa do Seu Antônio']);
    expect(await db.all("SELECT * FROM leituras WHERE domicilio_id = 'd1'")).toEqual([]);
    await repo.substituirTodas([]);
    expect(await repo.listar()).toEqual([]);
  });

  it('registrarLeitura muda o nível e a última leitura passa a ser a nova', async () => {
    const repo = new CasaRepositorySqlite(await banco());
    await repo.salvar(casa('d1'));
    const nova = { momento: '2026-11-18T18:10:00Z', temperaturaC: 37, umidadePct: 45, indiceDeCalorC: 42.1 };
    await repo.registrarLeitura('d1', nova, 'PERIGO');
    const c = (await repo.buscar('d1'))!;
    expect([c.nivelAtual, c.ultimaLeitura]).toEqual(['PERIGO', nova]);
  });

  it('um check-in antigo que saiu atrasado não sobrescreve o mais novo', async () => {
    const repo = new CasaRepositorySqlite(await banco());
    await repo.salvar(casa('d1', { ultimoCheckIn: null }));
    await repo.registrarCheckIn('d1', '2026-11-18T18:00:00Z');
    await repo.registrarCheckIn('d1', '2026-11-18T17:00:00Z');
    expect((await repo.buscar('d1'))!.ultimoCheckIn).toBe('2026-11-18T18:00:00Z');
  });

  it('Sentinela offline e casa sem Sentinela', async () => {
    const repo = new CasaRepositorySqlite(await banco());
    await repo.salvar(casa('d1', { sentinela: null, ultimaLeitura: null }));
    expect((await repo.buscar('d1'))!.sentinela).toBeNull();
    await repo.definirSentinelaOnline('d1', 'snt-9', false);
    expect((await repo.buscar('d1'))!.sentinela).toEqual({ deviceId: 'snt-9', online: false });
  });

  it('o alerta local tem prioridade sobre o resumo do servidor, e resolvido some da casa', async () => {
    const db = await banco();
    const casas = new CasaRepositorySqlite(db);
    const alertas = new AlertaRepositorySqlite(db);
    const resumo = { id: 'a1', tipo: 'DE_SENTINELA' as const, estado: 'AGUARDANDO_CHECKIN', degrau: 0, prazo: '2026-11-18T18:15:00Z' };
    await casas.salvar(casa('d1', { alertaAberto: resumo }));
    expect((await casas.buscar('d1'))!.alertaAberto).toEqual(resumo);

    const clock = FixedClock.at('2026-11-18T18:00:00Z');
    const alerta = valorOuErro(
      AlertaDeCalor.abrir({ id: 'a1', domicilioId: 'd1', tipo: 'DE_SENTINELA', nivel: 'PERIGO', temVizinho: true, clock }),
    );
    await alertas.salvar(valorOuErro(alerta.reconhecer('VOU_LIGAR', clock)), 'Casa d1');
    expect((await casas.buscar('d1'))!.alertaAberto?.estado).toBe('RECONHECIDO');

    await alertas.salvar(valorOuErro(alerta.checkIn('ESTOU_BEM', 'APP', clock)), 'Casa d1');
    expect((await casas.buscar('d1'))!.alertaAberto).toBeNull();

    await casas.definirAlertaAberto('d1', null);
    expect((await casas.buscar('d1'))!.alertaAberto).toBeNull();
  });
});

describe('AlertaRepository', () => {
  it('guarda o alerta do domínio com a linha do tempo e restaura igual', async () => {
    const repo = new AlertaRepositorySqlite(await banco());
    const clock = FixedClock.at('2026-11-18T18:00:00Z');
    let alerta = valorOuErro(
      AlertaDeCalor.abrir({ id: 'a1', domicilioId: 'd1', tipo: 'DE_SENTINELA', nivel: 'PERIGO', temVizinho: false, clock }),
    );
    clock.avancarMinutos(15);
    alerta = valorOuErro(alerta.escalonar(clock)).alerta;
    await repo.salvar(alerta, 'Casa da Dona Lurdes');
    const salvo = (await repo.buscar('a1'))!;
    expect(salvo.apelidoDaCasa).toBe('Casa da Dona Lurdes');
    expect(salvo.alerta.dados).toEqual(alerta.dados);
    expect(await repo.buscar('x')).toBeNull();
  });

  it('lista os ativos primeiro e substituirTodos troca a lista inteira', async () => {
    const repo = new AlertaRepositorySqlite(await banco());
    const clock = FixedClock.at('2026-11-18T18:00:00Z');
    const abrir = (id: string) =>
      valorOuErro(AlertaDeCalor.abrir({ id, domicilioId: 'd1', tipo: 'DE_SENTINELA', nivel: 'PERIGO', temVizinho: true, clock }));
    const resolvido = valorOuErro(abrir('velho').checkIn('ESTOU_BEM', 'APP', clock));
    clock.avancarMinutos(60);
    await repo.substituirTodos([
      { alerta: resolvido, apelidoDaCasa: 'A' },
      { alerta: abrir('ativo'), apelidoDaCasa: 'A' },
    ]);
    expect((await repo.listar()).map((s) => s.alerta.dados.id)).toEqual(['ativo', 'velho']);
    await repo.substituirTodos([]);
    expect(await repo.listar()).toEqual([]);
  });
});

describe('LeituraRepository', () => {
  it('devolve em ordem desde o início pedido e poda as velhas sem apagar a última', async () => {
    const db = await banco();
    await new CasaRepositorySqlite(db).salvar(casa('d1', { ultimaLeitura: null }));
    const repo = new LeituraRepositorySqlite(db);
    const l = (momento: string, indice: number) => ({ momento, temperaturaC: indice - 1, umidadePct: 50, indiceDeCalorC: indice });
    await repo.inserir('d1', [l('2026-11-18T10:00:00Z', 28), l('2026-11-17T10:00:00Z', 27), l('2026-11-18T12:00:00Z', 31)]);
    expect((await repo.desde('d1', new Date('2026-11-18T00:00:00Z'))).map((x) => x.indiceDeCalorC)).toEqual([28, 31]);
    expect(await repo.podarAntesDe(new Date('2026-11-19T00:00:00Z'))).toBe(2);
    expect((await repo.desde('d1', new Date(0))).map((x) => x.indiceDeCalorC)).toEqual([31]);
  });
});

describe('morador, rede, preferências e sincronização', () => {
  it('morador guarda só a forma mascarada', async () => {
    const db = await banco();
    await new CasaRepositorySqlite(db).salvar(casa('d1'));
    const repo = new MoradorRepositorySqlite(db);
    const morador = {
      id: 'm1',
      nome: 'Lurdes A.',
      idade: 78,
      telefone: null,
      cpf: '***.982.247-**',
      cartaoSus: '*** **** **** 1234',
      perfilDeSaude: '[DADO DE SAÚDE PROTEGIDO]',
      fatoresDeRisco: ['USO_DE_DIURETICO' as const],
    };
    await repo.substituirDaCasa('d1', [morador]);
    expect(await repo.listarDaCasa('d1')).toEqual([morador]);
  });

  it('rede de cuidado mantém a ordem', async () => {
    const db = await banco();
    await new CasaRepositorySqlite(db).salvar(casa('d1'));
    const repo = new RedeRepositorySqlite(db);
    const contatos = [
      { id: 'r2', nome: 'Marta (filha)', papel: 'FAMILIAR' as const, telefone: '(11) 90000-0003' },
      { id: 'r1', nome: 'Dona Zélia (vizinha)', papel: 'VIZINHO' as const, telefone: '(11) 90000-0004' },
    ];
    await repo.substituirDaCasa('d1', contatos);
    expect(await repo.listarDaCasa('d1')).toEqual(contatos);
  });

  it('preferências e marcas de sincronização', async () => {
    const db = await banco();
    const prefs = new PreferenciaRepositorySqlite(db);
    await prefs.gravar('modoIdoso', 'd1');
    expect(await prefs.ler('modoIdoso')).toBe('d1');
    await prefs.gravar('modoIdoso', null);
    expect(await prefs.ler('modoIdoso')).toBeNull();

    const sinc = new SincronizacaoRepositorySqlite(db);
    expect(await sinc.quando('casas')).toBeNull();
    await sinc.marcar('casas', new Date('2026-11-18T18:00:00Z'));
    expect(await sinc.quando('casas')).toBe('2026-11-18T18:00:00.000Z');
  });

  it('apagarDadosLocais limpa tudo, e as preferências só quando pedido', async () => {
    const db = await banco();
    await new CasaRepositorySqlite(db).salvar(casa('d1'));
    await new PreferenciaRepositorySqlite(db).gravar('tema', 'escuro');
    await apagarDadosLocais(db, { inclusivePreferencias: false });
    expect(await db.all('SELECT * FROM casas')).toEqual([]);
    expect(await db.all('SELECT * FROM leituras')).toEqual([]);
    expect(await new PreferenciaRepositorySqlite(db).ler('tema')).toBe('escuro');
    await apagarDadosLocais(db, { inclusivePreferencias: true });
    expect(await new PreferenciaRepositorySqlite(db).ler('tema')).toBeNull();
  });

  it('concorrência: o ROLLBACK de uma transação não desfaz a escrita de outra cadeia', async () => {
    const db = await banco();
    const repo = new CasaRepositorySqlite(db);
    let soltar!: () => void;
    const falhando = db.transaction(async (tx) => {
      await new CasaRepositorySqlite(tx).salvar(casa('d1'));
      await new Promise<void>((r) => (soltar = r));
      throw new Error('falhou');
    });
    // Escrita fora da transação, pedida enquanto ela está aberta: espera a vez em vez de entrar nela.
    const independente = repo.salvar(casa('d2'));
    await new Promise((r) => setTimeout(r, 0));
    soltar();
    await expect(falhando).rejects.toThrow('falhou');
    await independente;
    expect((await repo.listar()).map((c) => c.id)).toEqual(['d2']);
  });

  it('concorrência: duas gravações do mesmo alerta ao mesmo tempo não se misturam', async () => {
    const db = await banco();
    const repo = new AlertaRepositorySqlite(db);
    const clock = FixedClock.at('2026-11-18T18:00:00Z');
    const alerta = valorOuErro(
      AlertaDeCalor.abrir({ id: 'a1', domicilioId: 'd1', tipo: 'DE_SENTINELA', nivel: 'PERIGO', temVizinho: true, clock }),
    );
    const reconhecido = valorOuErro(alerta.reconhecer('VOU_LIGAR', clock));
    await Promise.all([repo.salvar(alerta, 'A'), repo.salvar(reconhecido, 'A'), repo.salvar(alerta, 'A')]);
    expect((await repo.buscar('a1'))!.alerta.dados.linhaDoTempo).toHaveLength(1);
  });

  it('transação desfaz tudo se algo falhar no meio', async () => {
    const db = await banco();
    const repo = new CasaRepositorySqlite(db);
    await expect(
      db.transaction(async (tx) => {
        await new CasaRepositorySqlite(tx).salvar(casa('d1'));
        throw new Error('falhou');
      }),
    ).rejects.toThrow('falhou');
    expect(await repo.listar()).toEqual([]);
  });
});
