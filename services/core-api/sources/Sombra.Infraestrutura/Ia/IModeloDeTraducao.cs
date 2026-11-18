using System.Threading;
using System.Threading.Tasks;
using Sombra.Privacidade.Traducao;

namespace Sombra.Infraestrutura.Ia;

/// <summary>O provedor de linguagem que traduz um evento de segurança nas três audiências (D7: Gemini Flash).</summary>
public interface IModeloDeTraducao
{
    /// <summary>Gets a value indicating whether há chave: sem ela, o tradutor usa só templates.</summary>
    bool Configurado { get; }

    /// <summary>Gets o modelo em uso.</summary>
    string Modelo { get; }

    /// <summary>Pede a tradução.</summary>
    /// <param name="sistema">A instrução de sistema.</param>
    /// <param name="prompt">O prompt, só com metadados.</param>
    /// <param name="cancellationToken">Cancela (o tradutor dá o timeout).</param>
    /// <returns>As três traduções, com <c>fonte: IA</c>, ainda sem passar pela guarda.</returns>
    Task<TraducaoDeRisco> TraduzirAsync(string sistema, string prompt, CancellationToken cancellationToken);
}
