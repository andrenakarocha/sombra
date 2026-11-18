import { containerDeTeste } from '@test/container';
import { renderComContainer } from '@test/render';
import { act, fireEvent, screen } from '@testing-library/react-native';

import { preferenciasStore } from '@/application/estado/preferencias';
import RotinaDaCasa from '@/app/(familia)/casa/[id]/rotina';
import VincularSentinela from '@/app/(familia)/casa/[id]/sentinela';
import ModoIdoso from '@/app/idoso';
import { proximaOcorrenciaEmSaoPaulo } from '@/domain/compartilhado/clock';
import { valorOuErro } from '@/domain/compartilhado/result';
import { Rotina } from '@/domain/rotina/rotina';
import { lerQrDeVinculo, segredoValido, vinculoDigitado } from '@/domain/sentinela/qr-de-vinculo';
import { CASAS } from '@/infrastructure/demo/fixtures';

const LURDES = CASAS[0]!.id;
jest.mock('expo-router', () => ({
  router: { push: jest.fn() },
  useLocalSearchParams: () => ({ id: '0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10' }),
  Stack: { Screen: () => null },
}));
jest.mock('expo-camera', () => ({ CameraView: () => null, useCameraPermissions: () => [{ granted: false }, async () => ({ granted: false })] }));
jest.mock('expo-speech', () => ({ speak: jest.fn(), stop: jest.fn() }));
jest.mock('expo-haptics', () => ({ notificationAsync: jest.fn(), NotificationFeedbackType: {} }));

const codigo = (r: { ok: boolean; erro?: { codigo: string } }) => (r.ok ? null : r.erro!.codigo);

describe('QR de vínculo da Sentinela', () => {
  it('lê o QR e normaliza o que foi digitado', () => {
    expect(valorOuErro(lerQrDeVinculo('sombra://sentinela/vincular?v=1&d=snt-7f3a9c&c=K7P2QX'))).toEqual({ deviceId: 'snt-7f3a9c', codigo: 'K7P2QX' });
    expect(valorOuErro(vinculoDigitado(' SNT-7F3A9C ', 'k7p2qx'))).toEqual({ deviceId: 'snt-7f3a9c', codigo: 'K7P2QX' });
  });

  it.each([
    ['https://exemplo.com', 'QR_DESCONHECIDO'],
    ['sombra://sentinela/vincular?v=2&d=snt-7f3a9c&c=K7P2QX', 'QR_VERSAO'],
    ['sombra://sentinela/vincular?v=1&d=snt-7f3a9&c=K7P2QX', 'QR_DEVICE'],
    ['sombra://sentinela/vincular?v=1&d=snt-7f3a9c&c=K7P2Q0', 'QR_CODIGO'],
    ['sombra://sentinela/vincular?v=1&d=snt-7f3a9c', 'QR_CODIGO'],
  ])('%s → %s', (texto, esperado) => {
    expect(codigo(lerQrDeVinculo(texto))).toBe(esperado);
  });

  it('segredo HMAC: 32 bytes em hexadecimal minúsculo', () => {
    expect(segredoValido('a'.repeat(64))).toBe(true);
    expect(segredoValido('A'.repeat(64))).toBe(false);
    expect(segredoValido('a'.repeat(63))).toBe(false);
  });
});

