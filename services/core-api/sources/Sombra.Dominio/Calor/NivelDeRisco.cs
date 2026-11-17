namespace Sombra.Dominio.Calor;

/// <summary>Os cinco níveis de calor do contrato (docs/contratos/niveis-de-risco.md), do mais leve ao mais grave.</summary>
public enum NivelDeRisco
{
    /// <summary>Índice de calor abaixo de 26,67 °C: "Tudo bem por aqui."</summary>
    Normal = 0,

    /// <summary>De 26,67 a menos de 32,22 °C: "Está quente. Beba água."</summary>
    Atencao = 1,

    /// <summary>De 32,22 a menos de 39,44 °C: "Calor forte. Fique na sombra e beba água."</summary>
    CautelaExtrema = 2,

    /// <summary>De 39,44 a menos de 51,67 °C: "Calor perigoso. Aperte o botão verde se você está bem."</summary>
    Perigo = 3,

    /// <summary>A partir de 51,67 °C: "Calor extremo. Peça ajuda."</summary>
    PerigoExtremo = 4,
}
