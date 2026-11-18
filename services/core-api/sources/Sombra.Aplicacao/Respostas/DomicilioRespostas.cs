using System;
using System.Collections.Generic;
using System.Linq;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Sentinelas;

namespace Sombra.Aplicacao.Respostas;

/// <summary>A casa, sem o endereço completo: só o bairro sai da API.</summary>
/// <param name="Id">O identificador.</param>
/// <param name="Apelido">Como a família chama a casa.</param>
/// <param name="Bairro">O bairro.</param>
/// <param name="RegiaoClimatica">O código da região climática.</param>
/// <param name="Monitorado">Se o monitoramento está ligado.</param>
public sealed record DomicilioResposta(Guid Id, string Apelido, string Bairro, string RegiaoClimatica, bool Monitorado)
{
    /// <summary>Monta a resposta a partir da casa.</summary>
    /// <param name="casa">A casa.</param>
    /// <returns>A resposta.</returns>
    public static DomicilioResposta De(Domicilio casa)
    {
        ArgumentNullException.ThrowIfNull(casa);
        return new(casa.Id, casa.Apelido, casa.Endereco.Bairro, casa.RegiaoClimatica, casa.Monitorado);
    }
}

/// <summary>A visão da casa para o app e o painel (contrato <c>GET /domicilios/{id}</c>).</summary>
/// <param name="Id">O identificador.</param>
/// <param name="Apelido">Como a família chama a casa.</param>
/// <param name="Bairro">O bairro.</param>
/// <param name="RegiaoClimatica">O código da região climática.</param>
/// <param name="Monitorado">Se o monitoramento está ligado.</param>
/// <param name="NivelAtual">O nível da última leitura, ou normal quando ainda não há leitura.</param>
/// <param name="UltimaLeitura">A última leitura da Sentinela.</param>
/// <param name="Sentinela">A Sentinela vinculada.</param>
/// <param name="UltimoCheckIn">Quando foi o último check-in.</param>
/// <param name="AlertaAberto">O alerta aberto mais importante (o que pede resposta vem antes do preventivo).</param>
public sealed record DomicilioDetalheResposta(
    Guid Id,
    string Apelido,
    string Bairro,
    string RegiaoClimatica,
    bool Monitorado,
    NivelDeRisco NivelAtual,
    LeituraResposta? UltimaLeitura,
    SentinelaResposta? Sentinela,
    DateTimeOffset? UltimoCheckIn,
    AlertaResposta? AlertaAberto)
{
    /// <summary>Monta a visão a partir da casa e do que aconteceu nela por último.</summary>
    /// <param name="casa">A casa.</param>
    /// <param name="ultimaLeitura">A última leitura, se houver.</param>
    /// <param name="ultimoCheckIn">Quando foi o último check-in, se houver.</param>
    /// <param name="alerta">O alerta aberto, se houver.</param>
    /// <param name="agora">O instante de referência, para saber se a Sentinela está online.</param>
    /// <returns>A visão.</returns>
    public static DomicilioDetalheResposta De(Domicilio casa, Leitura? ultimaLeitura, DateTimeOffset? ultimoCheckIn, AlertaDeCalor? alerta, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(casa);
        Sentinela? sentinela = casa.Sentinelas.Count > 0 ? casa.Sentinelas[0] : null;
        return new(
            casa.Id,
            casa.Apelido,
            casa.Endereco.Bairro,
            casa.RegiaoClimatica,
            casa.Monitorado,
            ultimaLeitura?.Nivel ?? NivelDeRisco.Normal,
            ultimaLeitura is null ? null : LeituraResposta.De(ultimaLeitura),
            sentinela is null ? null : SentinelaResposta.De(sentinela, agora),
            ultimoCheckIn,
            alerta is null ? null : AlertaResposta.De(alerta, casa.Apelido));
    }

    /// <summary>O alerta que a casa mostra: o que pede resposta vem antes do preventivo.</summary>
    /// <param name="abertos">Os alertas abertos da casa, na ordem de abertura.</param>
    /// <returns>O alerta, ou nulo quando não há nenhum aberto.</returns>
    public static AlertaDeCalor? AlertaQueAparece(IReadOnlyList<AlertaDeCalor> abertos)
    {
        ArgumentNullException.ThrowIfNull(abertos);
        return abertos.FirstOrDefault(a => a is not AlertaPreventivo) ?? (abertos.Count > 0 ? abertos[0] : null);
    }
}

/// <summary>A Sentinela e se ela está falando com o Core (R9).</summary>
/// <param name="DeviceId">O identificador público.</param>
/// <param name="Online">Se o último contato é recente.</param>
public sealed record SentinelaResposta(string DeviceId, bool Online)
{
    /// <summary>Monta a resposta a partir da Sentinela.</summary>
    /// <param name="sentinela">A Sentinela.</param>
    /// <param name="agora">O instante de referência.</param>
    /// <returns>A resposta.</returns>
    public static SentinelaResposta De(Sentinela sentinela, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(sentinela);
        return new(sentinela.DeviceId, sentinela.EstaOnline(agora));
    }
}

/// <summary>
/// A Sentinela recém-vinculada, com o segredo HMAC que assina as mensagens (contrato MQTT). O segredo aparece só nesta
/// resposta: grave-o na Sentinela.
/// </summary>
/// <param name="DeviceId">O identificador público.</param>
/// <param name="Online">Se já está falando com o Core.</param>
/// <param name="SegredoHex">Os 32 bytes do segredo, em hexadecimal.</param>
public sealed record SentinelaVinculadaResposta(string DeviceId, bool Online, string SegredoHex);

/// <summary>Uma leitura da Sentinela, com o índice de calor calculado pelo Core.</summary>
/// <param name="Momento">O instante da medição, em UTC.</param>
/// <param name="TemperaturaC">A temperatura, em °C.</param>
/// <param name="UmidadePct">A umidade relativa, em %.</param>
/// <param name="IndiceDeCalorC">O índice de calor, em °C, com duas casas.</param>
/// <param name="Nivel">O nível pelos limites pessoais da casa.</param>
public sealed record LeituraResposta(DateTimeOffset Momento, double TemperaturaC, double UmidadePct, double IndiceDeCalorC, NivelDeRisco Nivel)
{
    /// <summary>Monta a resposta a partir da leitura.</summary>
    /// <param name="leitura">A leitura.</param>
    /// <returns>A resposta.</returns>
    public static LeituraResposta De(Leitura leitura)
    {
        ArgumentNullException.ThrowIfNull(leitura);
        return new(leitura.Momento, leitura.Temperatura.Celsius, leitura.Umidade.Percentual, Math.Round(leitura.IndiceDeCalor.Celsius, 2), leitura.Nivel);
    }
}
