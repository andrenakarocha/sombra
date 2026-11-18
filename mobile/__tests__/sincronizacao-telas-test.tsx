import { containerDeTeste } from '@test/container';
import { renderComContainer } from '@test/render';
import { act, fireEvent, screen } from '@testing-library/react-native';

import { CentralDeSincronizacao } from '@/application/casos-de-uso/central-de-sincronizacao';
import { ErroDaApi } from '@/application/erros';
import Inicio from '@/app/(familia)/(abas)/index';
import Sincronizacao from '@/app/(familia)/sincronizacao';
import { CASAS } from '@/infrastructure/demo/fixtures';

const LURDES = CASAS[0]!.id;
jest.mock('expo-router', () => ({ router: { push: jest.fn() }, useLocalSearchParams: () => ({}), Stack: { Screen: () => null } }));

describe('Central de sincronização', () => {
  it('🔎 forçar offline na tela → o "estou bem" fica esperando → desligar → enviado uma vez', async () => {
    const { container, rede } = await containerDeTeste();
    await container.consultas.painelDaCasa(LURDES);
    await renderComContainer(<Sincronizacao />, container);
    expect(await screen.findByText('Tudo enviado.')).toBeOnTheScreen();

    await fireEvent(screen.getByLabelText('Simular sem internet'), 'valueChange', true);
    await act(async () => {
      await container.escritas.registrarCheckIn(LURDES, 'ESTOU_BEM');
    });
    expect(await screen.findByText('1 esperando · 0 não aceitos')).toBeOnTheScreen();
    expect(screen.getByText(/"Estou bem" · 15:00/)).toBeOnTheScreen();
    expect(rede.recebidas).toBe(0);

    await act(async () => {
      await fireEvent(screen.getByLabelText('Simular sem internet'), 'valueChange', false);
      await container.sincronizacao.motor.drenar();
    });
    expect(await screen.findByText('Tudo enviado.')).toBeOnTheScreen();
    expect(rede.recebidas).toBe(1);
  });

  it('não aceito pelo Core: aparece com o motivo, e dá para reenviar ou descartar', async () => {
    const { container, core } = await containerDeTeste();
    const [alerta] = (await container.consultas.alertas()).dados;
    jest.spyOn(core, 'enviar').mockRejectedValueOnce(new ErroDaApi(422, 'Alerta já resolvido', 'abcdef12'));
    await container.escritas.reconhecerAlerta(alerta!.id, 'VOU_LIGAR');
    await container.escritas.registrarCheckIn(LURDES, 'PRECISO_DE_AJUDA');
    jest.spyOn(core, 'enviar').mockRejectedValueOnce(new ErroDaApi(404, 'Casa removida', '12345678'));
    await container.escritas.registrarCheckIn(LURDES, 'ESTOU_BEM');

    await renderComContainer(<Sincronizacao />, container);
    expect(await screen.findByText('0 esperando · 2 não aceitos')).toBeOnTheScreen();
    expect(screen.getByText('Não aceito: 422 Alerta já resolvido (abcdef12)')).toBeOnTheScreen();
    expect(screen.getByText(/"Vou ligar"/)).toBeOnTheScreen();

    const [reenviar] = screen.getAllByRole('button', { name: 'Reenviar' });
    await act(async () => {
      await fireEvent.press(reenviar!);
    });
    expect(await screen.findByText('0 esperando · 1 não aceitos')).toBeOnTheScreen();
    await act(async () => {
      await fireEvent.press(screen.getByRole('button', { name: 'Descartar' }));
    });
    expect(await screen.findByText('Tudo enviado.')).toBeOnTheScreen();
    expect(screen.getByText(/Não disponível neste app de teste/)).toBeOnTheScreen();
  });

  it('a pílula do Início avisa o que está esperando a internet', async () => {
    const { container, rede } = await containerDeTeste();
    await container.consultas.casas();
    rede.offline = true;
    await container.escritas.registrarCheckIn(LURDES, 'ESTOU_BEM');
    await renderComContainer(<Inicio />, container);
    expect(await screen.findByRole('button', { name: '1 aviso esperando a internet' })).toBeOnTheScreen();
  });
});

describe('CentralDeSincronizacao', () => {
  it('registra o worker ao iniciar, compacta o histórico e mostra o estado; falha do worker vira INDISPONIVEL', async () => {
    const { container } = await containerDeTeste();
    const outbox = container.sincronizacao.outbox;
    const compactar = jest.spyOn(outbox, 'compactarEnviadasAntesDe');
    const ativa = new CentralDeSincronizacao({
      outbox,
      motor: container.sincronizacao.motor,
      worker: { registrar: async () => 'ATIVO' },
      clock: container.clock,
    });
    expect((await ativa.resumo()).segundoPlano).toBeNull();
    await ativa.iniciar();
    expect((await ativa.resumo()).segundoPlano).toBe('ATIVO');
    expect(compactar).toHaveBeenCalledWith(new Date('2026-11-11T18:00:00.000Z'));

    const quebrada = new CentralDeSincronizacao({
      outbox,
      motor: container.sincronizacao.motor,
      worker: { registrar: async () => Promise.reject(new Error('sem TaskManager')) },
      clock: container.clock,
    });
    await quebrada.iniciar();
    expect((await quebrada.resumo()).segundoPlano).toBe('INDISPONIVEL');
  });
});
