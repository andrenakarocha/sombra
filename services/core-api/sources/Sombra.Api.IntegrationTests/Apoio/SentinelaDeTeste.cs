using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Protocol;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Calor;
using Sombra.Infraestrutura.Mqtt;

namespace Sombra.Api.IntegrationTests.Apoio;

/// <summary>Um relógio que começa agora e anda quando o teste manda (o escalonador conta minutos por ele).</summary>
internal sealed class RelogioDeTeste : IRelogio
{
    private long _avanco;

    /// <inheritdoc/>
    public DateTimeOffset Agora => DateTimeOffset.UtcNow.AddTicks(Interlocked.Read(ref _avanco));

    /// <summary>Anda com o relógio.</summary>
    /// <param name="quanto">Quanto tempo.</param>
    internal void Avancar(TimeSpan quanto) => Interlocked.Add(ref _avanco, quanto.Ticks);
}

/// <summary>
/// Uma Sentinela de mentira, em C#, falando o contrato MQTT: publica telemetria e eventos assinados e guarda os comandos
/// que chegam, já com a assinatura conferida.
/// </summary>
internal sealed class SentinelaDeTeste : IAsyncDisposable
{
    private readonly IMqttClient _cliente = new MqttClientFactory().CreateMqttClient();
    private readonly string _prefixo;
    private readonly string _segredo;

    private SentinelaDeTeste(string deviceId, string segredo, string prefixo)
    {
        DeviceId = deviceId;
        _segredo = segredo;
        _prefixo = prefixo;
    }

    /// <summary>Gets o identificador.</summary>
    internal string DeviceId { get; }

    /// <summary>Gets os comandos recebidos com assinatura válida.</summary>
    internal ConcurrentQueue<JsonElement> Comandos { get; } = new();

    /// <summary>Conecta no broker dos testes e assina o tópico de comandos.</summary>
    /// <param name="deviceId">A Sentinela.</param>
    /// <param name="segredo">O segredo HMAC.</param>
    /// <param name="prefixo">O prefixo dos tópicos da API em teste.</param>
    /// <returns>A Sentinela conectada.</returns>
    internal static async Task<SentinelaDeTeste> ConectarAsync(string deviceId, string segredo, string prefixo)
    {
        SentinelaDeTeste sentinela = new(deviceId, segredo, prefixo);
        sentinela._cliente.ApplicationMessageReceivedAsync += argumentos =>
        {
            JsonElement comando = JsonDocument.Parse(argumentos.ApplicationMessage.ConvertPayloadToString()).RootElement.Clone();
            string canonica = AssinaturaMqtt.CanonicaDoComando(deviceId, comando.GetProperty("v").GetInt64(), comando.GetProperty("ts").GetInt64(), comando.GetProperty("seq").GetInt64(), comando.GetProperty("tipo").GetString()!, comando.GetProperty("mensagem").GetString()!);
            if (AssinaturaMqtt.Conferir(segredo, canonica, comando.GetProperty("sig").GetString()))
            {
                sentinela.Comandos.Enqueue(comando);
            }

            return Task.CompletedTask;
        };
        await sentinela._cliente.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer(AmbienteDaApi.MqttHost, AmbienteDaApi.MqttPorta).WithClientId($"teste-{deviceId}-{Guid.NewGuid():N}"[..22]).Build());
        await sentinela._cliente.SubscribeAsync(new MqttClientSubscribeOptionsBuilder().WithTopicFilter($"{prefixo}/{deviceId}/comando", MqttQualityOfServiceLevel.AtLeastOnce).Build());
        return sentinela;
    }

    /// <summary>A telemetria assinada, como a Sentinela monta (números com uma casa).</summary>
    /// <param name="deviceId">A Sentinela.</param>
    /// <param name="segredo">O segredo HMAC.</param>
    /// <param name="temperatura">A temperatura.</param>
    /// <param name="umidade">A umidade.</param>
    /// <param name="seq">O contador.</param>
    /// <param name="agora">O relógio da Sentinela.</param>
    /// <param name="adulterar">Muda a temperatura depois de assinar.</param>
    /// <returns>O JSON publicado.</returns>
    internal static string MontarTelemetria(string deviceId, string segredo, double temperatura, double umidade, long seq, DateTimeOffset agora, bool adulterar = false)
    {
        double t = Math.Round(temperatura, 1);
        double rh = Math.Round(umidade, 1);
        IndiceDeCalor indice = IndiceDeCalor.Calcular(Temperatura.Criar(t), UmidadeRelativa.Criar(rh));
        double hi = Math.Round(indice.Celsius, 1);
        string nivel = JsonNamingPolicy.SnakeCaseUpper.ConvertName(LimitesTermicos.Padrao.Classificar(indice).ToString());
        long ts = agora.ToUnixTimeSeconds();
        string sig = AssinaturaMqtt.Assinar(segredo, AssinaturaMqtt.CanonicaDaTelemetria(deviceId, 1, ts, seq, t, rh, hi, nivel));
        return JsonSerializer.Serialize(new { v = 1, ts, seq, t = adulterar ? t + 5 : t, rh, hi, nivel, rssi = -60, sig });
    }

    /// <summary>Publica uma telemetria assinada.</summary>
    /// <param name="temperatura">A temperatura.</param>
    /// <param name="umidade">A umidade.</param>
    /// <param name="seq">O contador.</param>
    /// <param name="agora">O relógio da Sentinela.</param>
    /// <param name="adulterar">Muda a temperatura depois de assinar.</param>
    /// <returns>A publicação.</returns>
    internal Task TelemetriaAsync(double temperatura, double umidade, long seq, DateTimeOffset agora, bool adulterar = false)
        => PublicarAsync("telemetria", MontarTelemetria(DeviceId, _segredo, temperatura, umidade, seq, agora, adulterar));

    /// <summary>Publica um evento assinado (botão).</summary>
    /// <param name="tipo">O tipo.</param>
    /// <param name="seq">O contador.</param>
    /// <param name="agora">O relógio da Sentinela.</param>
    /// <returns>A publicação.</returns>
    internal Task EventoAsync(string tipo, long seq, DateTimeOffset agora)
    {
        long ts = agora.ToUnixTimeSeconds();
        string sig = AssinaturaMqtt.Assinar(_segredo, AssinaturaMqtt.CanonicaDoEvento(DeviceId, 1, ts, seq, tipo));
        return PublicarAsync("evento", JsonSerializer.Serialize(new { v = 1, ts, seq, tipo, sig }));
    }

    /// <summary>Espera um comando de um tipo.</summary>
    /// <param name="tipo">O tipo do comando.</param>
    /// <returns>O comando.</returns>
    internal async Task<JsonElement> ComandoAsync(string tipo)
    {
        await Esperar.AteAsync(() => Comandos.Any(c => c.GetProperty("tipo").GetString() == tipo));
        return Comandos.First(c => c.GetProperty("tipo").GetString() == tipo);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _cliente.DisconnectAsync();
        _cliente.Dispose();
    }

    private Task PublicarAsync(string canal, string conteudo)
        => _cliente.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic($"{_prefixo}/{DeviceId}/{canal}")
            .WithPayload(Encoding.UTF8.GetBytes(conteudo))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build());
}

