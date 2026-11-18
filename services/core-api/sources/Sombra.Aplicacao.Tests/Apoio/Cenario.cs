using System;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Alertas;
using Sombra.Aplicacao.Cadastro;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Pessoas;
using Sombra.Dominio.Saude;
using Sombra.Dominio.Tests.Apoio;

namespace Sombra.Aplicacao.Tests.Apoio;

/// <summary>O Sombra montado com os falsos, em 18/11/2026 às 18h UTC (15h em São Paulo, o pico da tarde).</summary>
internal sealed class Cenario
{
    /// <summary>A Sentinela da casa da Dona Lurdes.</summary>
    internal const string DeviceId = "snt-7f3a9c";

    /// <summary>O instante em que o cenário começa.</summary>
    internal static readonly DateTimeOffset Inicio = new(2026, 11, 18, 18, 0, 0, TimeSpan.Zero);

    /// <summary>Initializes a new instance of the <see cref="Cenario"/> class.</summary>
    internal Cenario() => Notificador = new NotificadorGravado(Relogio);

    /// <summary>Gets o relógio.</summary>
    internal RelogioFixo Relogio { get; } = new(Inicio);

    /// <summary>Gets as casas.</summary>
    internal DomiciliosEmMemoria Domicilios { get; } = new();

    /// <summary>Gets os alertas.</summary>
    internal AlertasEmMemoria Alertas { get; } = new();

    /// <summary>Gets as leituras.</summary>
    internal LeiturasEmMemoria Leituras { get; } = new();

    /// <summary>Gets os check-ins.</summary>
    internal CheckInsEmMemoria CheckIns { get; } = new();

    /// <summary>Gets os avisos registrados.</summary>
    internal NotificadorGravado Notificador { get; }

    /// <summary>Gets o Clima Service.</summary>
    internal ClimaFalso Clima { get; } = new();

    /// <summary>Gets a unidade de trabalho.</summary>
    internal UnidadeDeTrabalhoContada UnidadeDeTrabalho { get; } = new();

    /// <summary>Gets quem confere o código do QR da Sentinela.</summary>
    internal CodigosDeVinculoDeTeste CodigosDeVinculo { get; } = new();

    /// <summary>O caso de uso de cadastro.</summary>
    /// <returns>O caso de uso.</returns>
    internal CadastroDeDomicilios Cadastro() => new(Domicilios, Alertas, Leituras, CheckIns, Relogio, UnidadeDeTrabalho, CodigosDeVinculo);

    /// <summary>O caso de uso da telemetria.</summary>
    /// <returns>O caso de uso.</returns>
    internal RegistrarLeitura RegistrarLeitura() => new(Domicilios, Alertas, Leituras, Notificador, UnidadeDeTrabalho);

    /// <summary>O caso de uso do check-in.</summary>
    /// <returns>O caso de uso.</returns>
    internal RegistrarCheckIn RegistrarCheckIn() => new(Domicilios, Alertas, Leituras, CheckIns, Relogio, Notificador, UnidadeDeTrabalho);

    /// <summary>O caso de uso da resposta do cuidador.</summary>
    /// <returns>O caso de uso.</returns>
    internal ReconhecerAlerta ReconhecerAlerta() => new(Alertas, Domicilios, Relogio, Notificador, UnidadeDeTrabalho);

    /// <summary>O caso de uso da rotina.</summary>
    /// <returns>O caso de uso.</returns>
    internal LembrarRotina LembrarRotina() => new(Domicilios, Leituras, CheckIns, Notificador, Relogio, Notificador, UnidadeDeTrabalho);

    /// <summary>O caso de uso do escalonador.</summary>
    /// <returns>O caso de uso.</returns>
    internal EscalonarAlertas EscalonarAlertas() => new(Domicilios, Alertas, Relogio, Notificador, UnidadeDeTrabalho);

    /// <summary>O caso de uso do monitor de previsão.</summary>
    /// <returns>O caso de uso.</returns>
    internal AvaliarPrevisoes AvaliarPrevisoes() => new(Domicilios, Alertas, Clima, Relogio, Notificador, UnidadeDeTrabalho);

    /// <summary>
    /// A casa da Dona Lurdes (70 anos, limites padrão), com a filha e a vizinha na rede, a Sentinela vinculada e o
    /// monitoramento ligado.
    /// </summary>
    /// <param name="regiao">A região climática.</param>
    /// <param name="deviceId">A Sentinela.</param>
    /// <returns>A casa, já guardada.</returns>
    internal Domicilio CasaMonitorada(string regiao = "SP-MIRANTE", string deviceId = DeviceId)
    {
        DateOnly hoje = DateOnly.FromDateTime(Inicio.UtcDateTime);
        Domicilio casa = Domicilio.Cadastrar(
            "Casa da Dona Lurdes",
            Endereco.Criar("Rua das Flores", "12", "Santana", "São Paulo", "SP", "02012-000"),
            regiao);
        casa.AdicionarMorador(Morador.Cadastrar("Lurdes A.", hoje.AddYears(-70), Cpf.Criar(DocumentosSinteticos.Cpf(1)), null, PerfilDeSaude.Vazio, hoje));
        casa.DefinirRedeDeCuidado(
        [
            new ContatoDeCuidado("Ana", Telefone.Criar("11987654321"), PapelDoContato.Familiar),
            new ContatoDeCuidado("Rosa (vizinha)", Telefone.Criar("11912345678"), PapelDoContato.Vizinho),
        ]);
        casa.VincularSentinela(deviceId);
        casa.AtivarMonitoramento();
        Domicilios.Adicionar(casa);
        return casa;
    }

    /// <summary>Uma medição da Sentinela agora.</summary>
    /// <param name="temperatura">A temperatura, em °C.</param>
    /// <param name="umidade">A umidade relativa, em %.</param>
    /// <param name="deviceId">A Sentinela.</param>
    /// <returns>O resultado do caso de uso.</returns>
    internal Task<LeituraRegistradaResposta> MedirAsync(double temperatura, double umidade, string deviceId = DeviceId)
        => RegistrarLeitura().ExecutarAsync(
            new NovaLeituraDto { DeviceId = deviceId, Momento = Relogio.Agora, TemperaturaC = temperatura, UmidadePct = umidade },
            CancellationToken.None);

    /// <summary>Um check-in agora.</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="origem">De onde veio.</param>
    /// <param name="situacao">O que a pessoa disse.</param>
    /// <returns>O resultado do caso de uso.</returns>
    internal Task<CheckInResposta> CheckInAsync(Guid domicilioId, OrigemDoCheckIn origem, SituacaoDoCheckIn situacao)
        => RegistrarCheckIn().ExecutarAsync(domicilioId, new NovoCheckInDto { Origem = origem, Situacao = situacao }, CancellationToken.None);
}
