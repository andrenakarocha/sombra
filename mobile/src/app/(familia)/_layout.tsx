import { Stack } from 'expo-router';

import { fonte, useTema } from '@/presentation/tema';

export default function FamiliaLayout() {
  const tema = useTema();
  return (
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
      <Stack.Screen name="alerta/[id]" options={{ title: 'Alerta' }} />
    </Stack>
  );
}
