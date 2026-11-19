using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Geo;

namespace Sombra.Infraestrutura.Clima;

/// <summary>
/// O Clima Service de verdade (contrato <c>docs/contratos/clima-service.openapi.json</c>, amostras reais em
/// <c>docs/contratos/amostras-clima/</c>): o risco de calor de cada região e os pontos de resfriamento perto da casa.
/// Resiliente: timeout curto, circuito que abre depois de falhas seguidas e o último risco conhecido como resposta
/// enquanto o Clima não volta. Nunca lança para quem chama (o contrato de <see cref="IClimaGateway"/>).
/// </summary>
public sealed partial class ClimaClient : IClimaGateway, IPontosDeResfriamento
{
    /// <summary>O tempo máximo de uma chamada.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly EstadoDoClima _estado;
    private readonly ILogger<ClimaClient> _log;

    /// <summary>Initializes a new instance of the <see cref="ClimaClient"/> class.</summary>
    /// <param name="http">O cliente HTTP (com a correlação e o guarda de saída), com o endereço do Clima.</param>
    /// <param name="estado">O circuito e o último risco conhecido.</param>
    /// <param name="log">O log.</param>
    public ClimaClient(HttpClient http, EstadoDoClima estado, ILogger<ClimaClient> log)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _estado = estado ?? throw new ArgumentNullException(nameof(estado));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <inheritdoc/>
    public async Task<RiscoDaRegiao?> ObterRiscoAsync(string regiaoClimatica, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regiaoClimatica);
        (bool respondeu, RiscoDaRegiao? risco) = await ChamarAsync<RiscoResposta, RiscoDaRegiao>(
            $"api/v1/regioes/{Uri.EscapeDataString(regiaoClimatica)}/risco-calor", "risco-calor", Converter, cancellationToken);
        if (!respondeu)
        {
            return _estado.UltimoRisco(regiaoClimatica);
        }

        if (risco is not null)
        {
            _estado.Lembrar(regiaoClimatica, risco);
        }

        return risco;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PontoDeResfriamentoResposta>> ProximosAsync(PontoGeografico origem, double raioKm, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(origem);
        string rota = string.Create(
            CultureInfo.InvariantCulture,
            $"api/v1/pontos-de-resfriamento/proximos?lat={origem.Latitude}&lon={origem.Longitude}&raioKm={raioKm}");
        (_, IReadOnlyList<PontoDeResfriamentoResposta>? pontos) = await ChamarAsync<List<PontoResposta>, IReadOnlyList<PontoDeResfriamentoResposta>>(
            rota, "pontos-de-resfriamento", Converter, cancellationToken);
        return pontos ?? [];
    }

    /// <summary>Converte o código do contrato (<c>CAUTELA_EXTREMA</c>) no nível do domínio.</summary>
    /// <param name="codigo">O código.</param>
    /// <returns>O nível.</returns>
    /// <exception cref="JsonException">Quando o código não é um dos cinco níveis.</exception>
    internal static NivelDeRisco Nivel(string? codigo) =>
        Enum.TryParse((codigo ?? string.Empty).Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true, out NivelDeRisco nivel) && Enum.IsDefined(nivel)
            ? nivel
            : throw new JsonException($"nível de risco desconhecido: {codigo}");

