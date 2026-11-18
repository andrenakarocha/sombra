using System.Collections.Generic;

namespace Sombra.Privacidade.Regras;

/// <summary>Uma regra do <c>pii-regras.json</c>: o que procurar, onde e como proteger.</summary>
/// <param name="Id">O identificador estável da regra (<c>cpf</c>, <c>telefone</c>...).</param>
/// <param name="Categoria">A família do dado.</param>
/// <param name="Severidade">O peso do vazamento.</param>
/// <param name="Chaves">Os nomes de campo que já denunciam o dado (<c>cpf</c>, <c>laudo</c>, <c>senha</c>).</param>
/// <param name="Padrao">A expressão regular que reconhece o dado no conteúdo, ou <c>null</c> para só usar chaves e termos.</param>
/// <param name="Validador">O nome do validador que confirma um candidato (<c>cpf</c>, <c>cns</c>...), ou <c>nenhum</c>.</param>
/// <param name="Termos">O dicionário de termos (já em minúsculas ou não: o motor ignora caixa e acentos).</param>
/// <param name="Mascara">O formato da máscara aplicada ao dado (<c>cpf</c>, <c>saude</c>, <c>removido</c>...).</param>
/// <param name="BaseLegal">O artigo da LGPD que justifica a proteção.</param>
public sealed record RegraDePii(
    string Id,
    CategoriaDePii Categoria,
    SeveridadeDePii Severidade,
    IReadOnlyList<string> Chaves,
    string? Padrao,
    string Validador,
    IReadOnlyList<string> Termos,
    string Mascara,
    string BaseLegal);
