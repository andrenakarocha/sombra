import { View } from 'react-native';
import Svg, { Path } from 'react-native-svg';

import type { FarolDePrivacidade as Farol } from '@/api/tipos';
import { raio, useTema } from '@/tema';

import { Texto } from './Texto';

// Farol de Risco Cidadão (spec 07): mede a privacidade, não o calor. Também nunca só pela cor.
const INFO: Record<Farol, { rotulo: string; token: 'normal' | 'atencao' | 'perigo'; marca: string }> = {
  VERDE: { rotulo: 'Dados protegidos', token: 'normal', marca: 'm8.5 12.2 2.5 2.5 4.5-5' },
  AMARELO: { rotulo: 'Atenção', token: 'atencao', marca: 'M12 7.5v5.5M12 16.6v.1' },
  VERMELHO: { rotulo: 'Risco alto', token: 'perigo', marca: 'm9 9 6 6M15 9l-6 6' },
};

export function FarolDePrivacidade({ farol }: { farol: Farol }) {
  const info = INFO[farol];
  const cores = useTema().risco[info.token];
  return (
    <View
      accessible
      accessibilityRole="text"
      accessibilityLabel={`Privacidade: ${info.rotulo}`}
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        alignSelf: 'flex-start',
        gap: 8,
        minHeight: 40,
        paddingLeft: 10,
        paddingRight: 14,
        borderRadius: raio.pilula,
        borderWidth: 2,
        borderColor: cores.borda,
        backgroundColor: cores.fundo,
      }}
    >
      <Svg width={22} height={22} viewBox="0 0 24 24">
        <Path
          d="M12 2.5 20 5.6v6.1c0 4.8-3.3 8.4-8 9.8-4.7-1.4-8-5-8-9.8V5.6z"
          fill="none"
          stroke={cores.texto}
          strokeWidth={2.2}
          strokeLinejoin="round"
        />
        <Path d={info.marca} fill="none" stroke={cores.texto} strokeWidth={2.4} strokeLinecap="round" strokeLinejoin="round" />
      </Svg>
      <Texto forte style={{ color: cores.texto, fontSize: 16 }}>
        {info.rotulo}
      </Texto>
    </View>
  );
}
