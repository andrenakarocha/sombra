import { ScrollView, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { BotaoGigante } from '@/componentes/BotaoGigante';
import { CartaoCasa } from '@/componentes/CartaoCasa';
import { Farol } from '@/componentes/Farol';
import { Texto } from '@/componentes/Texto';
import { CASAS } from '@/demo/fixtures';
import { NIVEIS } from '@/dominio/nivel';
import { espaco, useTema } from '@/tema';

// Vitrine dos componentes-base (F6.1). As abas da família e o Modo Idoso substituem esta tela na F6.3.
export default function Vitrine() {
  const tema = useTema();
  const nada = () => {};
  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: tema.fundo }}>
      <ScrollView contentContainerStyle={{ padding: espaco[5], gap: espaco[5] }}>
        <Texto variante="tituloGrande" accessibilityRole="header">
          Sombra
        </Texto>
        <View style={{ gap: espaco[2] }}>
          {NIVEIS.map((nivel) => (
            <Farol key={nivel} nivel={nivel} />
          ))}
        </View>
        {CASAS.map((casa) => (
          <CartaoCasa key={casa.id} casa={casa} aoAbrir={nada} aoLigar={nada} />
        ))}
        <BotaoGigante rotulo="ESTOU BEM" instrucao="aperte uma vez" variante="bem" aoAcionar={nada} />
        <BotaoGigante
          rotulo="PRECISO DE AJUDA"
          instrucao="segure por 2 segundos"
          variante="ajuda"
          segurarPorMs={2000}
          aoAcionar={nada}
        />
      </ScrollView>
    </SafeAreaView>
  );
}
