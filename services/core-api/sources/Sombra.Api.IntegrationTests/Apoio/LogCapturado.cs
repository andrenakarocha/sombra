using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Serilog.Core;
using Serilog.Events;
using Sombra.Api.Composicao;

namespace Sombra.Api.IntegrationTests.Apoio;

/// <summary>Guarda cada evento de log já formatado como no stdout (o mesmo template), para o teste ler as linhas.</summary>
internal sealed class LogCapturado : ILogEventSink
{
    private readonly ConcurrentQueue<string> _linhas = new();

    /// <summary>Gets as linhas, na ordem em que saíram.</summary>
    internal IReadOnlyList<string> Linhas => [.. _linhas];

    /// <summary>Gets as linhas já lidas como JSON.</summary>
    internal IReadOnlyList<JsonElement> Eventos => [.. _linhas.Select(l => JsonDocument.Parse(l).RootElement.Clone())];

    /// <inheritdoc/>
    public void Emit(LogEvent logEvent)
    {
        using StringWriter saida = new();
        LogDoSombra.Formato.Format(logEvent, saida);
        _linhas.Enqueue(saida.ToString());
    }
}
