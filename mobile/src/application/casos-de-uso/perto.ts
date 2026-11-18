// Pontos de resfriamento a partir de onde a pessoa está: distância e rumo calculados no aparelho (GPS), ou a
// distância que o servidor mandou (a partir da casa) quando não há localização.

import { PontoGeografico } from '@/domain/geo/ponto-geografico';

import type { PontoDeResfriamento } from '../modelos';
import type { Coordenadas } from '../ports/servicos';

export type PontoPerto = PontoDeResfriamento & { rumoGraus: number | null; distanciaDoAparelho: boolean };

export function pontosAPartirDe(pontos: PontoDeResfriamento[], origem: Coordenadas | null): PontoPerto[] {
  const daqui = origem ? PontoGeografico.criar(origem.lat, origem.lon) : null;
  const lista = pontos.map((p): PontoPerto => {
    const destino = p.lat !== undefined && p.lon !== undefined ? PontoGeografico.criar(p.lat, p.lon) : null;
    if (!daqui?.ok || !destino?.ok) return { ...p, rumoGraus: null, distanciaDoAparelho: false };
    return { ...p, distanciaKm: daqui.valor.distanciaKm(destino.valor), rumoGraus: daqui.valor.rumoAte(destino.valor), distanciaDoAparelho: true };
  });
  return lista.sort((a, b) => a.distanciaKm - b.distanciaKm);
}
