using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using Sombra.Api.Autenticacao;
using Sombra.Aplicacao.Dtos;
using Testcontainers.PostgreSql;

namespace Sombra.Api.IntegrationTests.Apoio;

/// <summary>Um PostgreSQL 16 de verdade para a montagem inteira; cada fábrica da API ganha um banco só dela.</summary>
[TestClass]
public sealed class AmbienteDaApi
{
    private static PostgreSqlContainer? _postgres;

    /// <summary>Gets o JSON do contrato: camelCase e enums em SNAKE_CASE maiúsculo.</summary>
    internal static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    /// <summary>Sobe o contêiner.</summary>
    /// <param name="contexto">O contexto do MSTest.</param>
    /// <returns>A subida.</returns>
    [AssemblyInitialize]
    public static async Task IniciarAsync(TestContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await _postgres.StartAsync(contexto.CancellationToken);
    }

    /// <summary>Derruba o contêiner.</summary>
    /// <returns>A descida.</returns>
    [AssemblyCleanup]
    public static async Task EncerrarAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    /// <summary>Uma conexão para um banco novo (a API o cria e migra ao subir).</summary>
    /// <returns>A string de conexão.</returns>
    internal static string NovaConexao()
        => new NpgsqlConnectionStringBuilder(_postgres!.GetConnectionString()) { Database = $"api_{Guid.NewGuid():N}" }.ConnectionString;
}

/// <summary>
/// A Core API de verdade em memória, com banco próprio, migrações, seed e contas da demo. O segredo do JWT e a senha
/// da demo são gerados na hora: nada de credencial no código.
/// </summary>
internal sealed class FabricaDaApi : WebApplicationFactory<Program>
{
    private readonly string _conexao = AmbienteDaApi.NovaConexao();
    private readonly string _segredoDoJwt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private readonly int _loginPorMinuto;

    /// <summary>Initializes a new instance of the <see cref="FabricaDaApi"/> class.</summary>
    /// <param name="loginPorMinuto">O limite de logins por minuto.</param>
    internal FabricaDaApi(int loginPorMinuto = 1000) => _loginPorMinuto = loginPorMinuto;

    /// <summary>Gets a senha das contas da demo neste teste.</summary>
    internal string SenhaDaDemo { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));

    /// <summary>Um cliente já autenticado com uma conta da demo.</summary>
    /// <param name="email">O e-mail da conta.</param>
    /// <returns>O cliente com o header <c>Authorization</c>.</returns>
    internal async Task<HttpClient> ClienteDeAsync(string email)
    {
        HttpClient cliente = CreateClient();
        using HttpResponseMessage resposta = await cliente.PostAsJsonAsync("/api/v1/auth/login", new LoginDto { Email = email, Senha = SenhaDaDemo }, AmbienteDaApi.Json);
        resposta.EnsureSuccessStatusCode();
        TokenResposta? token = await resposta.Content.ReadFromJsonAsync<TokenResposta>(AmbienteDaApi.Json);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.Token);
        return cliente;
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testes");
        builder.UseSetting("ConnectionStrings:Core", _conexao);
        builder.UseSetting("Jwt:Segredo", _segredoDoJwt);
        builder.UseSetting("Demo:Senha", SenhaDaDemo);
        builder.UseSetting("Banco:MigrarAoIniciar", "true");
        builder.UseSetting("LimiteDeRequisicoes:LoginPorMinuto", _loginPorMinuto.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("Versao:Commit", "teste01");
    }
}

/// <summary>As contas e as casas da demonstração.</summary>
internal static class Demo
{
    /// <summary>A filha da Dona Lurdes (Cuidador).</summary>
    internal const string Ana = "ana.filha@demo.sombra";

    /// <summary>O filho do Seu Joaquim (Cuidador).</summary>
    internal const string Marcos = "marcos.filho@demo.sombra";

    /// <summary>A operação (Operador).</summary>
    internal const string Operador = "operador@demo.sombra";

    /// <summary>A casa da Dona Lurdes.</summary>
    internal static readonly Guid Lurdes = new("0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10");

    /// <summary>A casa do Seu Joaquim.</summary>
    internal static readonly Guid Joaquim = new("4e2b7c90-58a1-4d3f-9b6e-1a7c2d9e8f31");
}
