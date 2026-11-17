import { falha, ok, type Result } from '../compartilhado/result';

// CNS: 15 dígitos; a soma ponderada (pesos 15 a 1) é múltipla de 11, tanto no definitivo (1, 2) quanto no
// provisório (7, 8, 9).
export class CartaoSus {
  private constructor(private readonly numero: string) {}

  static criar(texto: string): Result<CartaoSus> {
    const d = texto.replace(/\D/g, '');
    if (d.length !== 15) return falha('CNS_TAMANHO', 'O Cartão SUS tem 15 números.');
    if (!'12789'.includes(d[0]!)) return falha('CNS_INICIO', 'O Cartão SUS começa com 1, 2, 7, 8 ou 9.');
    const soma = [...d].reduce((total, c, i) => total + Number(c) * (15 - i), 0);
    if (soma % 11 !== 0) return falha('CNS_DIGITO', 'O Cartão SUS não é válido. Confira os números.');
    return ok(new CartaoSus(d));
  }

  get mascarado() {
    return `*** **** **** ${this.numero.slice(-4)}`;
  }

  paraEnvio() {
    return this.numero;
  }
}
