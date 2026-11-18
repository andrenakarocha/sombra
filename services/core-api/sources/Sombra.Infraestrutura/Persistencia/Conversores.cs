using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Pessoas;
using Sombra.Dominio.Saude;

namespace Sombra.Infraestrutura.Persistencia;

/// <summary>
/// Os conversores dos value objects de um valor só: o banco guarda o valor, o domínio recebe o objeto já validado. A
/// F2.3 troca os de PII por conversores que cifram.
/// </summary>
internal static class Conversores
{
    /// <summary>CPF ↔ os 11 dígitos.</summary>
    internal static readonly ValueConverter<Cpf, string> CpfEmTexto = new(c => c.Valor, s => Cpf.Criar(s));

    /// <summary>Cartão SUS ↔ os 15 dígitos. É opcional, mas o EF nunca passa nulo para um conversor.</summary>
    internal static readonly ValueConverter<CartaoSus?, string> CartaoSusEmTexto = new(c => c!.Valor, s => CartaoSus.Criar(s));

    /// <summary>Telefone do morador ↔ os dígitos, com o DDD. É opcional, como o Cartão SUS.</summary>
    internal static readonly ValueConverter<Telefone?, string> TelefoneEmTexto = new(t => t!.Valor, s => Telefone.Criar(s));

    /// <summary>Temperatura ↔ °C.</summary>
    internal static readonly ValueConverter<Temperatura, double> TemperaturaEmCelsius = new(t => t.Celsius, c => Temperatura.Criar(c));

    /// <summary>Umidade relativa ↔ %.</summary>
    internal static readonly ValueConverter<UmidadeRelativa, double> UmidadeEmPercentual = new(u => u.Percentual, p => UmidadeRelativa.Criar(p));

    /// <summary>Perfil de saúde ↔ os códigos dos fatores, em ordem e separados por ponto e vírgula.</summary>
    internal static readonly ValueConverter<PerfilDeSaude, string> PerfilEmCodigos = new(p => CodigosDe(p), s => PerfilDe(s));

    /// <summary>
    /// Rede de cuidado ↔ JSON (jsonb), na ordem de escalonamento. Uma coluna só preserva a ordem sem depender da ordem
    /// física das linhas; nome e telefone são PII, e a F2.3 cifra a coluna inteira.
    /// </summary>
    internal static readonly ValueConverter<RedeDeCuidado, string> RedeEmJson = new(r => JsonDa(r), j => RedeDo(j));

    /// <summary>A rede muda por dentro (<see cref="RedeDeCuidado.Definir"/>): o EF compara pelo conteúdo e guarda uma cópia.</summary>
    internal static readonly ValueComparer<RedeDeCuidado> RedePorConteudo = new(
        (a, b) => JsonDa(a) == JsonDa(b),
        r => JsonDa(r).GetHashCode(StringComparison.Ordinal),
        r => RedeDo(JsonDa(r)));

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static string JsonDa(RedeDeCuidado? rede)
        => JsonSerializer.Serialize(rede?.Contatos.Select(c => new ContatoGravado(c.Nome, c.Telefone.Valor, c.Papel)) ?? [], _json);

    private static RedeDeCuidado RedeDo(string json)
    {
        RedeDeCuidado rede = new();
        rede.Definir((JsonSerializer.Deserialize<ContatoGravado[]>(json, _json) ?? []).Select(c => new ContatoDeCuidado(c.Nome, Telefone.Criar(c.Telefone), c.Papel)));
        return rede;
    }

    private static string CodigosDe(PerfilDeSaude perfil) => string.Join(';', perfil.Fatores.Select(f => f.Codigo).Order(StringComparer.Ordinal));

    private static PerfilDeSaude PerfilDe(string codigos)
        => PerfilDeSaude.Com(codigos.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(FatorDeRisco.DoCodigo));
}

/// <summary>Um contato como fica no JSON da rede.</summary>
/// <param name="Nome">O nome ou apelido.</param>
/// <param name="Telefone">O DDD e o número, só dígitos.</param>
/// <param name="Papel">O papel no escalonamento.</param>
internal sealed record ContatoGravado(string Nome, string Telefone, PapelDoContato Papel);
