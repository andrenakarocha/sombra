import { containerDeTeste } from '@test/container';
import { renderComContainer } from '@test/render';
import { render, screen } from '@testing-library/react-native';

import Casa from '@/app/(familia)/casa/[id]';
import { LIMIARES_C } from '@/domain/clima/nivel-de-risco';
import { FATORES_DE_RISCO, type FatorDeRisco } from '@/domain/morador/fator-de-risco';
import { RiscoPersonalizado } from '@/domain/risco/risco-personalizado';
import { CASAS } from '@/infrastructure/demo/fixtures';
import { ExplicacaoDoRisco } from '@/presentation/componentes/ExplicacaoDoRisco';
import { formatarGraus } from '@/presentation/util/formato';

jest.mock('expo-router', () => ({
  router: { push: jest.fn() },
  useLocalSearchParams: () => ({ id: '0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10' }),
  Stack: { Screen: () => null },
}));

// As 8 combinações de fatores, em duas idades (a de 85 anos já conta "80 anos ou mais" sozinha).
const combinacoes: [FatorDeRisco[], number][] = [70, 85].flatMap((idade) =>
  Array.from({ length: 8 }, (_, m) => [FATORES_DE_RISCO.filter((_, i) => m & (1 << i)), idade] as [FatorDeRisco[], number]),
);

describe('🔎 o limiar exibido bate com o domínio em toda combinação de fatores', () => {
  it.each(combinacoes)('fatores %j, %i anos', async (fatores, idade) => {
    const risco = RiscoPersonalizado.para(fatores, idade);
    await render(<ExplicacaoDoRisco nome="Lurdes" risco={risco} indiceC={34.4} />);
    expect(
      screen.getByLabelText(
        `Para Lurdes, o perigo começa em ${formatarGraus(risco.limiares.PERIGO)} graus. O padrão é ${formatarGraus(LIMIARES_C.PERIGO)}.`,
      ),
    ).toBeOnTheScreen();
    for (const c of risco.contribuicoes) {
      expect(screen.getByText(`−${formatarGraus(c.reducaoC)} °C`, { exact: false })).toBeOnTheScreen();
    }
    if (risco.contribuicoes.length === 0) expect(screen.getByText(/vale a régua padrão/)).toBeOnTheScreen();
    const { personalizado, folgaAtePerigoC } = risco.avaliar(34.4);
    expect(screen.getByText(new RegExp(`Agora: 34,4 °C · .* para Lurdes`))).toBeOnTheScreen();
    expect(screen.getByText(folgaAtePerigoC > 0 ? /Faltam .* para o perigo/ : /Já está no perigo/)).toBeOnTheScreen();
    expect(personalizado).toBeDefined();
  });

  it('com os três fatores, explica o piso de 26,67 °C e mostra a diferença para a régua padrão', async () => {
    await render(<ExplicacaoDoRisco nome="Lurdes" risco={RiscoPersonalizado.para([...FATORES_DE_RISCO], 90)} indiceC={34.4} />);
    expect(screen.getByText(/nunca fica abaixo de 26,7 °C/)).toBeOnTheScreen();
    expect(screen.getByText(/Já está no perigo para Lurdes\. Pela régua padrão seria "Cautela extrema"\./)).toBeOnTheScreen();
  });

  it('sem leitura, não inventa o "agora"', async () => {
    await render(<ExplicacaoDoRisco nome="Lurdes" risco={RiscoPersonalizado.para([], 70)} indiceC={null} />);
    expect(screen.queryByText(/Agora:/)).toBeNull();
  });
});

it('a tela da casa explica o risco da Dona Lurdes com os dados do domínio', async () => {
  const { container } = await containerDeTeste();
  await renderComContainer(<Casa />, container);
  expect(await screen.findByText('Por que Lurdes está em risco')).toBeOnTheScreen();
  expect(screen.getByText('Toma diurético')).toBeOnTheScreen();
  const perigo = RiscoPersonalizado.para(['USO_DE_DIURETICO'], 78).limiares.PERIGO;
  expect(screen.getByLabelText(`Para Lurdes, o perigo começa em ${formatarGraus(perigo)} graus. O padrão é 39,4.`)).toBeOnTheScreen();
  expect(CASAS[0]!.id).toBe('0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10');
});
