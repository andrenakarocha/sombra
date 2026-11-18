// Tradução entre o modelo de leitura (o que o contrato transporta e a tela mostra) e o domínio.

import { AlertaDeCalor } from '@/domain/alerta/alerta-de-calor';
import type { Clock } from '@/domain/compartilhado/clock';

import type { Alerta } from './modelos';
import type { AlertaSalvo } from './ports/repositorios';

// O contrato não traz `temVizinho`, `pedidoDeAjuda` nem `resolvidoPor`: valem os padrões mais seguros.
export function alertaDoServidor(dto: Alerta): AlertaSalvo {
  return {
    apelidoDaCasa: dto.apelidoDaCasa,
    alerta: AlertaDeCalor.restaurar({
      id: dto.id,
      domicilioId: dto.domicilioId,
      tipo: dto.tipo,
      estado: dto.estado,
      nivel: dto.nivel,
      degrau: dto.degrau,
      abertoEm: dto.abertoEm,
      prazo: dto.prazo,
      pedidoDeAjuda: false,
      temVizinho: true,
      resolvidoPor: null,
      linhaDoTempo: dto.linhaDoTempo ?? [],
    }),
  };
}

export function alertaParaTela({ alerta, apelidoDaCasa }: AlertaSalvo, clock: Clock): Alerta {
  const d = alerta.dados;
  return {
    id: d.id,
    domicilioId: d.domicilioId,
    apelidoDaCasa,
    tipo: d.tipo,
    estado: d.estado,
    nivel: d.nivel,
    degrau: d.degrau,
    abertoEm: d.abertoEm,
    prazo: d.prazo,
    linhaDoTempo: [...d.linhaDoTempo],
    minutosAteProximoDegrau: alerta.minutosAteProximoDegrau(clock),
  };
}
