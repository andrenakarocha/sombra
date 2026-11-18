// Lê o app.json e acrescenta o que depende de quem builda: o projeto EAS vem de EAS_PROJECT_ID (secret do CI ou
// variável local), para o repositório público não carregar o id da conta de ninguém.
module.exports = ({ config }) => ({
  ...config,
  extra: {
    ...config.extra,
    ...(process.env.EAS_PROJECT_ID ? { eas: { projectId: process.env.EAS_PROJECT_ID } } : {}),
  },
});
