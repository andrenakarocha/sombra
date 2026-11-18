using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Sombra.Infraestrutura.Autenticacao;

/// <summary>
/// PBKDF2-HMAC-SHA512 com 100 mil iterações e sal aleatório de 16 bytes (o padrão do ASP.NET Core Identity). O texto
/// guardado leva o algoritmo e as iterações, para dar para subir o custo depois sem quebrar as contas antigas.
/// </summary>
internal static class HashDeSenha
{
    private const string Algoritmo = "pbkdf2-sha512";
    private const int Iteracoes = 100_000;
    private const int TamanhoDoSal = 16;
    private const int TamanhoDoHash = 32;

    /// <summary>Gets um hash de uma senha aleatória, para gastar o mesmo tempo quando a conta não existe.</summary>
    internal static string Ficticio { get; } = Gerar(Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)));

    /// <summary>Gera o hash de uma senha.</summary>
    /// <param name="senha">A senha.</param>
    /// <returns>O texto <c>pbkdf2-sha512$iteracoes$sal$hash</c>.</returns>
    internal static string Gerar(string senha)
    {
        ArgumentException.ThrowIfNullOrEmpty(senha);
        byte[] sal = RandomNumberGenerator.GetBytes(TamanhoDoSal);
        byte[] hash = Derivar(senha, sal, Iteracoes);
        return string.Create(CultureInfo.InvariantCulture, $"{Algoritmo}${Iteracoes}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}");
    }

    /// <summary>Confere uma senha contra um hash guardado, em tempo constante.</summary>
    /// <param name="senha">A senha digitada.</param>
    /// <param name="guardado">O hash guardado.</param>
    /// <returns><c>true</c> quando confere.</returns>
    internal static bool Conferir(string senha, string guardado)
    {
        string[] partes = (guardado ?? string.Empty).Split('$');
        if (string.IsNullOrEmpty(senha) || partes.Length != 4 || partes[0] != Algoritmo
            || !int.TryParse(partes[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iteracoes))
        {
            return false;
        }

        byte[] esperado = Convert.FromBase64String(partes[3]);
        return CryptographicOperations.FixedTimeEquals(Derivar(senha, Convert.FromBase64String(partes[2]), iteracoes), esperado);
    }

    private static byte[] Derivar(string senha, byte[] sal, int iteracoes)
        => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(senha), sal, iteracoes, HashAlgorithmName.SHA512, TamanhoDoHash);
}
