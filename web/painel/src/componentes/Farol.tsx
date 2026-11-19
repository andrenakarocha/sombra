import type { Farol } from '@/api/schemas';
import { FAROL } from '@/dominio/niveis';
import { Icone } from './Icone';

/** O selo de um farol: cor, forma e palavra. */
export function SeloDeFarol({ farol }: { farol: Farol }) {
  const f = FAROL[farol];
  return (
    <span className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-sm font-semibold ${f.classes}`}>
      <Icone nome={f.icone} />
      {f.rotulo}
    </span>
  );
}

/** O farol global, como manchete: a cor com a forma, a pontuação e a frase para quem decide. */
export function FarolGrande({ farol, pontuacao, eventos }: { farol: Farol; pontuacao: number; eventos: number }) {
  const f = FAROL[farol];
  return (
    <div className={`flex flex-col gap-4 rounded-grande border-2 p-5 sm:flex-row sm:items-center ${f.classes}`} aria-live="polite">
      <Icone nome={f.icone} className="size-16 shrink-0" titulo={`Farol ${f.rotulo.toLowerCase()}`} />
      <div className="flex-1">
        <p className="text-sm font-semibold uppercase tracking-wide">Risco Cidadão</p>
        <p className="font-display text-3xl font-semibold">Farol {f.rotulo.toLowerCase()}</p>
        <p className="mt-1">{f.frase}</p>
      </div>
      <div className="text-left sm:text-right">
        <p className="font-display text-5xl font-semibold tabular-nums">{Math.round(pontuacao)}</p>
        <p className="text-sm">de 100 · {eventos} evento(s) na janela</p>
      </div>
    </div>
  );
}
