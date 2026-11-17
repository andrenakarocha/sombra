import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import type { Resultado } from '@/api/consultas';
import type { Domicilio, Morador, PrivacidadeDaCasa } from '@/api/tipos';
import Privacidade from '@/app/(familia)/(abas)/privacidade';
import MoradorDaCasa from '@/app/(familia)/casa/[id]/morador';
import { CASAS } from '@/demo/fixtures';
import { VAZIO, validar } from '@/dominio/morador';
import { abertoAgora } from '@/util/formato';
import { aplicarPadrao, exibirCartaoSus, exibirCpf, exibirTelefone, PADRAO } from '@/util/mascaras';

const casa = CASAS[0]!;
jest.mock('expo-router', () => ({ useLocalSearchParams: () => ({ id: '0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10' }) }));

// CPF sintético: os 9 primeiros dígitos com os verificadores calculados aqui, sem nenhum documento real.
function cpfSintetico(base: string) {
  const dv = (d: string) => ((([...d].reduce((s, c, i) => s + Number(c) * (d.length + 1 - i), 0) * 10) % 11) % 10);
  const comPrimeiro = base + dv(base);
  return comPrimeiro + dv(comPrimeiro);
}

const metricas = { frame: { x: 0, y: 0, width: 390, height: 844 }, insets: { top: 0, left: 0, right: 0, bottom: 0 } };
function comCliente(cliente: QueryClient, tela: React.ReactElement) {
  return render(
    <SafeAreaProvider initialMetrics={metricas}>
      <QueryClientProvider client={cliente}>{tela}</QueryClientProvider>
    </SafeAreaProvider>,
  );
}
const novoCliente = () => new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, retry: false } } });

describe('máscaras', () => {
  const cpf = cpfSintetico('123456789');

  it('formata enquanto a pessoa digita', () => {
    expect(aplicarPadrao('1234', PADRAO.cpf)).toBe('123.4');
    expect(aplicarPadrao(cpf, PADRAO.cpf)).toBe('123.456.789-09');
    expect(aplicarPadrao('11987654321', PADRAO.telefone)).toBe('(11) 98765-4321');
  });

  it('mascara na tela o que a API devolver inteiro, e mantém o que já veio mascarado', () => {
    expect(exibirCpf(aplicarPadrao(cpf, PADRAO.cpf))).toBe('***.456.789-**');
    expect(exibirCpf('***.982.247-**')).toBe('***.982.247-**');
    expect(exibirCartaoSus('700000000001234')).toBe('*** **** **** 1234');
    expect(exibirTelefone('(11) 98765-4321')).toBe('(11) *****-4321');
  });

  it('validar: CPF obrigatório no cadastro, opcional na edição', () => {
    const base = { ...VAZIO, nome: 'Lurdes A.', idade: '78' };
    expect(validar(base, false)).toBe('O CPF tem 11 números.');
    expect(validar(base, true)).toBeNull();
    expect(validar({ ...base, cpf }, false)).toBeNull();
  });
});

it('abertoAgora compara no horário de São Paulo', () => {
  expect(abertoAgora('09:00', '18:00', new Date('2026-11-18T15:00:00Z'))).toBe(true); // 12:00 em SP
  expect(abertoAgora('09:00', '18:00', new Date('2026-11-18T22:00:00Z'))).toBe(false); // 19:00 em SP
});

it('cadastro do morador mostra CPF, Cartão SUS e saúde mascarados', async () => {
  const cliente = novoCliente();
  const morador: Morador = {
    id: 'm1',
    nome: 'Lurdes A.',
    idade: 78,
    telefone: null,
    cpf: '***.982.247-**',
    cartaoSus: '*** **** **** 1234',
    perfilDeSaude: '[DADO DE SAÚDE PROTEGIDO]',
    fatoresDeRisco: ['IDADE_AVANCADA'],
  };
  cliente.setQueryData(['moradores', casa.id], [morador]);
  await comCliente(cliente, <MoradorDaCasa />);
  expect(screen.getByLabelText('CPF mascarado: ***.982.247-**')).toBeOnTheScreen();
  expect(screen.getByText('Cartão SUS *** **** **** 1234')).toBeOnTheScreen();
  expect(screen.getByText('[DADO DE SAÚDE PROTEGIDO]')).toBeOnTheScreen();
});

it('o farol de privacidade reflete o que o Core devolve, com texto e não só cor', async () => {
  const cliente = novoCliente();
  cliente.setQueryData<Resultado<Domicilio[]>>(['casas'], { dados: CASAS, atualizadoEm: '', origem: 'api' });
  cliente.setQueryData<PrivacidadeDaCasa>(['privacidade', casa.id], {
    farol: 'AMARELO',
    mensagens: [
      {
        momento: '2026-11-18T18:05:00Z',
        mensagemCidadao: 'Tentaram acessar o laudo de saúde da Dona Lurdes, mas o Sombra bloqueou.',
        fonte: 'IA',
      },
    ],
  });
  await comCliente(cliente, <Privacidade />);
  expect(screen.getByLabelText('Privacidade: Atenção')).toBeOnTheScreen();
  expect(screen.getByText(/o Sombra bloqueou/)).toBeOnTheScreen();
});
