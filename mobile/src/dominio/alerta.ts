import type { Alerta, Domicilio, EstadoDoAlerta } from '@/application/modelos';
import { formatarHora } from '@/util/formato';

export const ROTULO_DO_ESTADO: Record<EstadoDoAlerta, string> = {
  AGUARDANDO_CHECKIN: 'Esperando o "estou bem"',
  ESCALONADO: 'Avisando a rede de cuidado',
  RECONHECIDO: 'Alguém já está cuidando',
  RESOLVIDO: 'Resolvido',
};

export const ativo = (alerta: Alerta) => alerta.estado !== 'RESOLVIDO';

// D13: sempre dizer o que acontece se ela não responder.
export function proximoPasso(casa: Domicilio | undefined) {
  if (casa?.alertaAberto) {
    return `Se não apertar até ${formatarHora(casa.alertaAberto.prazo)}, sua família recebe um lembrete.`;
  }
  return 'Se você não estiver bem, segure o botão vermelho.';
}
