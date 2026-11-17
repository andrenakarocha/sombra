// R3: limites térmicos personalizados pelos fatores de risco do morador, com piso de 26,67 °C (início de ATENCAO).
// Cada fator baixa os limiares de CAUTELA_EXTREMA, PERIGO e PERIGO_EXTREMO; a tela mostra quanto cada um pesou.

import { classificar, LIMIARES_C, type NivelDeRisco } from '../clima/nivel-de-risco';
import { type FatorDeRisco, fatoresEfetivos } from '../morador/fator-de-risco';

export const REDUCAO_C: Record<FatorDeRisco, number> = {
  IDADE_AVANCADA: 2,
  CARDIOPATIA: 2.5,
  USO_DE_DIURETICO: 1.5,
};

const PORQUE: Record<FatorDeRisco, string> = {
  IDADE_AVANCADA: 'Com 80 anos ou mais, o corpo sente menos sede e demora mais para se refrescar.',
  CARDIOPATIA: 'O coração trabalha mais no calor para esfriar o corpo.',
  USO_DE_DIURETICO: 'O diurético faz o corpo perder água mais rápido.',
};

export type Contribuicao = { fator: FatorDeRisco; reducaoC: number; porque: string };

export type Limiares = Record<keyof typeof LIMIARES_C, number>;

export class RiscoPersonalizado {
  private constructor(
    readonly limiares: Limiares,
    readonly contribuicoes: Contribuicao[],
  ) {}

  static para(fatores: readonly FatorDeRisco[], idade: number): RiscoPersonalizado {
    const contribuicoes = fatoresEfetivos(fatores, idade).map((fator) => ({
      fator,
      reducaoC: REDUCAO_C[fator],
      porque: PORQUE[fator],
    }));
    const reducao = contribuicoes.reduce((total, c) => total + c.reducaoC, 0);
    const piso = LIMIARES_C.ATENCAO;
    const baixar = (limiar: number) => Math.round(Math.max(piso, limiar - reducao) * 100) / 100;
    return new RiscoPersonalizado(
      {
        ATENCAO: piso,
        CAUTELA_EXTREMA: baixar(LIMIARES_C.CAUTELA_EXTREMA),
        PERIGO: baixar(LIMIARES_C.PERIGO),
        PERIGO_EXTREMO: baixar(LIMIARES_C.PERIGO_EXTREMO),
      },
      contribuicoes,
    );
  }

  // Quanto o perigo começa antes do padrão da NWS (pode ser menor que a soma, por causa do piso).
  get antecipacaoDoPerigoC() {
    return Math.round((LIMIARES_C.PERIGO - this.limiares.PERIGO) * 100) / 100;
  }

  avaliar(indiceC: number): { padrao: NivelDeRisco; personalizado: NivelDeRisco; folgaAtePerigoC: number } {
    return {
      padrao: classificar(indiceC),
      personalizado: classificar(indiceC, this.limiares),
      folgaAtePerigoC: Math.round((this.limiares.PERIGO - indiceC) * 10) / 10,
    };
  }
}
