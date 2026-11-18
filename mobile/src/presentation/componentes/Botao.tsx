import { Pressable } from 'react-native';

import { espaco, raio, toque, useTema } from '@/presentation/tema';

import { Texto } from './Texto';

type Props = {
  rotulo: string;
  dica?: string;
  variante?: 'primario' | 'secundario';
  aoTocar: () => void;
};

export function Botao({ rotulo, dica, variante = 'primario', aoTocar }: Props) {
  const tema = useTema();
  const primario = variante === 'primario';
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={rotulo}
      accessibilityHint={dica}
      onPress={aoTocar}
      style={({ pressed }) => ({
        minHeight: toque.minimo,
        paddingHorizontal: espaco[5],
        borderRadius: raio.pilula,
        borderWidth: 2,
        borderColor: tema.marca,
        backgroundColor: primario ? tema.marca : 'transparent',
        alignItems: 'center',
        justifyContent: 'center',
        opacity: pressed ? 0.8 : 1,
      })}
    >
      <Texto forte style={{ color: primario ? tema.textoSobreMarca : tema.marca }}>
        {rotulo}
      </Texto>
    </Pressable>
  );
}
