using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;

namespace Sombra.Aplicacao.Comum;

/// <summary>
/// Confere os atributos de validação de um DTO de entrada antes de o caso de uso tocar no domínio. Formato e presença
/// dão 400 aqui; regra de negócio fica com o domínio e dá 422.
/// </summary>
public static class Validacao
{
    /// <summary>Lança <see cref="ValidacaoException"/> com todos os erros do DTO, por campo.</summary>
    /// <param name="dto">O DTO.</param>
    /// <exception cref="ValidacaoException">Quando algum atributo falha.</exception>
    public static void Garantir(object dto) => Garantir(dto, string.Empty);

    /// <summary>Lança <see cref="ValidacaoException"/> com todos os erros do DTO, por campo, com um prefixo.</summary>
    /// <param name="dto">O DTO.</param>
    /// <param name="prefixo">O caminho do DTO dentro do corpo, ex.: <c>contatos[0]</c>.</param>
    /// <exception cref="ValidacaoException">Quando algum atributo falha.</exception>
    public static void Garantir(object dto, string prefixo)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(prefixo);
        List<ValidationResult> resultados = [];
        if (Validator.TryValidateObject(dto, new ValidationContext(dto), resultados, validateAllProperties: true))
        {
            return;
        }

        Dictionary<string, string[]> erros = resultados
            .SelectMany(r => r.MemberNames.DefaultIfEmpty(string.Empty).Select(campo => (Campo: NomeDoCampo(prefixo, campo), Mensagem: r.ErrorMessage ?? "Valor inválido.")))
            .GroupBy(e => e.Campo, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Mensagem).ToArray(), StringComparer.Ordinal);
        throw new ValidacaoException(erros);
    }

    private static string NomeDoCampo(string prefixo, string campo)
    {
        string nome = JsonNamingPolicy.CamelCase.ConvertName(campo);
        return prefixo.Length == 0 ? nome : $"{prefixo}.{nome}";
    }
}
