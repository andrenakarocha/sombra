import type { z } from 'zod';

import { RespostaForaDoContrato } from '@/application/erros';
import type { DadosDaRotina, NovoMorador } from '@/application/modelos';
import type { CoreGateway } from '@/application/ports/core-gateway';
import type { Operacao } from '@/application/ports/outbox';

import type { ClienteHttp, Opcoes } from './cliente-http';
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
} from './schemas';

export class CoreHttpGateway implements CoreGateway {
  constructor(private readonly http: ClienteHttp) {}

  private async pedir<S extends z.ZodType>(schema: S, caminho: string, opcoes?: Opcoes): Promise<z.infer<S>> {
    const resposta = schema.safeParse(await this.http.requisitar(caminho, opcoes));
    if (!resposta.success) {
      // Só o caminho do campo e o problema: o valor pode ser PII e nunca vai para a mensagem.
      const detalhe = resposta.error.issues.map((i) => `${i.path.join('.') || '(raiz)'}: ${i.code}`).join('; ');
      throw new RespostaForaDoContrato(caminho.split('?')[0]!, detalhe);
    }
    return resposta.data;
  }

  entrar(email: string, senha: string) {
    return this.pedir(LoginSchema, '/auth/login', { metodo: 'POST', corpo: { email, senha } });
  }

  async listarCasas() {
    return (await this.pedir(pagina(DomicilioSchema), '/domicilios?tamanho=100')).itens;
  }

  buscarCasa(id: string) {
    return this.pedir(DomicilioSchema, `/domicilios/${encodeURIComponent(id)}`);
  }

  // A API devolve da mais recente para a mais antiga; o app guarda em qualquer ordem e ordena na leitura.
  async leituras(id: string, de: Date, ate: Date) {
    const q = `de=${encodeURIComponent(de.toISOString())}&ate=${encodeURIComponent(ate.toISOString())}&tamanho=100`;
    return (await this.pedir(pagina(LeituraSchema), `/domicilios/${encodeURIComponent(id)}/leituras?${q}`)).itens;
  }

  moradores(id: string) {
    return this.pedir(MoradorSchema.array(), `/domicilios/${encodeURIComponent(id)}/moradores`);
  }

  redeDeCuidado(id: string) {
    return this.pedir(ContatoSchema.array(), `/domicilios/${encodeURIComponent(id)}/rede-de-cuidado`);
  }

  async listarAlertas() {
    return (await this.pedir(pagina(AlertaSchema), '/alertas?tamanho=100')).itens;
  }

  buscarAlerta(id: string) {
    return this.pedir(AlertaSchema, `/alertas/${encodeURIComponent(id)}`);
  }

  pontosDeResfriamento(id: string) {
    return this.pedir(PontoSchema.array(), `/domicilios/${encodeURIComponent(id)}/pontos-de-resfriamento?raioKm=3`);
  }

  privacidade(id: string) {
    return this.pedir(PrivacidadeSchema, `/domicilios/${encodeURIComponent(id)}/privacidade`);
  }

  salvarMorador(domicilioId: string, moradorId: string | undefined, dados: Partial<NovoMorador>) {
    const base = `/domicilios/${encodeURIComponent(domicilioId)}/moradores`;
    return this.pedir(MoradorSchema, moradorId ? `${base}/${encodeURIComponent(moradorId)}` : base, {
      metodo: moradorId ? 'PUT' : 'POST',
      corpo: dados,
    });
  }

  rotina(id: string) {
    return this.pedir(RotinaSchema, `/domicilios/${encodeURIComponent(id)}/rotina`);
  }

  salvarRotina(id: string, rotina: DadosDaRotina) {
    return this.pedir(RotinaSchema, `/domicilios/${encodeURIComponent(id)}/rotina`, { metodo: 'PUT', corpo: rotina });
  }

  vincularSentinela(id: string, deviceId: string, codigo: string) {
    return this.pedir(SentinelaVinculadaSchema, `/domicilios/${encodeURIComponent(id)}/sentinelas`, {
      metodo: 'POST',
      corpo: { deviceId, codigoDeVinculo: codigo },
    });
  }

  // O operationId vai como Idempotency-Key: reenviar depois de uma resposta perdida não duplica nada no Core.
  async enviar(op: Operacao) {
    const idempotencyKey = op.operationId;
    if (op.tipo === 'CHECK_IN') {
      const { domicilioId, situacao, momento } = op.payload;
      await this.http.requisitar(`/domicilios/${encodeURIComponent(domicilioId)}/check-ins`, {
        metodo: 'POST',
        corpo: { origem: 'APP', situacao, momento },
        idempotencyKey,
      });
      return;
    }
    const { alertaId, acao } = op.payload;
    await this.http.requisitar(`/alertas/${encodeURIComponent(alertaId)}/reconhecer`, { metodo: 'POST', corpo: { acao }, idempotencyKey });
  }
}
