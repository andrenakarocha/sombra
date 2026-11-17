import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen } from '@testing-library/react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import type { Resultado } from '@/api/consultas';
import { aplicarEvento } from '@/api/tempo-real';
import type { Domicilio } from '@/api/tipos';
import Inicio from '@/app/(familia)/(abas)/index';
import { CASAS } from '@/demo/fixtures';

jest.mock('@/api/consultas', () => {
  const real = jest.requireActual('@/api/consultas');
  const { useQuery } = jest.requireActual('@tanstack/react-query');
  // Sem rede no teste: a tela lê só o que já está no cache do TanStack Query.
  return { ...real, useCasas: () => useQuery({ queryKey: ['casas'], queryFn: () => new Promise(() => {}) }) };
});

const casa = CASAS[0]!;

function clienteCom(casas: Domicilio[]) {
  const cliente = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity } } });
  cliente.setQueryData<Resultado<Domicilio[]>>(['casas'], { dados: casas, atualizadoEm: '', origem: 'api' });
  cliente.setQueryData<Resultado<Domicilio>>(['casa', casa.id], { dados: casa, atualizadoEm: '', origem: 'api' });
  return cliente;
}

describe('aplicarEvento', () => {
  it('LeituraRegistrada muda o nível e a leitura da casa na lista e no detalhe', () => {
    const cliente = clienteCom(CASAS);
    aplicarEvento(cliente, 'LeituraRegistrada', {
      domicilioId: casa.id,
      momento: '2026-11-18T18:10:00Z',
      temperaturaC: 37,
      umidadePct: 45,
      indiceDeCalorC: 42.1,
      nivel: 'PERIGO',
    });
    const lista = cliente.getQueryData<Resultado<Domicilio[]>>(['casas'])!.dados[0]!;
    const detalhe = cliente.getQueryData<Resultado<Domicilio>>(['casa', casa.id])!.dados;
    for (const c of [lista, detalhe]) {
      expect(c.nivelAtual).toBe('PERIGO');
      expect(c.ultimaLeitura?.indiceDeCalorC).toBe(42.1);
    }
  });

  it('SentinelaStatus e CheckInRegistrado atualizam a casa', () => {
    const cliente = clienteCom(CASAS);
    aplicarEvento(cliente, 'SentinelaStatus', { domicilioId: casa.id, deviceId: 'snt-7f3a9c', online: false });
    aplicarEvento(cliente, 'CheckInRegistrado', { domicilioId: casa.id, momento: '2026-11-18T18:12:00Z' });
    const c = cliente.getQueryData<Resultado<Domicilio[]>>(['casas'])!.dados[0]!;
    expect(c.sentinela?.online).toBe(false);
    expect(c.ultimoCheckIn).toBe('2026-11-18T18:12:00Z');
  });

  it('eventos de alerta invalidam casas e alertas', () => {
    const cliente = clienteCom(CASAS);
    const invalidar = jest.spyOn(cliente, 'invalidateQueries');
    aplicarEvento(cliente, 'AlertaEscalonado', { domicilioId: casa.id, alertaId: 'a1', degrau: 1 });
    const chaves = invalidar.mock.calls.map(([filtro]) => filtro?.queryKey);
    expect(chaves).toEqual(expect.arrayContaining([['casas'], ['alertas'], ['alerta', 'a1']]));
  });
});

it('o card da tela Início fica vermelho ao vivo, sem recarregar', async () => {
  const cliente = clienteCom(CASAS);
  await render(
    <SafeAreaProvider initialMetrics={{ frame: { x: 0, y: 0, width: 390, height: 844 }, insets: { top: 0, left: 0, right: 0, bottom: 0 } }}>
      <QueryClientProvider client={cliente}>
        <Inicio />
      </QueryClientProvider>
    </SafeAreaProvider>,
  );
  expect(screen.getByLabelText('Nível de calor: Cautela extrema')).toBeOnTheScreen();

  await act(async () => {
    aplicarEvento(cliente, 'LeituraRegistrada', {
      domicilioId: casa.id,
      momento: '2026-11-18T18:10:00Z',
      temperaturaC: 37,
      umidadePct: 45,
      indiceDeCalorC: 42.1,
      nivel: 'PERIGO',
    });
  });

  expect(await screen.findByLabelText('Nível de calor: Perigo')).toBeOnTheScreen();
  expect(screen.getByLabelText('Índice de calor 42,1 graus')).toBeOnTheScreen();
});
