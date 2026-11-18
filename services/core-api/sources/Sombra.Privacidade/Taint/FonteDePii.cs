namespace Sombra.Privacidade.Taint;

/// <summary>De onde veio o dado marcado (as fontes do taint, spec 07 §2).</summary>
public enum FonteDePii
{
    /// <summary>O corpo JSON da requisição.</summary>
    Corpo,

    /// <summary>A query string.</summary>
    Query,

    /// <summary>Um header (o <c>Authorization</c>).</summary>
    Cabecalho,
}
