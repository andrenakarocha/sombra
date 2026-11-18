import { AGORA, containerDeTeste } from '@test/container';

import { CasaNaoEncontrada } from '@/application/casos-de-uso/consultas';
import { ErroDaApi, SemConexao } from '@/application/erros';
import { preferenciasStore } from '@/application/estado/preferencias';
import { sessaoStore, TOKEN_DEMO } from '@/application/estado/sessao';
import { CASAS } from '@/infrastructure/demo/fixtures';

const LURDES = CASAS[0]!.id;
const ANTONIO = CASAS[1]!.id;

describe('consultas local-first', () => {
  it('online: vem do servidor, fica no aparelho e responde do banco local', async () => {
    const { container } = await containerDeTeste();
    const r = await container.consultas.casas();
    expect(r.origem).toBe('servidor');
    expect(r.atualizadoEm).toBe(AGORA);
    expect(r.dados.map((c) => c.apelido)).toEqual(['Casa da Dona Lurdes', 'Casa do Seu Antônio']);
  });

  it('modo avião depois de uma sincronização: os últimos dados, marcados como locais', async () => {
    const { container, rede, clock } = await containerDeTeste();
    await container.consultas.casas();
    clock.avancarMinutos(30);
    rede.offline = true;
    const r = await container.consultas.casas();
    expect([r.origem, r.atualizadoEm, r.dados.length]).toEqual(['local', AGORA, 2]);
  });

  it('modo avião sem nunca ter sincronizado: erro (não há o que mostrar)', async () => {
    const { container, rede } = await containerDeTeste();
    rede.offline = true;
    await expect(container.consultas.casas()).rejects.toBeInstanceOf(SemConexao);
  });

  it('erro que não é "fora do ar" aparece, mesmo com dado local', async () => {
    const { container, core } = await containerDeTeste();
    await container.consultas.casas();
    jest.spyOn(core, 'listarCasas').mockRejectedValueOnce(new ErroDaApi(403, 'Proibido', 'x'));
    await expect(container.consultas.casas()).rejects.toBeInstanceOf(ErroDaApi);
  });

  it('painel da casa: casa, 24 h de leituras, moradores, rede e o risco personalizado do morador', async () => {
    const { container } = await containerDeTeste();
    const { dados, origem } = await container.consultas.painelDaCasa(LURDES);
    expect(origem).toBe('servidor');
    expect(dados.casa.alertaAberto?.estado).toBe('AGUARDANDO_CHECKIN');
    expect(dados.leituras24h).toHaveLength(48);
    expect(dados.leituras24h[0]!.momento < dados.leituras24h.at(-1)!.momento).toBe(true);
    expect(dados.rede.map((c) => c.papel)).toEqual(['FAMILIAR', 'VIZINHO']);
    expect(dados.risco?.contribuicoes.map((c) => c.fator)).toEqual(['USO_DE_DIURETICO']);
  });

  it('casa que saiu da rede de cuidado', async () => {
    const { container, core } = await containerDeTeste();
    jest.spyOn(core, 'buscarCasa').mockRejectedValueOnce(new ErroDaApi(503, 'x', 'x'));
    await container.consultas.casas();
    // Sem rede no painel e sem nunca ter aberto esta casa: a consulta propaga o 503.
    await expect(container.consultas.painelDaCasa(LURDES)).rejects.toBeInstanceOf(ErroDaApi);
    await expect(container.consultas.painelDaCasa('nao-existe')).rejects.toBeInstanceOf(ErroDaApi);
    expect(new CasaNaoEncontrada('x').message).toMatch(/rede de cuidado/);
  });

  it('alertas: ativos primeiro, com o tempo até o próximo degrau calculado no aparelho', async () => {
    const { container } = await containerDeTeste();
    const { dados } = await container.consultas.alertas();
    expect(dados.map((a) => a.estado)).toEqual(['AGUARDANDO_CHECKIN', 'RESOLVIDO']);
    expect(dados[0]!.minutosAteProximoDegrau).toBe(20);
    expect(dados[1]!.minutosAteProximoDegrau).toBeNull();
    expect((await container.consultas.alerta(dados[0]!.id)).dados.linhaDoTempo).toHaveLength(1);
  });

  it('pontos de resfriamento e privacidade vêm sempre do servidor', async () => {
    const { container } = await containerDeTeste();
    expect((await container.consultas.pontosDeResfriamento(LURDES)).length).toBe(2);
    expect((await container.consultas.privacidade(LURDES)).farol).toBe('AMARELO');
    expect((await container.consultas.privacidade(ANTONIO)).farol).toBe('VERDE');
  });
});

