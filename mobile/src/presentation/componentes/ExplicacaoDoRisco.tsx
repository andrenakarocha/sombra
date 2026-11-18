import { View } from 'react-native';

import { INFO_DO_NIVEL, LIMIARES_C } from '@/domain/clima/nivel-de-risco';
import type { RiscoPersonalizado } from '@/domain/risco/risco-personalizado';

import { espaco, raio, useTema } from '../tema';
import { TOKEN_DO_NIVEL } from '../tema/risco';
import { formatarGraus } from '../util/formato';

import { Texto } from './Texto';

const ROTULO_DO_FATOR = {
  IDADE_AVANCADA: '80 anos ou mais',
  CARDIOPATIA: 'Doença do coração',
  USO_DE_DIURETICO: 'Toma diurético',
} as const;

// R3 explicado para a família: onde o perigo começa para esta pessoa, quanto cada fator pesou e quanto falta agora.
export function ExplicacaoDoRisco({ nome, risco, indiceC }: { nome: string; risco: RiscoPersonalizado; indiceC: number | null }) {
  const tema = useTema();
  const perigo = risco.limiares.PERIGO;
  const avaliacao = indiceC === null ? null : risco.avaliar(indiceC);
  const cores = avaliacao ? tema.risco[TOKEN_DO_NIVEL[avaliacao.personalizado]] : null;

  return (
    <View style={{ backgroundColor: tema.superficie, borderRadius: raio.grande, padding: espaco[5], gap: espaco[3] }}>
      <Texto variante="subtitulo" accessibilityRole="header">
        Por que {nome} está em risco
      </Texto>

      <Texto accessibilityLabel={`Para ${nome}, o perigo começa em ${formatarGraus(perigo)} graus. O padrão é ${formatarGraus(LIMIARES_C.PERIGO)}.`}>
        Para {nome}, o perigo começa em <Texto forte>{formatarGraus(perigo)} °C</Texto> de índice de calor (o padrão da NWS é{' '}
        {formatarGraus(LIMIARES_C.PERIGO)} °C).
      </Texto>

      {risco.contribuicoes.length === 0 ? (
        <Texto suave>Sem fator de risco cadastrado: vale a régua padrão.</Texto>
      ) : (
        <View accessibilityRole="list" style={{ gap: espaco[3] }}>
          {risco.contribuicoes.map((c) => (
            <View key={c.fator} accessible accessibilityLabel={`${ROTULO_DO_FATOR[c.fator]}: o perigo começa ${formatarGraus(c.reducaoC)} graus antes. ${c.porque}`}>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco[2] }}>
                {/* A barra é proporcional à redução; o número está ao lado (nada só pela forma). */}
                <View
                  style={{
                    height: 10,
                    width: `${Math.min(100, (c.reducaoC / risco.antecipacaoDoPerigoC || 0) * 100)}%`,
                    maxWidth: '60%',
                    minWidth: 12,
                    borderRadius: raio.pilula,
                    backgroundColor: tema.destaque,
                  }}
                />
                <Texto forte>−{formatarGraus(c.reducaoC)} °C</Texto>
              </View>
              <Texto forte>{ROTULO_DO_FATOR[c.fator]}</Texto>
              <Texto suave>{c.porque}</Texto>
            </View>
          ))}
          {risco.limiares.CAUTELA_EXTREMA <= LIMIARES_C.ATENCAO && (
            <Texto variante="pequeno" suave>
              O limite nunca fica abaixo de {formatarGraus(LIMIARES_C.ATENCAO)} °C, o começo de &quot;Atenção&quot;.
            </Texto>
          )}
        </View>
      )}

      {avaliacao && cores && (
        <View style={{ borderLeftWidth: 4, borderLeftColor: cores.borda, paddingLeft: espaco[3], gap: espaco[1] }}>
          <Texto forte>
            Agora: {formatarGraus(indiceC!)} °C · {INFO_DO_NIVEL[avaliacao.personalizado].rotulo} para {nome}
          </Texto>
          <Texto suave>
            {avaliacao.folgaAtePerigoC > 0
              ? `Faltam ${formatarGraus(avaliacao.folgaAtePerigoC)} °C para o perigo.`
              : `Já está no perigo para ${nome}.`}
            {avaliacao.padrao !== avaliacao.personalizado && ` Pela régua padrão seria "${INFO_DO_NIVEL[avaliacao.padrao].rotulo}".`}
          </Texto>
        </View>
      )}
    </View>
  );
}
