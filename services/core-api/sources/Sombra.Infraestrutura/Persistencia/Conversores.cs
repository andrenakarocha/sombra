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
using Sombra.Privacidade.Repouso;

namespace Sombra.Infraestrutura.Persistencia;

/// <summary>
/// Os conversores dos value objects de um valor só: o banco guarda o valor, o domínio recebe o objeto já validado. Os
/// de PII cifram (AES-256-GCM, F2.3): recebem a <see cref="CifraDeCampos"/> e o nome da coluna, que entra autenticado.
/// </summary>
internal static class Conversores
{
    /// <summary>Temperatura ↔ °C.</summary>
    internal static readonly ValueConverter<Temperatura, double> TemperaturaEmCelsius = new(t => t.Celsius, c => Temperatura.Criar(c));

    /// <summary>Umidade relativa ↔ %.</summary>
    internal static readonly ValueConverter<UmidadeRelativa, double> UmidadeEmPercentual = new(u => u.Percentual, p => UmidadeRelativa.Criar(p));

    /// <summary>CPF ↔ os 11 dígitos, cifrados.</summary>
    /// <param name="cifra">A cifra.</param>
    /// <param name="coluna">A coluna.</param>
    /// <returns>O conversor.</returns>
    internal static ValueConverter<Cpf, string> CpfCifrado(CifraDeCampos cifra, string coluna)
        => new(c => cifra.Cifrar(c.Valor, coluna), s => Cpf.Criar(cifra.Decifrar(s, coluna)));

    /// <summary>Cartão SUS ↔ os 15 dígitos, cifrados. É opcional, mas o EF nunca passa nulo para um conversor.</summary>
    /// <param name="cifra">A cifra.</param>
    /// <param name="coluna">A coluna.</param>
    /// <returns>O conversor.</returns>
    internal static ValueConverter<CartaoSus?, string> CartaoSusCifrado(CifraDeCampos cifra, string coluna)
        => new(c => cifra.Cifrar(c!.Valor, coluna), s => CartaoSus.Criar(cifra.Decifrar(s, coluna)));

    /// <summary>Telefone do morador ↔ os dígitos com o DDD, cifrados. É opcional, como o Cartão SUS.</summary>
    /// <param name="cifra">A cifra.</param>
    /// <param name="coluna">A coluna.</param>
    /// <returns>O conversor.</returns>
    internal static ValueConverter<Telefone?, string> TelefoneCifrado(CifraDeCampos cifra, string coluna)
        => new(t => cifra.Cifrar(t!.Valor, coluna), s => Telefone.Criar(cifra.Decifrar(s, coluna)));

    /// <summary>Perfil de saúde ↔ os códigos dos fatores (em ordem, separados por ponto e vírgula), cifrados.</summary>
    /// <param name="cifra">A cifra.</param>
    /// <param name="coluna">A coluna.</param>
    /// <returns>O conversor.</returns>
    internal static ValueConverter<PerfilDeSaude, string> PerfilCifrado(CifraDeCampos cifra, string coluna)
        => new(p => cifra.Cifrar(CodigosDe(p), coluna), s => PerfilDe(cifra.Decifrar(s, coluna)));

    /// <summary>Um texto livre (as observações de saúde), cifrado.</summary>
    /// <param name="cifra">A cifra.</param>
    /// <param name="coluna">A coluna.</param>
    /// <returns>O conversor.</returns>
    internal static ValueConverter<string?, string> TextoCifrado(CifraDeCampos cifra, string coluna)
        => new(t => cifra.Cifrar(t!, coluna), s => cifra.Decifrar(s, coluna));

    /// <summary>
    /// Rede de cuidado ↔ JSON cifrado, na ordem de escalonamento. Uma coluna só preserva a ordem sem depender da ordem
    /// física das linhas; nome e telefone são PII, então a coluna inteira é cifrada.
    /// </summary>
    /// <param name="cifra">A cifra.</param>
    /// <param name="coluna">A coluna.</param>
    /// <returns>O conversor.</returns>
    internal static ValueConverter<RedeDeCuidado, string> RedeCifrada(CifraDeCampos cifra, string coluna)
        => new(r => cifra.Cifrar(JsonDa(r), coluna), s => RedeDo(cifra.Decifrar(s, coluna)));

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
