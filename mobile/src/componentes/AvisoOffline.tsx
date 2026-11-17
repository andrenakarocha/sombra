import { View } from 'react-native';

import { espaco, raio, useTema } from '@/tema';
import { formatarHora } from '@/util/formato';

import { Texto } from './Texto';

export function AvisoOffline({ atualizadoEm }: { atualizadoEm: string }) {
  const tema = useTema();
  return (
    <View
      accessibilityRole="alert"
      style={{ backgroundColor: tema.superficieAlternativa, borderRadius: raio.medio, padding: espaco[4] }}
    >
      <Texto forte>Sem conexão.</Texto>
      <Texto>Mostrando os dados salvos às {formatarHora(atualizadoEm)}.</Texto>
    </View>
  );
}
