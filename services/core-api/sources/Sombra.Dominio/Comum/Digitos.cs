using System.Linq;

namespace Sombra.Dominio.Comum;

/// <summary>Operações sobre os dígitos de documentos e números digitados com pontuação.</summary>
internal static class Digitos
{
    /// <summary>Devolve só os dígitos ASCII do texto, na ordem.</summary>
    /// <param name="texto">O texto digitado, com ou sem pontuação.</param>
    /// <returns>Os dígitos, ou vazio quando não há nenhum.</returns>
    internal static string Extrair(string? texto)
        => texto is null ? string.Empty : new string(texto.Where(char.IsAsciiDigit).ToArray());

    /// <summary>Converte o caractere de um dígito ASCII no seu valor.</summary>
    /// <param name="digito">Um caractere entre '0' e '9'.</param>
    /// <returns>O valor de 0 a 9.</returns>
    internal static int Valor(char digito) => digito - '0';
}
