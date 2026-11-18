import { createContext, type ReactNode, useContext } from 'react';

import type { Container } from '@/application/container';

const ContextoDoContainer = createContext<Container | null>(null);

export function ContainerProvider({ container, children }: { container: Container; children: ReactNode }) {
  return <ContextoDoContainer.Provider value={container}>{children}</ContextoDoContainer.Provider>;
}

export function useContainer(): Container {
  const container = useContext(ContextoDoContainer);
  if (!container) throw new Error('useContainer fora do ContainerProvider');
  return container;
}
