import { containerDeTeste } from '@test/container';
import { renderComContainer } from '@test/render';
import { act, screen } from '@testing-library/react-native';

import { pontosAPartirDe } from '@/application/casos-de-uso/perto';
import Perto from '@/app/(familia)/(abas)/perto';
import { valorOuErro } from '@/domain/compartilhado/result';
import { anguloDaSeta, direcaoRelativa, PontoGeografico } from '@/domain/geo/ponto-geografico';
import { PONTOS } from '@/infrastructure/demo/fixtures';

jest.mock('expo-router', () => ({ router: { push: jest.fn() } }));

const ponto = (lat: number, lon: number) => valorOuErro(PontoGeografico.criar(lat, lon));
// A estação A701 (Mirante de Santana), referência do Sombra.
const SANTANA = ponto(-23.4964, -46.62);

describe('PontoGeografico', () => {
  it('haversine: São Paulo (Sé) ao Rio de Janeiro dá uns 360 km; um ponto até ele mesmo, zero', () => {
    expect(ponto(-23.5505, -46.6333).distanciaKm(ponto(-22.9068, -43.1729))).toBeCloseTo(360.7, 0);
    expect(SANTANA.distanciaKm(SANTANA)).toBe(0);
  });

  it('rumo: norte 0°, leste 90°, sul 180°, oeste 270°', () => {
    expect(SANTANA.rumoAte(ponto(-23.4, -46.62))).toBeCloseTo(0, 0);
    expect(SANTANA.rumoAte(ponto(-23.4964, -46.5))).toBeCloseTo(90, 0);
    expect(SANTANA.rumoAte(ponto(-23.6, -46.62))).toBeCloseTo(180, 0);
    expect(SANTANA.rumoAte(ponto(-23.4964, -46.7))).toBeCloseTo(270, 0);
  });

  it.each([
    [91, 0, 'LATITUDE'],
    [0, -181, 'LONGITUDE'],
    [Number.NaN, 0, 'LATITUDE'],
  ])('recusa (%p, %p)', (lat, lon, codigo) => {
    const r = PontoGeografico.criar(lat, lon);
    expect(r.ok ? null : r.erro.codigo).toBe(codigo);
  });
});

describe('a seta acompanha a bússola', () => {
  it('ângulo = rumo do destino − direção do aparelho, sempre de 0 a 360', () => {
    expect(anguloDaSeta(90, 0)).toBe(90);
    expect(anguloDaSeta(10, 350)).toBe(20);
    expect(anguloDaSeta(350, 10)).toBe(340);
  });

  it.each([
    [0, 'FRENTE'],
    [22, 'FRENTE'],
    [23, 'FRENTE_DIREITA'],
    [90, 'DIREITA'],
    [180, 'ATRAS'],
    [270, 'ESQUERDA'],
    [338, 'FRENTE'],
    [-90, 'ESQUERDA'],
    [315, 'FRENTE_ESQUERDA'],
  ] as const)('%p° → %s', (angulo, direcao) => {
    expect(direcaoRelativa(angulo)).toBe(direcao);
  });
});

describe('pontos a partir de onde a pessoa está', () => {
  it('com GPS: distância e rumo daqui, do mais perto ao mais longe', () => {
    const lista = pontosAPartirDe(PONTOS, { lat: -23.507, lon: -46.6214 });
    expect(lista.map((p) => p.id)).toEqual(['p2', 'p1']);
    expect(lista[0]!.distanciaKm).toBeLessThan(0.05);
    expect(lista.every((p) => p.distanciaDoAparelho && p.rumoGraus !== null)).toBe(true);
  });

  it('sem GPS (ou ponto sem coordenadas): a distância do servidor, a partir da casa', () => {
    const semCoordenada = { ...PONTOS[0]!, lat: undefined, lon: undefined };
    expect(pontosAPartirDe([semCoordenada], { lat: -23.5, lon: -46.6 })[0]).toMatchObject({ distanciaKm: 0.8, rumoGraus: null, distanciaDoAparelho: false });
    expect(pontosAPartirDe(PONTOS, null).map((p) => p.distanciaKm)).toEqual([0.8, 1.6]);
  });
});

describe('Perto de mim', () => {
  it('🔎 com GPS e bússola, a seta grande diz em palavras para onde ir', async () => {
    let girar: (g: number) => void = () => {};
    const { container } = await containerDeTeste({
      localizacao: {
        atual: async () => ({ lat: -23.4964, lon: -46.62 }),
        observarDirecao: async (o) => {
          girar = o;
          return () => {};
        },
      },
    });
    await renderComContainer(<Perto />, container);
    expect(await screen.findByText('Biblioteca do bairro (exemplo)')).toBeOnTheScreen();
    await act(async () => girar(0));
    // A biblioteca de exemplo fica a noroeste de Santana: olhando para o norte, é em frente e um pouco à esquerda.
    expect(await screen.findByLabelText(/^Em frente, um pouco à esquerda · \d+ m até Biblioteca do bairro \(exemplo\)$/)).toBeOnTheScreen();
    await act(async () => girar(180));
    expect(await screen.findByLabelText(/^Para trás, à direita · /)).toBeOnTheScreen();
    expect(screen.getAllByText(/daqui ·/).length).toBe(2);
  });

  it('sem localização, explica e usa a distância da casa', async () => {
    const { container } = await containerDeTeste();
    await renderComContainer(<Perto />, container);
    expect(await screen.findByText(/Com a localização ligada/)).toBeOnTheScreen();
    expect(screen.getByText(/^800 m da casa ·/)).toBeOnTheScreen();
  });
});
