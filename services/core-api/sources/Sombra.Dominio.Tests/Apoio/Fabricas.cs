using System;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Pessoas;
using Sombra.Dominio.Saude;
using Sombra.Dominio.Sentinelas;

namespace Sombra.Dominio.Tests.Apoio;

/// <summary>Monta objetos de domínio com dados fictícios (a persona "Dona Lurdes" e a rede dela).</summary>
internal static class Fabricas
{
    /// <summary>A data de referência dos testes.</summary>
    internal static readonly DateOnly Hoje = new(2026, 11, 18);

    /// <summary>Cria um morador com a idade e os fatores pedidos.</summary>
    /// <param name="idade">A idade em anos completos na data <see cref="Hoje"/>.</param>
    /// <param name="semente">Semente do CPF sintético.</param>
    /// <param name="fatores">Os fatores declarados.</param>
    /// <returns>O morador.</returns>
    internal static Morador Morador(int idade, int semente = 1, params FatorDeRisco[] fatores)
        => global::Sombra.Dominio.Casas.Morador.Cadastrar(
            "Lurdes A.",
            Hoje.AddYears(-idade),
            Cpf.Criar(DocumentosSinteticos.Cpf(semente)),
            null,
            PerfilDeSaude.Com(fatores),
            Hoje);

    /// <summary>Cria uma casa em Santana, região SP-MIRANTE, sem moradores.</summary>
    /// <returns>A casa.</returns>
    internal static Domicilio Casa()
        => Domicilio.Cadastrar(
            "Casa da Dona Lurdes",
            Endereco.Criar("Rua das Flores", "12", "Santana", "São Paulo", "SP", "02012-000"),
            "SP-MIRANTE");

    /// <summary>Uma leitura da Sentinela de uma casa com uma moradora de 70 anos (limites padrão).</summary>
    /// <param name="temperatura">A temperatura, em °C.</param>
    /// <param name="umidade">A umidade relativa, em %.</param>
    /// <param name="momento">O instante da leitura.</param>
    /// <returns>A leitura classificada.</returns>
    internal static Leitura Leitura(double temperatura, double umidade, DateTimeOffset momento)
    {
        Domicilio casa = Casa();
        casa.AdicionarMorador(Morador(70));
        casa.VincularSentinela("snt-7f3a9c");
        return casa.RegistrarLeitura("snt-7f3a9c", momento, Temperatura.Criar(temperatura), UmidadeRelativa.Criar(umidade));
    }

    /// <summary>A filha (familiar) e a vizinha, nessa ordem.</summary>
    /// <returns>Os dois contatos.</returns>
    internal static ContatoDeCuidado[] FilhaEVizinha()
        =>
        [
            new("Ana", Telefone.Criar("11987654321"), PapelDoContato.Familiar),
            new("Cida (vizinha)", Telefone.Criar("11912345678"), PapelDoContato.Vizinho),
        ];
}
