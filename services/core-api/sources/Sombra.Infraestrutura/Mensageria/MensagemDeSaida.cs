using System;
using Sombra.Aplicacao.Portas;

namespace Sombra.Infraestrutura.Mensageria;

/// <summary>
/// Um aviso no outbox: gravado na mesma transação do caso de uso e entregue depois pelo publicador, que tenta de novo
/// quando um canal falha. Guarda só IDs, o destinatário, o motivo, a mensagem (sem dado pessoal) e quantos contatos
/// recebem; telefones e nomes ficam na casa.
/// </summary>
public sealed class MensagemDeSaida
{
    /// <summary>Quantas vezes o publicador tenta antes de desistir.</summary>
    public const int MaximoDeTentativas = 10;

    private MensagemDeSaida(Guid id, DateTimeOffset criadaEm, Guid domicilioId, Guid? alertaId, Destinatario destinatario, MotivoDoAviso motivo, string mensagem, int contatos, string? correlationId)
    {
        Id = id;
        CriadaEm = criadaEm;
        DomicilioId = domicilioId;
        AlertaId = alertaId;
        Destinatario = destinatario;
        Motivo = motivo;
        Mensagem = mensagem;
        Contatos = contatos;
        CorrelationId = correlationId;
    }

    /// <summary>Gets o identificador.</summary>
    public Guid Id { get; }

    /// <summary>Gets quando entrou no outbox.</summary>
    public DateTimeOffset CriadaEm { get; }

    /// <summary>Gets a casa.</summary>
    public Guid DomicilioId { get; }

    /// <summary>Gets o alerta, quando há.</summary>
    public Guid? AlertaId { get; }

    /// <summary>Gets para quem vai.</summary>
    public Destinatario Destinatario { get; }

    /// <summary>Gets por que foi enviado.</summary>
    public MotivoDoAviso Motivo { get; }

    /// <summary>Gets o texto.</summary>
    public string Mensagem { get; }

    /// <summary>Gets quantos contatos recebem (zero para o morador).</summary>
    public int Contatos { get; }

    /// <summary>Gets a correlação de quem gerou o aviso.</summary>
    public string? CorrelationId { get; }

    /// <summary>Gets quando foi entregue a todos os canais.</summary>
    public DateTimeOffset? PublicadaEm { get; private set; }

    /// <summary>Gets quantas entregas falharam.</summary>
    public int Tentativas { get; private set; }

    /// <summary>Gets o tipo da última falha.</summary>
    public string? UltimaFalha { get; private set; }

    /// <summary>Põe um aviso no outbox.</summary>
    /// <param name="notificacao">O aviso.</param>
    /// <param name="momento">Agora.</param>
    /// <param name="correlationId">A correlação atual.</param>
    /// <returns>A mensagem.</returns>
    public static MensagemDeSaida De(Notificacao notificacao, DateTimeOffset momento, string? correlationId)
    {
        ArgumentNullException.ThrowIfNull(notificacao);
        return new(Guid.CreateVersion7(), momento.ToUniversalTime(), notificacao.DomicilioId, notificacao.AlertaId, notificacao.Destinatario, notificacao.Motivo, notificacao.Mensagem, notificacao.Contatos.Count, correlationId);
    }

    /// <summary>Marca como entregue.</summary>
    /// <param name="momento">Quando.</param>
    public void MarcarPublicada(DateTimeOffset momento) => PublicadaEm = momento.ToUniversalTime();

    /// <summary>Registra uma entrega que falhou.</summary>
    /// <param name="falha">O tipo da falha (nunca a mensagem da exceção, que pode ter dado).</param>
    public void RegistrarFalha(string falha)
    {
        Tentativas++;
        UltimaFalha = falha;
    }
}