describe('escritas otimistas e a outbox', () => {
  it('🔎 modo avião: o "estou bem" resolve na tela na hora, espera na fila e sai uma vez só quando a rede volta', async () => {
    const { container, rede, mudarRede, clock, core } = await containerDeTeste();
    await container.consultas.painelDaCasa(LURDES);
    rede.offline = true;
    clock.avancarMinutos(5);

    expect(await container.escritas.registrarCheckIn(LURDES, 'ESTOU_BEM')).toBe('NA_FILA');
    const offline = await container.consultas.painelDaCasa(LURDES);
    expect([offline.origem, offline.dados.casa.alertaAberto, offline.dados.casa.ultimoCheckIn]).toEqual([
      'local',
      null,
      '2026-11-18T18:05:00.000Z',
    ]);
    expect(await container.sincronizacao.outbox.contar()).toEqual({ pendentes: 1, deadLetter: 0 });

    mudarRede(true);
    clock.avancarMinutos(1);
    await container.sincronizacao.motor.drenar();
    await container.sincronizacao.motor.drenar();
    expect(rede.recebidas).toBe(1);
    expect(await container.sincronizacao.outbox.contar()).toEqual({ pendentes: 0, deadLetter: 0 });
    expect((await core.buscarCasa(LURDES)).alertaAberto).toBeNull();
  });

  it('rebase: um retrato do servidor que ainda não viu o check-in não desfaz o que ela fez', async () => {
    const { container, core, clock } = await containerDeTeste();
    await container.consultas.painelDaCasa(LURDES);
    // O Core fica fora só para o envio: as consultas continuam vendo o alerta aberto lá.
    jest.spyOn(core, 'enviar').mockRejectedValue(new ErroDaApi(503, 'Indisponível', 'x'));
    clock.avancarMinutos(2);
    expect(await container.escritas.registrarCheckIn(LURDES, 'ESTOU_BEM')).toBe('NA_FILA');

    const depois = await container.consultas.painelDaCasa(LURDES);
    expect(depois.origem).toBe('servidor');
    expect(depois.dados.casa.alertaAberto).toBeNull();
    const [alerta] = (await container.consultas.alertas()).dados;
    expect(alerta!.estado).toBe('RESOLVIDO');
    expect(alerta!.linhaDoTempo!.filter((e) => /celular/.test(e.descricao))).toHaveLength(1);
  });

  it('reconhecer o alerta para o escalonamento já no aparelho', async () => {
    const { container } = await containerDeTeste();
    const [alerta] = (await container.consultas.alertas()).dados;
    expect(await container.escritas.reconhecerAlerta(alerta!.id, 'ESTOU_INDO')).toBe('ENVIADO');
    const r = await container.consultas.alerta(alerta!.id);
    expect([r.dados.estado, r.dados.minutosAteProximoDegrau]).toEqual(['RECONHECIDO', null]);
  });

  it('o Core recusar de vez vira RECUSADO (dead-letter), sem travar a fila', async () => {
    const { container, core } = await containerDeTeste();
    const [alerta] = (await container.consultas.alertas()).dados;
    jest.spyOn(core, 'enviar').mockRejectedValueOnce(new ErroDaApi(422, 'Alerta já resolvido', 'abcdef12'));
    expect(await container.escritas.reconhecerAlerta(alerta!.id, 'VOU_LIGAR')).toBe('RECUSADO');
    expect(await container.sincronizacao.outbox.contar()).toEqual({ pendentes: 0, deadLetter: 1 });
    expect(await container.escritas.registrarCheckIn(LURDES, 'ESTOU_BEM')).toBe('ENVIADO');
  });

  it('cadastro do morador: os value objects validam antes da rede, e só o mascarado fica no aparelho', async () => {
    const { container, core } = await containerDeTeste();
    await container.consultas.painelDaCasa(LURDES);
    const espiao = jest.spyOn(core, 'salvarMorador');
    const base = { nome: 'Ana B.', idade: 81, cpf: '', cartaoSus: '', telefone: '', perfilDeSaude: '', fatoresDeRisco: [] };

    const semCpf = await container.escritas.salvarMorador(LURDES, undefined, base);
    expect(semCpf.ok ? null : semCpf.erro.mensagem).toBe('O CPF tem 11 números.');
    for (const [mudanca, mensagem] of [
      [{ nome: ' ' }, 'Escreva o nome.'],
      [{ idade: Number.NaN }, 'Confira a idade.'],
      [{ cpf: '123.456.789-09', telefone: '123' }, 'Confira o telefone com DDD.'],
    ] as const) {
      const r = await container.escritas.salvarMorador(LURDES, undefined, { ...base, ...mudanca });
      expect(r.ok ? null : r.erro.mensagem).toBe(mensagem);
    }
    expect(espiao).not.toHaveBeenCalled();

    const ok = await container.escritas.salvarMorador(LURDES, undefined, {
      ...base,
      cpf: '123.456.789-09',
      telefone: '(11) 98765-4321',
      perfilDeSaude: 'Hipertensa',
    });
    expect(ok.ok).toBe(true);
    expect(espiao).toHaveBeenCalledWith(LURDES, undefined, expect.objectContaining({ cpf: '12345678909', telefone: '11987654321' }));
    const moradores = (await container.consultas.painelDaCasa(LURDES)).dados.moradores;
    const ana = moradores.find((m) => m.nome === 'Ana B.')!;
    expect([ana.cpf, ana.perfilDeSaude]).toEqual(['***.456.789-**', '[DADO DE SAÚDE PROTEGIDO]']);

    // Edição: campos de documento vazios ficam como estão.
    const editado = await container.escritas.salvarMorador(LURDES, ana.id, { ...base, nome: 'Ana B. S.' });
    expect(editado.ok && editado.valor.cpf).toBe('***.456.789-**');
  });
});

