using System;

namespace Sombra.Aplicacao.Portas;

/// <summary>O relógio do sistema, injetável para os testes e para acelerar o tempo na demonstração (regra R6).</summary>
public interface IRelogio
{
    /// <summary>Gets o instante atual.</summary>
    DateTimeOffset Agora { get; }
}
