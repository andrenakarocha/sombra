import type { Alerta, EstadoDoAlerta } from '@/api/tipos';

export const ROTULO_DO_ESTADO: Record<EstadoDoAlerta, string> = {
  AGUARDANDO_CHECKIN: 'Esperando o "estou bem"',
  ESCALONADO: 'Avisando a rede de cuidado',
  RECONHECIDO: 'Alguém já está cuidando',
  RESOLVIDO: 'Resolvido',
};

export const ativo = (alerta: Alerta) => alerta.estado !== 'RESOLVIDO';
