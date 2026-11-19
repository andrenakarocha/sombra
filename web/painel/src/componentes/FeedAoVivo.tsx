import { useCasas } from '@/api/consultas';
import type { EventoAoVivo, NomeDoEvento } from '@/api/tempo-real';
import { useTempoReal } from '@/api/tempo-real';
import { DEGRAU } from '@/dominio/niveis';
import { formatarGraus, formatarHora } from '@/dominio/formato';
import { Vazio } from './Base';

const DESTINATARIO: Record<string, string> = { FAMILIAR: 'a família', VIZINHO: 'quem está perto', REDE: 'toda a rede' };

/** Uma frase curta para cada evento, sem dado pessoal (os eventos do hub não trazem PII). */
export function descrever(e: EventoAoVivo): string {
  const d = e.dados as Record<string, string | number | boolean | null>;
  switch (e.nome as NomeDoEvento) {
    case 'AlertaAberto':
      return d.tipo === 'PREVENTIVO' ? 'Alerta preventivo aberto: previsão de calor perigoso em 24 h.' : 'Alerta de calor aberto pela Sentinela.';
    case 'AlertaEscalonado':
      return `Alerta escalonado para o degrau ${d.degrau} (${DEGRAU[Number(d.degrau)] ?? ''}): avisando ${DESTINATARIO[String(d.destinatario)] ?? 'a rede'}.`;
    case 'AlertaResolvido':
      return d.minutosAteCheckIn !== null && d.minutosAteCheckIn !== undefined
        ? `Alerta resolvido: "estou bem" em ${Number(d.minutosAteCheckIn).toLocaleString('pt-BR', { maximumFractionDigits: 1 })} min.`
        : 'Alerta resolvido.';
    case 'CheckInRegistrado':
      return d.situacao === 'PRECISO_DE_AJUDA' ? 'Pedido de ajuda recebido.' : '"Estou bem" recebido.';
    case 'LeituraRegistrada':
      return `Leitura da Sentinela: índice de calor ${formatarGraus(Number(d.indiceDeCalorC))}.`;
    case 'SentinelaStatus':
      return d.online ? 'Sentinela voltou a responder.' : 'Sentinela parou de responder.';
    case 'AvisoParaARede':
      return d.motivo === 'PREVISAO' ? 'A família foi avisada da previsão de calor.' : 'A família foi avisada: o check-in diário não veio.';
    case 'RiscoCidadaoAtualizado':
      return `Risco Cidadão agora ${String(d.farol).toLowerCase()}.`;
    case 'AlertaDeInfra':
      return `Infra: ${String(d.mensagem ?? d.recurso)}.`;
  }
}

/**
 * O feed dos eventos que chegaram pelo hub desde que a página abriu. O evento traz só o ID da casa; o apelido vem da
 * lista de casas que o painel já tem em cache.
 */
export function FeedAoVivo({ filtro }: { filtro?: (e: EventoAoVivo) => boolean }) {
  const { eventos } = useTempoReal();
  const casas = useCasas();
  const apelido = new Map((casas.data?.itens ?? []).map((c) => [c.id, c.apelido]));
  const lista = filtro ? eventos.filter(filtro) : eventos;
  if (lista.length === 0) return <Vazio>Nenhum evento desde que você abriu o painel. Eles aparecem aqui ao vivo.</Vazio>;
  return (
    <ol className="space-y-2" aria-live="polite">
      {lista.slice(0, 12).map((e) => (
        <li key={`${e.recebidoEm}-${e.nome}`} className="flex gap-3 rounded-medio border border-borda bg-fundo px-3 py-2">
          <time className="shrink-0 font-mono text-sm text-texto-suave">{formatarHora(e.recebidoEm)}</time>
          <span>
            {apelido.has(String(e.dados.domicilioId)) ? <strong>{apelido.get(String(e.dados.domicilioId))}: </strong> : null}
            {descrever(e)}
          </span>
        </li>
      ))}
    </ol>
  );
}
