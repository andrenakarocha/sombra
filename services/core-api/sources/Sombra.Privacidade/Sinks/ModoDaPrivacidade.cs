using System;

namespace Sombra.Privacidade.Sinks;

/// <summary>
/// Se os guardas bloqueiam (<c>Privacidade:Aplicacao=Ligada</c>, o padrão) ou só registram (<c>Desligada</c>): o modo
/// inseguro da auditoria antes × depois (spec 07 §5). Desligado, cada guarda continua detectando, deixa o dado passar e
/// grava o evento como vazado. Só vale no ambiente <c>Demo</c>; em qualquer outro, o Core não sobe.
/// </summary>
/// <param name="Bloqueia">Se os guardas bloqueiam.</param>
public sealed record ModoDaPrivacidade(bool Bloqueia)
{
    /// <summary>O ambiente único em que o modo inseguro é aceito.</summary>
    public const string AmbienteDaDemo = "Demo";

    /// <summary>Gets o modo normal: os guardas bloqueiam.</summary>
    public static ModoDaPrivacidade Ligada { get; } = new(true);

    /// <summary>Lê o modo da configuração.</summary>
    /// <param name="valor">O valor de <c>Privacidade:Aplicacao</c> (vazio é <c>Ligada</c>).</param>
    /// <param name="ambiente">O ambiente do host.</param>
    /// <returns>O modo.</returns>
    /// <exception cref="InvalidOperationException">Valor desconhecido, ou <c>Desligada</c> fora do ambiente <c>Demo</c>.</exception>
    public static ModoDaPrivacidade De(string? valor, string ambiente)
    {
        if (string.IsNullOrWhiteSpace(valor) || valor.Equals("Ligada", StringComparison.OrdinalIgnoreCase))
        {
            return Ligada;
        }

        if (!valor.Equals("Desligada", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Privacidade:Aplicacao='{valor}' não existe: use Ligada ou Desligada.");
        }

        return string.Equals(ambiente, AmbienteDaDemo, StringComparison.Ordinal)
            ? new ModoDaPrivacidade(false)
            : throw new InvalidOperationException($"Privacidade:Aplicacao=Desligada só vale no ambiente {AmbienteDaDemo} (este é {ambiente}): é o modo inseguro da auditoria.");
    }
}
