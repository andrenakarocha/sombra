// O Core visto pelo app (padrão BFF: o app só fala com o Core). Duas implementações: HTTP de verdade, com Zod
// validando cada resposta, e a demonstração em memória.

import type {
  Alerta,
  ContatoDaRede,
  Domicilio,
  Leitura,
  Morador,
  NovoMorador,
  PontoDeResfriamento,
  PrivacidadeDaCasa,
} from '../modelos';
import type { EnviadorDeOperacoes } from './outbox';

export interface CoreGateway extends EnviadorDeOperacoes {
  entrar(email: string, senha: string): Promise<{ token: string }>;
  listarCasas(): Promise<Domicilio[]>;
  buscarCasa(id: string): Promise<Domicilio>;
  leituras(id: string, de: Date, ate: Date): Promise<Leitura[]>;
  moradores(id: string): Promise<Morador[]>;
  redeDeCuidado(id: string): Promise<ContatoDaRede[]>;
  listarAlertas(): Promise<Alerta[]>;
  buscarAlerta(id: string): Promise<Alerta>;
  pontosDeResfriamento(id: string): Promise<PontoDeResfriamento[]>;
  privacidade(id: string): Promise<PrivacidadeDaCasa>;
  salvarMorador(domicilioId: string, moradorId: string | undefined, dados: Partial<NovoMorador>): Promise<Morador>;
}
