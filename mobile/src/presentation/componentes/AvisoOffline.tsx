import { View } from 'react-native';

import { espaco, raio, useTema } from '@/presentation/tema';
import { formatarHora } from '@/presentation/util/formato';

import { Texto } from './Texto';

export function AvisoOffline({ atualizadoEm }: { atualizadoEm: string | null }) {
  const tema = useTema();
  return (
    <View
      accessibilityRole="alert"
      style={{ backgroundColor: tema.superficieAlternativa, borderRadius: raio.medio, padding: espaco[4] }}
    >
      <Texto forte>Sem conexão.</Texto>
      <Texto>
        {atualizadoEm ? `Mostrando os dados salvos às ${formatarHora(atualizadoEm)}.` : 'Mostrando os dados salvos no aparelho.'}
      </Texto>
    </View>
  );
}
