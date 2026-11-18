namespace Sombra.Privacidade.Sinks;

/// <summary>Recebe cada vazamento bloqueado. Precisa ser rápido e não logar direto: é chamado de dentro dos sinks.</summary>
public interface IObservadorDeVazamentos
{
    /// <summary>Registra um vazamento bloqueado.</summary>
    /// <param name="evento">O evento, só com metadados.</param>
    void Registrar(VazamentoBloqueado evento);
}
