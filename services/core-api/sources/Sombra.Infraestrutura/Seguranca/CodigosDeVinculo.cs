using System;
using Microsoft.Extensions.Configuration;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Sentinelas;

namespace Sombra.Infraestrutura.Seguranca;

/// <summary>
/// Confere o código do QR da Sentinela com a chave de vínculo do servidor (<c>Sentinela__ChaveDeVinculo</c>: 32 bytes em
/// hexadecimal, gerada pelo <c>make env</c> e nunca versionada).
/// </summary>
public sealed class CodigosDeVinculo : ICodigosDeVinculo
{
    private readonly byte[] _chave;

    /// <summary>Initializes a new instance of the <see cref="CodigosDeVinculo"/> class.</summary>
    /// <param name="configuracao">A configuração (<c>Sentinela:ChaveDeVinculo</c>).</param>
    /// <exception cref="InvalidOperationException">Sem a chave, ou com uma chave curta.</exception>
    public CodigosDeVinculo(IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        const string ajuda = "Defina Sentinela__ChaveDeVinculo (32 bytes em hexadecimal): rode make env.";
        string hex = configuracao["Sentinela:ChaveDeVinculo"] ?? string.Empty;
        if (hex.Length < 64)
        {
            throw new InvalidOperationException(ajuda);
        }

        try
        {
            _chave = Convert.FromHexString(hex);
        }
        catch (FormatException erro)
        {
            throw new InvalidOperationException(ajuda, erro);
        }
    }

    /// <inheritdoc/>
    public bool Confere(string deviceId, string codigo) => CodigoDeVinculo.Confere(deviceId, codigo, _chave);
}
