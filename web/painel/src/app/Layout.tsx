import { Link, Outlet, useNavigate } from '@tanstack/react-router';
import { sessao } from '@/api/sessao';
import { Folhagem, Marca } from '@/componentes/Icone';
import { desconectar, useTempoReal } from '@/api/tempo-real';

const PAGINAS = [
  { para: '/', rotulo: 'Visão geral' },
  { para: '/alertas', rotulo: 'Alertas' },
  { para: '/casas', rotulo: 'Casas' },
  { para: '/risco-cidadao', rotulo: 'Risco Cidadão' },
  { para: '/operacao', rotulo: 'Operação' },
] as const;

/** A moldura do painel: topo com a folhagem, navegação, o estado do tempo real e o commit no rodapé. */
export function Layout() {
  const navegar = useNavigate();
  const atual = sessao.atual();
  const { conectado } = useTempoReal();

  function sair() {
    desconectar();
    sessao.sair();
    void navegar({ to: '/entrar' });
  }

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="topo border-b border-borda">
        <Folhagem />
        <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-4 px-4 py-4 sm:px-6">
          <Link to="/" className="flex items-center gap-3 rounded-medio">
            <Marca tamanho={40} />
            <span>
              <strong className="block font-display text-xl leading-tight">Sombra</strong>
              <span className="text-sm text-texto-suave">Central de cuidado</span>
            </span>
          </Link>
          <div className="flex items-center gap-3 text-sm">
            <span
              className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 ${conectado ? 'border-normal-borda bg-normal-fundo text-normal-texto' : 'border-borda text-texto-suave'}`}
              title={conectado ? 'Recebendo os eventos do Core ao vivo' : 'Reconectando ao Core'}
            >
              <span aria-hidden="true" className={`size-2 rounded-full ${conectado ? 'bg-current' : 'bg-texto-suave'}`} />
              {conectado ? 'Ao vivo' : 'Reconectando'}
            </span>
            <span className="hidden text-texto-suave sm:inline">{atual?.nome}</span>
            <button type="button" onClick={sair} className="min-h-11 rounded-medio border border-borda px-3 hover:bg-superficie-alt">
              Sair
            </button>
          </div>
        </div>
        <nav aria-label="Páginas do painel" className="mx-auto max-w-7xl overflow-x-auto px-4 sm:px-6">
          <ul className="flex gap-1">
            {PAGINAS.map((p) => (
              <li key={p.para}>
                <Link
                  to={p.para}
                  activeOptions={{ exact: p.para === '/' }}
                  className="inline-flex min-h-11 items-center whitespace-nowrap rounded-t-medio border-b-3 border-transparent px-3 font-semibold text-texto-suave hover:text-texto data-[status=active]:border-marca data-[status=active]:text-texto"
                >
                  {p.rotulo}
                </Link>
              </li>
            ))}
          </ul>
        </nav>
      </header>
      <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-6 sm:px-6">
        <Outlet />
      </main>
      <footer className="border-t border-borda px-4 py-3 text-center text-sm text-texto-suave">
        Sombra · ninguém enfrenta o calor sozinho · painel <span className="font-mono">{__COMMIT__}</span> · PII sempre mascarada
      </footer>
    </div>
  );
}
