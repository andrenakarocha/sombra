// Os caminhos que a jornada feliz não passa: erros do Core, estados vazios, permissões negadas, edição e confirmação.

import { containerDeTeste } from '@test/container';
import { renderComContainer } from '@test/render';
import { act, fireEvent, render, screen } from '@testing-library/react-native';
import { router } from 'expo-router';
import { Linking, Share } from 'react-native';

import { CasaNaoEncontrada } from '@/application/casos-de-uso/consultas';
import { ErroDaApi, RespostaForaDoContrato, SemConexao } from '@/application/erros';
import { reproducaoStore } from '@/application/estado/reproducao';
import { sessaoStore, TOKEN_DEMO } from '@/application/estado/sessao';
import Ajustes from '@/app/(familia)/(abas)/ajustes';
import Alertas from '@/app/(familia)/(abas)/alertas';
import Perto from '@/app/(familia)/(abas)/perto';
import Privacidade from '@/app/(familia)/(abas)/privacidade';
import AlertaDetalhe from '@/app/(familia)/alerta/[id]';
import Casa from '@/app/(familia)/casa/[id]';
import MoradorDaCasa from '@/app/(familia)/casa/[id]/morador';
import VincularSentinela from '@/app/(familia)/casa/[id]/sentinela';
import Sincronizacao from '@/app/(familia)/sincronizacao';
import { CoreDemoGateway } from '@/infrastructure/demo/core-demo-gateway';
import { CASAS } from '@/infrastructure/demo/fixtures';
import { FixedClock } from '@/domain/compartilhado/clock';
import { GraficoDeCalor } from '@/presentation/componentes/GraficoDeCalor';
import { PilulaDeSincronizacao } from '@/presentation/componentes/PilulaDeSincronizacao';
import { mensagemDeErro } from '@/presentation/util/mensagens';

const LURDES = CASAS[0]!.id;
const ALERTA = CASAS[0]!.alertaAberto!.id;
const parametros: { id?: string } = {};
let mockAoLerQr: ((r: { data: string }) => void) | null = null;
let mockPermissao = { granted: false };

jest.mock('expo-router', () => ({ router: { push: jest.fn() }, useLocalSearchParams: () => parametros, Stack: { Screen: () => null } }));
jest.mock('expo-camera', () => ({
  CameraView: (p: { onBarcodeScanned: (r: { data: string }) => void }) => {
    mockAoLerQr = p.onBarcodeScanned;
    return null;
  },
  useCameraPermissions: () => [mockPermissao, async () => mockPermissao],
}));

beforeEach(() => {
  parametros.id = LURDES;
  mockPermissao = { granted: false };
  mockAoLerQr = null;
});
afterEach(() => jest.restoreAllMocks());

describe('mensagens de erro para a pessoa', () => {
  it.each([
    [new SemConexao(), /Confira a internet/],
    [new ErroDaApi(409, 'Conflito', 'abcdef1234'), /^Conflito \(código do erro: abcdef12\)$/],
    [new RespostaForaDoContrato('/x', 'y'), /jeito inesperado/],
    [new CasaNaoEncontrada('x'), /rede de cuidado/],
    [new Error('qualquer'), /Algo deu errado/],
  ])('%p', (erro, texto) => {
    expect(mensagemDeErro(erro)).toMatch(texto);
  });

  it('a mensagem por status substitui o título do Core', () => {
    expect(mensagemDeErro(new ErroDaApi(401, 'x', '12345678'), { 401: 'Senha errada.' })).toBe('Senha errada. (código do erro: 12345678)');
  });
});

