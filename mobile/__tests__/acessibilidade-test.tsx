// Acessibilidade (F6.7): o tamanho do texto escolhido no app e a dica do leitor de tela em todo elemento tocável.

import { containerDeTeste } from '@test/container';
import { renderComContainer } from '@test/render';
import { act, fireEvent, render, screen } from '@testing-library/react-native';
import { StyleSheet } from 'react-native';

import { preferenciasStore } from '@/application/estado/preferencias';
import { sessaoStore } from '@/application/estado/sessao';
import Ajustes from '@/app/(familia)/(abas)/ajustes';
import Morador from '@/app/(familia)/casa/[id]/morador';
import Rotina from '@/app/(familia)/casa/[id]/rotina';
import Sincronizacao from '@/app/(familia)/sincronizacao';
import { Texto } from '@/presentation/componentes/Texto';
import { tamanho } from '@/presentation/tema';

jest.mock('expo-router', () => ({
  router: { push: jest.fn(), back: jest.fn() },
  useLocalSearchParams: () => ({ id: '0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10' }),
  Stack: { Screen: () => null },
}));

const fonteDe = (texto: string) => StyleSheet.flatten(screen.getByText(texto).props.style).fontSize as number;

describe('tamanho do texto no app', () => {
  it('multiplica o texto por 1,2 e 1,4; o número gigante (display) fica como está', async () => {
    preferenciasStore.setState({ escalaDoTexto: 'NORMAL' });
    await render(
      <>
        <Texto>corpo</Texto>
        <Texto variante="display">40</Texto>
      </>,
    );
    expect(fonteDe('corpo')).toBe(tamanho.texto);

    await act(async () => preferenciasStore.setState({ escalaDoTexto: 'GRANDE' }));
    expect(fonteDe('corpo')).toBe(Math.round(tamanho.texto * 1.2));

    await act(async () => preferenciasStore.setState({ escalaDoTexto: 'MAIOR' }));
    expect(fonteDe('corpo')).toBe(Math.round(tamanho.texto * 1.4));
    expect(fonteDe('40')).toBe(tamanho.display);
  });

  it('escolhido em Ajustes, fica gravado no aparelho e sobrevive a sair da conta', async () => {
    const { container } = await containerDeTeste();
    sessaoStore.setState({ token: 'modo-demonstracao' });
    await renderComContainer(<Ajustes />, container);

    await fireEvent.press(screen.getByRole('radio', { name: 'Texto maior' }));
    expect(preferenciasStore.getState().escalaDoTexto).toBe('MAIOR');
    expect(screen.getByRole('radio', { name: 'Texto maior' }).props.accessibilityState).toMatchObject({ checked: true });

    await act(async () => container.sessao.sair());
    preferenciasStore.setState({ escalaDoTexto: 'NORMAL' });
    await container.sessao.carregar();
    expect(preferenciasStore.getState().escalaDoTexto).toBe('MAIOR');
  });

  it('um valor estranho no banco vira o normal; apagar tudo volta ao normal', async () => {
    const { container, db } = await containerDeTeste();
    await db.run('INSERT OR REPLACE INTO preferencias (chave, valor) VALUES (?, ?)', ['escalaDoTexto', 'ENORME']);
    await container.sessao.carregar();
    expect(preferenciasStore.getState().escalaDoTexto).toBe('NORMAL');

    await container.sessao.definirEscalaDoTexto('GRANDE');
    await container.privacidadeLocal.apagarTudo();
    expect(preferenciasStore.getState().escalaDoTexto).toBe('NORMAL');
    await container.sessao.carregar();
    expect(preferenciasStore.getState().escalaDoTexto).toBe('NORMAL');
  });
});

describe('leitor de tela: rótulo, papel e dica em todo elemento tocável', () => {
  const PAPEIS = ['button', 'radio', 'checkbox', 'switch'] as const;

  const semDica = () =>
    PAPEIS.flatMap((papel) => screen.queryAllByRole(papel))
      .filter((e) => !e.props.accessibilityHint)
      .map((e) => e.props.accessibilityLabel ?? '(sem rótulo)');

  it.each([
    ['Ajustes', () => <Ajustes />, 'Tamanho do texto'],
    ['Rotina', () => <Rotina />, 'Salvar a rotina'],
    ['Morador', () => <Morador />, 'Doença do coração'],
    ['Sincronização', () => <Sincronizacao />, 'Simular sem internet'],
  ])('%s', async (_nome, tela, marco) => {
    const { container } = await containerDeTeste();
    sessaoStore.setState({ token: 'modo-demonstracao' });
    await renderComContainer(tela(), container);
    expect((await screen.findAllByText(marco)).length).toBeGreaterThan(0);
    expect(semDica()).toEqual([]);
  });
});
