import { QueryClientProvider } from '@tanstack/react-query';
import { useFonts } from 'expo-font';
import { Stack } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { StatusBar } from 'expo-status-bar';
import { useEffect } from 'react';

import { useSincronizacaoDaFila } from '@/api/check-in';
import { queryClient } from '@/api/consultas';
import { useTempoReal } from '@/api/tempo-real';
import { usePreferencias } from '@/sessao/preferencias';
import { useSessao } from '@/sessao/sessao';
import { useTema } from '@/tema';
import { ARQUIVOS_DE_FONTE } from '@/tema/fontes';

SplashScreen.preventAutoHideAsync();

export default function RootLayout() {
  const tema = useTema();
  const [carregou, erro] = useFonts(ARQUIVOS_DE_FONTE);
  const { token, carregada, carregar } = useSessao();
  const { modoIdosoCasaId, carregada: preferenciasCarregadas, carregar: carregarPreferencias } = usePreferencias();
  const pronto = (carregou || erro) && carregada && preferenciasCarregadas;
  const modoIdoso = !!token && !!modoIdosoCasaId;
  useTempoReal();
  useSincronizacaoDaFila();

  useEffect(() => {
    carregar();
    carregarPreferencias();
  }, [carregar, carregarPreferencias]);

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
        <Stack.Protected guard={!!token && !modoIdoso}>
          <Stack.Screen name="(familia)" />
        </Stack.Protected>
        <Stack.Protected guard={modoIdoso}>
          <Stack.Screen name="idoso" />
        </Stack.Protected>
      </Stack>
    </QueryClientProvider>
  );
}