describe('Rotina (R11, R12)', () => {
  const rotina = (horario: string, agua = true, ativo = true) => Rotina.criar({ checkInDiario: { ativo, horario }, lembreteDeAgua: { ativo: agua } });

  it('check-in diário só de dia: 22h–7h é recusado', () => {
    expect(rotina('07:00').ok).toBe(true);
    expect(rotina('21:59').ok).toBe(true);
    expect(codigo(rotina('22:00'))).toBe('R12');
    expect(codigo(rotina('06:59'))).toBe('R12');
    expect(codigo(rotina('9h'))).toBe('HORARIO');
    expect(rotina('23:00', true, false).ok).toBe(true);
  });

  it('água só em dia de calor, duas vezes no pico da tarde, em ordem com o check-in', () => {
    const r = valorOuErro(rotina('16:00'));
    expect(r.lembretesDoDia('NORMAL').map((l) => l.tipo)).toEqual(['CHECK_IN_DIARIO']);
    expect(r.lembretesDoDia('ATENCAO').map((l) => `${l.tipo} ${l.horario}`)).toEqual(['AGUA 15:00', 'AGUA 15:45', 'CHECK_IN_DIARIO 16:00']);
    expect(valorOuErro(rotina('09:00', false)).lembretesDoDia('PERIGO').map((l) => l.tipo)).toEqual(['CHECK_IN_DIARIO']);
    expect(Rotina.padrao().lembretesDoDia('CAUTELA_EXTREMA')).toHaveLength(2);
  });

  it('o horário é o de São Paulo, seja qual for o fuso do aparelho', () => {
    // 18:00 UTC = 15:00 em SP.
    expect(proximaOcorrenciaEmSaoPaulo(new Date('2026-11-18T18:00:00Z'), 15, 45)).toEqual({ quando: new Date('2026-11-18T18:45:00Z'), jaPassouHoje: false });
    expect(proximaOcorrenciaEmSaoPaulo(new Date('2026-11-18T18:00:00Z'), 9, 0)).toEqual({ quando: new Date('2026-11-19T12:00:00Z'), jaPassouHoje: true });
    // 01:00 UTC do dia 19 ainda é 22h do dia 18 em SP.
    expect(proximaOcorrenciaEmSaoPaulo(new Date('2026-11-19T01:00:00Z'), 9, 0).quando).toEqual(new Date('2026-11-19T12:00:00Z'));
  });
});

describe('telas', () => {
  it('🔎 vincular digitando: o segredo aparece uma vez e fica no cofre', async () => {
    const { container } = await containerDeTeste();
    await container.consultas.painelDaCasa(LURDES);
    await renderComContainer(<VincularSentinela />, container);
    await fireEvent.changeText(screen.getByLabelText('Número da Sentinela'), 'snt-12ab');
    await fireEvent.press(screen.getByRole('button', { name: 'Vincular com o número digitado' }));
    expect(await screen.findByText('O número da Sentinela tem o formato snt-7f3a9c.')).toBeOnTheScreen();

    await fireEvent.changeText(screen.getByLabelText('Número da Sentinela'), 'snt-12ab34');
    await fireEvent.changeText(screen.getByLabelText('Código de vínculo'), 'k7p2qx');
    await fireEvent.press(screen.getByRole('button', { name: 'Vincular com o número digitado' }));
    expect(await screen.findByText('Sentinela snt-12ab34 vinculada a esta casa.')).toBeOnTheScreen();
    const segredo = await container.casaESentinela.segredoDa('snt-12ab34');
    expect(segredoValido(segredo!)).toBe(true);
    expect(screen.getByText(segredo!)).toBeOnTheScreen();
    expect((await container.consultas.painelDaCasa(LURDES)).dados.casa.sentinela?.deviceId).toBe('snt-12ab34');
  });

  it('sem câmera, explica e oferece a digitação', async () => {
    const { container } = await containerDeTeste();
    await renderComContainer(<VincularSentinela />, container);
    await fireEvent.press(screen.getByRole('button', { name: 'Ler o QR da Sentinela' }));
    expect(await screen.findByText('Sem a câmera, digite o número e o código abaixo.')).toBeOnTheScreen();
  });

  it('a rotina salva vai para o Core', async () => {
    const { container, core } = await containerDeTeste();
    await renderComContainer(<RotinaDaCasa />, container);
    await fireEvent.press(await screen.findByRole('radio', { name: '10 horas' }));
    await fireEvent(screen.getByLabelText('Lembrete de água'), 'valueChange', false);
    await fireEvent.press(screen.getByRole('button', { name: 'Salvar a rotina' }));
    expect(await screen.findByText(/Rotina salva/)).toBeOnTheScreen();
    expect(await core.rotina(LURDES)).toEqual({ checkInDiario: { ativo: true, horario: '10:00' }, lembreteDeAgua: { ativo: false } });
  });

  it('o Modo Idoso programa os lembretes do dia no celular da pessoa', async () => {
    const programar = jest.fn(async () => 3);
    const { container } = await containerDeTeste({ agendador: { programar } });
    preferenciasStore.setState({ modoIdosoCasaId: LURDES });
    await renderComContainer(<ModoIdoso />, container);
    await act(async () => {
      await new Promise((r) => setTimeout(r, 50));
    });
    expect(programar).toHaveBeenCalled();
    const [lembretes] = programar.mock.calls.at(-1)! as unknown as [{ tipo: string }[]];
    expect(lembretes.map((l) => l.tipo)).toEqual(['CHECK_IN_DIARIO', 'AGUA', 'AGUA']);
  });
});
