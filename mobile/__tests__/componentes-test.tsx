import { fireEvent, render, screen } from '@testing-library/react-native';

import { BotaoGigante } from '@/presentation/componentes/BotaoGigante';
import { CartaoCasa } from '@/presentation/componentes/CartaoCasa';
import { Farol } from '@/presentation/componentes/Farol';
import { CASAS } from '@/infrastructure/demo/fixtures';
import { INFO_DO_NIVEL, NIVEIS } from '@/domain/clima/nivel-de-risco';

const casa = CASAS[0]!;

describe('Farol', () => {
  it.each(NIVEIS)('mostra %s com texto e rótulo acessível, não só cor', async (nivel) => {
    await render(<Farol nivel={nivel} />);
    expect(screen.getByText(INFO_DO_NIVEL[nivel].rotulo)).toBeOnTheScreen();
    expect(screen.getByLabelText(`Nível de calor: ${INFO_DO_NIVEL[nivel].rotulo}`)).toBeOnTheScreen();
  });
});

describe('CartaoCasa', () => {
  it('mostra o nível, o índice de calor e os horários em São Paulo', async () => {
    await render(<CartaoCasa casa={casa} aoAbrir={() => {}} />);
    expect(screen.getByText('Casa da Dona Lurdes')).toBeOnTheScreen();
    expect(screen.getByText('Cautela extrema')).toBeOnTheScreen();
    expect(screen.getByLabelText('Índice de calor 34,4 graus')).toBeOnTheScreen();
    expect(screen.getByLabelText('Último "estou bem": 09:02')).toBeOnTheScreen();
    expect(screen.getByLabelText('Responder até: 15:30')).toBeOnTheScreen();
  });

  it('só mostra o botão Ligar quando há a quem ligar', async () => {
    const aoLigar = jest.fn();
    await render(<CartaoCasa casa={casa} aoAbrir={() => {}} aoLigar={aoLigar} />);
    await fireEvent.press(screen.getByRole('button', { name: 'Ligar' }));
    expect(aoLigar).toHaveBeenCalledTimes(1);
  });
});

describe('BotaoGigante', () => {
  it('aciona com um toque', async () => {
    const aoAcionar = jest.fn();
    await render(<BotaoGigante rotulo="ESTOU BEM" instrucao="aperte uma vez" variante="bem" aoAcionar={aoAcionar} />);
    await fireEvent.press(screen.getByRole('button', { name: 'ESTOU BEM' }));
    expect(aoAcionar).toHaveBeenCalledTimes(1);
  });

  it('com segurarPorMs, um toque rápido não aciona', async () => {
    const aoAcionar = jest.fn();
    await render(
      <BotaoGigante
        rotulo="PRECISO DE AJUDA"
        instrucao="segure por 2 segundos"
        variante="ajuda"
        segurarPorMs={2000}
        aoAcionar={aoAcionar}
      />,
    );
    const botao = screen.getByRole('button', { name: 'PRECISO DE AJUDA' });
    await fireEvent.press(botao);
    expect(aoAcionar).not.toHaveBeenCalled();
    await fireEvent(botao, 'longPress');
    expect(aoAcionar).toHaveBeenCalledTimes(1);
  });
});
