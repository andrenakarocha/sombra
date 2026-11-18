import { useState } from 'react';
import { Pressable, View } from 'react-native';

import type { Domicilio } from '@/application/modelos';
import { espaco, raio, toque, useTema } from '@/presentation/tema';

import { Texto } from './Texto';

// Com uma casa só, não aparece nada; com várias, um botão por casa.
export function useCasaEscolhida(casas: Domicilio[]) {
  const [id, setId] = useState<string | null>(null);
  const casa = casas.find((c) => c.id === id) ?? casas[0];
  return { casa, escolher: setId };
}

export function SeletorDeCasa({
  casas,
  escolhida,
  aoEscolher,
}: {
  casas: Domicilio[];
  escolhida: Domicilio | undefined;
  aoEscolher: (id: string) => void;
}) {
  const tema = useTema();
  if (casas.length < 2) return null;
  return (
    <View accessibilityRole="radiogroup" style={{ flexDirection: 'row', flexWrap: 'wrap', gap: espaco[2] }}>
      {casas.map((c) => {
        const ativa = c.id === escolhida?.id;
        return (
          <Pressable
            key={c.id}
            accessibilityRole="radio"
            accessibilityState={{ checked: ativa }}
            accessibilityLabel={c.apelido}
            accessibilityHint="Mostra os dados desta casa"
            onPress={() => aoEscolher(c.id)}
            style={{
              minHeight: toque.minimo,
              paddingHorizontal: espaco[4],
              justifyContent: 'center',
              borderRadius: raio.pilula,
              borderWidth: 2,
              borderColor: tema.marca,
              backgroundColor: ativa ? tema.marca : 'transparent',
            }}
          >
            <Texto forte style={{ color: ativa ? tema.textoSobreMarca : tema.marca }}>
              {c.apelido}
            </Texto>
          </Pressable>
        );
      })}
    </View>
  );
}
