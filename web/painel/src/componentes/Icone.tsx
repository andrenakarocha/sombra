import type { SVGProps } from 'react';

/** Os ícones da direção de arte (design/amostra.html): cada nível tem uma forma própria, nunca só a cor (D12). */
const caminhos = {
  normal: (
    <>
      <circle cx="12" cy="12" r="10" fill="none" stroke="currentColor" strokeWidth="2.2" />
      <path d="m7.5 12.4 3 3 6-6.4" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round" />
    </>
  ),
  atencao: (
    <>
      <circle cx="12" cy="12" r="10" fill="none" stroke="currentColor" strokeWidth="2.2" />
      <path d="M12 6.5v7" stroke="currentColor" strokeWidth="2.6" strokeLinecap="round" />
      <circle cx="12" cy="17.2" r="1.5" fill="currentColor" />
    </>
  ),
  cautela: (
    <>
      <circle cx="12" cy="12" r="10" fill="none" stroke="currentColor" strokeWidth="2.2" />
      <path d="M9 6.5v7M15 6.5v7" stroke="currentColor" strokeWidth="2.6" strokeLinecap="round" />
      <circle cx="9" cy="17.2" r="1.5" fill="currentColor" />
      <circle cx="15" cy="17.2" r="1.5" fill="currentColor" />
    </>
  ),
  perigo: (
    <>
      <path d="M12 2.8 22 20.5H2z" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinejoin="round" />
      <path d="M12 9v5.5" stroke="currentColor" strokeWidth="2.6" strokeLinecap="round" />
      <circle cx="12" cy="17.5" r="1.4" fill="currentColor" />
    </>
  ),
  extremo: (
    <>
      <path d="M8.1 2.5h7.8l5.6 5.6v7.8l-5.6 5.6H8.1l-5.6-5.6V8.1z" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinejoin="round" />
      <path d="M12 7v6.5" stroke="currentColor" strokeWidth="2.6" strokeLinecap="round" />
      <circle cx="12" cy="16.8" r="1.5" fill="currentColor" />
    </>
  ),
  escudo: (
    <>
      <path d="M12 2.5 20 5.6v6.1c0 4.8-3.3 8.4-8 9.8-4.7-1.4-8-5-8-9.8V5.6z" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinejoin="round" />
      <path d="M12 7.5v6" stroke="currentColor" strokeWidth="2.6" strokeLinecap="round" />
      <circle cx="12" cy="16.6" r="1.4" fill="currentColor" />
    </>
  ),
} as const;

export type NomeDoIcone = keyof typeof caminhos;

export function Icone({ nome, titulo, ...props }: { nome: NomeDoIcone; titulo?: string } & SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" width="1.25em" height="1.25em" role={titulo ? 'img' : undefined} aria-hidden={titulo ? undefined : true} {...props}>
      {titulo ? <title>{titulo}</title> : null}
      {caminhos[nome]}
    </svg>
  );
}

/** A marca do Sombra: o sol, a casa e o telhado. */
export function Marca({ tamanho = 40 }: { tamanho?: number }) {
  return (
    <svg viewBox="0 0 64 64" width={tamanho} height={tamanho} aria-hidden="true">
      <circle cx="42" cy="21" r="11" fill="var(--cor-ocre-500)" />
      <path d="M14 33.5h36V54a2 2 0 0 1-2 2H16a2 2 0 0 1-2-2z" fill="var(--marca)" />
      <path d="M28 56V45.5a4 4 0 0 1 8 0V56z" fill="var(--fundo)" />
      <path d="M7 35 32 15.5 57 35" fill="none" stroke="var(--cor-telha-500)" strokeWidth="6.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

/** A textura de sombra de folhas do topo (SVG procedural, sem imagem). */
export function Folhagem() {
  return (
    <svg className="folhagem" aria-hidden="true" focusable="false">
      <defs>
        <filter id="folhas-dia" x="0" y="0" width="100%" height="100%">
          <feTurbulence type="fractalNoise" baseFrequency="0.011 0.016" numOctaves={3} seed={11} />
          <feColorMatrix type="matrix" values="0 0 0 0 0.043  0 0 0 0 0.231  0 0 0 0 0.224  0 0 0 16 -7.1" />
          <feGaussianBlur stdDeviation="1.2" />
        </filter>
        <filter id="folhas-noite" x="0" y="0" width="100%" height="100%">
          <feTurbulence type="fractalNoise" baseFrequency="0.011 0.016" numOctaves={3} seed={11} />
          <feColorMatrix type="matrix" values="0 0 0 0 0.941  0 0 0 0 0.812  0 0 0 0 0.514  0 0 0 -18 8.6" />
          <feGaussianBlur stdDeviation="1.2" />
        </filter>
      </defs>
      <rect className="dia" width="100%" height="100%" filter="url(#folhas-dia)" />
      <rect className="noite" width="100%" height="100%" filter="url(#folhas-noite)" />
    </svg>
  );
}