describe('Sentinela', () => {
  it('lê o QR pela câmera e vincula; QR estranho explica', async () => {
    mockPermissao = { granted: true };
    const { container } = await containerDeTeste();
    await container.consultas.painelDaCasa(LURDES);
    await renderComContainer(<VincularSentinela />, container);
    await fireEvent.press(screen.getByRole('button', { name: 'Ler o QR da Sentinela' }));
    await act(async () => mockAoLerQr!({ data: 'https://outra.coisa' }));
    expect(await screen.findByText('Este QR não é de uma Sentinela do Sombra.')).toBeOnTheScreen();
    await act(async () => mockAoLerQr!({ data: 'sombra://sentinela/vincular?v=1&d=snt-abcdef&c=K7P2QX' }));
    expect(await screen.findByText('Sentinela snt-abcdef vinculada a esta casa.')).toBeOnTheScreen();
  });

  it('sem rede, ou com resposta estranha do Core, não guarda nada', async () => {
    const { container, rede, core } = await containerDeTeste();
    await renderComContainer(<VincularSentinela />, container);
    rede.offline = true;
    await fireEvent.changeText(screen.getByLabelText('Número da Sentinela'), 'snt-abcdef');
    await fireEvent.changeText(screen.getByLabelText('Código de vínculo'), 'K7P2QX');
    await fireEvent.press(screen.getByRole('button', { name: 'Vincular com o número digitado' }));
    expect(await screen.findByText(/Confira a internet/)).toBeOnTheScreen();

    rede.offline = false;
    jest.spyOn(core, 'vincularSentinela').mockResolvedValueOnce({ deviceId: 'snt-000000', segredo: 'curto' });
    await fireEvent.press(screen.getByRole('button', { name: 'Vincular com o número digitado' }));
    expect(await screen.findByText('O Sombra respondeu de um jeito inesperado. Tente de novo.')).toBeOnTheScreen();
    expect(await container.casaESentinela.segredoDa('snt-abcdef')).toBeNull();
    await fireEvent.changeText(screen.getByLabelText('Código de vínculo'), 'K7');
    await fireEvent.press(screen.getByRole('button', { name: 'Vincular com o número digitado' }));
    expect(await screen.findByText('O código de vínculo tem 6 letras e números.')).toBeOnTheScreen();
  });
});

describe('rotina no caso de uso', () => {
  it('rotina inválida do servidor vira a padrão; salvar de madrugada é recusado sem ir ao Core', async () => {
    const { container, core } = await containerDeTeste();
    jest.spyOn(core, 'rotina').mockResolvedValueOnce({ checkInDiario: { ativo: true, horario: '03:00' }, lembreteDeAgua: { ativo: true } });
    expect((await container.casaESentinela.rotina(LURDES)).dados.checkInDiario.ativo).toBe(false);
    const salvar = jest.spyOn(core, 'salvarRotina');
    const r = await container.casaESentinela.salvarRotina(LURDES, { checkInDiario: { ativo: true, horario: '23:30' }, lembreteDeAgua: { ativo: true } });
    expect(r.ok ? null : r.erro.codigo).toBe('R12');
    expect(salvar).not.toHaveBeenCalled();
    jest.spyOn(core, 'rotina').mockRejectedValueOnce(new SemConexao());
    expect(await container.casaESentinela.programarLembretes(LURDES, 'ATENCAO')).toBe(0);
  });
});

