using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Pessoas;
using Sombra.Dominio.Saude;
using Sombra.Dominio.Tests.Apoio;

namespace Sombra.Infraestrutura.Tests.Apoio;

/// <summary>As casas do seed e uma fábrica de casas de teste (dados fictícios).</summary>
internal static class Casas
{
    /// <summary>A casa da Dona Lurdes no seed (Santana, Sentinela <c>snt-7f3a9c</c>).</summary>
    internal static readonly Guid Lurdes = new("0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10");

    /// <summary>A casa do Seu Joaquim no seed (Vila Maria, Sentinela <c>snt-2b81d4</c>, só um familiar na rede).</summary>
    internal static readonly Guid Joaquim = new("4e2b7c90-58a1-4d3f-9b6e-1a7c2d9e8f31");

    /// <summary>A casa da Dona Benedita no seed (Tucuruvi, sem monitoramento).</summary>
    internal static readonly Guid Benedita = new("9c4d1e72-3b5a-4f8e-a2d6-7e1b9c0f4a52");

    /// <summary>O instante de referência: 18/11/2026 às 18h UTC (15h em São Paulo).</summary>
    internal static readonly DateTimeOffset Agora = new(2026, 11, 18, 18, 0, 0, TimeSpan.Zero);

    /// <summary>Uma casa monitorada com um morador de 82 anos com cardiopatia, filha, vizinha e rotina.</summary>
    /// <param name="deviceId">A Sentinela.</param>
    /// <returns>A casa, ainda fora do banco.</returns>
    internal static Domicilio Monitorada(string deviceId)
    {
        DateOnly hoje = DateOnly.FromDateTime(Agora.UtcDateTime);
        Domicilio casa = Domicilio.Cadastrar("Casa de teste", Endereco.Criar("Rua A", "1", "Santana", "São Paulo", "SP", "02012-000"), "SP-MIRANTE");
        casa.AdicionarMorador(Morador.Cadastrar(
            "Pessoa T.",
            hoje.AddYears(-82),
            Cpf.Criar(DocumentosSinteticos.Cpf(41)),
            CartaoSus.Criar(DocumentosSinteticos.CartaoSus(41)),
            PerfilDeSaude.Com([new Cardiopatia()]),
            hoje));
        casa.DefinirRedeDeCuidado(
        [
            new ContatoDeCuidado("Ana (filha)", Telefone.Criar("11987654321"), PapelDoContato.Familiar),
            new ContatoDeCuidado("Rosa (vizinha)", Telefone.Criar("11912345678"), PapelDoContato.Vizinho),
        ]);
        casa.DefinirRotina(Rotina.Criar(new TimeOnly(9, 0), lembreteDeAguaAtivo: true));
        casa.VincularSentinela(deviceId);
        casa.AtivarMonitoramento();
        return casa;
    }
}

/// <summary>Um relógio parado, que o teste avança na mão.</summary>
internal sealed class RelogioFixo : IRelogio
{
    /// <summary>Initializes a new instance of the <see cref="RelogioFixo"/> class.</summary>
    /// <param name="agora">O instante inicial.</param>
    internal RelogioFixo(DateTimeOffset agora) => Agora = agora;

    /// <inheritdoc/>
    public DateTimeOffset Agora { get; private set; }

    /// <summary>Anda com o relógio.</summary>
    /// <param name="quanto">Quanto tempo.</param>
    internal void Avancar(TimeSpan quanto) => Agora += quanto;
}

/// <summary>Guarda os avisos em vez de entregar (o outbox chega na F1.9).</summary>
internal sealed class NotificadorGravado : INotificador
{
    /// <summary>Gets os avisos, na ordem em que foram registrados.</summary>
    internal List<Notificacao> Avisos { get; } = [];

    /// <inheritdoc/>
    public Task NotificarAsync(Notificacao notificacao, CancellationToken cancellationToken)
    {
        Avisos.Add(notificacao);
        return Task.CompletedTask;
    }
}
