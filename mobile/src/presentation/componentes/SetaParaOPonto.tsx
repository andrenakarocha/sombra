import { View } from 'react-native';
import Svg, { Path } from 'react-native-svg';

import { anguloDaSeta, type DirecaoRelativa, direcaoRelativa } from '@/domain/geo/ponto-geografico';

import { espaco, raio, useTema } from '../tema';
import { formatarKm } from '../util/formato';

import { Texto } from './Texto';

export const DIRECAO_EM_PALAVRAS: Record<DirecaoRelativa, string> = {
  FRENTE: 'Siga em frente',
  FRENTE_DIREITA: 'Em frente, um pouco à direita',
  DIREITA: 'Vire à direita',
  ATRAS_DIREITA: 'Para trás, à direita',
  ATRAS: 'Vire para trás',
  ATRAS_ESQUERDA: 'Para trás, à esquerda',
  ESQUERDA: 'Vire à esquerda',
  FRENTE_ESQUERDA: 'Em frente, um pouco à esquerda',
};

// Uma seta grande que acompanha a bússola, e a mesma informação em palavras (a seta sozinha não basta).
export function SetaParaOPonto({ nome, rumoGraus, direcaoGraus, distanciaKm }: { nome: string; rumoGraus: number; direcaoGraus: number; distanciaKm: number }) {
  const tema = useTema();
  const angulo = anguloDaSeta(rumoGraus, direcaoGraus);
  const frase = `${DIRECAO_EM_PALAVRAS[direcaoRelativa(angulo)]} · ${formatarKm(distanciaKm)} até ${nome}`;
  return (
    <View
      accessible
      accessibilityRole="text"
      accessibilityLabel={frase}
      accessibilityLiveRegion="polite"
      style={{ alignItems: 'center', gap: espaco[3], backgroundColor: tema.marcaSuave, borderRadius: raio.grande, padding: espaco[5] }}
    >
      <View style={{ transform: [{ rotate: `${Math.round(angulo)}deg` }] }}>
        <Svg width={140} height={140} viewBox="0 0 24 24">
          <Path d="M12 2 19 20 12 16 5 20Z" fill={tema.marca} stroke={tema.textoSobreMarca} strokeWidth={0.8} strokeLinejoin="round" />
        </Svg>
      </View>
      <Texto variante="subtitulo" forte style={{ textAlign: 'center' }}>
        {frase}
      </Texto>
    </View>
  );
}
