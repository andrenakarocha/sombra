// A API fala em UTC; a tela mostra o horário de São Paulo (docs/contratos/core-api.md).

const hora = new Intl.DateTimeFormat('pt-BR', {
  hour: '2-digit',
  minute: '2-digit',
  timeZone: 'America/Sao_Paulo',
});

export function formatarHora(iso: string): string {
  return hora.format(new Date(iso));
}

export function formatarGraus(valor: number): string {
  return valor.toFixed(1).replace('.', ',');
}
