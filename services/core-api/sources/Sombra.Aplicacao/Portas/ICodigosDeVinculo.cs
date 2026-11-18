namespace Sombra.Aplicacao.Portas;

/// <summary>Confere o código de vínculo que a Sentinela mostra no QR, com a chave que só o servidor tem.</summary>
public interface ICodigosDeVinculo
{
    /// <summary>Diz se o código é o desta Sentinela.</summary>
    /// <param name="deviceId">O identificador público da Sentinela.</param>
    /// <param name="codigo">O código lido no QR ou digitado.</param>
    /// <returns><c>true</c> quando confere.</returns>
    bool Confere(string deviceId, string codigo);
}
