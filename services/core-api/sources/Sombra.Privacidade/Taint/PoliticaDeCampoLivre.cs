namespace Sombra.Privacidade.Taint;

/// <summary>O que fazer com dado pessoal fora do lugar, num campo de texto livre (configuração <c>Privacidade:CampoLivre</c>).</summary>
public enum PoliticaDeCampoLivre
{
    /// <summary>Mascara o trecho e segue (padrão): o dado nunca chega ao banco nem ao log.</summary>
    Mascarar,

    /// <summary>Recusa a requisição com 422, dizendo o campo e a família, nunca o valor.</summary>
    Recusar,
}
