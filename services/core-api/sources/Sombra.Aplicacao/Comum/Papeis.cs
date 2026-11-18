namespace Sombra.Aplicacao.Comum;

/// <summary>Os papéis de acesso do contrato da API (<c>docs/contratos/core-api.md</c>).</summary>
public static class Papeis
{
    /// <summary>Familiar ou pessoa da rede: só as casas que cadastrou ou às quais foi vinculada.</summary>
    public const string Cuidador = "Cuidador";

    /// <summary>Quem opera o painel: todas as casas, sempre com PII mascarada.</summary>
    public const string Operador = "Operador";

    /// <summary>Cadastros e operação.</summary>
    public const string Admin = "Admin";
}
