// Rotina e vínculo da Sentinela: escritas que exigem conexão (o segredo da Sentinela não pode esperar numa fila,
// e a rotina é a fonte para os lembretes de todos os celulares da casa).

import type { Clock } from '@/domain/compartilhado/clock';
import { falha, ok, type Result } from '@/domain/compartilhado/result';
import type { NivelDeRisco } from '@/domain/clima/nivel-de-risco';
import { Rotina } from '@/domain/rotina/rotina';
import { segredoValido, type Vinculo } from '@/domain/sentinela/qr-de-vinculo';

import type { Barramento } from '../barramento';
import type { DadosDaRotina } from '../modelos';
import type { CoreGateway } from '../ports/core-gateway';
import type { CasaRepository } from '../ports/repositorios';
import type { AgendadorDeLembretes, CofreDeSegredos } from '../ports/servicos';

export class CasaESentinela {
  constructor(
    private readonly d: {
      gateway: CoreGateway;
      casas: CasaRepository;
      segredos: CofreDeSegredos;
      agendador: AgendadorDeLembretes;
      barramento: Barramento;
      clock: Clock;
    },
  ) {}

  async rotina(domicilioId: string): Promise<Rotina> {
    const r = Rotina.criar(await this.d.gateway.rotina(domicilioId));
    return r.ok ? r.valor : Rotina.padrao();
  }

  async salvarRotina(domicilioId: string, dados: DadosDaRotina): Promise<Result<Rotina>> {
    const r = Rotina.criar(dados);
    if (!r.ok) return r;
    await this.d.gateway.salvarRotina(domicilioId, r.valor.dados);
    return r;
  }

  // No celular do Modo Idoso: os lembretes de hoje (água só em dia de calor), sempre reprogramados do zero.
  async programarLembretes(domicilioId: string, nivelDoDia: NivelDeRisco) {
    const rotina = await this.rotina(domicilioId).catch(() => Rotina.padrao());
    return this.d.agendador.programar(rotina.lembretesDoDia(nivelDoDia), this.d.clock.now());
  }

  async vincularSentinela(domicilioId: string, vinculo: Vinculo): Promise<Result<{ deviceId: string; segredo: string }>> {
    const resposta = await this.d.gateway.vincularSentinela(domicilioId, vinculo.deviceId, vinculo.codigo);
    if (resposta.deviceId !== vinculo.deviceId || !segredoValido(resposta.segredo)) {
      return falha('VINCULO', 'O Sombra respondeu de um jeito inesperado. Tente de novo.');
    }
    await this.d.segredos.guardar(resposta.deviceId, resposta.segredo);
    await this.d.casas.definirSentinelaOnline(domicilioId, resposta.deviceId, false);
    this.d.barramento.publicar({ recurso: 'casas' }, { recurso: 'casa', id: domicilioId });
    return ok(resposta);
  }

  segredoDa(deviceId: string) {
    return this.d.segredos.ler(deviceId);
  }
}
