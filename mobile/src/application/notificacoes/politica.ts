// Notificações locais em degraus (D13, spec 05): lembrete leve → "quem está perto" → alerta, nunca alarme à toa.

import { ehNoite, horarioEmSaoPaulo } from '@/domain/compartilhado/clock';

import type { Notificacao } from '../ports/servicos';

type Contexto = { modoIdoso: boolean; apelido: (domicilioId: string) => string; agora: Date };

export function notificacaoDoEvento(evento: string, payload: Record<string, unknown>, ctx: Contexto): Notificacao | null {
  // 22h–7h: nada de lembrete que não seja perigo (D13). Os degraus só sobem com perigo de verdade, então passam.
  const noite = ehNoite(ctx.agora);
  const casa = () => ctx.apelido(String(payload.domicilioId));

  if (ctx.modoIdoso) {
    // Uma por vez, sempre dizendo o que acontece se ela não responder.
    if (evento !== 'AlertaAberto' || payload.tipo === 'PREVENTIVO') return null;
    return {
      canal: 'alertas',
      titulo: 'Está muito quente',
      corpo: `Abra o Sombra e aperte ESTOU BEM. Se não apertar até ${horarioEmSaoPaulo(String(payload.prazo))}, sua família recebe um lembrete.`,
    };
  }

  switch (evento) {
    case 'AlertaAberto':
      // Degrau 0 é com a própria pessoa (a Sentinela pede o "estou bem"); a família só recebe o aviso preventivo.
      if (payload.tipo !== 'PREVENTIVO' || noite) return null;
      return { canal: 'lembretes', titulo: casa(), corpo: 'Amanhã: calor perigoso. Combine de ligar para ela à tarde.' };
    case 'AlertaEscalonado': {
      const degrau = Number(payload.degrau);
      const mensagem = String(payload.mensagem);
      if (degrau === 1) return { canal: 'lembretes', titulo: 'Lembrete', corpo: mensagem };
      if (degrau === 2) return { canal: 'lembretes', titulo: 'Quem está perto', corpo: mensagem };
      return { canal: 'alertas', titulo: `Alerta: ${casa()}`, corpo: mensagem };
    }
    case 'CheckInRegistrado':
      if (payload.situacao !== 'PRECISO_DE_AJUDA') return null;
      return { canal: 'alertas', titulo: `Pedido de ajuda: ${casa()}`, corpo: 'Ela apertou PRECISO DE AJUDA. Ligue agora.' };
    case 'AlertaResolvido':
      if (noite) return null;
      return { canal: 'lembretes', titulo: casa(), corpo: 'Tudo certo: ela confirmou que está bem.' };
    default:
      return null;
  }
}
