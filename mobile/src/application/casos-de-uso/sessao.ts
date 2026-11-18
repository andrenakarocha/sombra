// Entrar, sair e a demonstração. Sair apaga os dados de casa do aparelho; a sessão vencida (401) só tira o token,
// para os check-ins que estão na fila saírem quando a pessoa entrar de novo.

import { ehEscalaDoTexto, preferenciasStore, type EscalaDoTexto } from '../estado/preferencias';
import { sessaoStore, TOKEN_DEMO } from '../estado/sessao';
import type { CoreGateway } from '../ports/core-gateway';
import type { PreferenciaRepository } from '../ports/repositorios';
import type { CofreDeSessao } from '../ports/servicos';

const MODO_IDOSO = 'modoIdosoCasaId';
const ESCALA_DO_TEXTO = 'escalaDoTexto';

export type DepsDaSessao = {
  gateway: CoreGateway;
  cofre: CofreDeSessao;
  preferencias: PreferenciaRepository;
  apagarDadosLocais: () => Promise<void>;
};

export class ServicoDeSessao {
  constructor(private readonly d: DepsDaSessao) {}

  async carregar() {
    const [token, modoIdosoCasaId, escala] = await Promise.all([
      this.d.cofre.ler(),
      this.d.preferencias.ler(MODO_IDOSO),
      this.d.preferencias.ler(ESCALA_DO_TEXTO),
    ]);
    preferenciasStore.setState({ modoIdosoCasaId, escalaDoTexto: ehEscalaDoTexto(escala) ? escala : 'NORMAL', carregada: true });
    sessaoStore.setState({ token, carregada: true });
  }

  async entrar(email: string, senha: string) {
    const { token } = await this.d.gateway.entrar(email.trim(), senha);
    await this.d.cofre.salvar(token);
    sessaoStore.setState({ token });
  }

  async entrarNaDemonstracao() {
    await this.d.cofre.salvar(TOKEN_DEMO);
    sessaoStore.setState({ token: TOKEN_DEMO });
  }

  async sair() {
    await this.d.cofre.apagar();
    await this.d.apagarDadosLocais();
    await this.definirModoIdoso(null);
    sessaoStore.setState({ token: null });
  }

  async sessaoVencida() {
    await this.d.cofre.apagar();
    sessaoStore.setState({ token: null });
  }

  async definirModoIdoso(casaId: string | null) {
    await this.d.preferencias.gravar(MODO_IDOSO, casaId);
    preferenciasStore.setState({ modoIdosoCasaId: casaId });
  }

  // É do aparelho, não da conta: sair mantém o tamanho escolhido.
  async definirEscalaDoTexto(escala: EscalaDoTexto) {
    await this.d.preferencias.gravar(ESCALA_DO_TEXTO, escala);
    preferenciasStore.setState({ escalaDoTexto: escala });
  }
}