    /// <summary>
    /// Uma chamada GET com o circuito: aberto, nem sai. 404 é resposta (o recurso não existe no Clima) e não conta como
    /// falha; rede, timeout, 5xx ou corpo ilegível contam. A conversão para o domínio acontece aqui dentro, para um corpo
    /// fora do contrato também contar como falha e nunca lançar para quem chama.
    /// </summary>
    /// <returns>Se o Clima respondeu (200 ou 404) e o valor convertido (nulo no 404).</returns>
    private async Task<(bool Respondeu, TResultado? Valor)> ChamarAsync<TCorpo, TResultado>(
        string rota, string recurso, Func<TCorpo, TResultado> converter, CancellationToken cancellationToken)
        where TCorpo : class
        where TResultado : class
    {
        if (_estado.Aberto)
        {
            return (false, null);
        }

        try
        {
            using HttpResponseMessage resposta = await _http.GetAsync(new Uri(rota, UriKind.Relative), cancellationToken);
            if (resposta.StatusCode == HttpStatusCode.NotFound)
            {
                Sucesso();
                return (true, null);
            }

            if (!resposta.IsSuccessStatusCode)
            {
                Falha(recurso, $"HTTP {(int)resposta.StatusCode}");
                return (false, null);
            }

            TCorpo corpo = await resposta.Content.ReadFromJsonAsync<TCorpo>(_json, cancellationToken)
                ?? throw new JsonException("corpo vazio");
            TResultado valor = converter(corpo);
            Sucesso();
            return (true, valor);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or NotSupportedException
            || (e is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            Falha(recurso, e.GetType().Name);
            return (false, null);
        }
    }

    private static RiscoDaRegiao Converter(RiscoResposta r) =>
        r.Agora is { } agora && r.Previsao24h is { } previsao
            ? new RiscoDaRegiao(Nivel(agora.Nivel), Nivel(previsao.NivelPrevisto), previsao.ProbabilidadePerigo, previsao.Fonte ?? "regra")
            : throw new JsonException("risco sem agora ou sem previsão");

    private static IReadOnlyList<PontoDeResfriamentoResposta> Converter(List<PontoResposta> pontos) =>
        [.. pontos.Select(p => new PontoDeResfriamentoResposta(
            p.Id, p.Nome, p.Tipo, p.DistanciaKm, new HorarioDeFuncionamentoResposta(p.Horario.Abre, p.Horario.Fecha),
            p.Acessivel, p.Fonte, p.Latitude, p.Longitude))];

    private void Sucesso()
    {
        if (_estado.RegistrarSucesso())
        {
            ClimaRecuperado(_log);
        }
    }

    private void Falha(string recurso, string motivo)
    {
        bool primeira = _estado.RegistrarFalha();
        if (primeira || _estado.Aberto)
        {
            ClimaIndisponivel(_log, recurso, motivo, _estado.Aberto);
        }
    }

    [LoggerMessage(EventId = 4601, EventName = "clima.indisponivel", Level = LogLevel.Warning,
        Message = "Clima indisponível em {recurso} ({motivo}); circuito aberto: {circuitoAberto}. O Core segue com o último risco conhecido")]
    private static partial void ClimaIndisponivel(ILogger logger, string recurso, string motivo, bool circuitoAberto);

    [LoggerMessage(EventId = 4602, EventName = "clima.recuperado", Level = LogLevel.Information,
        Message = "Clima respondeu de novo; circuito fechado")]
    private static partial void ClimaRecuperado(ILogger logger);
}

/// <summary>O corpo de <c>GET /regioes/{codigo}/risco-calor</c>, só com o que o Core usa.</summary>
/// <param name="Regiao">A região.</param>
/// <param name="Agora">A observação mais recente.</param>
/// <param name="Previsao24h">A previsão de 24 h.</param>
/// <param name="FonteDados">De onde veio o dado: <c>open-meteo</c> ou <c>replay-inmet</c>.</param>
internal sealed record RiscoResposta(string Regiao, AgoraResposta? Agora, PrevisaoResposta? Previsao24h, string FonteDados);

/// <summary>A observação mais recente da estação da região.</summary>
/// <param name="ObservadaEm">A hora da observação.</param>
/// <param name="IndiceDeCalorC">O índice de calor, °C.</param>
/// <param name="Nivel">O nível (<c>CAUTELA_EXTREMA</c>…).</param>
internal sealed record AgoraResposta(DateTimeOffset ObservadaEm, double IndiceDeCalorC, string Nivel);

/// <summary>A previsão de 24 h.</summary>
/// <param name="ProbabilidadePerigo">A probabilidade de perigo, de 0 a 1.</param>
/// <param name="NivelPrevisto">O nível previsto.</param>
/// <param name="IndiceDeCalorPrevistoC">O pico previsto, °C.</param>
/// <param name="Fonte"><c>onnx</c> ou <c>regra</c>.</param>
internal sealed record PrevisaoResposta(double ProbabilidadePerigo, string NivelPrevisto, double IndiceDeCalorPrevistoC, string? Fonte);

/// <summary>Um ponto de <c>GET /pontos-de-resfriamento/proximos</c>.</summary>
/// <param name="Id">O identificador.</param>
/// <param name="Nome">O nome.</param>
/// <param name="Tipo">O tipo.</param>
/// <param name="DistanciaKm">A distância até a origem, km.</param>
/// <param name="Horario">O horário.</param>
/// <param name="Acessivel">Se a fonte confirma a acessibilidade.</param>
/// <param name="Fonte">De onde veio o dado.</param>
/// <param name="Latitude">A latitude.</param>
/// <param name="Longitude">A longitude.</param>
internal sealed record PontoResposta(
    string Id, string Nome, string Tipo, double DistanciaKm, HorarioResposta Horario, bool Acessivel, string Fonte, double Latitude, double Longitude);

/// <summary>O horário de um ponto.</summary>
/// <param name="Abre">Quando abre (HH:mm).</param>
/// <param name="Fecha">Quando fecha (HH:mm).</param>
internal sealed record HorarioResposta(string Abre, string Fecha);
