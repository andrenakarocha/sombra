// Os vetores compartilhados da R3 (docs/contratos/risco-personalizado.vetores.json): o Core confere os mesmos (D17).
import vetores from '../../../docs/contratos/risco-personalizado.vetores.json';

import { classificar } from '@/domain/clima/nivel-de-risco';
import { type FatorDeRisco, fatoresEfetivos } from '@/domain/morador/fator-de-risco';
import { REDUCAO_C, RiscoPersonalizado } from '@/domain/risco/risco-personalizado';

describe('R3 nos vetores compartilhados com o Core', () => {
  it('usa as mesmas reduções por fator', () => {
    expect(vetores.reducaoPorFatorC).toEqual(REDUCAO_C);
  });

  it.each(vetores.vetores.map((v) => [v.fatoresDeclarados.join('+') || 'sem fatores', v.idade, v] as const))(
    '%s aos %i anos',
    (_nome, idade, v) => {
      const declarados = v.fatoresDeclarados as FatorDeRisco[];
      const risco = RiscoPersonalizado.para(declarados, idade);

      expect(fatoresEfetivos(declarados, idade)).toEqual(v.fatoresQueValem);
      expect(risco.limiares).toEqual(v.limiares);
      for (const amostra of v.classificacoes) {
        expect(classificar(amostra.indiceC, risco.limiares)).toBe(amostra.nivel);
        expect(risco.avaliar(amostra.indiceC).personalizado).toBe(amostra.nivel);
      }
    },
  );
});
