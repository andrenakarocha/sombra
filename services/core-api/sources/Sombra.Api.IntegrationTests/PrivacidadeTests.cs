using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Api.IntegrationTests.Apoio;
using Sombra.Aplicacao.Dtos;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Tests.Apoio;
using Sombra.Infraestrutura.Persistencia;

namespace Sombra.Api.IntegrationTests;

/// <summary>
/// Cenário A1 da auditoria (spec 07 §5): um CPF escrito no texto livre do morador é mascarado antes de gravar e de
/// logar, ou a requisição é recusada com 422, conforme a política de campo livre.
/// </summary>
[TestClass]
public sealed class PrivacidadeTests
{
    private static readonly string _cpfDoFilho = DocumentosSinteticos.Cpf(77);

    /// <summary>
    /// A1 com a política padrão: o morador é criado, o perfil de saúde vai para o banco com o CPF do filho mascarado (o
    /// dado de saúde esperado fica), nenhuma linha do log tem o CPF e o evento <c>privacidade.pii_detectada</c> registra
    /// campo, família e desfecho.
    /// </summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task A1CpfNoTextoLivreEhMascaradoAntesDeGravarELogar()
    {
        LogCapturado log = new();
        await using FabricaDaApi api = new(log: log);
        using HttpClient ana = await api.ClienteDeAsync(Demo.Ana);

        using HttpResponseMessage resposta = await ana.PostAsJsonAsync($"/api/v1/domicilios/{Demo.Lurdes}/moradores", NovoMorador(), AmbienteDaApi.Json);

        Assert.AreEqual(HttpStatusCode.Created, resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
        Guid id = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        string gravado = await ObservacoesGravadasAsync(api, id);
        Assert.AreEqual($"Toma losartana. Em caso de urgência, CPF do filho ***.{_cpfDoFilho[3..6]}.***-{_cpfDoFilho[9..]}", gravado);

        string tudo = string.Join('\n', log.Linhas);
        foreach (string proibido in FormasDoCpf())
        {
            Assert.DoesNotContain(proibido, tudo, "o log vazou o CPF do texto livre");
        }

        JsonElement evento = log.Eventos.Single(e => Texto(e, "event") == "privacidade.pii_detectada" && Texto(e, "desfecho") == "Mascarado");
        Assert.AreEqual("Warning", Texto(evento, "level"));
        Assert.AreEqual("$.perfilDeSaude", Texto(evento, "campo"));
        Assert.AreEqual("Identificacao", Texto(evento, "categoria"));
        Assert.AreEqual("cpf", Texto(evento, "regra"));
    }

    /// <summary>
    /// A1 com <c>Privacidade:CampoLivre=Recusar</c>: 422 com o campo e a família (nunca o valor) e nenhum morador novo.
    /// </summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task A1NaPoliticaDeRecusaResponde422SemGravar()
    {
        await using FabricaDaApi api = new(configuracao: new Dictionary<string, string> { ["Privacidade:CampoLivre"] = "Recusar" });
        using HttpClient ana = await api.ClienteDeAsync(Demo.Ana);
        int antes = await QuantosMoradoresAsync(api);

        using HttpResponseMessage resposta = await ana.PostAsJsonAsync($"/api/v1/domicilios/{Demo.Lurdes}/moradores", NovoMorador(), AmbienteDaApi.Json);

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        string corpo = await resposta.Content.ReadAsStringAsync();
        JsonElement campo = JsonDocument.Parse(corpo).RootElement.GetProperty("campos").EnumerateArray().Single();
        Assert.AreEqual("$.perfilDeSaude", campo.GetProperty("campo").GetString());
        Assert.IsTrue(JsonDocument.Parse(corpo).RootElement.TryGetProperty("correlationId", out _));
        foreach (string proibido in FormasDoCpf())
        {
            Assert.DoesNotContain(proibido, corpo);
        }

        Assert.AreEqual(antes, await QuantosMoradoresAsync(api));
    }

    /// <summary>O cadastro comum, com CPF, Cartão SUS e telefone nos campos próprios, passa sem nada mascarado no meio.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CamposPropriosNaoSaoTocados()
    {
        LogCapturado log = new();
        await using FabricaDaApi api = new(log: log, configuracao: new Dictionary<string, string> { ["Privacidade:CampoLivre"] = "Recusar" });
        using HttpClient ana = await api.ClienteDeAsync(Demo.Ana);

        using HttpResponseMessage resposta = await ana.PostAsJsonAsync(
            $"/api/v1/domicilios/{Demo.Lurdes}/moradores",
            NovoMorador() with { PerfilDeSaude = "Hipertensão, toma losartana." },
            AmbienteDaApi.Json);

        Assert.AreEqual(HttpStatusCode.Created, resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
        Assert.IsFalse(log.Eventos.Any(e => Texto(e, "event") == "privacidade.pii_detectada" && Texto(e, "level") == "Warning"));
    }

    /// <summary>
    /// F2.3: um morador cadastrado pela API fica cifrado no banco (o que um <c>psql</c> veria: prefixo <c>v1:</c>, nenhum
    /// dígito do CPF, do Cartão SUS ou do telefone, nem o texto de saúde), com o índice cego do CPF, e a API devolve tudo
    /// mascarado.
    /// </summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task MoradorFicaCifradoNoBancoEMascaradoNaApi()
    {
        await using FabricaDaApi api = new();
        using HttpClient ana = await api.ClienteDeAsync(Demo.Ana);
        NovoMoradorDto morador = NovoMorador() with { PerfilDeSaude = "Hipertensão, toma losartana." };

        using HttpResponseMessage criado = await ana.PostAsJsonAsync($"/api/v1/domicilios/{Demo.Lurdes}/moradores", morador, AmbienteDaApi.Json);
        Guid id = (await criado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        string[] colunas = await ColunasDoMoradorAsync(api, id);
        JsonElement lido = await ana.GetFromJsonAsync<JsonElement>($"/api/v1/domicilios/{Demo.Lurdes}/moradores/{id}", AmbienteDaApi.Json);

        Assert.AreEqual(HttpStatusCode.Created, criado.StatusCode);
        Assert.IsTrue(colunas[..5].All(c => c.StartsWith("v1:", StringComparison.Ordinal)), string.Join(" | ", colunas));
        Assert.AreEqual(64, colunas[5].Length);
        foreach (string claro in new[] { morador.Cpf, morador.CartaoSus, "97654", "losartana" })
        {
            Assert.IsFalse(colunas.Any(c => c.Contains(claro, StringComparison.Ordinal)), $"o banco guardou em claro: {claro}");
        }

        Assert.AreEqual($"***.{morador.Cpf[3..6]}.***-{morador.Cpf[9..]}", lido.GetProperty("cpf").GetString());
        Assert.AreEqual($"*** **** **** {morador.CartaoSus[^4..]}", lido.GetProperty("cartaoSus").GetString());
        Assert.AreEqual("[DADO DE SAÚDE PROTEGIDO]", lido.GetProperty("perfilDeSaude").GetString());
    }

    private static NovoMoradorDto NovoMorador() => new()
    {
        Nome = "Iolanda Teixeira",
        Idade = 84,
        Cpf = DocumentosSinteticos.Cpf(76),
        CartaoSus = DocumentosSinteticos.CartaoSus(76),
        Telefone = "(11) 97654-3210",
        PerfilDeSaude = $"Toma losartana. Em caso de urgência, CPF do filho {_cpfDoFilho}",
        FatoresDeRisco = [],
    };

    private static string[] FormasDoCpf()
        => [_cpfDoFilho, $"{_cpfDoFilho[..3]}.{_cpfDoFilho[3..6]}.{_cpfDoFilho[6..9]}-{_cpfDoFilho[9..]}"];

    private static async Task<string> ObservacoesGravadasAsync(FabricaDaApi api, Guid moradorId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        SombraDbContext db = escopo.ServiceProvider.GetRequiredService<SombraDbContext>();
        Domicilio casa = await db.Domicilios.AsNoTracking().SingleAsync(d => d.Id == Demo.Lurdes);
        return casa.Moradores.Single(m => m.Id == moradorId).ObservacoesDeSaude!;
    }

    private static async Task<string[]> ColunasDoMoradorAsync(FabricaDaApi api, Guid moradorId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        DbConnection conexao = escopo.ServiceProvider.GetRequiredService<SombraDbContext>().Database.GetDbConnection();
        await conexao.OpenAsync();
        await using DbCommand comando = conexao.CreateCommand();
        comando.CommandText = "SELECT cpf, cartao_sus, telefone, perfil_de_saude, observacoes_de_saude, cpf_indice FROM moradores WHERE id = @id";
        DbParameter parametro = comando.CreateParameter();
        parametro.ParameterName = "id";
        parametro.Value = moradorId;
        comando.Parameters.Add(parametro);
        await using DbDataReader leitor = await comando.ExecuteReaderAsync();
        await leitor.ReadAsync();
        return [.. Enumerable.Range(0, leitor.FieldCount).Select(leitor.GetString)];
    }

    private static async Task<int> QuantosMoradoresAsync(FabricaDaApi api)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        SombraDbContext db = escopo.ServiceProvider.GetRequiredService<SombraDbContext>();
        return (await db.Domicilios.AsNoTracking().SingleAsync(d => d.Id == Demo.Lurdes)).Moradores.Count;
    }

    private static string? Texto(JsonElement evento, string campo)
        => evento.TryGetProperty(campo, out JsonElement valor) ? valor.ToString() : null;
}
