import { falha, ok, type Result } from '../compartilhado/result';

const RAIO_DA_TERRA_KM = 6371.0088;
const rad = (graus: number) => (graus * Math.PI) / 180;
const graus = (r: number) => (r * 180) / Math.PI;

export class PontoGeografico {
  private constructor(
    readonly lat: number,
    readonly lon: number,
  ) {}

  static criar(lat: number, lon: number): Result<PontoGeografico> {
    if (!Number.isFinite(lat) || lat < -90 || lat > 90) return falha('LATITUDE', 'Latitude fora de −90 a 90.');
    if (!Number.isFinite(lon) || lon < -180 || lon > 180) return falha('LONGITUDE', 'Longitude fora de −180 a 180.');
    return ok(new PontoGeografico(lat, lon));
  }

  // Haversine: distância em linha reta sobre a esfera (boa o bastante para "a 800 m daqui").
  distanciaKm(ate: PontoGeografico): number {
    const dLat = rad(ate.lat - this.lat);
    const dLon = rad(ate.lon - this.lon);
    const a = Math.sin(dLat / 2) ** 2 + Math.cos(rad(this.lat)) * Math.cos(rad(ate.lat)) * Math.sin(dLon / 2) ** 2;
    return 2 * RAIO_DA_TERRA_KM * Math.asin(Math.min(1, Math.sqrt(a)));
  }

  // Rumo inicial em graus a partir do norte, no sentido horário (0 = norte, 90 = leste).
  rumoAte(ate: PontoGeografico): number {
    const y = Math.sin(rad(ate.lon - this.lon)) * Math.cos(rad(ate.lat));
    const x = Math.cos(rad(this.lat)) * Math.sin(rad(ate.lat)) - Math.sin(rad(this.lat)) * Math.cos(rad(ate.lat)) * Math.cos(rad(ate.lon - this.lon));
    return (graus(Math.atan2(y, x)) + 360) % 360;
  }
}

export type DirecaoRelativa = 'FRENTE' | 'FRENTE_DIREITA' | 'DIREITA' | 'ATRAS_DIREITA' | 'ATRAS' | 'ATRAS_ESQUERDA' | 'ESQUERDA' | 'FRENTE_ESQUERDA';

const SETORES: DirecaoRelativa[] = ['FRENTE', 'FRENTE_DIREITA', 'DIREITA', 'ATRAS_DIREITA', 'ATRAS', 'ATRAS_ESQUERDA', 'ESQUERDA', 'FRENTE_ESQUERDA'];

// Para onde a seta aponta em relação a quem segura o celular: rumo do destino menos a direção do aparelho.
export function anguloDaSeta(rumoGraus: number, direcaoDoAparelhoGraus: number): number {
  return (((rumoGraus - direcaoDoAparelhoGraus) % 360) + 360) % 360;
}

// A seta também em palavras (oito setores de 45°), para o leitor de tela e para quem não lê setas.
export function direcaoRelativa(anguloGraus: number): DirecaoRelativa {
  return SETORES[Math.round((((anguloGraus % 360) + 360) % 360) / 45) % 8]!;
}
