// Sombra.Api: ponto de entrada da Core API. Os endpoints chegam na F1.7 (docs/PLANO.md §8.2).
using Microsoft.AspNetCore.Builder;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();
app.Run();

/// <summary>Ponto de entrada da Core API, público para os testes de integração.</summary>
public partial class Program
{
}
