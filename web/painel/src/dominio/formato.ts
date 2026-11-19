/** Datas e números como o operador lê: pt-BR, no fuso de São Paulo. */
const FUSO = 'America/Sao_Paulo';

const dataHora = new Intl.DateTimeFormat('pt-BR', { timeZone: FUSO, day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
const hora = new Intl.DateTimeFormat('pt-BR', { timeZone: FUSO, hour: '2-digit', minute: '2-digit', second: '2-digit' });
const relativo = new Intl.RelativeTimeFormat('pt-BR', { numeric: 'auto' });

export function formatarDataHora(iso: string | null | undefined): string {
  return iso ? dataHora.format(new Date(iso)) : '—';
}

export function formatarHora(instante: number | string): string {
  return hora.format(new Date(instante));
}

/** "há 5 minutos", "em 2 horas", com o relógio injetável para os testes. */
export function haQuanto(iso: string | null | undefined, agora: number = Date.now()): string {
  if (!iso) return '—';
  const segundos = Math.round((new Date(iso).getTime() - agora) / 1000);
  const abs = Math.abs(segundos);
  if (abs < 60) return relativo.format(segundos, 'second');
  if (abs < 3600) return relativo.format(Math.round(segundos / 60), 'minute');
  if (abs < 86_400) return relativo.format(Math.round(segundos / 3600), 'hour');
  return relativo.format(Math.round(segundos / 86_400), 'day');
}

export function formatarNumero(valor: number | null | undefined, casas = 0): string {
  return valor === null || valor === undefined
    ? '—'
    : valor.toLocaleString('pt-BR', { minimumFractionDigits: casas, maximumFractionDigits: casas });
}

export function formatarGraus(valor: number | null | undefined): string {
  return valor === null || valor === undefined ? '—' : `${formatarNumero(valor, 1)} °C`;
}
