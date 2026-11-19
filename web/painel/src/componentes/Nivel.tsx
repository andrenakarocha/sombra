import type { Nivel } from '@/api/schemas';
import { NIVEL } from '@/dominio/niveis';
import { Icone } from './Icone';

/** O selo de um nível: cor, forma e palavra (D12). */
export function SeloDeNivel({ nivel, grande = false }: { nivel: Nivel; grande?: boolean }) {
  const n = NIVEL[nivel];
  return (
    <span
      className={`inline-flex items-center gap-1.5 whitespace-nowrap rounded-full border font-semibold ${n.classes} ${grande ? 'px-3 py-1.5 text-base' : 'px-2.5 py-0.5 text-sm'}`}
    >
      <Icone nome={n.icone} />
      {n.rotulo}
    </span>
  );
}
