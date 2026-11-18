import { containerDeTeste } from '@test/container';
import { renderComContainer } from '@test/render';
import { act, fireEvent, screen } from '@testing-library/react-native';
import * as Speech from 'expo-speech';
import { Alert } from 'react-native';

import { preferenciasStore } from '@/application/estado/preferencias';
import { sessaoStore, TOKEN_DEMO } from '@/application/estado/sessao';
import Inicio from '@/app/(familia)/(abas)/index';
import RotinaDaCasa from '@/app/(familia)/casa/[id]/rotina';
import ModoIdoso from '@/app/idoso';
import { ClienteHttp } from '@/infrastructure/http/cliente-http';
import { CoreHttpGateway } from '@/infrastructure/http/core-http-gateway';
import { CASAS } from '@/infrastructure/demo/fixtures';

const LURDES = CASAS[0]!.id;
jest.mock('expo-router', () => ({
  router: { push: jest.fn() },
  useLocalSearchParams: () => ({ id: '0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10' }),
  Stack: { Screen: () => null },
}));
jest.mock('expo-speech', () => ({ speak: jest.fn(), stop: jest.fn() }));
jest.mock('expo-haptics', () => ({ notificationAsync: jest.fn(), NotificationFeedbackType: {} }));
afterEach(() => jest.restoreAllMocks());

it('rotas HTTP de alerta, rotina e vínculo da Sentinela, com o contrato conferido', async () => {
  const respostas = [
    { ...CASAS[0] },
    { id: 'a1', domicilioId: 'd1', apelidoDaCasa: 'A', tipo: 'DE_SENTINELA', estado: 'ESCALONADO', nivel: 'PERIGO', degrau: 1, abertoEm: '2026-11-18T18:00:00Z', prazo: '2026-11-18T18:15:00Z' },
    { checkInDiario: { ativo: true, horario: '09:00' }, lembreteDeAgua: { ativo: false } },
    { checkInDiario: { ativo: false, horario: '09:00' }, lembreteDeAgua: { ativo: true } },
    { deviceId: 'snt-abcdef', segredo: 'f'.repeat(64) },
  ];
  const chamadas: [string, RequestInit][] = [];
  globalThis.fetch = jest.fn(async (url: string, init: RequestInit) => {
    chamadas.push([url, init]);
    return { ok: true, status: 200, headers: new Headers(), json: async () => respostas.shift() } as Response;
  }) as unknown as typeof fetch;
  const g = new CoreHttpGateway(new ClienteHttp(async () => {}));
  await g.buscarCasa('d1');
  expect((await g.buscarAlerta('a1')).degrau).toBe(1);
  expect((await g.rotina('d1')).checkInDiario.horario).toBe('09:00');
  await g.salvarRotina('d1', { checkInDiario: { ativo: false, horario: '09:00' }, lembreteDeAgua: { ativo: true } });
  expect(await g.vincularSentinela('d1', 'snt-abcdef', 'K7P2QX')).toEqual({ deviceId: 'snt-abcdef', segredo: 'f'.repeat(64) });
  expect(chamadas.map(([u, i]) => `${i.method} ${u.replace('http://localhost:5080/api/v1', '')}`)).toEqual([
    'GET /domicilios/d1',
    'GET /alertas/a1',
    'GET /domicilios/d1/rotina',
    'PUT /domicilios/d1/rotina',
    'POST /domicilios/d1/sentinelas',
  ]);
  expect(JSON.parse(chamadas[4]![1].body as string)).toEqual({ deviceId: 'snt-abcdef', codigoDeVinculo: 'K7P2QX' });
});

describe('Modo Idoso', () => {
  it('"Ouvir de novo", "Voltar" depois de responder, e o 192 (que na demonstração não liga)', async () => {
    const { container, rede } = await containerDeTeste();
    sessaoStore.setState({ token: TOKEN_DEMO });
    preferenciasStore.setState({ modoIdosoCasaId: LURDES });
    const aviso = jest.spyOn(Alert, 'alert').mockImplementation(() => {});
    await renderComContainer(<ModoIdoso />, container);
    await fireEvent.press(await screen.findByRole('button', { name: 'Ouvir de novo' }));
    expect(Speech.speak).toHaveBeenLastCalledWith(expect.stringContaining('Sua casa está com'), expect.anything());

    rede.offline = true;
    container.sincronizacao.motor.definirForcadoOffline(true);
    await fireEvent(screen.getByRole('button', { name: 'PRECISO DE AJUDA' }), 'longPress');
    await fireEvent.press(await screen.findByRole('button', { name: 'Ligar 192 (SAMU)' }));
    expect(aviso).toHaveBeenCalledWith('Modo demonstração', expect.any(String));
    await fireEvent.press(screen.getByRole('button', { name: 'Voltar' }));
    expect(await screen.findByRole('button', { name: 'ESTOU BEM' })).toBeOnTheScreen();
  });
});

it('a rede voltando com a tela aberta drena a fila e refaz as consultas', async () => {
  const { container, rede, mudarRede, clock } = await containerDeTeste();
  await container.consultas.casas();
  rede.offline = true;
  await container.escritas.registrarCheckIn(LURDES, 'ESTOU_BEM');
  await renderComContainer(<Inicio />, container);
  expect(await screen.findByRole('button', { name: '1 aviso esperando a internet' })).toBeOnTheScreen();
  clock.avancarMinutos(1);
  await act(async () => {
    mudarRede(true);
    await container.sincronizacao.motor.drenar();
  });
  expect(rede.recebidas).toBe(1);
  expect(screen.queryByRole('button', { name: '1 aviso esperando a internet' })).toBeNull();
});

describe('Rotina', () => {
  it('sem rede: avisa ao abrir e ao salvar; desligar o check-in esconde os horários', async () => {
    const { container, rede } = await containerDeTeste();
    rede.offline = true;
    await renderComContainer(<RotinaDaCasa />, container);
    expect(await screen.findByText(/Confira a internet/)).toBeOnTheScreen();
    await fireEvent(screen.getByLabelText('"Estou bem" todo dia'), 'valueChange', true);
    expect(screen.getByRole('radio', { name: '9 horas' })).toBeOnTheScreen();
    await fireEvent(screen.getByLabelText('"Estou bem" todo dia'), 'valueChange', false);
    expect(screen.queryByRole('radio', { name: '9 horas' })).toBeNull();
    await act(async () => {
      await fireEvent.press(screen.getByRole('button', { name: 'Salvar a rotina' }));
    });
    expect(await screen.findByText(/Confira a internet/)).toBeOnTheScreen();
  });
});
