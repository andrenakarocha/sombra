using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Sentinelas;

/// <summary>A Sentinela (ESP32) de um domicílio, identificada pelo <see cref="DeviceId"/> público do contrato MQTT.</summary>
public sealed partial class Sentinela
{
    /// <summary>De quanto em quanto tempo a Sentinela manda telemetria (contrato MQTT).</summary>
    public static readonly TimeSpan IntervaloDeTelemetria = TimeSpan.FromSeconds(10);

    /// <summary>Quantas telemetrias seguidas podem faltar antes de ela ser considerada offline (regra R9).</summary>
    public const int TelemetriasPerdidasParaOffline = 3;

    /// <summary>A diferença máxima entre o relógio da Sentinela e o do Core para aceitar uma mensagem (R10).</summary>
    public static readonly TimeSpan JanelaDeTempo = TimeSpan.FromSeconds(300);

    private Sentinela(string deviceId) => DeviceId = deviceId;

    /// <summary>Gets o identificador público, <c>snt-</c> seguido de 6 dígitos hexadecimais.</summary>
    public string DeviceId { get; }

    /// <summary>Gets o instante da última mensagem recebida, ou nulo se nunca falou.</summary>
    public DateTimeOffset? UltimoContato { get; private set; }

    /// <summary>
    /// Gets o segredo HMAC de 32 bytes, em hexadecimal, que assina as mensagens nos dois sentidos (R10). É gerado no
    /// vínculo e mostrado uma única vez; nulo numa Sentinela que ainda não recebeu segredo, cujas mensagens são recusadas.
    /// </summary>
    public string? SegredoHex { get; private set; }

    /// <summary>Gets o último <c>seq</c> aceito vindo da Sentinela (anti-replay, R10).</summary>
    public long UltimoSeqRecebido { get; private set; }

    /// <summary>Gets o último <c>seq</c> usado num comando para a Sentinela.</summary>
    public long UltimoSeqEnviado { get; private set; }

    /// <summary>Cria uma Sentinela a partir do identificador do contrato.</summary>
    /// <param name="deviceId">O identificador, ex.: <c>snt-7f3a9c</c>.</param>
    /// <returns>A Sentinela.</returns>
    /// <exception cref="RegraVioladaException">Quando o identificador não segue o contrato.</exception>
    public static Sentinela Criar(string? deviceId)
    {
        string id = (deviceId ?? string.Empty).Trim().ToLowerInvariant();
        if (!FormatoDoDeviceId().IsMatch(id))
        {
            throw new RegraVioladaException("R10", "Identificador de Sentinela inválido.");
        }

        return new Sentinela(id) { SegredoHex = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)) };
    }

    /// <summary>Troca o segredo (rotação, ou a Sentinela da demonstração, que usa o segredo do <c>.env</c>).</summary>
    /// <param name="segredoHex">Os 32 bytes em hexadecimal.</param>
    /// <exception cref="RegraVioladaException">Quando não são 64 dígitos hexadecimais.</exception>
    public void DefinirSegredo(string segredoHex)
    {
        string segredo = (segredoHex ?? string.Empty).Trim().ToLowerInvariant();
        if (!FormatoDoSegredo().IsMatch(segredo))
        {
            throw new RegraVioladaException("R10", "O segredo da Sentinela tem 32 bytes em hexadecimal.");
        }

        SegredoHex = segredo;
    }

    /// <summary>
    /// Regra R10 (anti-replay): aceita uma mensagem já com a assinatura conferida só se o <c>seq</c> for maior que o último
    /// e o <c>ts</c> estiver a no máximo 300 s do relógio do Core. Aceita, registra o contato.
    /// </summary>
    /// <param name="seq">O contador da mensagem.</param>
    /// <param name="ts">O instante informado pela Sentinela.</param>
    /// <param name="agora">O relógio do Core.</param>
    /// <returns>Por que a mensagem foi recusada, ou <see cref="RecusaDaMensagem.Nenhuma"/> quando foi aceita.</returns>
    public RecusaDaMensagem ReceberMensagem(long seq, DateTimeOffset ts, DateTimeOffset agora)
    {
        if (seq <= UltimoSeqRecebido)
        {
            return RecusaDaMensagem.Repetida;
        }

        if ((agora - ts).Duration() > JanelaDeTempo)
        {
            return RecusaDaMensagem.ForaDaJanela;
        }

        UltimoSeqRecebido = seq;
        RegistrarContato(ts);
        return RecusaDaMensagem.Nenhuma;
    }

    /// <summary>
    /// Reserva o próximo <c>seq</c> de um comando: o maior entre o último mais um e os segundos Unix de agora. Continua
    /// crescente (anti-replay da Sentinela) mesmo se o banco do Core for recriado.
    /// </summary>
    /// <param name="agora">O relógio do Core.</param>
    /// <returns>O <c>seq</c>.</returns>
    public long ProximoSeqDeComando(DateTimeOffset agora)
    {
        UltimoSeqEnviado = Math.Max(UltimoSeqEnviado + 1, agora.ToUnixTimeSeconds());
        return UltimoSeqEnviado;
    }

    /// <summary>Registra que a Sentinela deu sinal de vida.</summary>
    /// <param name="momento">O instante da mensagem.</param>
    public void RegistrarContato(DateTimeOffset momento)
    {
        if (UltimoContato is null || momento > UltimoContato)
        {
            UltimoContato = momento.ToUniversalTime();
        }
    }

    /// <summary>Regra R9: online enquanto não faltarem três telemetrias seguidas.</summary>
    /// <param name="agora">O instante de referência.</param>
    /// <returns><c>true</c> quando o último contato é recente.</returns>
    public bool EstaOnline(DateTimeOffset agora)
        => UltimoContato is { } ultimo && agora - ultimo <= IntervaloDeTelemetria * TelemetriasPerdidasParaOffline;

    [GeneratedRegex("^snt-[0-9a-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoDoDeviceId();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoDoSegredo();
}

/// <summary>Por que uma mensagem assinada da Sentinela foi recusada (R10).</summary>
public enum RecusaDaMensagem
{
    /// <summary>Aceita.</summary>
    Nenhuma = 0,

    /// <summary>O <c>seq</c> não é maior que o último aceito (replay).</summary>
    Repetida = 1,

    /// <summary>O <c>ts</c> está a mais de 300 s do relógio do Core.</summary>
    ForaDaJanela = 2,
}