describe('sessão', () => {
  it('entrar, carregar, sair (apaga os dados de casa) e sessão vencida (guarda a fila)', async () => {
    const { container, rede } = await containerDeTeste({ logado: false });
    await container.sessao.entrar('ana@exemplo.com', 'senha');
    expect(sessaoStore.getState().token).toBe('demo');

    await container.consultas.casas();
    await container.sessao.definirModoIdoso(LURDES);
    sessaoStore.setState({ token: null, carregada: false });
    await container.sessao.carregar();
    expect([sessaoStore.getState().token, preferenciasStore.getState().modoIdosoCasaId]).toEqual(['demo', LURDES]);

    rede.offline = true;
    await container.escritas.registrarCheckIn(LURDES, 'ESTOU_BEM');
    await container.sessao.sessaoVencida();
    expect(sessaoStore.getState().token).toBeNull();
    expect((await container.sincronizacao.outbox.contar()).pendentes).toBe(1);

    await container.sessao.entrarNaDemonstracao();
    expect(sessaoStore.getState().token).toBe(TOKEN_DEMO);
    await container.sessao.sair();
    expect([sessaoStore.getState().token, preferenciasStore.getState().modoIdosoCasaId]).toEqual([null, null]);
    expect(await container.sincronizacao.outbox.contar()).toEqual({ pendentes: 0, deadLetter: 0 });
  });
});

describe('eventos do hub', () => {
  it('a onda de calor chega pelo hub: a casa vai a Perigo no banco local e a família é avisada só no degrau 1', async () => {
    const { container, core, notificacoes } = await containerDeTeste();
    await container.consultas.casas();
    const parar = container.tempoReal.conectar('t', (e, p) => void container.eventos.receber(e, p), () => {});
    const mudancas: string[] = [];
    container.barramento.assinar((m) => mudancas.push(m.recurso));

    const leitura = { momento: AGORA, temperaturaC: 37.2, umidadePct: 45, indiceDeCalorC: 42.1 };
    const alertaId = core.simularPerigo(leitura);
    await new Promise((r) => setTimeout(r, 0));
    const casa = (await container.consultas.casas()).dados.find((c) => c.id === LURDES)!;
    expect([casa.nivelAtual, casa.ultimaLeitura?.indiceDeCalorC]).toEqual(['PERIGO', 42.1]);
    expect(mudancas).toEqual(expect.arrayContaining(['casas', 'casa', 'alertas']));
    expect(notificacoes).toEqual([]);

    expect(core.escalonar(alertaId)).toBe(true);
    await new Promise((r) => setTimeout(r, 0));
    expect(notificacoes).toEqual([
      { canal: 'lembretes', titulo: 'Lembrete', corpo: 'A Dona Lurdes ainda não confirmou que está bem. Quer ligar?' },
    ]);
    parar();
  });

  it('Sentinela, check-in e farol de privacidade (lidos do aparelho, sem esperar o servidor)', async () => {
    const { container, rede } = await containerDeTeste();
    await container.consultas.casas();
    await container.eventos.receber('SentinelaStatus', { domicilioId: LURDES, deviceId: 'snt-7f3a9c', online: false });
    await container.eventos.receber('CheckInRegistrado', { domicilioId: LURDES, momento: '2026-11-18T18:30:00.000Z', situacao: 'ESTOU_BEM' });
    const mudancas: unknown[] = [];
    container.barramento.assinar((m) => mudancas.push(m));
    await container.eventos.receber('RiscoCidadaoAtualizado', { domicilioId: null, farol: 'AMARELO' });
    await container.eventos.receber('EventoQueNaoExiste', {});
    rede.offline = true;
    const casa = (await container.consultas.casas()).dados[0]!;
    expect([casa.sentinela?.online, casa.ultimoCheckIn]).toEqual([false, '2026-11-18T18:30:00.000Z']);
    expect(mudancas).toEqual([{ recurso: 'privacidade' }]);
  });
});
