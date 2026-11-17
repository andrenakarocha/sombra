import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen } from '@testing-library/react-native';
import * as Notifications from 'expo-notifications';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { requisitar } from '@/api/cliente';
import { queryClient, type Resultado } from '@/api/consultas';
import type { Domicilio } from '@/application/modelos';
import Entrar from '@/app/(auth)/entrar';
import Inicio from '@/app/(familia)/(abas)/index';
import { reiniciarDemo } from '@/demo/api-demo';
import { CASAS } from '@/demo/fixtures';
import { simularOndaDeCalor } from '@/demo/simulacao';
import { notificacaoDoEvento } from '@/notificacoes/notificacoes';
import { TOKEN_DEMO, useSessao } from '@/sessao/sessao';

jest.mock('expo-router', () => ({ router: { push: jest.fn() } }));

const metricas = { frame: { x: 0, y: 0, width: 390, height: 844 }, insets: { top: 0, left: 0, right: 0, bottom: 0 } };
const tela = (cliente: QueryClient, el: React.ReactElement) =>
  render(
    <SafeAreaProvider initialMetrics={metricas}>
      <QueryClientProvider client={cliente}>{el}</QueryClientProvider>
    </SafeAreaProvider>,
  );

beforeEach(() => {
  reiniciarDemo();
  useSessao.setState({ token: null });
  globalThis.fetch = jest.fn(async () => {
    throw new TypeError('Network request failed');
  });
});

describe('modo demonstração', () => {
  it('sem backend, o app abre com dados de exemplo e um aviso claro', async () => {
    await tela(new QueryClient(), <Entrar />);
    await fireEvent.press(screen.getByRole('button', { name: 'Ver com dados de exemplo' }));
    expect(useSessao.getState().token).toBe(TOKEN_DEMO);

    await tela(new QueryClient(), <Inicio />);
    expect(await screen.findByText('Casa da Dona Lurdes')).toBeOnTheScreen();
    expect(screen.getByText('Casa do Seu Antônio')).toBeOnTheScreen();
    expect(screen.getByText('Modo demonstração')).toBeOnTheScreen();
    expect(globalThis.fetch).not.toHaveBeenCalled();
  });

  it('o "estou bem" resolve o alerta da casa nos dados de exemplo', async () => {
    useSessao.setState({ token: TOKEN_DEMO });
    const antes = await requisitar<Domicilio>(`/domicilios/${CASAS[0]!.id}`);
    expect(antes.alertaAberto).not.toBeNull();
    await requisitar(`/domicilios/${CASAS[0]!.id}/check-ins`, {
      metodo: 'POST',
      corpo: { origem: 'APP', situacao: 'ESTOU_BEM', momento: new Date().toISOString() },
    });
    const depois = await requisitar<Domicilio>(`/domicilios/${CASAS[0]!.id}`);
    expect(depois.alertaAberto).toBeNull();
  });

  it('a onda de calor simulada deixa a casa em Perigo na hora e manda o lembrete leve depois', async () => {
    jest.useFakeTimers();
    useSessao.setState({ token: TOKEN_DEMO });
    queryClient.setQueryData<Resultado<Domicilio[]>>(['casas'], { dados: CASAS, atualizadoEm: '', origem: 'api' });

    act(() => simularOndaDeCalor());
    expect(queryClient.getQueryData<Resultado<Domicilio[]>>(['casas'])!.dados[0]!.nivelAtual).toBe('PERIGO');
    expect(Notifications.scheduleNotificationAsync).not.toHaveBeenCalled();

    act(() => jest.advanceTimersByTime(8000));
    expect(Notifications.scheduleNotificationAsync).toHaveBeenCalledWith(
      expect.objectContaining({
        content: expect.objectContaining({ title: 'Lembrete', body: 'A Dona Lurdes ainda não confirmou que está bem. Quer ligar?' }),
        trigger: { channelId: 'lembretes' },
      }),
    );
    jest.useRealTimers();
  });
});

describe('notificações em degraus (D13)', () => {
  const ctx = { modoIdoso: false, apelido: () => 'Casa da Dona Lurdes', agora: new Date('2026-11-18T18:00:00Z') };
  const noite = { ...ctx, agora: new Date('2026-11-19T03:00:00Z') }; // meia-noite em SP

  it('degrau 1 e 2 são lembretes; degrau 3 é alerta', () => {
    expect(notificacaoDoEvento('AlertaEscalonado', { degrau: 1, mensagem: 'm' }, ctx)?.canal).toBe('lembretes');
    expect(notificacaoDoEvento('AlertaEscalonado', { degrau: 2, mensagem: 'm' }, ctx)?.titulo).toBe('Quem está perto');
    expect(notificacaoDoEvento('AlertaEscalonado', { degrau: 3, mensagem: 'm' }, ctx)?.canal).toBe('alertas');
  });

  it('a família não recebe nada no degrau 0 (é a Sentinela que pede o "estou bem")', () => {
    expect(notificacaoDoEvento('AlertaAberto', { tipo: 'DE_SENTINELA', prazo: '2026-11-18T18:30:00Z' }, ctx)).toBeNull();
  });

  it('pedido de ajuda vira alerta, mesmo de noite', () => {
    expect(notificacaoDoEvento('CheckInRegistrado', { situacao: 'PRECISO_DE_AJUDA' }, noite)?.canal).toBe('alertas');
  });

  it('de noite, nada de aviso preventivo', () => {
    expect(notificacaoDoEvento('AlertaAberto', { tipo: 'PREVENTIVO' }, noite)).toBeNull();
    expect(notificacaoDoEvento('AlertaAberto', { tipo: 'PREVENTIVO' }, ctx)?.canal).toBe('lembretes');
  });

  it('no Modo Idoso, explica o que acontece se ela não responder', () => {
    const n = notificacaoDoEvento('AlertaAberto', { tipo: 'DE_SENTINELA', prazo: '2026-11-18T18:30:00Z' }, { ...ctx, modoIdoso: true });
    expect(n?.corpo).toBe('Abra o Sombra e aperte ESTOU BEM. Se não apertar até 15:30, sua família recebe um lembrete.');
  });
});
