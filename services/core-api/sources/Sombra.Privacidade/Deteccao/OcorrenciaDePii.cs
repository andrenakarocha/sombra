using Sombra.Privacidade.Regras;

namespace Sombra.Privacidade.Deteccao;

/// <summary>
/// Um dado pessoal achado num texto. Guarda só metadados e a posição, <b>nunca o valor</b>: pode ir para log, evento e
/// relatório sem vazar nada. A posição se refere ao texto normalizado em NFC (<see cref="MotorDePii.Normalizar"/>).
/// </summary>
/// <param name="RegraId">A regra que disparou.</param>
/// <param name="Categoria">A família do dado.</param>
/// <param name="Severidade">O peso do vazamento.</param>
/// <param name="Origem">Se veio do nome do campo ou do conteúdo.</param>
/// <param name="Inicio">O índice do primeiro caractere.</param>
/// <param name="Tamanho">Quantos caracteres o dado ocupa.</param>
public sealed record OcorrenciaDePii(
    string RegraId,
    CategoriaDePii Categoria,
    SeveridadeDePii Severidade,
    OrigemDaDeteccao Origem,
    int Inicio,
    int Tamanho)
{
    /// <summary>Gets o índice logo depois do último caractere.</summary>
    public int Fim => Inicio + Tamanho;

    /// <summary>Diz se duas ocorrências disputam o mesmo trecho.</summary>
    /// <param name="outra">A outra ocorrência.</param>
    /// <returns><c>true</c> quando os trechos se cruzam.</returns>
    public bool Sobrepoe(OcorrenciaDePii outra) => outra is not null && Inicio < outra.Fim && outra.Inicio < Fim;
}
