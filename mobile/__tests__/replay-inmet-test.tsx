import { containerDeTeste } from '@test/container';
import { renderComContainer } from '@test/render';
import { act, screen } from '@testing-library/react-native';

import manifesto from '../../ml/dados/brutos/manifesto.json';

import { reproducaoStore } from '@/application/estado/reproducao';
import { sessaoStore, TOKEN_DEMO } from '@/application/estado/sessao';
import Inicio from '@/app/(familia)/(abas)/index';
import { gravidade } from '@/domain/clima/nivel-de-risco';
import { passoDoReplay, REPLAY } from '@/infrastructure/demo/replay-inmet';

jest.mock('expo-router', () => ({ router: { push: jest.fn() } }));

const AGORA = new Date('2026-11-18T18:00:00Z');
const pico = REPLAY.horas.reduce((a, b) => (passoDoReplay(b, undefined, AGORA).leitura.indiceDeCalorC > passoDoReplay(a, undefined, AGORA).leitura.indiceDeCalorC ? b : a));

describe('dados reais do INMET (A701, nov/2023)', () => {
  it('vêm do CSV versionado, conferido pelo SHA-256 do manifesto', () => {
    expect(REPLAY.sha256Csv).toBe(manifesto.arquivos['2023'].sha256Csv);
    expect(REPLAY.url).toBe(manifesto.arquivos['2023'].url);
    expect(REPLAY.horas).toHaveLength(96);
    expect(REPLAY.horasFaltando).toBe(0);
  });

  it('o pico real é um índice de 35,5 °C, nas tardes de 14/11 (37,2 °C, 19%) e 15/11 (35,7 °C, 29%)', () => {
    expect(passoDoReplay(pico, undefined, AGORA).leitura.indiceDeCalorC).toBe(35.5);
    const no355 = REPLAY.horas.filter((h) => passoDoReplay(h, undefined, AGORA).leitura.indiceDeCalorC === 35.5);
    expect(no355).toEqual([
      { momento: '2023-11-14T17:00:00Z', temperaturaC: 37.2, umidadePct: 19 },
      { momento: '2023-11-15T18:00:00Z', temperaturaC: 35.7, umidadePct: 29 },
    ]);
  });

  it('pela régua da NWS, nenhuma hora real passa de CAUTELA_EXTREMA (a estação é a céu aberto)', () => {
    const niveis = new Set(REPLAY.horas.map((h) => passoDoReplay(h, undefined, AGORA).nivelPadrao));
    expect(niveis.has('CAUTELA_EXTREMA')).toBe(true);
    expect([...niveis].every((n) => gravidade(n) <= gravidade('CAUTELA_EXTREMA'))).toBe(true);
  });

  it('R3: o mesmo pico real é PERIGO para quem tem cardiopatia e toma diurético', () => {
    const lurdes = passoDoReplay(pico, { fatoresDeRisco: ['USO_DE_DIURETICO'], idade: 78 }, AGORA);
    const comCoracao = passoDoReplay(pico, { fatoresDeRisco: ['CARDIOPATIA', 'USO_DE_DIURETICO'], idade: 78 }, AGORA);
    expect([lurdes.nivelPadrao, lurdes.nivelPersonalizado, comCoracao.nivelPersonalizado]).toEqual([
      'CAUTELA_EXTREMA',
      'CAUTELA_EXTREMA',
      'PERIGO',
    ]);
  });
});

describe('replay no modo demonstração', () => {
  afterEach(() => jest.useRealTimers());

  it('🔎 toca as horas reais: o card muda de nível ao vivo, a Sentinela pede o "estou bem" e a tela diz que é dado real', async () => {
    jest.useFakeTimers({ now: AGORA });
    const { container } = await containerDeTeste();
    sessaoStore.setState({ token: TOKEN_DEMO });
    await renderComContainer(<Inicio />, container);
    expect(await screen.findByLabelText('Nível de calor: Cautela extrema')).toBeOnTheScreen();

    await act(async () => container.demonstracao.reproduzirOndaReal(100));
    // 12/11, 0h em SP: madrugada fresca.
    expect(await screen.findByLabelText('Nível de calor: Normal')).toBeOnTheScreen();
    expect(screen.getByText(/Dados reais do INMET · 12\/11\/2023/)).toBeOnTheScreen();

    // Até 15/11 às 15h em SP, uma das duas horas de pico (passo 88 de 96).
    while (reproducaoStore.getState().momentoOriginal !== '2023-11-15T18:00:00Z') {
      await act(async () => jest.advanceTimersByTime(100));
    }
    expect(reproducaoStore.getState()).toMatchObject({ passo: 88, total: 96, indiceC: 35.5 });
    expect(await screen.findByLabelText('Nível de calor: Cautela extrema')).toBeOnTheScreen();
    expect(screen.getByText(/Régua padrão \(NWS\): Cautela extrema · para quem mora na casa: Cautela extrema/)).toBeOnTheScreen();
    expect((await container.demoGateway.listarAlertas()).some((a) => a.estado !== 'RESOLVIDO')).toBe(true);

    await act(async () => jest.advanceTimersByTime(20 * 100));
    expect(reproducaoStore.getState().ativo).toBe(false);
  });

  it('parar e recomeçar zeram o replay', async () => {
    jest.useFakeTimers({ now: AGORA });
    const { container } = await containerDeTeste();
    sessaoStore.setState({ token: TOKEN_DEMO });
    container.demonstracao.reproduzirOndaReal(100);
    expect(reproducaoStore.getState().ativo).toBe(true);
    container.demonstracao.pararReproducao();
    expect(reproducaoStore.getState().ativo).toBe(false);
    container.demonstracao.reproduzirOndaReal(100);
    await container.demonstracao.recomecar();
    expect(reproducaoStore.getState()).toMatchObject({ ativo: false, passo: 0 });
  });
});
