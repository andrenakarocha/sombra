using System;
using System.Collections.Generic;
using System.Linq;
using Sombra.Dominio.Casas;
using Sombra.Privacidade;

namespace Sombra.Aplicacao.Respostas;

/// <summary>Um morador, com CPF, Cartão SUS e saúde mascarados (spec 07). Os fatores saem só como código.</summary>
/// <param name="Id">O identificador.</param>
/// <param name="Nome">O nome.</param>
/// <param name="Idade">A idade em anos completos.</param>
/// <param name="Cpf">O CPF mascarado.</param>
/// <param name="CartaoSus">O Cartão SUS mascarado, quando houver.</param>
/// <param name="PerfilDeSaude">Sempre <c>[DADO DE SAÚDE PROTEGIDO]</c>.</param>
/// <param name="FatoresDeRisco">Os códigos dos fatores que valem hoje, inclusive a idade avançada.</param>
public sealed record MoradorResposta(Guid Id, string Nome, int Idade, string Cpf, string? CartaoSus, string PerfilDeSaude, IReadOnlyList<string> FatoresDeRisco)
{
    /// <summary>Monta a resposta, mascarando a PII.</summary>
    /// <param name="morador">O morador.</param>
    /// <param name="hoje">A data de referência, para a idade.</param>
    /// <returns>A resposta.</returns>
    public static MoradorResposta De(Morador morador, DateOnly hoje)
    {
        ArgumentNullException.ThrowIfNull(morador);
        return new(
            morador.Id,
            morador.Nome,
            morador.Idade(hoje),
            MascaradorDePii.MascararCpf(morador.Cpf.Valor),
            morador.CartaoSus is null ? null : MascaradorDePii.MascararCartaoSus(morador.CartaoSus.Valor),
            MascaradorDePii.SaudeProtegida,
            [.. morador.FatoresDeRisco(hoje).Select(f => f.Codigo).Order(StringComparer.Ordinal)]);
    }
}

/// <summary>Um contato da rede de cuidado, com o telefone mascarado.</summary>
/// <param name="Nome">O nome ou apelido.</param>
/// <param name="Telefone">O telefone mascarado.</param>
/// <param name="Papel">O papel no escalonamento.</param>
public sealed record ContatoResposta(string Nome, string Telefone, PapelDoContato Papel);

/// <summary>A rede de cuidado, na ordem de escalonamento.</summary>
/// <param name="Contatos">Os contatos.</param>
public sealed record RedeDeCuidadoResposta(IReadOnlyList<ContatoResposta> Contatos)
{
    /// <summary>Monta a resposta, mascarando os telefones.</summary>
    /// <param name="rede">A rede.</param>
    /// <returns>A resposta.</returns>
    public static RedeDeCuidadoResposta De(RedeDeCuidado rede)
    {
        ArgumentNullException.ThrowIfNull(rede);
        return new([.. rede.Contatos.Select(c => new ContatoResposta(c.Nome, MascaradorDePii.MascararTelefone(c.Telefone.Valor), c.Papel))]);
    }
}

/// <summary>A rotina combinada com a pessoa.</summary>
/// <param name="HorarioDoCheckInDiario">O horário local do check-in diário, ou nulo.</param>
/// <param name="LembreteDeAguaAtivo">Se o lembrete de água está ligado.</param>
public sealed record RotinaResposta(TimeOnly? HorarioDoCheckInDiario, bool LembreteDeAguaAtivo)
{
    /// <summary>Monta a resposta a partir da rotina.</summary>
    /// <param name="rotina">A rotina.</param>
    /// <returns>A resposta.</returns>
    public static RotinaResposta De(Rotina rotina)
    {
        ArgumentNullException.ThrowIfNull(rotina);
        return new(rotina.HorarioDoCheckInDiario, rotina.LembreteDeAguaAtivo);
    }
}
