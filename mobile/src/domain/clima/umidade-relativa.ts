import { falha, ok, type Result } from '../compartilhado/result';

export class UmidadeRelativa {
  private constructor(readonly percentual: number) {}

  static de(valor: number): Result<UmidadeRelativa> {
    if (!Number.isFinite(valor) || valor < 0 || valor > 100) {
      return falha('UMIDADE_FORA_DA_FAIXA', 'A umidade relativa vai de 0 a 100%.');
    }
    return ok(new UmidadeRelativa(valor));
  }
}
