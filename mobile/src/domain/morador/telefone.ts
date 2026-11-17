import { falha, ok, type Result } from '../compartilhado/result';

// Telefone brasileiro com DDD: fixo (10 dígitos) ou celular (11, começando com 9).
export class Telefone {
  private constructor(private readonly numero: string) {}

  static criar(texto: string): Result<Telefone> {
    const d = texto.replace(/\D/g, '');
    if (d.length !== 10 && d.length !== 11) return falha('TELEFONE_TAMANHO', 'Confira o telefone com DDD.');
    const ddd = Number(d.slice(0, 2));
    if (ddd < 11 || ddd > 99 || d[1] === '0') return falha('TELEFONE_DDD', 'O DDD não existe.');
    if (d.length === 11 && d[2] !== '9') return falha('TELEFONE_CELULAR', 'Celular com 11 números começa com 9 depois do DDD.');
    return ok(new Telefone(d));
  }

  get mascarado() {
    return `(${this.numero.slice(0, 2)}) *****-${this.numero.slice(-4)}`;
  }

  // Para o botão LIGAR do Cuidador da própria casa.
  get discavel() {
    return this.numero;
  }

  paraEnvio() {
    return this.numero;
  }
}
