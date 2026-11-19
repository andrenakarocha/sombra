import type { ReactNode } from 'react';
import { ErroDaApi } from '@/api/http';

export function Cartao({ titulo, acao, children, className = '' }: { titulo?: string; acao?: ReactNode; children: ReactNode; className?: string }) {
  return (
    <section className={`rounded-grande border border-borda bg-superficie p-5 shadow-[0_18px_40px_-32px_var(--folhagem)] ${className}`}>
      {titulo ? (
        <header className="mb-4 flex items-baseline justify-between gap-3">
          <h2 className="font-display text-lg font-semibold">{titulo}</h2>
          {acao}
        </header>
      ) : null}
      {children}
    </section>
  );
}

export function Kpi({ rotulo, valor, detalhe, destaque = false }: { rotulo: string; valor: ReactNode; detalhe?: ReactNode; destaque?: boolean }) {
  return (
    <div className={`rounded-grande border p-4 ${destaque ? 'border-marca bg-marca-suave' : 'border-borda bg-superficie'}`}>
      <p className="text-sm text-texto-suave">{rotulo}</p>
      <p className="mt-1 font-display text-3xl font-semibold tabular-nums">{valor}</p>
      {detalhe ? <p className="mt-1 text-sm text-texto-suave">{detalhe}</p> : null}
    </div>
  );
}

/** O erro na tela, com o código curto para cruzar com o log (observabilidade §3). */
export function ErroNaTela({ erro }: { erro: unknown }) {
  const codigo = erro instanceof ErroDaApi ? erro.codigo : null;
  const mensagem = erro instanceof Error ? erro.message : 'Algo deu errado.';
  return (
    <div role="alert" className="rounded-medio border border-perigo-borda bg-perigo-fundo p-4 text-perigo-texto">
      <p className="font-semibold">{mensagem}</p>
      {codigo ? <p className="mt-1 font-mono text-sm">código do erro: {codigo}</p> : null}
    </div>
  );
}

export function Carregando({ texto = 'Carregando…' }: { texto?: string }) {
  return (
    <p className="animate-pulse text-texto-suave" aria-live="polite">
      {texto}
    </p>
  );
}

export function Vazio({ children }: { children: ReactNode }) {
  return <p className="rounded-medio border border-dashed border-borda p-6 text-center text-texto-suave">{children}</p>;
}
