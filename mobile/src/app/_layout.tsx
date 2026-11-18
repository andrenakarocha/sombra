import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useFonts } from 'expo-font';
import { Stack } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';

import type { Container } from '@/application/container';
import { criarContainerDoAparelho } from '@/infrastructure/container-do-aparelho';
import { usePreferencias, useSessao } from '@/presentation/hooks/estado';
import { opcoesDoQueryClient, useVidaDoApp } from '@/presentation/hooks/vida-do-app';
import { ContainerProvider } from '@/presentation/providers/container';
import { useTema } from '@/presentation/tema';
import { ARQUIVOS_DE_FONTE } from '@/presentation/tema/fontes';

SplashScreen.preventAutoHideAsync();

// A rota raiz é o único ponto que conhece a infraestrutura: monta o container e entrega para as telas.
export default function RootLayout() {
  const [container, setContainer] = useState<Container | null>(null);
  const [queryClient] = useState(() => new QueryClient(opcoesDoQueryClient));
  const [carregou, erro] = useFonts(ARQUIVOS_DE_FONTE);

  useEffect(() => {
    criarContainerDoAparelho().then(async (c) => {
      await c.sessao.carregar();
      setContainer(c);
    });
  }, []);

  const pronto = (carregou || erro) && container;
  useEffect(() => {
    if (pronto) SplashScreen.hideAsync();
  }, [pronto]);

  if (!pronto) return null;
  return (
    <ContainerProvider container={container}>
      <QueryClientProvider client={queryClient}>
        <Rotas container={container} queryClient={queryClient} />
      </QueryClientProvider>
    </ContainerProvider>
  );
}

function Rotas({ container, queryClient }: { container: Container; queryClient: QueryClient }) {
  const tema = useTema();
  const token = useSessao((s) => s.token);
  const modoIdosoCasaId = usePreferencias((p) => p.modoIdosoCasaId);
  const modoIdoso = !!token && !!modoIdosoCasaId;
  useVidaDoApp(container, queryClient);

  return (
    <>
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
    </>
  );
}
