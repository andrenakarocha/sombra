using System;
using System.Security.Cryptography;
using System.Text;

namespace Sombra.Dominio.Sentinelas;

/// <summary>
/// O código de 6 caracteres que a Sentinela mostra no QR de vínculo (<c>sombra://sentinela/vincular?v=1&amp;d=…&amp;c=…</c>,
/// formato do app). Ele sai do <c>deviceId</c> por HMAC-SHA256 com a chave de vínculo do servidor, então só quem tem a
/// Sentinela na mão vincula ela a uma casa. Vetores em <c>docs/contratos/vinculo-da-sentinela.vetores.json</c>.
/// </summary>
public static class CodigoDeVinculo
{
    /// <summary>Os caracteres do código, sem 0/O e 1/I, que se confundem (o mesmo alfabeto do app).</summary>
    public const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Quantos caracteres o código tem.</summary>
    public const int Tamanho = 6;

    /// <summary>Calcula o código de uma Sentinela: os 30 primeiros bits do HMAC, 5 por caractere.</summary>
    /// <param name="deviceId">O identificador público da Sentinela.</param>
    /// <param name="chave">A chave de vínculo do servidor.</param>
    /// <returns>O código, em maiúsculas.</returns>
    public static string Derivar(string deviceId, ReadOnlySpan<byte> chave)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        byte[] mac = HMACSHA256.HashData(chave, Encoding.UTF8.GetBytes(deviceId));
        ulong bits = ((ulong)mac[0] << 32) | ((ulong)mac[1] << 24) | ((ulong)mac[2] << 16) | ((ulong)mac[3] << 8) | mac[4];
        char[] codigo = new char[Tamanho];
        for (int i = 0; i < Tamanho; i++)
        {
            codigo[i] = Alfabeto[(int)((bits >> (35 - (5 * i))) & 31)];
        }

        return new string(codigo);
    }

    /// <summary>Confere o código digitado ou lido no QR, em tempo constante e sem diferenciar maiúsculas.</summary>
    /// <param name="deviceId">O identificador público da Sentinela.</param>
    /// <param name="codigo">O código informado.</param>
    /// <param name="chave">A chave de vínculo do servidor.</param>
    /// <returns><c>true</c> quando o código é o da Sentinela.</returns>
    public static bool Confere(string deviceId, string? codigo, ReadOnlySpan<byte> chave)
    {
        byte[] esperado = Encoding.ASCII.GetBytes(Derivar(deviceId, chave));
        byte[] informado = Encoding.ASCII.GetBytes((codigo ?? string.Empty).Trim().ToUpperInvariant());
        return CryptographicOperations.FixedTimeEquals(esperado, informado);
    }
}
