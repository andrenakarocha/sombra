using Sombra.Aplicacao.Comum;

namespace Sombra.Api.Controllers;

/// <summary>As combinações de papéis usadas nos atributos <c>[Authorize]</c>.</summary>
internal static class Acesso
{
    /// <summary>Quem enxerga casas e alertas (o Cuidador, só os dele).</summary>
    internal const string Todos = Papeis.Cuidador + "," + Papeis.Operador + "," + Papeis.Admin;

    /// <summary>Quem cadastra e altera.</summary>
    internal const string CuidadorOuAdmin = Papeis.Cuidador + "," + Papeis.Admin;

    /// <summary>Quem vê o painel da operação.</summary>
    internal const string OperadorOuAdmin = Papeis.Operador + "," + Papeis.Admin;
}
