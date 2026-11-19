import { createRootRoute, createRoute, createRouter, Outlet, redirect } from '@tanstack/react-router';
import { podeUsarOPainel, sessao } from '@/api/sessao';
import { Alertas } from '@/paginas/Alertas';
import { Casas } from '@/paginas/Casas';
import { Login } from '@/paginas/Login';
import { Operacao } from '@/paginas/Operacao';
import { RiscoCidadao } from '@/paginas/RiscoCidadao';
import { VisaoGeral } from '@/paginas/VisaoGeral';
import { Layout } from './Layout';

const raiz = createRootRoute({ component: Outlet });

const entrar = createRoute({
  getParentRoute: () => raiz,
  path: '/entrar',
  component: Login,
  beforeLoad: () => {
    const s = sessao.atual();
    if (s && podeUsarOPainel(s.papel)) throw redirect({ to: '/' });
  },
});

/** Tudo fora do /entrar pede um operador com sessão válida. */
const protegida = createRoute({
  getParentRoute: () => raiz,
  id: 'protegida',
  component: Layout,
  beforeLoad: () => {
    const s = sessao.atual();
    if (!s || !podeUsarOPainel(s.papel)) throw redirect({ to: '/entrar' });
  },
});

const arvore = raiz.addChildren([
  entrar,
  protegida.addChildren([
    createRoute({ getParentRoute: () => protegida, path: '/', component: VisaoGeral }),
    createRoute({ getParentRoute: () => protegida, path: '/alertas', component: Alertas }),
    createRoute({ getParentRoute: () => protegida, path: '/casas', component: Casas }),
    createRoute({ getParentRoute: () => protegida, path: '/risco-cidadao', component: RiscoCidadao }),
    createRoute({ getParentRoute: () => protegida, path: '/operacao', component: Operacao }),
  ]),
]);

export const roteador = createRouter({ routeTree: arvore, defaultPreload: 'intent' });

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof roteador;
  }
}
