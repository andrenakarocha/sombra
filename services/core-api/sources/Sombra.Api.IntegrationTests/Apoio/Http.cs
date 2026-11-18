using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sombra.Api.IntegrationTests.Apoio;

/// <summary>Atalhos para conferir status e corpo das respostas.</summary>
internal static class Http
{
    /// <summary>Confere o status e lê o corpo.</summary>
    /// <typeparam name="T">O tipo do corpo.</typeparam>
    /// <param name="resposta">A resposta.</param>
    /// <param name="esperado">O status esperado.</param>
    /// <returns>O corpo.</returns>
    internal static async Task<T> CorpoAsync<T>(this HttpResponseMessage resposta, HttpStatusCode esperado)
    {
        Assert.AreEqual(esperado, resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
        T? corpo = await resposta.Content.ReadFromJsonAsync<T>(AmbienteDaApi.Json);
        Assert.IsNotNull(corpo);
        return corpo;
    }

    /// <summary>Confere que a resposta é um ProblemDetails com o status esperado e o <c>correlationId</c>.</summary>
    /// <param name="resposta">A resposta.</param>
    /// <param name="esperado">O status esperado.</param>
    /// <returns>O JSON do problema.</returns>
    internal static async Task<JsonElement> ProblemaAsync(this HttpResponseMessage resposta, HttpStatusCode esperado)
    {
        string texto = await resposta.Content.ReadAsStringAsync();
        Assert.AreEqual(esperado, resposta.StatusCode, texto);
        Assert.AreEqual("application/problem+json", resposta.Content.Headers.ContentType?.MediaType, texto);
        JsonElement problema = JsonDocument.Parse(texto).RootElement.Clone();
        Assert.AreEqual((int)esperado, problema.GetProperty("status").GetInt32());
        Assert.IsTrue(problema.TryGetProperty("correlationId", out _), texto);
        return problema;
    }

    /// <summary>Envia um corpo JSON no formato do contrato.</summary>
    /// <param name="cliente">O cliente.</param>
    /// <param name="metodo">O método HTTP.</param>
    /// <param name="url">O endereço.</param>
    /// <param name="corpo">O corpo.</param>
    /// <returns>A resposta.</returns>
    internal static async Task<HttpResponseMessage> EnviarAsync(this HttpClient cliente, HttpMethod metodo, string url, object? corpo = null)
    {
        using HttpRequestMessage requisicao = new(metodo, url) { Content = corpo is null ? null : JsonContent.Create(corpo, options: AmbienteDaApi.Json) };
        return await cliente.SendAsync(requisicao);
    }
}
