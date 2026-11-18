// F6.26, o contrato vivo (D17): as amostras de docs/contratos/amostras/ são respostas reais do Core, gravadas e conferidas
// pelo teste de integração dele. Aqui elas passam pelos mesmos schemas Zod que o app usa em cada resposta.
import fs from 'node:fs';
import path from 'node:path';

import type { z } from 'zod';

import {
  AlertaSchema,
  ContatoSchema,
  DomicilioSchema,
  LeituraSchema,
  LoginSchema,
  MoradorSchema,
  pagina,
  PontoSchema,
  PrivacidadeSchema,
  RotinaSchema,
  SentinelaVinculadaSchema,
} from '@/infrastructure/http/schemas';

const PASTA = path.resolve(__dirname, '../../../docs/contratos/amostras');

// Toda amostra precisa de uma decisão: o schema que lê, ou null quando o app não lê o corpo daquela resposta.
const SCHEMAS: Record<string, z.ZodType | null> = {
  login: LoginSchema,
  domicilios: pagina(DomicilioSchema),
  domicilio: DomicilioSchema,
  leituras: pagina(LeituraSchema),
  moradores: MoradorSchema.array(),
  'rede-de-cuidado': ContatoSchema.array(),
  alertas: pagina(AlertaSchema),
  alerta: AlertaSchema,
  'alerta-reconhecido': AlertaSchema,
  rotina: RotinaSchema,
  'rotina-salva': RotinaSchema,
  'pontos-de-resfriamento': PontoSchema.array(),
  privacidade: PrivacidadeSchema,
  'morador-criado': MoradorSchema,
  'morador-editado': MoradorSchema,
  'sentinela-vinculada': SentinelaVinculadaSchema,
  // A fila offline só quer saber se o check-in foi aceito; o corpo não entra no app.
  'check-in': null,
};

const amostras = fs
  .readdirSync(PASTA)
  .filter((arquivo) => arquivo.endsWith('.json'))
  .map((arquivo) => arquivo.replace(/\.json$/, ''))
  .sort();

describe('contrato vivo com o Core: as respostas reais passam nos schemas do app', () => {
  it('toda amostra do Core tem um schema decidido aqui, e vice-versa', () => {
    expect(amostras).toEqual(Object.keys(SCHEMAS).sort());
  });

  it.each(amostras.filter((nome) => SCHEMAS[nome]))('%s', (nome) => {
    const corpo = JSON.parse(fs.readFileSync(path.join(PASTA, `${nome}.json`), 'utf8'));
    const resultado = SCHEMAS[nome]!.safeParse(corpo);
    const problemas = resultado.success ? [] : resultado.error.issues.map((i) => `${i.path.join('.') || '(raiz)'}: ${i.code}`);
    expect(problemas).toEqual([]);
  });
});
