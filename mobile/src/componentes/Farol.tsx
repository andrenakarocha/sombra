import { View } from 'react-native';

import { INFO_DO_NIVEL, type NivelDeRisco } from '@/dominio/nivel';
import { raio, useTema } from '@/tema';

import { IconeDoNivel } from './IconeDoNivel';
import { Texto } from './Texto';

// O nível nunca depende só da cor: cor + ícone de forma própria + texto (D12).
export function Farol({ nivel }: { nivel: NivelDeRisco }) {
  const info = INFO_DO_NIVEL[nivel];
  const cores = useTema().risco[info.token];
  return (
    <View
      accessible
      accessibilityRole="text"
      accessibilityLabel={`Nível de calor: ${info.rotulo}`}
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
      <IconeDoNivel nivel={nivel} cor={cores.texto} />
      <Texto forte style={{ color: cores.texto, fontSize: 16 }}>
        {info.rotulo}
      </Texto>
    </View>
  );
}
