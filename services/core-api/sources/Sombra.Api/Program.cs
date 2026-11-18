// Sombra.Api: ponto de entrada da Core API (docs/PLANO.md §8.2, contrato docs/contratos/core-api.md).
using Microsoft.AspNetCore.Builder;
using Sombra.Api.Composicao;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AdicionarCore();

WebApplication app = builder.Build();
await app.PrepararBancoAsync();
app.UsarCore();
await app.RunAsync();

/// <summary>Ponto de entrada da Core API, público para os testes de integração.</summary>
public partial class Program
{
}
