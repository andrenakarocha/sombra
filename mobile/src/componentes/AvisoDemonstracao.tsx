import { View } from 'react-native';

import { TOKEN_DEMO, useSessao } from '@/sessao/sessao';
import { espaco, raio, useTema } from '@/tema';

import { Texto } from './Texto';

export function AvisoDemonstracao() {
  const tema = useTema();
  const demo = useSessao((s) => s.token === TOKEN_DEMO);
  if (!demo) return null;
  return (
    <View
      accessibilityRole="alert"
      style={{ backgroundColor: tema.marcaSuave, borderRadius: raio.medio, padding: espaco[3], borderWidth: 1, borderColor: tema.marca }}
    >
      <Texto forte>Modo demonstração</Texto>
      <Texto variante="pequeno">Dados de exemplo, sem o servidor. Nada é enviado e nenhuma ligação é feita.</Texto>
    </View>
  );
}
