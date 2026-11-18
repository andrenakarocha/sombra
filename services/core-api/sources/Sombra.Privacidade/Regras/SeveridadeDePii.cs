namespace Sombra.Privacidade.Regras;

/// <summary>O quanto o vazamento de um dado pesa. A ordem dos valores serve para resolver sobreposições.</summary>
public enum SeveridadeDePii
{
    /// <summary>Sozinho, quase não identifica ninguém.</summary>
    Baixa = 1,

    /// <summary>Permite contatar ou reconhecer a pessoa.</summary>
    Media = 2,

    /// <summary>Identifica a pessoa ou expõe a saúde dela.</summary>
    Alta = 3,

    /// <summary>Dá acesso ao sistema: o pior caso.</summary>
    Critica = 4,
}
