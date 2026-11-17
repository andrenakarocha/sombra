import { ScrollView } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { sair } from '@/api/auth';
import { Botao } from '@/componentes/Botao';
import { Texto } from '@/componentes/Texto';
import { espaco, useTema } from '@/tema';

export default function Ajustes() {
  const tema = useTema();
  return (
    <SafeAreaView edges={['top']} style={{ flex: 1, backgroundColor: tema.fundo }}>
      <ScrollView contentContainerStyle={{ padding: espaco[5], gap: espaco[5] }}>
        <Texto variante="tituloGrande" accessibilityRole="header">
          Ajustes
        </Texto>
        <Botao
          rotulo="Sair da conta"
          variante="secundario"
          dica="Sai da conta e apaga os dados salvos no aparelho"
          aoTocar={sair}
        />
      </ScrollView>
    </SafeAreaView>
  );
}