describe('Morador', () => {
  it('edição: campos de documento vazios mantêm; cancelar volta ao cadastro; erro do Core aparece', async () => {
    const { container, core } = await containerDeTeste();
    await renderComContainer(<MoradorDaCasa />, container);
    await fireEvent.press(await screen.findByRole('button', { name: 'Editar' }));
    expect(screen.getByText('Editar Lurdes A.')).toBeOnTheScreen();
    expect(screen.getByDisplayValue('78')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('checkbox', { name: 'Toma diurético' }));
    await fireEvent.press(screen.getByRole('button', { name: 'Cancelar edição' }));
    expect(screen.getByText('Cadastrar morador')).toBeOnTheScreen();

    await fireEvent.press(screen.getByRole('button', { name: 'Editar' }));
    jest.spyOn(core, 'salvarMorador').mockRejectedValueOnce(new ErroDaApi(500, 'Falhou', 'ffffffff'));
    await fireEvent.press(screen.getByRole('button', { name: 'Salvar' }));
    expect(await screen.findByText('Falhou (código do erro: ffffffff)')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Salvar' }));
    expect(await screen.findByText(/Salvo\. Os documentos aparecem mascarados/)).toBeOnTheScreen();
  });
});

describe('Casa', () => {
  it('liga para a moradora fora da demonstração e leva às telas de rotina, Sentinela e morador', async () => {
    const { container } = await containerDeTeste();
    const abrir = jest.spyOn(Linking, 'openURL').mockResolvedValue(undefined);
    await renderComContainer(<Casa />, container);
    await fireEvent.press(await screen.findByRole('button', { name: 'Ligar para Lurdes A.' }));
    expect(abrir).toHaveBeenCalledWith('tel:11900000001');
    await fireEvent.press(screen.getByRole('button', { name: 'Rotina' }));
    await fireEvent.press(screen.getByRole('button', { name: 'Trocar a Sentinela' }));
    await fireEvent.press(screen.getByRole('button', { name: 'Morador e dados de saúde' }));
    await fireEvent.press(screen.getByRole('button', { name: 'Ver o alerta' }));
    expect(jest.mocked(router.push).mock.calls.map(([r]) => r)).toEqual(
      expect.arrayContaining([`/casa/${LURDES}/rotina`, `/casa/${LURDES}/sentinela`, `/casa/${LURDES}/morador`, `/alerta/${ALERTA}`]),
    );
  });

  it('casa que não está no aparelho e o servidor não acha', async () => {
    const { container } = await containerDeTeste();
    parametros.id = 'nao-existe';
    await renderComContainer(<Casa />, container);
    expect(await screen.findByText(/Casa não encontrada/)).toBeOnTheScreen();
  });
});

describe('Alertas', () => {
  it('lista vazia e erro', async () => {
    const { container, core, rede } = await containerDeTeste();
    jest.spyOn(core, 'listarAlertas').mockResolvedValueOnce([]);
    await renderComContainer(<Alertas />, container);
    expect(await screen.findByText('Nenhum alerta ativo. Tudo calmo por aqui.')).toBeOnTheScreen();
    rede.offline = true;
    await act(async () => container.barramento.publicar({ recurso: 'alertas' }));
    expect(await screen.findByText(/Sem conexão/)).toBeOnTheScreen();
  });

  it('no detalhe: abrir o alerta, guardar sem rede e o erro de alerta inexistente', async () => {
    const { container, rede } = await containerDeTeste();
    parametros.id = ALERTA;
    await renderComContainer(<AlertaDetalhe />, container);
    await fireEvent.press(await screen.findByRole('button', { name: 'Estou indo' }));
    expect(await screen.findByText('Pronto, a rede de cuidado foi avisada.')).toBeOnTheScreen();

    rede.offline = true;
    await fireEvent.press(screen.getByRole('button', { name: 'Ela está bem' }));
    expect(await screen.findByText(/Guardado\. Sem internet agora/)).toBeOnTheScreen();
  });

  it('alerta que não existe', async () => {
    const { container } = await containerDeTeste();
    parametros.id = 'nao-existe';
    await renderComContainer(<AlertaDetalhe />, container);
    expect(await screen.findByText(/Alerta não encontrado/)).toBeOnTheScreen();
  });
});

describe('Central e pílula de sincronização', () => {
  it('pendente que já tentou, pedido de ajuda, e "tentar agora"', async () => {
    const { container, rede, clock } = await containerDeTeste();
    rede.offline = true;
    await container.escritas.registrarCheckIn(LURDES, 'PRECISO_DE_AJUDA');
    await container.escritas.registrarCheckIn(LURDES, 'ESTOU_BEM');
    await renderComContainer(<PilulaDeSincronizacao />, container);
    await fireEvent.press(await screen.findByRole('button', { name: '2 avisos esperando a internet' }));
    expect(router.push).toHaveBeenCalledWith('/sincronizacao');
    await renderComContainer(<Sincronizacao />, container);
    expect(await screen.findByText(/Pedido de ajuda · 15:00/)).toBeOnTheScreen();
    // Cada um bateu na falta de rede uma vez (o motor para no primeiro "sem conexão" de cada drenagem).
    expect(screen.getAllByText(/Tentou 1 vez \(sem conexão\)/)).toHaveLength(2);
    rede.offline = false;
    clock.avancarMinutos(1);
    await act(async () => {
      await fireEvent.press(screen.getByRole('button', { name: 'Tentar enviar agora' }));
    });
    expect(await screen.findByText('Tudo enviado.')).toBeOnTheScreen();
  });

  it('a pílula avisa o que o Core não aceitou', async () => {
    const { container, core } = await containerDeTeste();
    jest.spyOn(core, 'enviar').mockRejectedValue(new ErroDaApi(404, 'x', 'x'));
    await container.escritas.registrarCheckIn(LURDES, 'ESTOU_BEM');
    await container.escritas.registrarCheckIn(LURDES, 'ESTOU_BEM');
    await renderComContainer(<PilulaDeSincronizacao />, container);
    expect(await screen.findByRole('button', { name: '2 avisos não aceitos pelo Sombra' })).toBeOnTheScreen();
  });
});

describe('Privacidade', () => {
  it('exportar abre o compartilhar; apagar pede confirmação', async () => {
    const { container } = await containerDeTeste();
    const compartilhar = jest.spyOn(Share, 'share').mockResolvedValue({ action: 'sharedAction', activityType: undefined });
    await renderComContainer(<Privacidade />, container);
    await act(async () => {
      await fireEvent.press(await screen.findByRole('button', { name: 'Exportar os dados (JSON)' }));
    });
    expect(compartilhar).toHaveBeenCalledWith(expect.objectContaining({ title: 'Dados do Sombra' }));
    await fireEvent.press(screen.getByRole('button', { name: 'Apagar tudo deste celular' }));
    await act(async () => {
      await fireEvent.press(screen.getByRole('button', { name: 'Toque de novo para apagar tudo' }));
    });
    expect(sessaoStore.getState().token).toBeNull();
  });

  it('erro do Core no farol', async () => {
    const { container, core } = await containerDeTeste();
    jest.spyOn(core, 'privacidade').mockRejectedValue(new ErroDaApi(503, 'Indisponível', '01234567'));
    await renderComContainer(<Privacidade />, container);
    expect(await screen.findByText('Indisponível (código do erro: 01234567)')).toBeOnTheScreen();
  });
});

describe('Perto de mim', () => {
  it('com posição e sem bússola pede para girar; erro do Core aparece; "abrir rota" abre o mapa', async () => {
    const { container } = await containerDeTeste({
      localizacao: { atual: async () => ({ lat: -23.5, lon: -46.62 }), observarDirecao: async () => () => {} },
    });
    const abrir = jest.spyOn(Linking, 'openURL').mockResolvedValue(undefined);
    await renderComContainer(<Perto />, container);
    expect(await screen.findByText('Gire o celular devagar para a bússola acordar.')).toBeOnTheScreen();
    await fireEvent.press(screen.getAllByRole('button', { name: 'Abrir rota' })[0]!);
    expect(abrir.mock.calls.at(-1)![0]).toMatch(/^https:\/\/www\.google\.com\/maps\/dir\/\?api=1&destination=-23\.\d+%2C-46\.\d+$/);
  });

  it('erro do Core e ponto sem coordenada', async () => {
    const { container, core } = await containerDeTeste();
    jest.spyOn(core, 'pontosDeResfriamento').mockResolvedValueOnce([
      { id: 'x', nome: 'Praça (exemplo)', tipo: 'PRACA', distanciaKm: 2.4, horario: { abre: '00:00', fecha: '23:59' }, acessivel: false, fonte: 'exemplo' },
    ]);
    const abrir = jest.spyOn(Linking, 'openURL').mockResolvedValue(undefined);
    await renderComContainer(<Perto />, container);
    expect(await screen.findByText('Sem informação de acessibilidade')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Abrir rota' }));
    expect(decodeURIComponent(abrir.mock.calls.at(-1)![0] as string)).toMatch(/Praça \(exemplo\), Santana, São Paulo$/);
  });
});

describe('Ajustes na demonstração', () => {
  it('reproduzir e parar a onda de calor real', async () => {
    jest.useFakeTimers();
    const { container } = await containerDeTeste();
    sessaoStore.setState({ token: TOKEN_DEMO });
    await renderComContainer(<Ajustes />, container);
    await fireEvent.press(await screen.findByRole('button', { name: 'Reproduzir a onda de calor real (INMET, nov/2023)' }));
    expect(reproducaoStore.getState().ativo).toBe(true);
    await fireEvent.press(await screen.findByRole('button', { name: 'Parar a onda de calor real' }));
    expect(reproducaoStore.getState().ativo).toBe(false);
    jest.useRealTimers();
  });
});

describe('gateway da demonstração: as recusas do "Core"', () => {
  const core = () => new CoreDemoGateway(FixedClock.at('2026-11-18T18:00:00Z'), 0);
  const op = (tipo: 'RECONHECER_ALERTA', alertaId: string) => ({
    operationId: `op-${alertaId}`,
    tipo,
    payload: { alertaId, acao: 'VOU_LIGAR' as const, momento: '2026-11-18T18:01:00Z' },
    criadoEm: '',
    tentativas: 0,
    proximaTentativaEm: '',
    estado: 'PENDENTE' as const,
    ultimoErro: null,
  });

  it('casa, alerta e reconhecimento inexistentes; reconhecer alerta resolvido; escalonar sem alerta', async () => {
    const c = core();
    await expect(c.buscarCasa('x')).rejects.toMatchObject({ status: 404 });
    await expect(c.buscarAlerta('x')).rejects.toMatchObject({ status: 404 });
    await expect(c.enviar(op('RECONHECER_ALERTA', 'x'))).rejects.toMatchObject({ status: 404 });
    const resolvido = (await c.listarAlertas()).find((a) => a.estado === 'RESOLVIDO')!;
    await expect(c.enviar(op('RECONHECER_ALERTA', resolvido.id))).rejects.toMatchObject({ status: 422 });
    expect(c.escalonar('x')).toBe(false);
    expect(c.escalonar(resolvido.id)).toBe(false);
    expect(await c.privacidade('sem-dados')).toEqual({ farol: 'VERDE', mensagens: [] });
    expect(await c.moradores('sem-moradores')).toEqual([]);
    expect(await c.redeDeCuidado('sem-rede')).toEqual([]);
  });

  it('o mesmo operationId vale uma vez só (Idempotency-Key)', async () => {
    const c = core();
    const aberto = (await c.listarAlertas()).find((a) => a.estado !== 'RESOLVIDO')!;
    await c.enviar(op('RECONHECER_ALERTA', aberto.id));
    await c.enviar(op('RECONHECER_ALERTA', aberto.id));
    expect((await c.buscarAlerta(aberto.id)).linhaDoTempo!.filter((e) => /vai ligar/.test(e.descricao))).toHaveLength(1);
  });

  it('escalona até a rede inteira e para', async () => {
    const c = core();
    const id = c.simularPerigo({ momento: '2026-11-18T18:00:00Z', temperaturaC: 37, umidadePct: 45, indiceDeCalorC: 42 });
    expect([c.escalonar(id), c.escalonar(id), c.escalonar(id), c.escalonar(id)]).toEqual([true, true, true, false]);
  });

  it('editar morador mantém o que não veio e cadastra numa casa sem moradores', async () => {
    const c = core();
    const [m] = await c.moradores(LURDES);
    expect((await c.salvarMorador(LURDES, m!.id, { idade: 79 })).cpf).toBe(m!.cpf);
    expect((await c.salvarMorador('casa-nova', undefined, { nome: 'Zé', cpf: '12345678909', cartaoSus: '700000000001234' })).cartaoSus).toBe(
      '*** **** **** 1234',
    );
  });
});

describe('gráfico de 24 h', () => {
  it('com poucas leituras explica; com várias desenha depois de medir a largura', async () => {
    await render(<GraficoDeCalor leituras={[]} nivel="NORMAL" />);
    expect(screen.getByText(/Ainda não há leituras suficientes/)).toBeOnTheScreen();
    const l = (m: string, i: number) => ({ momento: m, temperaturaC: i, umidadePct: 50, indiceDeCalorC: i });
    await render(<GraficoDeCalor leituras={[l('2026-11-18T10:00:00Z', 28), l('2026-11-18T12:00:00Z', 33)]} nivel="ATENCAO" />);
    const grafico = screen.getByLabelText(/mínimo 28,0, máximo 33,0, agora 33,0 graus/);
    await fireEvent(grafico, 'layout', { nativeEvent: { layout: { width: 300, height: 120, x: 0, y: 0 } } });
    expect(grafico).toBeOnTheScreen();
  });
});
