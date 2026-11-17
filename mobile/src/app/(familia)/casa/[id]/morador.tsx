import { useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, View } from 'react-native';

import { useMoradores, useSalvarMorador } from '@/api/consultas';
import { mensagemDeErro } from '@/api/mensagens';
import type { FatorDeRisco, Morador, NovoMorador } from '@/api/tipos';
import { Botao } from '@/componentes/Botao';
import { Campo } from '@/componentes/Campo';
import { Texto } from '@/componentes/Texto';
import { type Formulario, VAZIO, validar } from '@/dominio/morador';
import { espaco, raio, toque, useTema } from '@/tema';
import {
  aplicarPadrao,
  exibirCartaoSus,
  exibirCpf,
  exibirSaude,
  exibirTelefone,
  PADRAO,
} from '@/util/mascaras';

const FATORES: { fator: FatorDeRisco; rotulo: string }[] = [
  { fator: 'IDADE_AVANCADA', rotulo: '80 anos ou mais' },
  { fator: 'CARDIOPATIA', rotulo: 'Doença do coração' },
  { fator: 'USO_DE_DIURETICO', rotulo: 'Toma diurético' },
];

function corpo(f: Formulario): NovoMorador {
  const { idade, ...resto } = f;
  // Campos vazios não vão: na edição, o Core mantém o que já tem.
  const preenchidos = Object.fromEntries(Object.entries(resto).filter(([, v]) => v !== '')) as Omit<NovoMorador, 'idade'>;
  return { ...preenchidos, idade: Number(idade) };
}

export default function MoradorDaCasa() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const moradores = useMoradores(id);
  const [editando, setEditando] = useState<Morador | null>(null);
  const [form, setForm] = useState<Formulario>(VAZIO);
  const [erro, setErro] = useState<string | null>(null);
  const salvar = useSalvarMorador(id, editando?.id);

  const mudar = (campo: keyof Formulario) => (valor: string) => setForm((f) => ({ ...f, [campo]: valor }));

  function editar(m: Morador) {
    setEditando(m);
    setForm({ ...VAZIO, nome: m.nome, idade: String(m.idade), fatoresDeRisco: m.fatoresDeRisco as FatorDeRisco[] });
  }

  function alternar(fator: FatorDeRisco) {
    setForm((f) => ({
      ...f,
      fatoresDeRisco: f.fatoresDeRisco.includes(fator)
        ? f.fatoresDeRisco.filter((x) => x !== fator)
        : [...f.fatoresDeRisco, fator],
    }));
  }

  function aoSalvar() {
    const problema = validar(form, !!editando);
    setErro(problema);
    if (problema) return;
    salvar.mutate(corpo(form), {
      onSuccess: () => {
        setEditando(null);
        setForm(VAZIO);
      },
    });
  }

  return (
    <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView contentContainerStyle={{ padding: espaco[5], gap: espaco[4] }} keyboardShouldPersistTaps="handled">
        {moradores.isError && <Texto accessibilityRole="alert">{mensagemDeErro(moradores.error)}</Texto>}
        {moradores.data?.map((m) => (
          <CartaoMorador key={m.id} morador={m} aoEditar={() => editar(m)} />
        ))}

        <Texto variante="subtitulo" accessibilityRole="header" style={{ marginTop: espaco[4] }}>
          {editando ? `Editar ${editando.nome}` : 'Cadastrar morador'}
        </Texto>
        <Campo rotulo="Nome" value={form.nome} onChangeText={mudar('nome')} autoComplete="off" />
        <Campo rotulo="Idade" value={form.idade} onChangeText={mudar('idade')} keyboardType="number-pad" />
        <Campo
          rotulo={editando ? 'CPF (vazio mantém o atual)' : 'CPF'}
          value={form.cpf}
          onChangeText={(v) => mudar('cpf')(aplicarPadrao(v, PADRAO.cpf))}
          keyboardType="number-pad"
          autoComplete="off"
        />
        <Campo
          rotulo="Cartão SUS"
          value={form.cartaoSus}
          onChangeText={(v) => mudar('cartaoSus')(aplicarPadrao(v, PADRAO.cartaoSus))}
          keyboardType="number-pad"
          autoComplete="off"
        />
        <Campo
          rotulo="Telefone do morador"
          value={form.telefone}
          onChangeText={(v) => mudar('telefone')(aplicarPadrao(v, PADRAO.telefone))}
          keyboardType="phone-pad"
        />
        <Campo
          rotulo="Saúde (fica protegido)"
          value={form.perfilDeSaude}
          onChangeText={mudar('perfilDeSaude')}
          multiline
          autoComplete="off"
        />
        <View accessibilityRole="list" style={{ gap: espaco[2] }}>
          <Texto forte>Fatores de risco no calor</Texto>
          {FATORES.map(({ fator, rotulo }) => (
            <Marcador key={fator} rotulo={rotulo} marcado={form.fatoresDeRisco.includes(fator)} aoTocar={() => alternar(fator)} />
          ))}
        </View>

        {erro && <Texto accessibilityRole="alert">{erro}</Texto>}
        {salvar.isError && <Texto accessibilityRole="alert">{mensagemDeErro(salvar.error)}</Texto>}
        {salvar.isSuccess && <Texto accessibilityRole="alert">Salvo. Os documentos aparecem mascarados.</Texto>}
        <Botao rotulo={salvar.isPending ? 'Salvando…' : 'Salvar'} aoTocar={aoSalvar} />
        {editando && (
          <Botao
            rotulo="Cancelar edição"
            variante="secundario"
            aoTocar={() => {
              setEditando(null);
              setForm(VAZIO);
            }}
          />
        )}
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

function CartaoMorador({ morador: m, aoEditar }: { morador: Morador; aoEditar: () => void }) {
  const tema = useTema();
  return (
    <View style={{ backgroundColor: tema.superficie, borderRadius: raio.medio, padding: espaco[4], gap: espaco[1] }}>
      <Texto forte variante="subtitulo">
        {m.nome}, {m.idade} anos
      </Texto>
      <Texto accessibilityLabel={`CPF mascarado: ${exibirCpf(m.cpf)}`}>CPF {exibirCpf(m.cpf)}</Texto>
      <Texto>Cartão SUS {exibirCartaoSus(m.cartaoSus)}</Texto>
      {m.telefone && <Texto>Telefone {exibirTelefone(m.telefone)}</Texto>}
      <Texto suave>{exibirSaude(m.perfilDeSaude)}</Texto>
      <Botao rotulo="Editar" variante="secundario" dica={`Edita os dados de ${m.nome}`} aoTocar={aoEditar} />
    </View>
  );
}

function Marcador({ rotulo, marcado, aoTocar }: { rotulo: string; marcado: boolean; aoTocar: () => void }) {
  const tema = useTema();
  return (
    <Pressable
      accessibilityRole="checkbox"
      accessibilityState={{ checked: marcado }}
      accessibilityLabel={rotulo}
      onPress={aoTocar}
      style={{ minHeight: toque.minimo, flexDirection: 'row', alignItems: 'center', gap: espaco[3] }}
    >
      <View
        style={{
          width: 28,
          height: 28,
          borderRadius: raio.pequeno,
          borderWidth: 2,
          borderColor: tema.marca,
          backgroundColor: marcado ? tema.marca : 'transparent',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        {marcado && <Texto style={{ color: tema.textoSobreMarca, lineHeight: 22 }}>✓</Texto>}
      </View>
      <Texto>{rotulo}</Texto>
    </Pressable>
  );
}
