import { z } from 'zod';

/** Os formatos que o painel lê do Core (docs/contratos/core-api.md), conferidos na chegada. */
export const NIVEIS = ['NORMAL', 'ATENCAO', 'CAUTELA_EXTREMA', 'PERIGO', 'PERIGO_EXTREMO'] as const;
export const nivel = z.enum(NIVEIS);
export type Nivel = z.infer<typeof nivel>;

const momento = z.string();

export const LoginSchema = z.object({ token: z.string().min(1) });

export const pagina = <T extends z.ZodType>(item: T) =>
  z.object({ itens: z.array(item), pagina: z.number(), tamanho: z.number(), total: z.number() });

export const ResumoSchema = z.object({
  casasMonitoradas: z.number(),
  alertasAtivos: z.number(),
  tempoMedianoAteCheckInMin: z.number().nullable(),
  alertasResolvidosSemUltimoDegrauPct: z.number().nullable(),
  alarmesDesnecessariosSemana: z.number(),
  escalonamentosHojePorDegrau: z.record(z.string(), z.number()),
  sentinelasOffline: z.number(),
  alertasAtivosPorNivel: z.record(z.string(), z.number()),
  alertasDaSemanaPorBairro: z.record(z.string(), z.number()),
});
export type Resumo = z.infer<typeof ResumoSchema>;

export const EtapaSchema = z.object({ momento, degrau: z.number(), descricao: z.string() });

export const AlertaSchema = z.object({
  id: z.string(),
  domicilioId: z.string(),
  apelidoDaCasa: z.string(),
  tipo: z.enum(['PREVENTIVO', 'DE_SENTINELA']),
  estado: z.string(),
  nivel,
  degrau: z.number(),
  abertoEm: momento,
  prazo: momento.nullable(),
  encerradoEm: momento.nullable(),
  mensagemAoMorador: z.string().nullable().optional(),
  linhaDoTempo: z.array(EtapaSchema),
});
export type Alerta = z.infer<typeof AlertaSchema>;

export const LeituraSchema = z.object({
  momento,
  temperaturaC: z.number(),
  umidadePct: z.number(),
  indiceDeCalorC: z.number(),
  nivel: nivel.optional(),
});

export const DomicilioSchema = z.object({
  id: z.string(),
  apelido: z.string(),
  bairro: z.string(),
  regiaoClimatica: z.string(),
  monitorado: z.boolean(),
  nivelAtual: nivel,
  ultimaLeitura: LeituraSchema.nullable(),
  sentinela: z.object({ deviceId: z.string(), online: z.boolean() }).nullable(),
  ultimoCheckIn: momento.nullable(),
  alertaAberto: z.object({ id: z.string(), tipo: z.string(), estado: z.string(), degrau: z.number() }).passthrough().nullable(),
});
export type Domicilio = z.infer<typeof DomicilioSchema>;

export const FAROIS = ['VERDE', 'AMARELO', 'VERMELHO'] as const;
export const farol = z.enum(FAROIS);
export type Farol = z.infer<typeof farol>;

export const RiscoCidadaoSchema = z.object({
  pontuacao: z.number(),
  farol,
  eventosNaJanela: z.number(),
  porCategoria: z.array(z.object({ categoria: z.string(), pontuacao: z.number(), farol, eventos: z.number() })),
  porSink: z.array(z.object({ nome: z.string(), eventos: z.number() })),
  topRegras: z.array(z.object({ nome: z.string(), eventos: z.number() })),
  calculadoEm: momento,
});
export type RiscoCidadao = z.infer<typeof RiscoCidadaoSchema>;

export const EventoDeSegurancaSchema = z.object({
  id: z.string(),
  momento,
  tipo: z.string(),
  origem: z.string(),
  // eventos de integridade (ex.: MQTT_ASSINATURA_INVALIDA) não têm sink, família de dado nem regra
  sink: z.string().nullable(),
  categoria: z.string().nullable(),
  severidade: z.string().nullable(),
  regra: z.string().nullable(),
  bloqueado: z.boolean(),
  domicilioId: z.string().nullable(),
  correlationId: z.string().nullable(),
  detalhe: z.string().nullable(),
  traducao: z.object({ cidadao: z.string(), gestao: z.string(), tecnico: z.string(), fonte: z.string() }).partial().nullable(),
});
export type EventoDeSeguranca = z.infer<typeof EventoDeSegurancaSchema>;

export const VersaoSchema = z.object({ versao: z.string(), commit: z.string(), construidoEm: z.string() });
export const ProntidaoSchema = z.object({ status: z.string(), verificacoes: z.record(z.string(), z.string()) });
