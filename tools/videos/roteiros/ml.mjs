// Vídeo de IA & Machine Learning (ml/README.md e ml/RELATORIO_ML.md), até 5 min.
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const F = (n) => `ml/figuras/${n}.png`;

export default {
  nome: 'ml',
  limite: 300,
  voz: 'pf_dora',

  async preparar(ctx) {
    await ctx.slide({
      titulo: 'O calor das próximas 24 horas',
      sub: 'IA & Machine Learning · dados reais do INMET, estação A701 (Mirante de Santana)',
      itens: ['Classificação com XGBoost', 'Validação temporal, sem vazamento', 'Comparação honesta com baselines', 'Exportado para ONNX'],
    });
    await ctx.painel.goto(pathToFileURL(join(ctx.raiz, 'ml/notebooks/sombra_calor.html')).href);
  },

  cenas: [
    {
      id: 'abertura',
      tela: 'palco',
      fala: 'O Sombra avisa a família de uma pessoa idosa que mora sozinha antes de uma onda de calor. Para isso, a parte de Machine Learning responde uma pergunta só: com o que a estação do INMET mediu nas últimas 24 horas, o índice de calor em São Paulo vai chegar a 32,22 graus nas próximas 24 horas?',
      async acao() {},
    },
    {
      id: 'dados',
      tela: 'palco',
      fala: 'Os dados são reais: as medições horárias da estação A701, de 2010 a 2025, mais de 140 mil horas, com cada arquivo conferido por SHA-256. Na limpeza, o código menos 9999 vira ausente, o horário passa para o de São Paulo, buracos de até três horas são interpolados e dias com mais de um quarto faltando são descartados.',
      async acao(ctx) {
        await ctx.figura(F('01_indice_diario'), 'Índice de calor diário, 2010 a 2025');
      },
    },
    {
      id: 'alvo',
      tela: 'palco',
      fala: 'O alvo é 1 quando o máximo das 24 horas seguintes chega a 32,22 graus, o nível de cautela extrema da NWS. O nível de perigo não era opção: o maior índice medido em dezesseis anos foi 35,47 graus. São 63 features, todas calculadas só com a hora atual e as anteriores, e só com o que o serviço de clima tem em produção.',
      async acao(ctx) {
        await ctx.figura(F('02_dias_de_calor'), 'Dias de calor por ano');
      },
    },
    {
      id: 'sem-vazamento',
      tela: 'painel',
      fala: 'O cuidado principal foi não enganar a nós mesmos. A divisão é temporal, sem embaralhar: treino de 2010 a 2021 e teste de 2022 a 2025, com uma folga de 24 horas entre eles. E testes automatizados provam que mudar o futuro não altera as features. Este é o notebook executado, do começo ao fim.',
      async acao(ctx) {
        const p = ctx.painel;
        await ctx.esperar(1);
        const alvo = p.getByText(/Divis[ãa]o temporal/i).first();
        const y = await alvo.evaluate((el) => el.getBoundingClientRect().top + window.scrollY).catch(() => 2500);
        await ctx.rolarPainel(Math.max(0, y - 120), 2500);
        await ctx.esperar(ctx.restante() * 0.5);
        await ctx.rolarPainel(Math.max(0, y + 500), 2500);
      },
    },
    {
      id: 'testes',
      tela: 'palco',
      acelerar: 3,
      rotulo: 'acelerado 3x',
      fala: 'Aqui, os testes da preparação, do treino e da exportação rodando, incluindo os que garantem que nenhuma informação do futuro entra no modelo.',
      async acao(ctx) {
        await ctx.terminal({ titulo: 'operador@sombra: ~/sombra/ml' });
        await ctx.rodar('make test', { cwd: join(ctx.raiz, 'ml'), timeout: 240 });
      },
    },
    {
      id: 'modelos',
      tela: 'palco',
      fala: 'Comparamos três abordagens: a persistência, que diz que amanhã será como hoje, a regressão logística e o XGBoost, escolhido por tratar relações não lineares, aceitar dados ausentes e exportar para ONNX. A busca testou doze combinações de três hiperparâmetros, com validação temporal em cinco dobras só no treino.',
      async acao(ctx) {
        await ctx.terminal({ titulo: 'operador@sombra: ~/sombra/ml' });
        await ctx.rodar('column -t -s, modelo/busca_hiperparametros.csv', { cwd: join(ctx.raiz, 'ml') });
      },
    },
    {
      id: 'resultados',
      tela: 'palco',
      fala: 'No teste, de 2022 a 2025, o XGBoost teve recall de 0,97 e PR-AUC de 0,59. E aqui a parte honesta: ele não superou a persistência de forma conclusiva. A diferença de PR-AUC é pequena, e o intervalo de confiança cruza o zero. O ganho claro apareceu ao prever o valor do pico: erro médio de 1,65 grau, contra 2,21 da persistência.',
      async acao(ctx) {
        await ctx.figura(F('05_curvas_pr_roc'), 'Curvas PR e ROC no teste');
        await ctx.esperar(ctx.restante() * 0.55);
        await ctx.figura(F('04_matrizes_de_confusao'), 'Matrizes de confusão');
      },
    },
    {
      id: 'overfitting',
      tela: 'palco',
      fala: 'O overfitting foi medido: a PR-AUC cai de 0,66 no treino para 0,29 na validação, e árvores mais profundas melhoram o treino sem melhorar a validação. Por isso a busca ficou com as árvores mais rasas.',
      async acao(ctx) {
        await ctx.figura(F('06_overfitting'), 'Treino × validação por profundidade');
      },
    },
    {
      id: 'replay',
      tela: 'palco',
      fala: 'Na prática, nos 75 dias de calor do teste, o modelo teria avisado 74 com até 24 horas de antecedência, ao custo de 7,8 dias com alarme falso por mês. Com um limiar menos sensível, avisa 72, com 4,9. Este é o replay da onda de calor de novembro de 2023, com a probabilidade subindo hora a hora.',
      async acao(ctx) {
        await ctx.figura(F('09_replay_nov_2023'), 'Replay: a onda de calor de novembro de 2023');
      },
    },
    {
      id: 'conclusao',
      tela: 'palco',
      fala: 'Para o cidadão, a leitura é simples: de cada dez dias de calor forte, a família seria avisada de quase todos, com um dia de antecedência. Os dois modelos foram exportados para ONNX, com paridade de vinte em vinte casos, para rodar dentro do serviço em Java. O notebook, o relatório e os dados estão na pasta ml. Integrantes: André, João e Matheus. Obrigado!',
      folga: 1.2,
      async acao(ctx) {
        await ctx.slide({
          titulo: '74 de 75 dias de calor avisados',
          sub: 'com até 24 h de antecedência, no teste de 2022 a 2025',
          itens: ['7,8 dias com alarme falso por mês (ou 4,9, com 72 avisados)', 'Persistência empata: um baseline honesto', 'ONNX com paridade 20/20', 'ml/: notebook, relatório e dados'],
        });
      },
    },
  ],
};
