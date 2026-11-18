import { Stack } from 'expo-router';

import { TelaDeBloqueio } from '@/presentation/componentes/TelaDeBloqueio';
import { fonte, useTema } from '@/presentation/tema';

export default function FamiliaLayout() {
  const tema = useTema();
  return (
    <>
      <Stack
        screenOptions={{
          headerStyle: { backgroundColor: tema.superficie },
          headerTintColor: tema.marca,
          headerTitleStyle: { fontFamily: fonte.textoForte, color: tema.texto },
          headerBackButtonDisplayMode: 'minimal',
          contentStyle: { backgroundColor: tema.fundo },
        }}
      >
        <Stack.Screen name="(abas)" options={{ headerShown: false }} />
        <Stack.Screen name="casa/[id]" options={{ title: 'Casa' }} />
        <Stack.Screen name="casa/[id]/morador" options={{ title: 'Morador' }} />
        <Stack.Screen name="casa/[id]/rotina" options={{ title: 'Rotina' }} />
        <Stack.Screen name="casa/[id]/sentinela" options={{ title: 'Sentinela' }} />
        <Stack.Screen name="alerta/[id]" options={{ title: 'Alerta' }} />
        <Stack.Screen name="sincronizacao" options={{ title: 'Sincronização' }} />
      </Stack>
      {/* A área da família mostra dados sensíveis: com o bloqueio ligado, a tela de bloqueio cobre tudo. */}
      <TelaDeBloqueio />
    </>
  );
}
