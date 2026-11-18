// O app inteiro, com as rotas reais do Expo Router (layouts, guardas, abas e pilhas), sobre o composition root de
// verdade em SQLite (sql.js). Só os módulos do aparelho são trocados por fakes.

import { containerDeTeste } from '@test/container';
import { router } from 'expo-router';
import { act, fireEvent, renderRouter, screen } from 'expo-router/testing-library';
import { Linking } from 'react-native';

import type { Container } from '@/application/container';
import { bloqueioStore } from '@/application/estado/bloqueio';
import { preferenciasStore } from '@/application/estado/preferencias';
import { sessaoStore } from '@/application/estado/sessao';

let mockContainer: Container;
jest.mock('@/infrastructure/container-do-aparelho', () => ({ criarContainerDoAparelho: async () => mockContainer }));
jest.mock('expo-splash-screen', () => ({ preventAutoHideAsync: jest.fn(async () => true), hideAsync: jest.fn(async () => true) }));
jest.mock('expo-font', () => ({ ...jest.requireActual('expo-font'), useFonts: () => [true, null], isLoaded: () => true }));
jest.mock('expo-camera', () => ({ CameraView: () => null, useCameraPermissions: () => [{ granted: false }, async () => ({ granted: false })] }));
jest.mock('expo-speech', () => ({ speak: jest.fn(), stop: jest.fn() }));
jest.mock('expo-haptics', () => ({ notificationAsync: jest.fn(), NotificationFeedbackType: {} }));

jest.setTimeout(60_000);

const abrir = (initialUrl = '/') => renderRouter('./src/app', { initialUrl });

beforeEach(async () => {
  ({ container: mockContainer } = await containerDeTeste({ logado: false }));
  sessaoStore.setState({ token: null, carregada: false });
  preferenciasStore.setState({ modoIdosoCasaId: null, carregada: false });
  bloqueioStore.setState({ ativo: false, bloqueado: false });
});

it('sem sessão, qualquer endereço cai no Entrar', async () => {
  await abrir('/alertas');
  expect(await screen.findByRole('button', { name: 'Entrar' })).toBeOnTheScreen();
});

it('jornada da família: demonstração → abas → casa → alerta → Ajustes → sair', async () => {
  const rota = await abrir();
  await fireEvent.press(await screen.findByRole('button', { name: 'Ver com dados de exemplo' }));
  expect(await screen.findByText('Suas casas')).toBeOnTheScreen();
  expect(await screen.findByText('Casa da Dona Lurdes')).toBeOnTheScreen();

  await fireEvent.press(screen.getAllByRole('button', { name: 'Ver a casa' })[0]!);
  expect(await screen.findByText('Por que Lurdes está em risco')).toBeOnTheScreen();
  expect(rota.getPathname()).toBe('/casa/0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10');

  // Ligar, no modo demonstração, não liga (os telefones são de exemplo).
  const ligar = jest.spyOn(Linking, 'openURL');
  await fireEvent.press(screen.getAllByRole('button', { name: 'Ligar' })[0]!);
  expect(ligar).not.toHaveBeenCalled();

  await fireEvent.press(screen.getByRole('button', { name: 'Ver o alerta' }));
  expect(await screen.findByText('Linha do tempo')).toBeOnTheScreen();
  await fireEvent.press(screen.getByRole('button', { name: 'Vou ligar' }));
  expect(await screen.findByText('Pronto, a rede de cuidado foi avisada.')).toBeOnTheScreen();

  await act(async () => router.back());
  await act(async () => router.back());
  await fireEvent.press(await screen.findByLabelText(/^Alertas/));
  expect(await screen.findByText('Resolvidos')).toBeOnTheScreen();
  await fireEvent.press(screen.getByLabelText(/^Perto de mim, tab/));
  expect(await screen.findByText('Biblioteca do bairro (exemplo)')).toBeOnTheScreen();
  await fireEvent.press(screen.getByLabelText(/^Privacidade, tab/));
  expect(await screen.findByLabelText('Privacidade: Atenção')).toBeOnTheScreen();

  await fireEvent.press(screen.getByLabelText(/^Ajustes, tab/));
  await fireEvent.press(await screen.findByRole('button', { name: 'Central de sincronização' }));
  expect(await screen.findByText('Tudo enviado.')).toBeOnTheScreen();
  await act(async () => router.back());

  await fireEvent.press(await screen.findByRole('button', { name: 'Sair da demonstração' }));
  expect(await screen.findByRole('button', { name: 'Entrar' })).toBeOnTheScreen();
});

it('Modo Idoso: o celular passa a abrir só na tela grande, e segurar "Modo família" volta', async () => {
  await abrir();
  await fireEvent.press(await screen.findByRole('button', { name: 'Ver com dados de exemplo' }));
  await fireEvent.press(await screen.findByLabelText(/^Ajustes, tab/));
  await fireEvent.press(await screen.findByRole('button', { name: 'Usar neste celular: Casa da Dona Lurdes' }));
  expect(await screen.findByRole('button', { name: 'ESTOU BEM' })).toBeOnTheScreen();
  expect(screen.queryByText('Suas casas')).toBeNull();

  await fireEvent(screen.getByRole('button', { name: 'Modo família' }), 'longPress');
  expect(await screen.findByText('Suas casas')).toBeOnTheScreen();
});

it('com o bloqueio ligado antes, o app abre trancado e a digital libera', async () => {
  const autenticar = jest.fn(async () => true);
  ({ container: mockContainer } = await containerDeTeste({ logado: false, biometria: { disponivel: async () => true, autenticar } }));
  await mockContainer.sessao.entrarNaDemonstracao();
  await mockContainer.bloqueio.ativar();
  autenticar.mockResolvedValueOnce(false);
  await abrir();
  expect(await screen.findByText(/estão protegidos/)).toBeOnTheScreen();
  await fireEvent.press(screen.getByRole('button', { name: 'Desbloquear' }));
  expect(await screen.findByText('Suas casas')).toBeOnTheScreen();
  expect(screen.queryByText(/estão protegidos/)).toBeNull();
});
