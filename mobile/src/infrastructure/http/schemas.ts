// O contrato em código: toda resposta do Core passa por aqui antes de entrar no app. `satisfies` amarra cada schema
// ao modelo de leitura, então mudar um sem o outro não compila.

import { z } from 'zod';

import type {
  Alerta,
  ContatoDaRede,
  DadosDaRotina,
  Domicilio,
  Leitura,
  Morador,
  PontoDeResfriamento,
  PrivacidadeDaCasa,
  SentinelaVinculada,
} from '@/application/modelos';
import { NIVEIS } from '@/domain/clima/nivel-de-risco';
import { FATORES_DE_RISCO } from '@/domain/morador/fator-de-risco';

const momento = z.iso.datetime({ offset: true });
const nivel = z.enum(NIVEIS);
const tipoDeAlerta = z.enum(['PREVENTIVO', 'DE_SENTINELA']);

export const LeituraSchema = z.object({
  momento,
  temperaturaC: z.number(),
  umidadePct: z.number().min(0).max(100),
  indiceDeCalorC: z.number(),
}) satisfies z.ZodType<Leitura>;

export const DomicilioSchema = z.object({
  id: z.string().min(1),
  apelido: z.string(),
  bairro: z.string(),
  regiaoClimatica: z.string(),
  nivelAtual: nivel,
  ultimaLeitura: LeituraSchema.nullable(),
  sentinela: z.object({ deviceId: z.string(), online: z.boolean() }).nullable(),
  ultimoCheckIn: momento.nullable(),
  alertaAberto: z
    .object({ id: z.string(), tipo: tipoDeAlerta, estado: z.string(), degrau: z.number().int().min(0).max(3), prazo: momento })
    .nullable(),
}) satisfies z.ZodType<Domicilio>;

export const AlertaSchema = z.object({
  id: z.string().min(1),
  domicilioId: z.string(),
  apelidoDaCasa: z.string(),
  tipo: tipoDeAlerta,
  estado: z.enum(['AGUARDANDO_CHECKIN', 'ESCALONADO', 'RECONHECIDO', 'RESOLVIDO']),
  nivel,
  degrau: z.number().int().min(0).max(3),
  abertoEm: momento,
  prazo: momento,
  linhaDoTempo: z.array(z.object({ momento, degrau: z.number().int(), descricao: z.string() })).optional(),
}) satisfies z.ZodType<Alerta>;

export const MoradorSchema = z.object({
  id: z.string(),
  nome: z.string(),
  idade: z.number().int(),
  telefone: z.string().nullable(),
  cpf: z.string(),
  cartaoSus: z.string(),
  perfilDeSaude: z.string(),
  fatoresDeRisco: z.array(z.enum(FATORES_DE_RISCO)),
}) satisfies z.ZodType<Morador>;

export const ContatoSchema = z.object({
  id: z.string(),
  nome: z.string(),
  papel: z.enum(['FAMILIAR', 'VIZINHO']),
  telefone: z.string(),
}) satisfies z.ZodType<ContatoDaRede>;

export const PontoSchema = z.object({
  id: z.string(),
  nome: z.string(),
  tipo: z.string(),
  distanciaKm: z.number().nonnegative(),
  horario: z.object({ abre: z.string().regex(/^\d\d:\d\d$/), fecha: z.string().regex(/^\d\d:\d\d$/) }),
  acessivel: z.boolean(),
  fonte: z.string(),
  lat: z.number().optional(),
  lon: z.number().optional(),
}) satisfies z.ZodType<PontoDeResfriamento>;

export const PrivacidadeSchema = z.object({
  farol: z.enum(['VERDE', 'AMARELO', 'VERMELHO']),
  mensagens: z.array(z.object({ momento, mensagemCidadao: z.string(), fonte: z.enum(['IA', 'template']) })),
}) satisfies z.ZodType<PrivacidadeDaCasa>;

export const pagina = <T extends z.ZodType>(item: T) =>
  z.object({ itens: z.array(item), pagina: z.number(), tamanho: z.number(), total: z.number() });

export const LoginSchema = z.object({ token: z.string().min(1) });

const horario = z.string().regex(/^\d\d:\d\d$/);

export const RotinaSchema = z.object({
  checkInDiario: z.object({ ativo: z.boolean(), horario }),
  lembreteDeAgua: z.object({ ativo: z.boolean() }),
}) satisfies z.ZodType<DadosDaRotina>;

export const SentinelaVinculadaSchema = z.object({
  deviceId: z.string().regex(/^snt-[0-9a-f]{6}$/),
  segredo: z.string().regex(/^[0-9a-f]{64}$/),
}) satisfies z.ZodType<SentinelaVinculada>;
