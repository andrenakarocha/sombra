import { containerDeTeste } from '@test/container';
import { renderComContainer } from '@test/render';
import { act, fireEvent, screen } from '@testing-library/react-native';
import { AppState } from 'react-native';

import { bloqueioStore } from '@/application/estado/bloqueio';
import { preferenciasStore } from '@/application/estado/preferencias';
import { sessaoStore } from '@/application/estado/sessao';
import Ajustes from '@/app/(familia)/(abas)/ajustes';
import { CASAS } from '@/infrastructure/demo/fixtures';
import { TelaDeBloqueio } from '@/presentation/componentes/TelaDeBloqueio';

jest.mock('expo-router', () => ({ router: { push: jest.fn() } }));

const LURDES = CASAS[0]!.id;
const biometria = (disponivel: boolean, autentica: boolean) => ({
  disponivel: jest.fn(async () => disponivel),
  autenticar: jest.fn(async () => autentica),
});

beforeEach(() => bloqueioStore.setState({ ativo: false, bloqueado: false }));
afterEach(() => jest.restoreAllMocks());

describe('bloqueio por biometria', () => {
  it('sem digital cadastrada não liga, e explica', async () => {
    const { container } = await containerDeTeste({ biometria: biometria(false, true) });
    const r = await container.bloqueio.ativar();
    expect(r.ok ? null : r.erro.mensagem).toMatch(/não tem digital ou rosto/);
    expect(bloqueioStore.getState().ativo).toBe(false);
  });

  it('cancelar a confirmação não liga', async () => {
    const { container } = await containerDeTeste({ biometria: biometria(true, false) });
    expect((await container.bloqueio.ativar()).ok).toBe(false);
  });

  it('liga, fica gravado, e só tranca depois de 30 s fora do app (a janela de permissão não tranca)', async () => {
    const { container, clock } = await containerDeTeste({ biometria: biometria(true, true) });
    expect((await container.bloqueio.ativar()).ok).toBe(true);
    container.bloqueio.foiParaSegundoPlano();
    clock.avancarMinutos(0.4);
    container.bloqueio.voltou();
    expect(bloqueioStore.getState().bloqueado).toBe(false);

    container.bloqueio.foiParaSegundoPlano();
    clock.avancarMinutos(1);
    container.bloqueio.voltou();
    expect(bloqueioStore.getState().bloqueado).toBe(true);
    expect(await container.bloqueio.desbloquear()).toBe(true);
    expect(bloqueioStore.getState().bloqueado).toBe(false);

    // Ao abrir o app de novo, já começa trancado.
    bloqueioStore.setState({ ativo: false, bloqueado: false });
    await container.bloqueio.carregar();
    expect(bloqueioStore.getState()).toEqual({ ativo: true, bloqueado: true });
    await container.bloqueio.desativar();
    await container.bloqueio.carregar();
    expect(bloqueioStore.getState()).toEqual({ ativo: false, bloqueado: false });
  });

  it('a tela de bloqueio cobre a área da família e pede a digital; falhar mantém trancado', async () => {
    const bio = biometria(true, true);
    const { container } = await containerDeTeste({ biometria: bio });
    await container.bloqueio.ativar();
    // Ao abrir o app, a preferência gravada tranca; a digital falha na primeira vez.
    bio.autenticar.mockResolvedValueOnce(false);
    await renderComContainer(<TelaDeBloqueio />, container);
    expect(await screen.findByText(/estão protegidos/)).toBeOnTheScreen();
    expect(bio.autenticar).toHaveBeenCalled();
    bio.autenticar.mockResolvedValueOnce(true);
    await act(async () => {
      await fireEvent.press(screen.getByRole('button', { name: 'Desbloquear' }));
    });
    expect(screen.queryByText(/estão protegidos/)).toBeNull();
  });

  it('a tela de bloqueio ouve o app ir e voltar do segundo plano', async () => {
    const { container } = await containerDeTeste({ biometria: biometria(true, true) });
    const ouvir = jest.spyOn(AppState, 'addEventListener');
    const fora = jest.spyOn(container.bloqueio, 'foiParaSegundoPlano');
    const volta = jest.spyOn(container.bloqueio, 'voltou');
    await renderComContainer(<TelaDeBloqueio />, container);
    const ouvinte = ouvir.mock.calls.at(-1)![1] as (s: string) => void;
    await act(async () => ouvinte('background'));
    await act(async () => ouvinte('active'));
    expect([fora.mock.calls.length, volta.mock.calls.length]).toEqual([1, 1]);
  });

  it('em Ajustes, ligar sem biometria mostra o aviso', async () => {
    const { container } = await containerDeTeste({ biometria: biometria(false, false) });
    await renderComContainer(<Ajustes />, container);
    const interruptor = await screen.findByLabelText('Pedir digital ou rosto para abrir');
    await act(async () => {
      await fireEvent(interruptor, 'valueChange', true);
    });
    expect(await screen.findByText(/não tem digital ou rosto/)).toBeOnTheScreen();
  });
});

describe('Centro de Privacidade do aparelho (LGPD art. 18)', () => {
  it('🔎 o export traz o que o celular guarda, sem nenhuma PII inteira', async () => {
    const { container } = await containerDeTeste();
    await container.consultas.painelDaCasa(LURDES);
    await container.consultas.alertas();
    const json = await container.privacidadeLocal.exportar();
    const doc = JSON.parse(json);
    expect(doc.casas[0].moradores[0]).toMatchObject({ cpf: '***.982.247-**', cartaoSus: '*** **** **** 1234', telefone: '(11) *****-0001' });
    expect(doc.casas[0].redeDeCuidado.map((r: { telefone: string }) => r.telefone)).toEqual(['(11) *****-0003', '(11) *****-0004']);
    expect(doc.alertas.length).toBeGreaterThan(0);
    // Nenhuma sequência de 10 ou mais dígitos (CPF, CNS ou telefone inteiro), com ou sem pontuação.
    expect(json.replace(/\d{4}-\d{2}-\d{2}T[\d:.]+Z/g, '')).not.toMatch(/(\d[\s.()-]*){10,}/);
    expect(json).not.toMatch(/90000-000\d/);
  });

  it('apagar tudo: sai da conta e não sobra casa, fila nem preferência', async () => {
    const { container, rede, db } = await containerDeTeste();
    await container.consultas.casas();
    rede.offline = true;
    await container.escritas.registrarCheckIn(LURDES, 'ESTOU_BEM');
    await container.sessao.definirModoIdoso(LURDES);
    await container.privacidadeLocal.apagarTudo();
    expect(sessaoStore.getState().token).toBeNull();
    expect(preferenciasStore.getState().modoIdosoCasaId).toBeNull();
    for (const tabela of ['casas', 'alertas', 'outbox', 'preferencias', 'leituras', 'moradores', 'rede_de_cuidado']) {
      expect(await db.all(`SELECT * FROM ${tabela}`)).toEqual([]);
    }
  });
});
