import type { NomeDoIcone } from '@/componentes/Icone';
import type { Farol, Nivel } from '@/api/schemas';

/** Os cinco níveis (docs/contratos/niveis-de-risco.md): cor, forma e palavra juntas, nunca só a cor (D12). */
export const NIVEL: Record<Nivel, { rotulo: string; icone: NomeDoIcone; classes: string; ordem: number }> = {
  NORMAL: { rotulo: 'Normal', icone: 'normal', classes: 'bg-normal-fundo text-normal-texto border-normal-borda', ordem: 0 },
  ATENCAO: { rotulo: 'Atenção', icone: 'atencao', classes: 'bg-atencao-fundo text-atencao-texto border-atencao-borda', ordem: 1 },
  CAUTELA_EXTREMA: { rotulo: 'Cautela extrema', icone: 'cautela', classes: 'bg-cautela-fundo text-cautela-texto border-cautela-borda', ordem: 2 },
  PERIGO: { rotulo: 'Perigo', icone: 'perigo', classes: 'bg-perigo-fundo text-perigo-texto border-perigo-borda', ordem: 3 },
  PERIGO_EXTREMO: { rotulo: 'Perigo extremo', icone: 'extremo', classes: 'bg-extremo-fundo text-extremo-texto border-extremo-borda', ordem: 4 },
};

/** O farol de Risco Cidadão (spec 07): reaproveita as cores dos níveis, com a forma de cada um. */
export const FAROL: Record<Farol, { rotulo: string; frase: string; icone: NomeDoIcone; classes: string }> = {
  VERDE: { rotulo: 'Verde', frase: 'Nada estranho na última semana.', icone: 'normal', classes: 'bg-normal-fundo text-normal-texto border-normal-borda' },
  AMARELO: { rotulo: 'Amarelo', frase: 'O Sombra barrou tentativas de expor dados. Vale acompanhar.', icone: 'atencao', classes: 'bg-atencao-fundo text-atencao-texto border-atencao-borda' },
  VERMELHO: { rotulo: 'Vermelho', frase: 'Muitas tentativas de expor dados sensíveis: o time técnico precisa olhar.', icone: 'perigo', classes: 'bg-perigo-fundo text-perigo-texto border-perigo-borda' },
};

/** Os estados do alerta como o app e o Core falam (D17), em português para a tela. */
export const ESTADO_DO_ALERTA: Record<string, string> = {
  INFORMATIVO: 'Informativo',
  AGUARDANDO_CHECKIN: 'Aguardando o "estou bem"',
  ESCALONADO: 'Avisando a rede',
  RECONHECIDO: 'Alguém está cuidando',
  RESOLVIDO: 'Resolvido',
  EXPIRADO: 'Expirado',
};

export const DEGRAU: Record<number, string> = {
  0: 'Sentinela',
  1: 'Família',
  2: 'Quem está perto',
  3: 'Toda a rede',
};
