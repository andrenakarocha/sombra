import { useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { Pressable, ScrollView, Switch, View } from 'react-native';

import type { DadosDaRotina } from '@/application/modelos';
import { HORARIOS_DA_AGUA, Rotina } from '@/domain/rotina/rotina';
import { Botao } from '@/presentation/componentes/Botao';
import { Texto } from '@/presentation/componentes/Texto';
import { useContainer } from '@/presentation/providers/container';
import { espaco, raio, toque, useTema } from '@/presentation/tema';
import { mensagemDeErro } from '@/presentation/util/mensagens';

// Só horários de dia: entre 22h e 7h o Sombra não manda lembrete (R12).
const HORARIOS = Array.from({ length: 15 }, (_, i) => `${String(7 + i).padStart(2, '0')}:00`);

export default function RotinaDaCasa() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const tema = useTema();
  const { casaESentinela } = useContainer();
  const [dados, setDados] = useState<DadosDaRotina>(Rotina.padrao().dados);
  const [mensagem, setMensagem] = useState<string | null>(null);

  useEffect(() => {
    casaESentinela
      .rotina(id)
      .then((r) => setDados(r.dados))
      .catch((e) => setMensagem(mensagemDeErro(e)));
  }, [casaESentinela, id]);

  async function salvar() {
    setMensagem(null);
    try {
      const r = await casaESentinela.salvarRotina(id, dados);
      setMensagem(r.ok ? 'Rotina salva. O celular do Modo Idoso passa a avisar nestes horários.' : r.erro.mensagem);
    } catch (e) {
      setMensagem(mensagemDeErro(e));
    }
  }

  return (
    <ScrollView contentContainerStyle={{ padding: espaco[5], gap: espaco[4] }}>
      <Linha
        titulo='"Estou bem" todo dia'
        explicacao="Um lembrete por dia para apertar ESTOU BEM. Se ela esquecer, a família recebe só um lembrete leve, nunca um alarme."
        valor={dados.checkInDiario.ativo}
        aoMudar={(ativo) => setDados((d) => ({ ...d, checkInDiario: { ...d.checkInDiario, ativo } }))}
      />
      {dados.checkInDiario.ativo && (
        <View accessibilityRole="radiogroup" accessibilityLabel="Horário do lembrete" style={{ flexDirection: 'row', flexWrap: 'wrap', gap: espaco[2] }}>
          {HORARIOS.map((h) => {
            const escolhido = dados.checkInDiario.horario === h;
            return (
              <Pressable
                key={h}
                accessibilityRole="radio"
                accessibilityState={{ checked: escolhido }}
                accessibilityLabel={`${Number(h.slice(0, 2))} horas`}
                accessibilityHint="Escolhe o horário do check-in diário"
                onPress={() => setDados((d) => ({ ...d, checkInDiario: { ...d.checkInDiario, horario: h } }))}
                style={{
                  minHeight: toque.minimo,
                  minWidth: 72,
                  alignItems: 'center',
                  justifyContent: 'center',
                  borderRadius: raio.pilula,
                  borderWidth: 2,
                  borderColor: tema.marca,
                  backgroundColor: escolhido ? tema.marca : 'transparent',
                }}
              >
                <Texto forte style={{ color: escolhido ? tema.textoSobreMarca : tema.marca }}>
                  {h}
                </Texto>
              </Pressable>
            );
          })}
        </View>
      )}

      <Linha
        titulo="Lembrete de água"
        explicacao={`Nos dias de calor, às ${HORARIOS_DA_AGUA.join(' e às ')}, a hora mais quente. No máximo dois por dia.`}
        valor={dados.lembreteDeAgua.ativo}
        aoMudar={(ativo) => setDados((d) => ({ ...d, lembreteDeAgua: { ativo } }))}
      />

      <Texto suave>Entre 22h e 7h o Sombra não manda lembrete. Alerta de perigo toca a qualquer hora.</Texto>
      {mensagem && <Texto accessibilityRole="alert">{mensagem}</Texto>}
      <Botao rotulo="Salvar a rotina" dica="Envia os horários ao Sombra e à Sentinela" aoTocar={salvar} />
    </ScrollView>
  );
}

function Linha({ titulo, explicacao, valor, aoMudar }: { titulo: string; explicacao: string; valor: boolean; aoMudar: (v: boolean) => void }) {
  const tema = useTema();
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco[3], minHeight: toque.minimo }}>
      <View style={{ flex: 1, gap: espaco[1] }}>
        <Texto forte>{titulo}</Texto>
        <Texto variante="pequeno" suave>
          {explicacao}
        </Texto>
      </View>
      <Switch accessibilityLabel={titulo} accessibilityHint={explicacao} value={valor} onValueChange={aoMudar} trackColor={{ true: tema.marca, false: tema.borda }} />
    </View>
  );
}
