// Avisa quem estiver ouvindo (a camada de apresentação invalida as consultas) que um recurso local mudou.

export type Recurso = 'casas' | 'casa' | 'alertas' | 'alerta' | 'privacidade' | 'sincronizacao';

export type Mudanca = { recurso: Recurso; id?: string };

export class Barramento {
  private readonly ouvintes = new Set<(m: Mudanca) => void>();

  assinar(ouvinte: (m: Mudanca) => void) {
    this.ouvintes.add(ouvinte);
    return () => {
      this.ouvintes.delete(ouvinte);
    };
  }

  publicar(...mudancas: Mudanca[]) {
    for (const m of mudancas) for (const ouvinte of this.ouvintes) ouvinte(m);
  }
}