/// <summary>Um cliente do hub que guarda todo evento recebido.</summary>
internal sealed class OuvinteDoHub : IAsyncDisposable
{
    private static readonly string[] _eventos = ["LeituraRegistrada", "SentinelaStatus", "AlertaAberto", "AlertaEscalonado", "AlertaResolvido", "CheckInRegistrado", "AvisoParaARede", "AlertaDeInfra", "RiscoCidadaoAtualizado"];
    private readonly HubConnection _conexao;

    private OuvinteDoHub(HubConnection conexao) => _conexao = conexao;

    /// <summary>Gets os eventos recebidos, com o nome.</summary>
    internal ConcurrentQueue<(string Nome, JsonElement Dados)> Recebidos { get; } = new();

    /// <summary>Conecta no hub da API em teste com um token.</summary>
    /// <param name="api">A API.</param>
    /// <param name="token">O JWT.</param>
    /// <returns>O ouvinte conectado.</returns>
    internal static async Task<OuvinteDoHub> ConectarAsync(FabricaDaApi api, string token)
    {
        HubConnection conexao = new HubConnectionBuilder()
            .WithUrl(new Uri(api.Server.BaseAddress, "hubs/sombra"), opcoes =>
            {
                opcoes.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
                opcoes.Transports = HttpTransportType.LongPolling;
                opcoes.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        OuvinteDoHub ouvinte = new(conexao);
        foreach (string evento in _eventos)
        {
            conexao.On<JsonElement>(evento, dados => ouvinte.Recebidos.Enqueue((evento, dados)));
        }

        await conexao.StartAsync();
        return ouvinte;
    }

    /// <summary>Espera um evento que satisfaça a condição.</summary>
    /// <param name="nome">O nome do evento.</param>
    /// <param name="condicao">A condição sobre o payload.</param>
    /// <returns>O payload.</returns>
    internal async Task<JsonElement> EventoAsync(string nome, Func<JsonElement, bool>? condicao = null)
    {
        await Esperar.AteAsync(() => Recebidos.Any(r => r.Nome == nome && (condicao?.Invoke(r.Dados) ?? true)));
        return Recebidos.First(r => r.Nome == nome && (condicao?.Invoke(r.Dados) ?? true)).Dados;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _conexao.DisposeAsync();
}

/// <summary>Atalhos de leitura do JSON dos eventos.</summary>
internal static class Json
{
    /// <summary>Um campo como texto.</summary>
    /// <param name="elemento">O JSON.</param>
    /// <param name="campo">O campo.</param>
    /// <returns>O texto.</returns>
    internal static string Texto(this JsonElement elemento, string campo) => elemento.GetProperty(campo).ToString();

    /// <summary>Um campo inteiro.</summary>
    /// <param name="elemento">O JSON.</param>
    /// <param name="campo">O campo.</param>
    /// <returns>O número.</returns>
    internal static int Inteiro(this JsonElement elemento, string campo) => elemento.GetProperty(campo).GetInt32();

    /// <summary>Formata um número com cultura invariante.</summary>
    /// <param name="valor">O número.</param>
    /// <returns>O texto.</returns>
    internal static string Invariante(this long valor) => valor.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Acesso ao banco da API em teste, para conferir o que foi gravado.</summary>
internal static class BancoDaApi
{
    /// <summary>Roda uma consulta num escopo novo.</summary>
    /// <typeparam name="T">O resultado.</typeparam>
    /// <param name="api">A API.</param>
    /// <param name="consulta">A consulta.</param>
    /// <returns>O resultado.</returns>
    internal static async Task<T> ConsultarAsync<T>(this FabricaDaApi api, Func<Sombra.Infraestrutura.Persistencia.SombraDbContext, Task<T>> consulta)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        return await consulta(escopo.ServiceProvider.GetRequiredService<Sombra.Infraestrutura.Persistencia.SombraDbContext>());
    }
}
