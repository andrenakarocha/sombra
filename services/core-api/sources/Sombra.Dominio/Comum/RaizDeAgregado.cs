using System;
using System.Collections.Generic;

namespace Sombra.Dominio.Comum;

/// <summary>Um fato do domínio que outras partes do sistema precisam saber (SignalR, outbox, MQTT).</summary>
/// <param name="Momento">Quando o fato aconteceu.</param>
public abstract record EventoDeDominio(DateTimeOffset Momento);

/// <summary>Base das raízes de agregado: guarda os eventos de domínio até a aplicação publicá-los.</summary>
public abstract class RaizDeAgregado
{
    private readonly List<EventoDeDominio> _eventos = [];

    /// <summary>Gets os eventos ainda não publicados, na ordem em que aconteceram.</summary>
    public IReadOnlyList<EventoDeDominio> EventosDeDominio => _eventos;

    /// <summary>Esquece os eventos depois que a aplicação os publicou.</summary>
    public void LimparEventos() => _eventos.Clear();

    /// <summary>Guarda um evento para ser publicado.</summary>
    /// <param name="evento">O evento.</param>
    protected void Registrar(EventoDeDominio evento) => _eventos.Add(evento);
}
