import { QueryClientProvider } from '@tanstack/react-query';
import { useFonts } from 'expo-font';
import { Stack } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { StatusBar } from 'expo-status-bar';
import { useEffect } from 'react';

import { queryClient } from '@/api/consultas';
import { useSessao } from '@/sessao/sessao';
import { useTema } from '@/tema';
import { ARQUIVOS_DE_FONTE } from '@/tema/fontes';

SplashScreen.preventAutoHideAsync();

export default function RootLayout() {
  const tema = useTema();
  const [carregou, erro] = useFonts(ARQUIVOS_DE_FONTE);
  const { token, carregada, carregar } = useSessao();
  const pronto = (carregou || erro) && carregada;

  useEffect(() => {
    carregar();
  }, [carregar]);

  useEffect(() => {
    if (pronto) SplashScreen.hideAsync();
  }, [pronto]);

  if (!pronto) return null;

  return (
    <QueryClientProvider client={queryClient}>
      <StatusBar style="auto" />
      <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: tema.fundo } }}>
        <Stack.Protected guard={!token}>
          <Stack.Screen name="(auth)/entrar" />
        </Stack.Protected>
        <Stack.Protected guard={!!token}>
          <Stack.Screen name="(familia)" />
        </Stack.Protected>
      </Stack>
    </QueryClientProvider>
  );
}
