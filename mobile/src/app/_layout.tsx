import { useFonts } from 'expo-font';
import { Stack } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { StatusBar } from 'expo-status-bar';
import { useEffect } from 'react';

import { useTema } from '@/tema';
import { ARQUIVOS_DE_FONTE } from '@/tema/fontes';

SplashScreen.preventAutoHideAsync();

export default function RootLayout() {
  const tema = useTema();
  const [carregou, erro] = useFonts(ARQUIVOS_DE_FONTE);

  useEffect(() => {
    if (carregou || erro) SplashScreen.hideAsync();
  }, [carregou, erro]);

  if (!carregou && !erro) return null;

  return (
    <>
      <StatusBar style="auto" />
      <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: tema.fundo } }} />
    </>
  );
}
