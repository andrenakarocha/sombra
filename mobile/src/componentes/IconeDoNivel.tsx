import Svg, { Circle, Path } from 'react-native-svg';

import type { NivelDeRisco } from '@/dominio/nivel';

// Mesmos desenhos da design/amostra.html: a forma muda com o nível (círculo, triângulo, octógono).
export function IconeDoNivel({ nivel, cor, tamanho = 22 }: { nivel: NivelDeRisco; cor: string; tamanho?: number }) {
  const traco = { fill: 'none', stroke: cor, strokeWidth: 2.2, strokeLinejoin: 'round' as const };
  const risco = { stroke: cor, strokeWidth: 2.6, strokeLinecap: 'round' as const };
  return (
    <Svg width={tamanho} height={tamanho} viewBox="0 0 24 24" accessible={false}>
      {nivel === 'NORMAL' && (
        <>
          <Circle cx={12} cy={12} r={10} {...traco} />
          <Path d="m7.5 12.4 3 3 6-6.4" fill="none" {...risco} strokeWidth={2.4} strokeLinejoin="round" />
        </>
      )}
      {nivel === 'ATENCAO' && (
        <>
          <Circle cx={12} cy={12} r={10} {...traco} />
          <Path d="M12 6.5v7" {...risco} />
          <Circle cx={12} cy={17.2} r={1.5} fill={cor} />
        </>
      )}
      {nivel === 'CAUTELA_EXTREMA' && (
        <>
          <Circle cx={12} cy={12} r={10} {...traco} />
          <Path d="M9 6.5v7M15 6.5v7" {...risco} />
          <Circle cx={9} cy={17.2} r={1.5} fill={cor} />
          <Circle cx={15} cy={17.2} r={1.5} fill={cor} />
        </>
      )}
      {nivel === 'PERIGO' && (
        <>
          <Path d="M12 2.8 22 20.5H2z" {...traco} />
          <Path d="M12 9v5.5" {...risco} />
          <Circle cx={12} cy={17.5} r={1.4} fill={cor} />
        </>
      )}
      {nivel === 'PERIGO_EXTREMO' && (
        <>
          <Path d="M8.1 2.5h7.8l5.6 5.6v7.8l-5.6 5.6H8.1l-5.6-5.6V8.1z" {...traco} />
          <Path d="M12 7v6.5" {...risco} />
          <Circle cx={12} cy={16.8} r={1.5} fill={cor} />
        </>
      )}
    </Svg>
  );
}
