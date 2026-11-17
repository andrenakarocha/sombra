import { falha, ok, type Result } from '../compartilhado/result';

const digitos = (texto: string) => texto.replace(/\D/g, '');

function digitoVerificador(base: string): number {
  const soma = [...base].reduce((total, d, i) => total + Number(d) * (base.length + 1 - i), 0);
  return ((soma * 10) % 11) % 10;
}

// O valor inteiro existe só aqui dentro; para fora, `mascarado` (formato do contrato core-api.md).
export class Cpf {
  private constructor(private readonly numero: string) {}

  static criar(texto: string): Result<Cpf> {
    const d = digitos(texto);
    if (d.length !== 11) return falha('CPF_TAMANHO', 'O CPF tem 11 números.');
    if (/^(\d)\1{10}$/.test(d)) return falha('CPF_REPETIDO', 'O CPF não pode ter todos os números iguais.');
    const base = d.slice(0, 9);
    const primeiro = digitoVerificador(base);
    const segundo = digitoVerificador(base + primeiro);
    if (d.slice(9) !== `${primeiro}${segundo}`) return falha('CPF_DIGITO', 'O CPF não é válido. Confira os números.');
    return ok(new Cpf(d));
  }

  get mascarado() {
    return `***.${this.numero.slice(3, 6)}.${this.numero.slice(6, 9)}-**`;
  }

  // Só para enviar ao Core, que cifra e indexa (spec 07). Nunca para log nem tela.
  paraEnvio() {
    return this.numero;
  }
}
