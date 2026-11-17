import { QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react-native';
import * as Speech from 'expo-speech';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { registrarCheckIn, sincronizarFila } from '@/api/check-in';
import { ErroDaApi, SemConexao } from '@/api/cliente';
import { queryClient, type Resultado } from '@/api/consultas';
import type { Domicilio } from '@/api/tipos';
import ModoIdoso from '@/app/idoso';
import type { CheckInNaFila } from '@/banco/fila';
import { CASAS } from '@/demo/fixtures';
import { proximoPasso } from '@/dominio/alerta';
import { usePreferencias } from '@/sessao/preferencias';

// A fila do SQLite em memória: o teste é o modo avião de verdade, só que sem aparelho.
const fila: CheckInNaFila[] = [];
jest.mock('@/banco/fila', () => ({
  enfileirar: async (domicilioId: string, situacao: CheckInNaFila['situacao'], momento: string) => {
    fila.push({ id: fila.length + 1, domicilioId, situacao, momento });
  },
  pendentes: async () => [...fila],
  remover: async (id: number) => {
    fila.splice(fila.findIndex((i) => i.id === id), 1);
  },
}));
jest.mock('expo-speech', () => ({ speak: jest.fn(), stop: jest.fn() }));
jest.mock('expo-haptics', () => ({
  notificationAsync: jest.fn(),
  NotificationFeedbackType: { Success: 'success', Warning: 'warning' },
}));

const casa = CASAS[0]!;
const ok = { ok: true, status: 204, headers: new Headers(), json: async () => ({}) } as Response;
const semRede = async () => {
  throw new TypeError('Network request failed');
};

beforeEach(() => fila.splice(0));

describe('fila offline de check-in', () => {
  it('em modo avião o "estou bem" fica na fila e sincroniza quando a rede volta, com o horário do toque', async () => {
    globalThis.fetch = jest.fn(semRede);
    expect(await registrarCheckIn(casa.id, 'ESTOU_BEM')).toBe('NA_FILA');
    expect(fila).toHaveLength(1);
    const apertouEm = fila[0]!.momento;

    const fetchMock = jest.fn(async () => ok);
    globalThis.fetch = fetchMock as unknown as typeof fetch;
    expect(await sincronizarFila()).toBe(0);
    expect(fila).toHaveLength(0);
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toContain(`/domicilios/${casa.id}/check-ins`);
    expect(JSON.parse(init.body as string)).toEqual({ origem: 'APP', situacao: 'ESTOU_BEM', momento: apertouEm });
  });

  it('envia em ordem e para no primeiro erro que vale repetir', async () => {
    const itens: CheckInNaFila[] = [
      { id: 1, domicilioId: 'a', situacao: 'ESTOU_BEM', momento: '1' },
      { id: 2, domicilioId: 'a', situacao: 'PRECISO_DE_AJUDA', momento: '2' },
    ];
    const enviados: number[] = [];
    const removidos: number[] = [];
    const restantes = await sincronizarFila({
      pendentes: async () => itens,
      remover: async (id) => void removidos.push(id),
      enviar: async (item) => {
        if (item.id === 2) throw new SemConexao();
        enviados.push(item.id);
      },
    });
    expect(enviados).toEqual([1]);
    expect(removidos).toEqual([1]);
    expect(restantes).toBe(1);
  });

  it('erro definitivo (404) sai da fila para não travar os próximos', async () => {
    const removidos: number[] = [];
    const restantes = await sincronizarFila({
      pendentes: async () => [{ id: 7, domicilioId: 'x', situacao: 'ESTOU_BEM', momento: '1' }],
      remover: async (id) => void removidos.push(id),
      enviar: async () => {
        throw new ErroDaApi(404, 'Casa não encontrada', 'abc');
      },
    });
    expect(removidos).toEqual([7]);
    expect(restantes).toBe(0);
  });
});

describe('Modo Idoso', () => {
  const metricas = { frame: { x: 0, y: 0, width: 390, height: 844 }, insets: { top: 0, left: 0, right: 0, bottom: 0 } };

  beforeEach(() => {
    usePreferencias.setState({ modoIdosoCasaId: casa.id, carregada: true });
    queryClient.setQueryData<Resultado<Domicilio>>(['casa', casa.id], { dados: casa, atualizadoEm: '', origem: 'api' });
  });

  it('diz o que acontece se ela não responder (D13) e lê a situação em voz alta', async () => {
    await render(
      <SafeAreaProvider initialMetrics={metricas}>
        <QueryClientProvider client={queryClient}>
          <ModoIdoso />
        </QueryClientProvider>
      </SafeAreaProvider>,
    );
    expect(screen.getByText(proximoPasso(casa))).toBeOnTheScreen();
    expect(proximoPasso(casa)).toBe('Se não apertar até 15:30, sua família recebe um lembrete.');
    expect(Speech.speak).toHaveBeenCalledWith(expect.stringContaining('Sua casa está com 33 graus.'), expect.anything());
  });

  it('ESTOU BEM sem internet confirma que ficou guardado', async () => {
    globalThis.fetch = jest.fn(semRede);
    await render(
      <SafeAreaProvider initialMetrics={metricas}>
        <QueryClientProvider client={queryClient}>
          <ModoIdoso />
        </QueryClientProvider>
      </SafeAreaProvider>,
    );
    await fireEvent.press(screen.getByRole('button', { name: 'ESTOU BEM' }));
    expect(await screen.findByText(/Guardado\. Sem internet agora/)).toBeOnTheScreen();
    expect(fila).toHaveLength(1);
  });
});
