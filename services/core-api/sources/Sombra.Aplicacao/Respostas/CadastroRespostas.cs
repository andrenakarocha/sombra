using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Pessoas;
using Sombra.Privacidade;

namespace Sombra.Aplicacao.Respostas;

/// <summary>
/// Um morador, com CPF, Cartão SUS e saúde sempre mascarados (spec 07). O telefone sai inteiro só para o Cuidador da
/// própria casa, que precisa ligar. Os fatores saem só como código.
/// </summary>
/// <param name="Id">O identificador.</param>
/// <param name="Nome">O nome.</param>
/// <param name="Idade">A idade em anos completos.</param>
/// <param name="Telefone">O telefone da pessoa, quando houver.</param>
/// <param name="Cpf">O CPF mascarado.</param>
/// <param name="CartaoSus">O Cartão SUS mascarado, quando houver.</param>
/// <param name="PerfilDeSaude">Sempre <c>[DADO DE SAÚDE PROTEGIDO]</c>.</param>
/// <param name="FatoresDeRisco">Os códigos dos fatores que valem hoje, inclusive a idade avançada.</param>
public sealed record MoradorResposta(Guid Id, string Nome, int Idade, string? Telefone, string Cpf, string? CartaoSus, string PerfilDeSaude, IReadOnlyList<string> FatoresDeRisco)
{
    /// <summary>Monta a resposta, mascarando a PII.</summary>
    /// <param name="morador">O morador.</param>
    /// <param name="hoje">A data de referência, para a idade.</param>
    /// <param name="revelarTelefone">Se quem pede é o Cuidador da casa.</param>
    /// <returns>A resposta.</returns>
    public static MoradorResposta De(Morador morador, DateOnly hoje, bool revelarTelefone)
    {
        ArgumentNullException.ThrowIfNull(morador);
        return new(
            morador.Id,
            morador.Nome,
            morador.Idade(hoje),
            morador.Telefone is null ? null : TelefoneNaResposta.De(morador.Telefone, revelarTelefone),
            MascaradorDePii.MascararCpf(morador.Cpf.Valor),
            morador.CartaoSus is null ? null : MascaradorDePii.MascararCartaoSus(morador.CartaoSus.Valor),
            MascaradorDePii.SaudeProtegida,
            [.. morador.FatoresDeRisco(hoje).Select(f => f.Codigo).Order(StringComparer.Ordinal)]);
    }
}

/// <summary>Um contato da rede de cuidado, na ordem de escalonamento.</summary>
/// <param name="Id">Identifica o contato dentro da casa (a casa e a posição na rede).</param>
/// <param name="Nome">O nome ou apelido.</param>
/// <param name="Papel">O papel no escalonamento.</param>
/// <param name="Telefone">O telefone: inteiro para o Cuidador da casa, que liga dali; mascarado para os outros papéis.</param>
public sealed record ContatoResposta(string Id, string Nome, PapelDoContato Papel, string Telefone)
{
    /// <summary>Monta a rede como o app lê: uma lista, na ordem de escalonamento.</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="rede">A rede.</param>
    /// <param name="revelarTelefones">Se quem pede é o Cuidador da casa.</param>
    /// <returns>Os contatos.</returns>
    public static IReadOnlyList<ContatoResposta> DaRede(Guid domicilioId, RedeDeCuidado rede, bool revelarTelefones)
    {
        ArgumentNullException.ThrowIfNull(rede);
        return [.. rede.Contatos.Select((c, i) => new ContatoResposta(
            string.Create(CultureInfo.InvariantCulture, $"{domicilioId:N}-{i + 1}"),
            c.Nome,
            c.Papel,
            TelefoneNaResposta.De(c.Telefone, revelarTelefones)))];
    }
}

/// <summary>Como um telefone sai na API: inteiro para o Cuidador da casa, que liga dali; mascarado para os outros.</summary>
public static class TelefoneNaResposta
{
    /// <summary>O telefone formatado para quem pode ligar, ou mascarado.</summary>
    /// <param name="telefone">O telefone.</param>
    /// <param name="revelar">Se quem pede é o Cuidador da casa.</param>
    /// <returns>O texto do telefone.</returns>
    public static string De(Telefone telefone, bool revelar)
    {
        ArgumentNullException.ThrowIfNull(telefone);
        return revelar ? telefone.Formatado : MascaradorDePii.MascararTelefone(telefone.Valor);
    }
}

/// <summary>O check-in diário combinado com a pessoa.</summary>
/// <param name="Ativo">Se o check-in diário está ligado.</param>
/// <param name="Horario">O horário local, <c>HH:mm</c>.</param>
public sealed record CheckInDiarioResposta(bool Ativo, string Horario);

/// <summary>O lembrete de água no pico da tarde (R11).</summary>
/// <param name="Ativo">Se o lembrete está ligado.</param>
public sealed record LembreteDeAguaResposta(bool Ativo);

/// <summary>A rotina combinada com a pessoa, no formato do app (D17).</summary>
/// <param name="CheckInDiario">O check-in diário.</param>
/// <param name="LembreteDeAgua">O lembrete de água.</param>
public sealed record RotinaResposta(CheckInDiarioResposta CheckInDiario, LembreteDeAguaResposta LembreteDeAgua)
{
    /// <summary>Monta a resposta a partir da rotina.</summary>
    /// <param name="rotina">A rotina.</param>
    /// <returns>A resposta.</returns>
    public static RotinaResposta De(Rotina rotina)
    {
        ArgumentNullException.ThrowIfNull(rotina);
        return new(
            new CheckInDiarioResposta(rotina.CheckInDiarioAtivo, rotina.HorarioEscolhido.ToString("HH:mm", CultureInfo.InvariantCulture)),
            new LembreteDeAguaResposta(rotina.LembreteDeAguaAtivo));
    }
}
