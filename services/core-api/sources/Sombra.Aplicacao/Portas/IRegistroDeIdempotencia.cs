using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sombra.Aplicacao.Portas;

/// <summary>Uma resposta já dada para uma <c>Idempotency-Key</c>.</summary>
/// <param name="Rota">O método e a rota da primeira requisição.</param>
/// <param name="Status">O status HTTP da resposta; nulo enquanto a primeira requisição ainda está rodando.</param>
/// <param name="Corpo">O corpo JSON da resposta.</param>
public sealed record RespostaGuardada(string Rota, int? Status, string? Corpo);

/// <summary>
/// Guarda as respostas das escritas que o app pode reenviar (check-in e reconhecer, F6.23): a mesma chave devolve a
/// mesma resposta, sem repetir o efeito, por 24 horas.
/// </summary>
public interface IRegistroDeIdempotencia
{
    /// <summary>A resposta guardada para a chave deste usuário, se ainda valer.</summary>
    /// <param name="usuario">Quem fez a requisição.</param>
    /// <param name="chave">A <c>Idempotency-Key</c>.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>A resposta, ou nulo.</returns>
    Task<RespostaGuardada?> ObterAsync(Guid usuario, string chave, CancellationToken cancellationToken);

    /// <summary>Reserva a chave antes de executar; só uma requisição consegue.</summary>
    /// <param name="usuario">Quem fez a requisição.</param>
    /// <param name="chave">A <c>Idempotency-Key</c>.</param>
    /// <param name="rota">O método e a rota.</param>
    /// <param name="cancellationToken">Cancela a gravação.</param>
    /// <returns><c>true</c> quando esta requisição ficou com a chave.</returns>
    Task<bool> ReservarAsync(Guid usuario, string chave, string rota, CancellationToken cancellationToken);

    /// <summary>Guarda a resposta de sucesso para as próximas tentativas.</summary>
    /// <param name="usuario">Quem fez a requisição.</param>
    /// <param name="chave">A <c>Idempotency-Key</c>.</param>
    /// <param name="status">O status HTTP.</param>
    /// <param name="corpo">O corpo JSON.</param>
    /// <param name="cancellationToken">Cancela a gravação.</param>
    /// <returns>A gravação.</returns>
    Task ConcluirAsync(Guid usuario, string chave, int status, string corpo, CancellationToken cancellationToken);

    /// <summary>Solta a chave de uma requisição que falhou, para a próxima tentativa executar de novo.</summary>
    /// <param name="usuario">Quem fez a requisição.</param>
    /// <param name="chave">A <c>Idempotency-Key</c>.</param>
    /// <param name="cancellationToken">Cancela a gravação.</param>
    /// <returns>A remoção.</returns>
    Task LiberarAsync(Guid usuario, string chave, CancellationToken cancellationToken);
}
