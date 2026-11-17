import { useState } from 'react';
import { View } from 'react-native';
import Svg, { Circle, Path } from 'react-native-svg';

import type { Leitura } from '@/api/tipos';
import { INFO_DO_NIVEL, type NivelDeRisco } from '@/dominio/nivel';
import { useTema } from '@/tema';
import { formatarGraus } from '@/util/formato';

import { Texto } from './Texto';

const ALTURA = 120;
const MARGEM = 8;

// Descrição em texto para o leitor de tela: o gráfico sozinho não diz nada a quem não enxerga.
export function descreverLeituras(leituras: Leitura[]) {
  const valores = leituras.map((l) => l.indiceDeCalorC);
  const agora = valores.at(-1)!;
  return (
    `Índice de calor nas últimas 24 horas: mínimo ${formatarGraus(Math.min(...valores))}, ` +
    `máximo ${formatarGraus(Math.max(...valores))}, agora ${formatarGraus(agora)} graus.`
  );
}

export function GraficoDeCalor({ leituras, nivel }: { leituras: Leitura[]; nivel: NivelDeRisco }) {
  const tema = useTema();
  const [largura, setLargura] = useState(0);

  if (leituras.length < 2) {
    return <Texto suave>Ainda não há leituras suficientes nas últimas 24 horas.</Texto>;
  }

  const cor = tema.risco[INFO_DO_NIVEL[nivel].token].borda;
  const valores = leituras.map((l) => l.indiceDeCalorC);
  const tempos = leituras.map((l) => new Date(l.momento).getTime());
  const [min, max] = [Math.min(...valores) - 1, Math.max(...valores) + 1];
  const [inicio, fim] = [tempos[0]!, tempos.at(-1)!];
  const x = (t: number) => MARGEM + ((t - inicio) / (fim - inicio || 1)) * (largura - 2 * MARGEM);
  const y = (v: number) => MARGEM + (1 - (v - min) / (max - min)) * (ALTURA - 2 * MARGEM);
  const pontos = leituras.map((_, i) => `${x(tempos[i]!).toFixed(1)} ${y(valores[i]!).toFixed(1)}`);
  const linha = `M${pontos.join(' L')}`;
  const area = `${linha} L${x(fim).toFixed(1)} ${ALTURA} L${x(inicio).toFixed(1)} ${ALTURA} Z`;

  return (
    <View
      accessible
      accessibilityRole="image"
      accessibilityLabel={descreverLeituras(leituras)}
      onLayout={(e) => setLargura(e.nativeEvent.layout.width)}
      style={{ height: ALTURA }}
    >
      {largura > 0 && (
        <Svg width={largura} height={ALTURA}>
          <Path d={area} fill={cor} opacity={0.18} />
          <Path d={linha} fill="none" stroke={cor} strokeWidth={4} strokeLinecap="round" strokeLinejoin="round" />
          <Circle cx={x(fim)} cy={y(valores.at(-1)!)} r={5} fill={cor} />
        </Svg>
      )}
    </View>
  );
}
