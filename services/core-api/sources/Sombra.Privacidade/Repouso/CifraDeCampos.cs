using System;
using System.Security.Cryptography;
using System.Text;

namespace Sombra.Privacidade.Repouso;

/// <summary>
/// Criptografia de campos em repouso (spec 07 §1): AES-256-GCM com nonce aleatório por valor e o nome da coluna como
/// dado autenticado (um cifrado não pode ser trocado de coluna sem falhar), mais o índice cego HMAC-SHA256, com outra
/// chave, para achar e deduplicar CPF sem decifrar. As duas chaves vêm do ambiente, nunca do código.
/// </summary>
/// <remarks>Formato gravado: <c>v1:</c> + base64(nonce[12] ‖ etiqueta[16] ‖ cifrado).</remarks>
public sealed class CifraDeCampos
{
    /// <summary>O prefixo de todo valor cifrado, com a versão do formato.</summary>
    public const string Prefixo = "v1:";

    private const int TamanhoDaChave = 32;
    private const int TamanhoDoNonce = 12;
    private const int TamanhoDaEtiqueta = 16;

    private readonly byte[] _chaveAes;
    private readonly byte[] _chaveDoIndice;

    /// <summary>Initializes a new instance of the <see cref="CifraDeCampos"/> class.</summary>
    /// <param name="chaveAes">32 bytes para o AES-256-GCM.</param>
    /// <param name="chaveDoIndiceCego">32 bytes, diferentes, para o HMAC do índice cego.</param>
    public CifraDeCampos(byte[] chaveAes, byte[] chaveDoIndiceCego)
    {
        ArgumentNullException.ThrowIfNull(chaveAes);
        ArgumentNullException.ThrowIfNull(chaveDoIndiceCego);
        if (chaveAes.Length != TamanhoDaChave || chaveDoIndiceCego.Length != TamanhoDaChave)
        {
            throw new ArgumentException("as chaves têm 32 bytes cada");
        }

        if (CryptographicOperations.FixedTimeEquals(chaveAes, chaveDoIndiceCego))
        {
            throw new ArgumentException("a chave do índice cego precisa ser diferente da chave AES");
        }

        _chaveAes = [.. chaveAes];
        _chaveDoIndice = [.. chaveDoIndiceCego];
        Impressao = Convert.ToHexStringLower(SHA256.HashData([.. _chaveAes, .. _chaveDoIndice]))[..16];
    }

    /// <summary>
    /// Gets uma impressão do par de chaves (16 hex de um SHA-256): distingue configurações sem revelar a chave. O EF usa
    /// para guardar um modelo por par de chaves.
    /// </summary>
    public string Impressao { get; }

    /// <summary>Monta a cifra a partir da configuração (<c>Privacidade:ChaveAes</c> e <c>Privacidade:ChaveIndiceCego</c>).</summary>
    /// <param name="chaveAes">A chave AES em base64.</param>
    /// <param name="chaveDoIndiceCego">A chave do índice cego em base64.</param>
    /// <returns>A cifra.</returns>
    /// <exception cref="InvalidOperationException">Quando falta uma chave ou ela não tem 32 bytes.</exception>
    public static CifraDeCampos DeBase64(string? chaveAes, string? chaveDoIndiceCego)
        => new(Ler(chaveAes, "Privacidade:ChaveAes"), Ler(chaveDoIndiceCego, "Privacidade:ChaveIndiceCego"));

