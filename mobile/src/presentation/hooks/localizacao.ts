import { useEffect, useState } from 'react';

import type { Coordenadas } from '@/application/ports/servicos';

import { useContainer } from '../providers/container';

// Posição uma vez ao abrir a tela e a bússola enquanto ela estiver aberta.
export function useLocalizacao() {
  const { localizacao } = useContainer();
  const [posicao, setPosicao] = useState<Coordenadas | null>(null);
  const [direcao, setDirecao] = useState<number | null>(null);

  useEffect(() => {
    let ativo = true;
    let parar = () => {};
    localizacao.atual().then((p) => ativo && setPosicao(p)).catch(() => {});
    localizacao
      .observarDirecao((g) => ativo && setDirecao(g))
      .then((p) => (ativo ? (parar = p) : p()))
      .catch(() => {});
    return () => {
      ativo = false;
      parar();
    };
  }, [localizacao]);

  return { posicao, direcao };
}
