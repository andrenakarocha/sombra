namespace Sombra.Privacidade.Taint;

/// <summary>O que o módulo fez com o dado achado na entrada.</summary>
public enum DesfechoDaDeteccao
{
    /// <summary>Era o dado esperado do campo (o CPF no campo <c>cpf</c>): segue marcado pelo taint até os sinks.</summary>
    Marcado,

    /// <summary>Estava fora do lugar, num campo livre, e foi mascarado antes de gravar e logar.</summary>
    Mascarado,

    /// <summary>Estava fora do lugar e a requisição foi recusada (422).</summary>
    Recusado,

    /// <summary>Estava fora do lugar e passou como veio (modo inseguro da auditoria).</summary>
    Registrado,
}
