using System.Collections.Generic;
using Sombra.Privacidade.Regras;

namespace Sombra.Privacidade.Deteccao;

/// <summary>Reconhece, no conteúdo de um texto, o dado descrito por uma regra.</summary>
public interface IDetectorDePii
{
    /// <summary>Gets a regra que este detector aplica.</summary>
    RegraDePii Regra { get; }

    /// <summary>Procura o dado no texto.</summary>
    /// <param name="texto">O texto, já normalizado em NFC.</param>
    /// <returns>Cada ocorrência achada, sem o valor.</returns>
    IEnumerable<OcorrenciaDePii> Detectar(string texto);
}
