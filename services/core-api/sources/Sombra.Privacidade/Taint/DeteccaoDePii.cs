using Sombra.Privacidade.Deteccao;
using Sombra.Privacidade.Regras;

namespace Sombra.Privacidade.Taint;

/// <summary>Um dado pessoal achado na entrada, só com metadados: é o que vira o evento <c>privacidade.pii_detectada</c>.</summary>
/// <param name="Fonte">Corpo, query ou header.</param>
/// <param name="Campo">O caminho do campo (<c>$.perfilDeSaude</c>, <c>query.senha</c>); um nome de campo com PII vira <c>[PII]</c>.</param>
/// <param name="RegraId">A regra que disparou.</param>
/// <param name="Categoria">A família do dado.</param>
/// <param name="Severidade">O peso do vazamento.</param>
/// <param name="Origem">Pela chave ou pelo conteúdo.</param>
/// <param name="Desfecho">O que o módulo fez.</param>
public sealed record DeteccaoDePii(
    FonteDePii Fonte,
    string Campo,
    string RegraId,
    CategoriaDePii Categoria,
    SeveridadeDePii Severidade,
    OrigemDaDeteccao Origem,
    DesfechoDaDeteccao Desfecho);
