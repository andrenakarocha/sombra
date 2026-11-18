import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react-native';
import type { ReactElement } from 'react';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import type { Container } from '@/application/container';
import { useVidaDoApp } from '@/presentation/hooks/vida-do-app';
import { ContainerProvider } from '@/presentation/providers/container';

const metricas = { frame: { x: 0, y: 0, width: 390, height: 844 }, insets: { top: 0, left: 0, right: 0, bottom: 0 } };

function Vida({ container, queryClient, children }: { container: Container; queryClient: QueryClient; children: ReactElement }) {
  useVidaDoApp(container, queryClient);
  return children;
}

// Renderiza uma tela com o container de teste e a mesma ligação de eventos do app (barramento, rede, hub).
export async function renderComContainer(tela: ReactElement, container: Container) {
  // gcTime infinito: sem o timer de 5 min de coleta, que seguraria o Jest aberto depois dos testes.
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, networkMode: 'always', gcTime: Infinity }, mutations: { gcTime: Infinity } },
  });
  const resultado = await render(
    <SafeAreaProvider initialMetrics={metricas}>
      <ContainerProvider container={container}>
        <QueryClientProvider client={queryClient}>
          <Vida container={container} queryClient={queryClient}>
            {tela}
          </Vida>
        </QueryClientProvider>
      </ContainerProvider>
    </SafeAreaProvider>,
  );
  return { ...resultado, queryClient };
}
