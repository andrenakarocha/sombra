import { CasaNaoEncontrada } from '@/application/casos-de-uso/consultas';
import { ErroDaApi, RespostaForaDoContrato, SemConexao } from '@/application/erros';

// Texto para a pessoa, com o código curto do erro quando houver (docs/observabilidade.md §3).
export function mensagemDeErro(erro: unknown, porStatus: Record<number, string> = {}): string {
  if (erro instanceof SemConexao) return 'Não foi possível falar com o Sombra. Confira a internet e tente de novo.';
  if (erro instanceof ErroDaApi) {
    return `${porStatus[erro.status] ?? erro.titulo} (código do erro: ${erro.codigo})`;
  }
  if (erro instanceof RespostaForaDoContrato) return 'O servidor respondeu de um jeito inesperado. Tente de novo mais tarde.';
  if (erro instanceof CasaNaoEncontrada) return erro.message;
  return 'Algo deu errado. Tente de novo.';
}
