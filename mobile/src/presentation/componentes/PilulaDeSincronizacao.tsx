import { router } from 'expo-router';
import { Pressable } from 'react-native';

import { useSincronizacao } from '../hooks/consultas';
import { espaco, raio, toque, useTema } from '../tema';

import { Texto } from './Texto';

// Aparece só quando há algo esperando: o "estou bem" guardado nunca fica invisível para a família.
export function PilulaDeSincronizacao() {
  const tema = useTema();
  const { data } = useSincronizacao();
  if (!data || data.pendentes + data.deadLetter === 0) return null;
  const texto =
    data.deadLetter > 0
      ? `${data.deadLetter} aviso${data.deadLetter > 1 ? 's' : ''} não aceito${data.deadLetter > 1 ? 's' : ''} pelo Sombra`
      : `${data.pendentes} aviso${data.pendentes > 1 ? 's' : ''} esperando a internet`;
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={texto}
      accessibilityHint="Abre a Central de sincronização"
      onPress={() => router.push('/sincronizacao')}
      style={{
        alignSelf: 'flex-start',
        minHeight: toque.minimo,
        justifyContent: 'center',
        paddingHorizontal: espaco[4],
        borderRadius: raio.pilula,
        backgroundColor: data.deadLetter > 0 ? tema.risco.atencao.fundo : tema.marcaSuave,
      }}
    >
      <Texto forte style={{ color: data.deadLetter > 0 ? tema.risco.atencao.texto : tema.texto }}>
        {texto}
      </Texto>
    </Pressable>
  );
}
