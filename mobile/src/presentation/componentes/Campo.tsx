import { TextInput, type TextInputProps, View } from 'react-native';

import { espaco, fonte, raio, tamanho, toque, useTema } from '@/presentation/tema';

import { Texto } from './Texto';

type Props = TextInputProps & { rotulo: string };

// O rótulo fica sempre visível (placeholder some ao digitar e tem contraste baixo).
export function Campo({ rotulo, ...resto }: Props) {
  const tema = useTema();
  return (
    <View style={{ gap: espaco[2] }}>
      <Texto forte>{rotulo}</Texto>
      <TextInput
        accessibilityLabel={rotulo}
        placeholderTextColor={tema.textoSuave}
        style={{
          minHeight: toque.minimo,
          paddingHorizontal: espaco[4],
          borderWidth: 2,
          borderColor: tema.borda,
          borderRadius: raio.medio,
          backgroundColor: tema.superficie,
          color: tema.texto,
          fontFamily: fonte.texto,
          fontSize: tamanho.texto,
        }}
        {...resto}
      />
    </View>
  );
}
