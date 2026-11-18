import { AGORA, containerDeTeste } from '@test/container';
import { renderComContainer } from '@test/render';
import { act, fireEvent, screen } from '@testing-library/react-native';
import * as Speech from 'expo-speech';

import { notificacaoDoEvento } from '@/application/notificacoes/politica';
import { preferenciasStore } from '@/application/estado/preferencias';
import { sessaoStore, TOKEN_DEMO } from '@/application/estado/sessao';
import Entrar from '@/app/(auth)/entrar';
import Ajustes from '@/app/(familia)/(abas)/ajustes';
import Alertas from '@/app/(familia)/(abas)/alertas';
import Inicio from '@/app/(familia)/(abas)/index';
import Perto from '@/app/(familia)/(abas)/perto';
import Privacidade from '@/app/(familia)/(abas)/privacidade';
import AlertaDetalhe from '@/app/(familia)/alerta/[id]';
import Casa from '@/app/(familia)/casa/[id]';
import MoradorDaCasa from '@/app/(familia)/casa/[id]/morador';
import ModoIdoso from '@/app/idoso';
import { CASAS } from '@/infrastructure/demo/fixtures';
import { abertoAgora } from '@/presentation/util/formato';
import { aplicarPadrao, exibirCartaoSus, exibirCpf, exibirTelefone, PADRAO } from '@/presentation/util/mascaras';
import { proximoPasso } from '@/presentation/util/textos-do-alerta';

const LURDES = CASAS[0]!.id;
const parametros: { id?: string } = {};
jest.mock('expo-router', () => ({
  router: { push: jest.fn() },
  useLocalSearchParams: () => parametros,
  Stack: { Screen: () => null },
}));
jest.mock('expo-speech', () => ({ speak: jest.fn(), stop: jest.fn() }));
jest.mock('expo-haptics', () => ({
  notificationAsync: jest.fn(),
  NotificationFeedbackType: { Success: 'success', Warning: 'warning' },
}));

describe('Início', () => {
  it('🔎 o card fica vermelho ao vivo quando a Sentinela manda a leitura de perigo, sem recarregar', async () => {
    const { container, core } = await containerDeTeste();
    await renderComContainer(<Inicio />, container);
    expect(await screen.findByLabelText('Nível de calor: Cautela extrema')).toBeOnTheScreen();

    await act(async () => {
      core.simularPerigo({ momento: AGORA, temperaturaC: 37.2, umidadePct: 45, indiceDeCalorC: 42.1 });
    });
    expect(await screen.findByLabelText('Nível de calor: Perigo')).toBeOnTheScreen();
    expect(await screen.findByLabelText('Índice de calor 42,1 graus')).toBeOnTheScreen();
  });

  it('sem rede, mostra os dados salvos e avisa de quando são', async () => {
    const { container, rede } = await containerDeTeste();
    await container.consultas.casas();
    rede.offline = true;
    await renderComContainer(<Inicio />, container);
    expect(await screen.findByText('Sem conexão.')).toBeOnTheScreen();
    expect(screen.getByText('Mostrando os dados salvos às 15:00.')).toBeOnTheScreen();
    expect(screen.getByText('Casa da Dona Lurdes')).toBeOnTheScreen();
  });

  it('sem rede e sem nada salvo, explica o erro', async () => {
    const { container, rede } = await containerDeTeste();
    rede.offline = true;
    await renderComContainer(<Inicio />, container);
    expect(await screen.findByText(/Não foi possível falar com o Sombra/)).toBeOnTheScreen();
  });
});

