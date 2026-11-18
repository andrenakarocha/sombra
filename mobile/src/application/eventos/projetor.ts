// Eventos do hub (docs/contratos/signalr.md) → banco local → telas. Os eventos de alerta não trazem o alerta
// inteiro: avisam que mudou, e a consulta busca de novo no Core.

import type { Clock } from '@/domain/compartilhado/clock';
import type { NivelDeRisco } from '@/domain/clima/nivel-de-risco';

import type { Barramento } from '../barramento';
import { preferenciasStore } from '../estado/preferencias';
import { notificacaoDoEvento } from '../notificacoes/politica';
import type { CasaRepository } from '../ports/repositorios';
import type { Notificador } from '../ports/servicos';

export const EVENTOS_DO_HUB = [
  'LeituraRegistrada',
  'SentinelaStatus',
  'CheckInRegistrado',
  'AlertaAberto',
  'AlertaEscalonado',
  'AlertaResolvido',
  'RiscoCidadaoAtualizado',
] as const;

type Payload = Record<string, unknown>;

export class ProjetorDeEventos {
  constructor(
    private readonly d: { casas: CasaRepository; barramento: Barramento; notificador: Notificador; clock: Clock },
  ) {}

  async receber(evento: string, payload: Payload) {
    const domicilioId = typeof payload.domicilioId === 'string' ? payload.domicilioId : null;
    await this.projetar(evento, payload, domicilioId);
    await this.notificar(evento, payload);
  }

  private async projetar(evento: string, p: Payload, domicilioId: string | null) {
    const { casas, barramento } = this.d;
    const daCasa = domicilioId ? [{ recurso: 'casas' as const }, { recurso: 'casa' as const, id: domicilioId }] : [];
    switch (evento) {
      case 'LeituraRegistrada':
        await casas.registrarLeitura(
          domicilioId!,
          { momento: String(p.momento), temperaturaC: Number(p.temperaturaC), umidadePct: Number(p.umidadePct), indiceDeCalorC: Number(p.indiceDeCalorC) },
          p.nivel as NivelDeRisco,
        );
        return barramento.publicar(...daCasa);
      case 'SentinelaStatus':
        await casas.definirSentinelaOnline(domicilioId!, String(p.deviceId), Boolean(p.online));
        return barramento.publicar(...daCasa);
      case 'CheckInRegistrado':
        await casas.registrarCheckIn(domicilioId!, String(p.momento));
        return barramento.publicar(...daCasa);
      case 'AlertaAberto':
      case 'AlertaEscalonado':
      case 'AlertaResolvido':
        return barramento.publicar(...daCasa, { recurso: 'alertas' }, { recurso: 'alerta', id: String(p.alertaId) });
      case 'RiscoCidadaoAtualizado':
        return barramento.publicar({ recurso: 'privacidade', ...(domicilioId ? { id: domicilioId } : {}) });
    }
  }

  private async notificar(evento: string, payload: Payload) {
    const modoIdoso = !!preferenciasStore.getState().modoIdosoCasaId;
    const apelidos = new Map((await this.d.casas.listar()).map((c) => [c.id, c.apelido]));
    const n = notificacaoDoEvento(evento, payload, {
      modoIdoso,
      apelido: (id) => apelidos.get(id) ?? 'Sombra',
      agora: this.d.clock.now(),
    });
    if (n) await this.d.notificador.notificar(n, modoIdoso).catch(() => {});
  }
}
