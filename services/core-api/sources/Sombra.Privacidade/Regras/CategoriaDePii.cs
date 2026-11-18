namespace Sombra.Privacidade.Regras;

/// <summary>A família de um dado pessoal, que define o peso dele no risco do cidadão (spec 07).</summary>
public enum CategoriaDePii
{
    /// <summary>Documentos que identificam a pessoa: CPF, RG.</summary>
    Identificacao,

    /// <summary>Dado pessoal sensível de saúde (LGPD, art. 11): Cartão SUS, CID-10, laudos, remédios.</summary>
    Saude,

    /// <summary>Formas de falar com a pessoa: telefone e e-mail.</summary>
    Contato,

    /// <summary>Segredos de acesso: senhas, tokens e chaves.</summary>
    Credencial,

    /// <summary>Onde a pessoa mora: CEP e endereço.</summary>
    Localizacao,
}
