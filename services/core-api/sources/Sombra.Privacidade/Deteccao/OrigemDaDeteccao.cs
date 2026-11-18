namespace Sombra.Privacidade.Deteccao;

/// <summary>Como o motor reconheceu o dado.</summary>
public enum OrigemDaDeteccao
{
    /// <summary>Pelo nome do campo (<c>cpf</c>, <c>laudo</c>, <c>senha</c>): o valor inteiro é protegido.</summary>
    Chave,

    /// <summary>Pelo conteúdo (padrão, validador ou dicionário), em qualquer campo, inclusive texto livre.</summary>
    Conteudo,
}
