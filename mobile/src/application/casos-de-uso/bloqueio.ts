// Bloqueio por biometria na área da família, que mostra CPF mascarado, saúde e telefones da rede. É proteção de tela
// local (nada de biometria sai do aparelho). O Modo Idoso nunca é bloqueado: ele precisa abrir na hora.

import type { Clock } from '@/domain/compartilhado/clock';
import { falha, ok, type Result } from '@/domain/compartilhado/result';

import { bloqueioStore } from '../estado/bloqueio';
import type { PreferenciaRepository } from '../ports/repositorios';
import type { Biometria } from '../ports/servicos';

const PREFERENCIA = 'bloqueioBiometrico';
// Sair do app por menos que isso (ex.: a janela de permissão do sistema) não tranca.
export const TOLERANCIA_MS = 30_000;

export class ServicoDeBloqueio {
  private saiuEm: number | null = null;

  constructor(private readonly d: { preferencias: PreferenciaRepository; biometria: Biometria; clock: Clock }) {}

  async carregar() {
    const ativo = (await this.d.preferencias.ler(PREFERENCIA)) === 'sim';
    bloqueioStore.setState({ ativo, bloqueado: ativo });
  }

  async ativar(): Promise<Result<true>> {
    if (!(await this.d.biometria.disponivel())) {
      return falha('SEM_BIOMETRIA', 'Este celular não tem digital ou rosto cadastrado. Cadastre nos ajustes do aparelho.');
    }
    if (!(await this.d.biometria.autenticar('Confirme para ligar o bloqueio'))) return falha('CANCELADO', 'Bloqueio não ligado.');
    await this.d.preferencias.gravar(PREFERENCIA, 'sim');
    bloqueioStore.setState({ ativo: true, bloqueado: false });
    return ok(true);
  }

  async desativar() {
    await this.d.preferencias.gravar(PREFERENCIA, null);
    bloqueioStore.setState({ ativo: false, bloqueado: false });
  }

  foiParaSegundoPlano() {
    this.saiuEm = this.d.clock.now().getTime();
  }

  voltou() {
    const fora = this.saiuEm === null ? 0 : this.d.clock.now().getTime() - this.saiuEm;
    this.saiuEm = null;
    if (bloqueioStore.getState().ativo && fora >= TOLERANCIA_MS) bloqueioStore.setState({ bloqueado: true });
  }

  async desbloquear() {
    const liberou = await this.d.biometria.autenticar('Desbloqueie para ver as casas');
    if (liberou) bloqueioStore.setState({ bloqueado: false });
    return liberou;
  }
}