    /// <summary>Cifra um valor para uma coluna.</summary>
    /// <param name="texto">O valor em claro.</param>
    /// <param name="coluna">A coluna (<c>moradores.cpf</c>), autenticada junto.</param>
    /// <returns>O valor cifrado, no formato <c>v1:</c>.</returns>
    public string Cifrar(string texto, string coluna)
    {
        ArgumentNullException.ThrowIfNull(texto);
        ArgumentException.ThrowIfNullOrWhiteSpace(coluna);
        byte[] claro = Encoding.UTF8.GetBytes(texto);
        byte[] saida = new byte[TamanhoDoNonce + TamanhoDaEtiqueta + claro.Length];
        Span<byte> nonce = saida.AsSpan(0, TamanhoDoNonce);
        RandomNumberGenerator.Fill(nonce);
        using AesGcm aes = new(_chaveAes, TamanhoDaEtiqueta);
        aes.Encrypt(nonce, claro, saida.AsSpan(TamanhoDoNonce + TamanhoDaEtiqueta), saida.AsSpan(TamanhoDoNonce, TamanhoDaEtiqueta), Encoding.UTF8.GetBytes(coluna));
        return Prefixo + Convert.ToBase64String(saida);
    }

    /// <summary>Decifra um valor de uma coluna.</summary>
    /// <param name="cifrado">O valor gravado.</param>
    /// <param name="coluna">A coluna de onde ele veio.</param>
    /// <returns>O valor em claro.</returns>
    /// <exception cref="CryptographicException">Quando o valor não está cifrado, foi adulterado, veio de outra coluna ou de outra chave.</exception>
    public string Decifrar(string cifrado, string coluna)
    {
        ArgumentNullException.ThrowIfNull(cifrado);
        ArgumentException.ThrowIfNullOrWhiteSpace(coluna);
        if (!cifrado.StartsWith(Prefixo, StringComparison.Ordinal))
        {
            throw new CryptographicException($"{coluna}: valor sem cifra");
        }

        byte[] bruto;
        try
        {
            bruto = Convert.FromBase64String(cifrado[Prefixo.Length..]);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException($"{coluna}: cifra malformada", ex);
        }

        if (bruto.Length < TamanhoDoNonce + TamanhoDaEtiqueta)
        {
            throw new CryptographicException($"{coluna}: cifra curta demais");
        }

        byte[] claro = new byte[bruto.Length - TamanhoDoNonce - TamanhoDaEtiqueta];
        using AesGcm aes = new(_chaveAes, TamanhoDaEtiqueta);
        aes.Decrypt(bruto.AsSpan(0, TamanhoDoNonce), bruto.AsSpan(TamanhoDoNonce + TamanhoDaEtiqueta), bruto.AsSpan(TamanhoDoNonce, TamanhoDaEtiqueta), claro, Encoding.UTF8.GetBytes(coluna));
        return Encoding.UTF8.GetString(claro);
    }

    /// <summary>Diz se um valor gravado já está no formato cifrado.</summary>
    /// <param name="valor">O valor da coluna.</param>
    /// <returns><c>true</c> quando começa com <see cref="Prefixo"/>.</returns>
    public static bool EstaCifrado(string? valor) => valor?.StartsWith(Prefixo, StringComparison.Ordinal) == true;

    /// <summary>O índice cego: o mesmo valor gera sempre o mesmo índice, e o índice não revela o valor sem a chave.</summary>
    /// <param name="valor">O valor normalizado (os 11 dígitos do CPF).</param>
    /// <param name="coluna">A coluna, para o mesmo valor não ter o mesmo índice em colunas diferentes.</param>
    /// <returns>64 caracteres hexadecimais.</returns>
    public string IndiceCego(string valor, string coluna)
    {
        ArgumentNullException.ThrowIfNull(valor);
        ArgumentException.ThrowIfNullOrWhiteSpace(coluna);
        return Convert.ToHexStringLower(HMACSHA256.HashData(_chaveDoIndice, Encoding.UTF8.GetBytes($"{coluna}\n{valor}")));
    }

    private static byte[] Ler(string? base64, string nome)
    {
        try
        {
            byte[] chave = string.IsNullOrWhiteSpace(base64) ? [] : Convert.FromBase64String(base64);
            if (chave.Length == TamanhoDaChave)
            {
                return chave;
            }
        }
        catch (FormatException)
        {
        }

        throw new InvalidOperationException($"Defina {nome} com 32 bytes em base64 (rode make env).");
    }
}
