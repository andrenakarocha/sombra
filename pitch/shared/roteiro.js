// Roteiro do pitch do Sombra: fonte única de narração, ordem e tempo das cenas (o mesmo formato do pitch HENRY).
// `tools/sync.mjs` lê este arquivo, mede os áudios em audio/ e gera shared/timing.js + ROTEIRO.md.
// Campos: id, ato, sec (rótulo no HUD), min (duração mínima em s), tail (respiro depois da fala, opcional), texto.
// Todos os números saem do repositório (ml/RELATORIO_ML.md, security/auditoria, os READMEs); as pessoas são fictícias,
// e as falas da entrevista estão em docs/entrevista.md, sem nomes.
(function (root) {
  const ROTEIRO = [
    // ---------------- ATO 1 · o problema ----------------
    { id: "s01", ato: "ato1", sec: "São Paulo · novembro de 2023", min: 13.5,
      texto: "Novembro de 2023. São Paulo atravessa uma onda de calor. Numa casa que pega sol a tarde inteira, a Dona Lurdes, de setenta e oito anos, mora sozinha. E esquece de beber água." },
    { id: "s02", ato: "ato1", sec: "01 · Escutar", min: 18.5,
      texto: "Fomos conversar com quem vive isso. Uma senhora que mora sozinha contou: lá pelas três, quatro horas, parece que a casa virou um forno. Um dia, ela passou mal na cozinha, e foi a vizinha quem percebeu. A filha pediu uma coisa só: não quero receber alerta à toa." },
    { id: "s03", ato: "ato1", sec: "01 · O problema", min: 12.5,
      texto: "Em ondas de calor, idosos que moram sozinhos não percebem o perigo dentro de casa. E a família só fica sabendo quando já é tarde. Por isso criamos o Sombra." },

    // ---------------- ATO 2 · a solução ----------------
    { id: "s04", ato: "ato2", sec: "02 · A Sentinela", min: 15.5,
      texto: "Na casa, a Sentinela: um ESP32 que mede o calor de dentro e calcula o índice de calor ali mesmo. A luz é fixa e nunca pisca. E são dois botões grandes: estou bem, e preciso de ajuda." },
    { id: "s05", ato: "ato2", sec: "02 · Prever antes", min: 14,
      texto: "Antes de o calor chegar, um modelo treinado com dezesseis anos de dados reais do INMET prevê as próximas vinte e quatro horas. No teste, avisou setenta e quatro de setenta e cinco dias de calor forte." },
    { id: "s06", ato: "ato2", sec: "02 · Um degrau por vez", min: 15,
      texto: "Quando o perigo chega, ela só precisa apertar o verde. Se não aperta, o Sombra sobe um degrau por vez: um lembrete para ela, depois a família, depois quem está perto. Alarme de verdade, só em perigo." },
    { id: "s07", ato: "ato2", sec: "02 · A rede de cuidado", min: 13,
      texto: "A filha acompanha a casa pelo app, ao vivo: o calor de agora, o prazo para responder e um botão para ligar. E, no celular da Dona Lurdes, o Modo Idoso: letra gigante e um botão só." },

    // ---------------- ATO 3 · por dentro ----------------
    { id: "s08", ato: "ato3", sec: "03 · Arquitetura", min: 17.5,
      texto: "Por dentro, um Core em C sharp orquestra os alertas e a rede de cuidado. Um serviço de clima em Java, com Spring, é dono dos dados do INMET. A Sentinela fala MQTT, com cada mensagem assinada. E o app e o painel recebem tudo ao vivo." },
    { id: "s09", ato: "ato3", sec: "03 · Privacidade", min: 19,
      texto: "Os dados de saúde passam por um motor de privacidade: ele acha CPF, Cartão SUS e laudo, segue o dado até a saída e bloqueia o vazamento. Os oito ataques que vazavam com ele desligado são todos barrados. E a IA explica cada evento ao cidadão, sem nunca ver o dado." },
    { id: "s10", ato: "ato3", sec: "03 · Infraestrutura", min: 11,
      texto: "E tudo sobe numa máquina Linux vazia, só com scripts: rede, DNS, serviços e um monitor que se recupera sozinho das falhas." },

    // ---------------- ATO 4 · as entregas ----------------
    { id: "s11", ato: "ato4", sec: "04 · O que entregamos", min: 17.5,
      texto: "Em números: mais de quinhentos testes automatizados, sessenta e três casos de dado pessoal sem nenhum falso negativo, cinquenta casos de teste, quarenta e nove deles automatizados, e sete testes de falha na máquina, cada um com a sua prova." },

    // ---------------- ATO 5 · fechamento ----------------
    { id: "s12", ato: "ato5", sec: "Sombra", min: 12.5, tail: 4,
      texto: "Sombra. Porque, numa onda de calor, ninguém deveria enfrentar o calor sozinho. André, João e Matheus." },
  ];

  // Ritmo de planejamento quando ainda não há áudio gravado (palavras por segundo).
  const RITMO = { wps: 2.45, lead: 0.6, tail: 1.0 };

  if (typeof module !== "undefined" && module.exports) module.exports = { ROTEIRO, RITMO };
  else { root.ROTEIRO = ROTEIRO; root.RITMO = RITMO; }
})(typeof window !== "undefined" ? window : globalThis);