describe('Casa', () => {
  it('mostra gráfico, rede de cuidado em ordem e o alerta aberto', async () => {
    const { container } = await containerDeTeste();
    parametros.id = LURDES;
    await renderComContainer(<Casa />, container);
    expect(await screen.findByLabelText(/Índice de calor nas últimas 24 horas/)).toBeOnTheScreen();
    expect(screen.getByText('1. Marta (filha)')).toBeOnTheScreen();
    expect(screen.getByText('Quem está perto')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Ver o alerta' })).toBeOnTheScreen();
  });
});

describe('Alertas', () => {
  it('lista e detalhe com contagem até o próximo degrau; reconhecer para a contagem', async () => {
    const { container } = await containerDeTeste();
    await renderComContainer(<Alertas />, container);
    expect(await screen.findByText('Resolvidos')).toBeOnTheScreen();

    parametros.id = CASAS[0]!.alertaAberto!.id;
    await renderComContainer(<AlertaDetalhe />, container);
    expect(await screen.findByText('Sem resposta, o próximo aviso da rede sai em 20 min.')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button', { name: 'Estou indo' }));
    expect(await screen.findByText('Pronto, a rede de cuidado foi avisada.')).toBeOnTheScreen();
    expect(await screen.findByText('Alguém já está cuidando')).toBeOnTheScreen();
    expect(screen.queryByText(/próximo aviso da rede/)).toBeNull();
  });
});

describe('Modo Idoso', () => {
  beforeEach(() => preferenciasStore.setState({ modoIdosoCasaId: LURDES }));

  it('lê a situação em voz alta e diz o que acontece se ela não responder (D13)', async () => {
    const { container } = await containerDeTeste();
    preferenciasStore.setState({ modoIdosoCasaId: LURDES });
    await renderComContainer(<ModoIdoso />, container);
    expect(await screen.findByText('Se não apertar até 15:20, sua família recebe um lembrete.')).toBeOnTheScreen();
    expect(Speech.speak).toHaveBeenCalledWith(expect.stringContaining('Sua casa está com 33 graus.'), expect.anything());
  });

  it('🔎 ESTOU BEM em modo avião: guardado, e sai sozinho quando a rede volta', async () => {
    const { container, rede, mudarRede, clock } = await containerDeTeste();
    preferenciasStore.setState({ modoIdosoCasaId: LURDES });
    await container.consultas.painelDaCasa(LURDES);
    rede.offline = true;
    await renderComContainer(<ModoIdoso />, container);
    await fireEvent.press(await screen.findByRole('button', { name: 'ESTOU BEM' }));
    expect(await screen.findByText(/Guardado\. Sem internet agora/)).toBeOnTheScreen();
    expect(rede.recebidas).toBe(0);

    clock.avancarMinutos(1);
    await act(async () => mudarRede(true));
    await act(async () => {
      await container.sincronizacao.motor.drenar();
    });
    expect(rede.recebidas).toBe(1);
  });

  it('PRECISO DE AJUDA só com o toque longo; sem rede oferece o 192', async () => {
    const { container, rede } = await containerDeTeste();
    preferenciasStore.setState({ modoIdosoCasaId: LURDES });
    await container.consultas.painelDaCasa(LURDES);
    rede.offline = true;
    await renderComContainer(<ModoIdoso />, container);
    const botao = await screen.findByRole('button', { name: 'PRECISO DE AJUDA' });
    await fireEvent.press(botao);
    expect(await container.sincronizacao.outbox.contar()).toEqual({ pendentes: 0, deadLetter: 0 });
    await fireEvent(botao, 'longPress');
    expect(await screen.findByRole('button', { name: 'Ligar 192 (SAMU)' })).toBeOnTheScreen();
  });

  it('proximoPasso sem alerta aberto', () => {
    expect(proximoPasso({ ...CASAS[1]!, alertaAberto: null })).toBe('Se você não estiver bem, segure o botão vermelho.');
  });
});

describe('Morador', () => {
  it('cadastra com CPF digitado e mostra tudo mascarado; erro de validação aparece em pt-BR', async () => {
    const { container } = await containerDeTeste();
    parametros.id = LURDES;
    await renderComContainer(<MoradorDaCasa />, container);
    expect(await screen.findByLabelText('CPF mascarado: ***.982.247-**')).toBeOnTheScreen();

    await fireEvent.changeText(screen.getByLabelText('Nome'), 'Ana B.');
    await fireEvent.changeText(screen.getByLabelText('Idade'), '81');
    await fireEvent.changeText(screen.getByLabelText('CPF'), '12345678900');
    await fireEvent.press(screen.getByRole('button', { name: 'Salvar' }));
    expect(await screen.findByText('O CPF não é válido. Confira os números.')).toBeOnTheScreen();

    await fireEvent.changeText(screen.getByLabelText('CPF'), '12345678909');
    expect(screen.getByDisplayValue('123.456.789-09')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('checkbox', { name: 'Doença do coração' }));
    await fireEvent.press(screen.getByRole('button', { name: 'Salvar' }));
    expect(await screen.findByLabelText('CPF mascarado: ***.456.789-**')).toBeOnTheScreen();
    expect(screen.queryByText(/123\.456\.789-09/)).toBeNull();
  });
});

describe('Privacidade e Perto de mim', () => {
  it('o farol de privacidade reflete o Core, com texto e não só cor', async () => {
    const { container } = await containerDeTeste();
    await renderComContainer(<Privacidade />, container);
    expect(await screen.findByLabelText('Privacidade: Atenção')).toBeOnTheScreen();
    expect(screen.getByText(/o Sombra bloqueou/)).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('radio', { name: 'Casa do Seu Antônio' }));
    expect(await screen.findByLabelText('Privacidade: Dados protegidos')).toBeOnTheScreen();
  });

  it('pontos de resfriamento com distância, horário e acessibilidade', async () => {
    const { container } = await containerDeTeste();
    await renderComContainer(<Perto />, container);
    expect(await screen.findByText('Biblioteca do bairro (exemplo)')).toBeOnTheScreen();
    // Aberto ou fechado depende da hora em que o teste roda.
    expect(screen.getByText(/^800 m · (aberto até 18:00|fechado · abre às 09:00)$/)).toBeOnTheScreen();
  });
});

describe('Entrar e Ajustes', () => {
  it('sem servidor, entra na demonstração com aviso claro', async () => {
    const { container } = await containerDeTeste({ logado: false });
    await renderComContainer(<Entrar />, container);
    await fireEvent.press(screen.getByRole('button', { name: 'Ver com dados de exemplo' }));
    expect(sessaoStore.getState().token).toBe(TOKEN_DEMO);
    await renderComContainer(<Inicio />, container);
    expect(await screen.findByText('Modo demonstração')).toBeOnTheScreen();
    expect(await screen.findByText('Casa da Dona Lurdes')).toBeOnTheScreen();
  });

  it('login com erro de senha mostra a mensagem do 401', async () => {
    const { container, core } = await containerDeTeste({ logado: false });
    const { ErroDaApi } = jest.requireActual('@/application/erros');
    jest.spyOn(core, 'entrar').mockRejectedValueOnce(new ErroDaApi(401, 'Não autorizado', '0123456789abcdef'));
    await renderComContainer(<Entrar />, container);
    await fireEvent.changeText(screen.getByLabelText('E-mail'), 'ana@exemplo.com');
    await fireEvent.changeText(screen.getByLabelText('Senha'), 'errada');
    await fireEvent.press(screen.getByRole('button', { name: 'Entrar' }));
    expect(await screen.findByText('E-mail ou senha incorretos. (código do erro: 01234567)')).toBeOnTheScreen();
  });

  it('na demonstração, Ajustes simula a onda de calor e liga o Modo Idoso', async () => {
    jest.useFakeTimers();
    const { container } = await containerDeTeste();
    sessaoStore.setState({ token: TOKEN_DEMO });
    await renderComContainer(<Ajustes />, container);
    await fireEvent.press(await screen.findByRole('button', { name: 'Simular onda de calor' }));
    await fireEvent.press(screen.getByRole('button', { name: 'Recomeçar o exemplo' }));
    await fireEvent.press(await screen.findByRole('button', { name: 'Usar neste celular: Casa da Dona Lurdes' }));
    expect(preferenciasStore.getState().modoIdosoCasaId).toBe(LURDES);
    jest.useRealTimers();
  });
});

describe('máscaras e formatos', () => {
  it('formata enquanto a pessoa digita e mascara o que vier inteiro', () => {
    expect(aplicarPadrao('1234', PADRAO.cpf)).toBe('123.4');
    expect(aplicarPadrao('11987654321', PADRAO.telefone)).toBe('(11) 98765-4321');
    expect(exibirCpf('123.456.789-09')).toBe('***.456.789-**');
    expect(exibirCpf('***.982.247-**')).toBe('***.982.247-**');
    expect(exibirCartaoSus('700000000001234')).toBe('*** **** **** 1234');
    expect(exibirTelefone('(11) 98765-4321')).toBe('(11) *****-4321');
  });

  it('abertoAgora compara no horário de São Paulo', () => {
    expect(abertoAgora('09:00', '18:00', new Date('2026-11-18T15:00:00Z'))).toBe(true);
    expect(abertoAgora('09:00', '18:00', new Date('2026-11-18T22:00:00Z'))).toBe(false);
  });
});

describe('notificações em degraus (D13)', () => {
  const ctx = { modoIdoso: false, apelido: () => 'Casa da Dona Lurdes', agora: new Date('2026-11-18T18:00:00Z') };
  const noite = { ...ctx, agora: new Date('2026-11-19T03:00:00Z') };

  it('degraus 1 e 2 são lembretes; o 3 é alerta', () => {
    expect(notificacaoDoEvento('AlertaEscalonado', { degrau: 1, mensagem: 'm' }, ctx)?.canal).toBe('lembretes');
    expect(notificacaoDoEvento('AlertaEscalonado', { degrau: 2, mensagem: 'm' }, ctx)?.titulo).toBe('Quem está perto');
    expect(notificacaoDoEvento('AlertaEscalonado', { degrau: 3, mensagem: 'm' }, ctx)?.canal).toBe('alertas');
  });

  it('a família não recebe nada no degrau 0; pedido de ajuda é alerta mesmo de noite', () => {
    expect(notificacaoDoEvento('AlertaAberto', { tipo: 'DE_SENTINELA', prazo: AGORA }, ctx)).toBeNull();
    expect(notificacaoDoEvento('CheckInRegistrado', { situacao: 'PRECISO_DE_AJUDA' }, noite)?.canal).toBe('alertas');
    expect(notificacaoDoEvento('CheckInRegistrado', { situacao: 'ESTOU_BEM' }, ctx)).toBeNull();
  });

  it('de noite, nada preventivo nem de "tudo certo"', () => {
    expect(notificacaoDoEvento('AlertaAberto', { tipo: 'PREVENTIVO' }, noite)).toBeNull();
    expect(notificacaoDoEvento('AlertaAberto', { tipo: 'PREVENTIVO' }, ctx)?.canal).toBe('lembretes');
    expect(notificacaoDoEvento('AlertaResolvido', {}, noite)).toBeNull();
    expect(notificacaoDoEvento('AlertaResolvido', {}, ctx)?.corpo).toMatch(/confirmou/);
    expect(notificacaoDoEvento('LeituraRegistrada', {}, ctx)).toBeNull();
  });

  it('no Modo Idoso, explica o que acontece se ela não responder', () => {
    const idoso = { ...ctx, modoIdoso: true };
    expect(notificacaoDoEvento('AlertaAberto', { tipo: 'DE_SENTINELA', prazo: '2026-11-18T18:30:00Z' }, idoso)?.corpo).toBe(
      'Abra o Sombra e aperte ESTOU BEM. Se não apertar até 15:30, sua família recebe um lembrete.',
    );
    expect(notificacaoDoEvento('AlertaEscalonado', { degrau: 1 }, idoso)).toBeNull();
  });
});
