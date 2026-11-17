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

export function formatarKm(km: number): string {
  return km < 1 ? `${Math.round(km * 1000)} m` : `${km.toFixed(1).replace('.', ',')} km`;
}

// "HH:MM" comparável como texto com os horários dos pontos de resfriamento ("09:00").
export function abertoAgora(abre: string, fecha: string, agora = new Date()): boolean {
  const hhmm = hora.format(agora);
  return abre <= hhmm && hhmm < fecha;
}
