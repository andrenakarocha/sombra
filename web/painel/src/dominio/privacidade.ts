import type { EventoDeSeguranca } from '@/api/schemas';

/** Os nomes que o operador lê para as famílias de dado e os sinks (spec 07). */
export const CATEGORIA: Record<string, string> = {
  SAUDE: 'Saúde',
  IDENTIFICACAO: 'Identificação',
  CONTATO: 'Contato',
  CREDENCIAL: 'Credenciais',
  LOCALIZACAO: 'Localização',
};

export const SINKS = ['RESPOSTA_HTTP', 'LOG', 'SAIDA_EXTERNA', 'MQTT', 'EXCECAO', 'ENTRADA'] as const;
export type Sink = (typeof SINKS)[number];

export const SINK: Record<Sink, string> = {
  RESPOSTA_HTTP: 'Resposta HTTP',
  LOG: 'Log',
  SAIDA_EXTERNA: 'Saída externa (IA, HTTP)',
  MQTT: 'MQTT (Sentinela)',
  EXCECAO: 'Exceção',
  ENTRADA: 'Entrada (campo livre)',
};

/** O Core fala o sink em PascalCase nas contagens (RespostaHttp) e em UPPER_SNAKE nos eventos (RESPOSTA_HTTP). */
export function normalizarSink(nome: string): string {
  return nome.includes('_') ? nome.toUpperCase() : nome.replace(/([a-z])([A-Z])/g, '$1_$2').toUpperCase();
}

export function rotuloDaCategoria(c: string | null): string {
  if (!c) return 'Integridade';
  return CATEGORIA[c] ?? c.charAt(0) + c.slice(1).toLowerCase();
}

const TIPO: Record<string, string> = {
  ACESSO_NEGADO: 'Acesso negado',
  MQTT_ASSINATURA_INVALIDA: 'Assinatura MQTT inválida',
  MQTT_REPLAY: 'Mensagem MQTT repetida',
};

/** De onde o dado tentou sair, ou o tipo do evento quando ele não tem sink. */
export function rotuloDoSink(sink: string | null, tipo: string): string {
  if (!sink) return TIPO[tipo] ?? tipo;
  return SINK[normalizarSink(sink) as Sink] ?? sink;
}

/** Bloqueios por hora nas últimas 24 h, um balde por hora cheia, para cada sink (small multiples). */
export function seriePorSink(eventos: readonly EventoDeSeguranca[], agora: number = Date.now()) {
  const HORA = 3_600_000;
  const fim = Math.floor(agora / HORA) * HORA;
  const horas = Array.from({ length: 24 }, (_, i) => fim - (23 - i) * HORA);
  const contagem = new Map<string, number[]>();
  for (const e of eventos) {
    const t = Math.floor(new Date(e.momento).getTime() / HORA) * HORA;
    const i = horas.indexOf(t);
    if (i < 0) continue;
    if (!e.sink) continue;
    const sink = normalizarSink(e.sink);
    const serie = contagem.get(sink) ?? new Array<number>(24).fill(0);
    serie[i] = (serie[i] ?? 0) + 1;
    contagem.set(sink, serie);
  }
  return SINKS.map((sink) => ({
    sink,
    pontos: horas.map((hora, i) => ({ hora, eventos: contagem.get(sink)?.[i] ?? 0 })),
    total: (contagem.get(sink) ?? []).reduce((a, b) => a + b, 0),
  }));
}
