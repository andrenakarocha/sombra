using System;
using System.Collections.Generic;
using System.Linq;
using Sombra.Dominio.Comum;
using Sombra.Dominio.Pessoas;

namespace Sombra.Dominio.Casas;

/// <summary>O papel de um contato no escalonamento em degraus (D13).</summary>
public enum PapelDoContato
{
    /// <summary>Cuida à distância (ex.: a filha): recebe o lembrete leve (degrau 1).</summary>
    Familiar = 0,

    /// <summary>Mora perto (ex.: a vizinha): recebe o "pode dar uma olhada?" (degrau 2).</summary>
    Vizinho = 1,
}

/// <summary>Uma pessoa da rede de cuidado. Nome e telefone são dados pessoais.</summary>
/// <param name="Nome">O nome ou apelido do contato.</param>
/// <param name="Telefone">O telefone com DDD.</param>
/// <param name="Papel">O papel no escalonamento.</param>
public sealed record ContatoDeCuidado(string Nome, Telefone Telefone, PapelDoContato Papel)
{
    /// <summary>Nunca revela quem é o contato.</summary>
    /// <returns>O papel, sem dado pessoal.</returns>
    public override string ToString() => $"Contato {Papel}";
}

/// <summary>Os contatos de cuidado de um domicílio, na ordem em que o escalonamento os procura.</summary>
public sealed class RedeDeCuidado
{
    /// <summary>O máximo de contatos numa rede.</summary>
    public const int MaximoDeContatos = 5;

    private readonly List<ContatoDeCuidado> _contatos = [];

    /// <summary>Gets os contatos, na ordem de escalonamento.</summary>
    public IReadOnlyList<ContatoDeCuidado> Contatos => _contatos;

    /// <summary>Gets o primeiro familiar, que recebe o lembrete leve (degrau 1).</summary>
    public ContatoDeCuidado? PrimeiroFamiliar => _contatos.FirstOrDefault(c => c.Papel == PapelDoContato.Familiar);

    /// <summary>Gets o primeiro vizinho, que recebe o "quem está perto" (degrau 2).</summary>
    public ContatoDeCuidado? QuemEstaPerto => _contatos.FirstOrDefault(c => c.Papel == PapelDoContato.Vizinho);

    /// <summary>Substitui a rede inteira, na ordem informada.</summary>
    /// <param name="contatos">Os contatos, do primeiro ao último.</param>
    /// <exception cref="RegraVioladaException">Com mais de cinco contatos, nome vazio ou telefone repetido.</exception>
    public void Definir(IEnumerable<ContatoDeCuidado> contatos)
    {
        ArgumentNullException.ThrowIfNull(contatos);
        ContatoDeCuidado[] novos = [.. contatos];
        if (novos.Length > MaximoDeContatos)
        {
            throw new RegraVioladaException("R8", "A rede de cuidado tem no máximo cinco contatos.");
        }

        if (novos.Any(c => string.IsNullOrWhiteSpace(c.Nome)))
        {
            throw new RegraVioladaException("R8", "Todo contato de cuidado precisa de nome.");
        }

        if (novos.Select(c => c.Telefone.Valor).Distinct().Count() != novos.Length)
        {
            throw new RegraVioladaException("R8", "Dois contatos com o mesmo telefone.");
        }

        _contatos.Clear();
        _contatos.AddRange(novos);
    }
}
