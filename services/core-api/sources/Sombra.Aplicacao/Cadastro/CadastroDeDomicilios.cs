using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;
using Sombra.Dominio.Pessoas;
using Sombra.Dominio.Saude;
using Sombra.Dominio.Sentinelas;

namespace Sombra.Aplicacao.Cadastro;

/// <summary>
/// O cadastro da casa e do que é dela: moradores, rede de cuidado, rotina, Sentinela e monitoramento. Cada operação
/// carrega o agregado, chama o método de negócio do domínio e grava; as regras ficam no domínio.
/// </summary>
public sealed class CadastroDeDomicilios
{
    private readonly IDomicilioRepository _domicilios;
    private readonly IAlertaRepository _alertas;
    private readonly ILeituraRepository _leituras;
    private readonly ICheckInRepository _checkIns;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;
    private readonly ICodigosDeVinculo _codigosDeVinculo;

    /// <summary>Initializes a new instance of the <see cref="CadastroDeDomicilios"/> class.</summary>
    /// <param name="domicilios">As casas.</param>
    /// <param name="alertas">Os alertas.</param>
    /// <param name="leituras">As leituras.</param>
    /// <param name="checkIns">Os check-ins.</param>
    /// <param name="relogio">O relógio.</param>
    /// <param name="unidadeDeTrabalho">A transação.</param>
    /// <param name="codigosDeVinculo">Confere o código do QR da Sentinela.</param>
    public CadastroDeDomicilios(IDomicilioRepository domicilios, IAlertaRepository alertas, ILeituraRepository leituras, ICheckInRepository checkIns, IRelogio relogio, IUnitOfWork unidadeDeTrabalho, ICodigosDeVinculo codigosDeVinculo)
    {
        _domicilios = domicilios ?? throw new ArgumentNullException(nameof(domicilios));
        _alertas = alertas ?? throw new ArgumentNullException(nameof(alertas));
        _leituras = leituras ?? throw new ArgumentNullException(nameof(leituras));
        _checkIns = checkIns ?? throw new ArgumentNullException(nameof(checkIns));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _unidadeDeTrabalho = unidadeDeTrabalho ?? throw new ArgumentNullException(nameof(unidadeDeTrabalho));
        _codigosDeVinculo = codigosDeVinculo ?? throw new ArgumentNullException(nameof(codigosDeVinculo));
    }

    private DateOnly Hoje => DateOnly.FromDateTime(_relogio.Agora.UtcDateTime);

    /// <summary>Cadastra uma casa, ainda sem monitoramento.</summary>
    /// <param name="dto">O apelido, o endereço e a região.</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>A casa criada.</returns>
    public async Task<DomicilioResposta> CadastrarAsync(NovoDomicilioDto dto, CancellationToken cancellationToken)
    {
        Validacao.Garantir(dto);
        Domicilio casa = Domicilio.Cadastrar(
            dto.Apelido,
            Endereco.Criar(dto.Logradouro, dto.Numero, dto.Bairro, dto.Cidade, dto.Uf, dto.Cep),
            dto.RegiaoClimatica);
        _domicilios.Adicionar(casa);
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return DomicilioResposta.De(casa);
    }

    /// <summary>A visão da casa: última leitura, Sentinela, último check-in e o alerta aberto mais importante.</summary>
    /// <param name="id">A casa.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>A visão.</returns>
    public async Task<DomicilioDetalheResposta> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        Domicilio casa = await CasaAsync(id, cancellationToken);
        Leitura? ultimaLeitura = await _leituras.ObterUltimaAsync(casa.Id, cancellationToken);
        CheckIn? ultimoCheckIn = await _checkIns.ObterUltimoAsync(casa.Id, cancellationToken);
        IReadOnlyList<AlertaDeCalor> abertos = await _alertas.ListarAbertosDaCasaAsync(casa.Id, cancellationToken);
        return DomicilioDetalheResposta.De(casa, ultimaLeitura, ultimoCheckIn?.Momento, DomicilioDetalheResposta.AlertaQueAparece(abertos), _relogio.Agora);
    }

    /// <summary>Corrige o apelido e o endereço da casa.</summary>
    /// <param name="id">A casa.</param>
    /// <param name="dto">O apelido e o endereço novos.</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>A casa.</returns>
    public async Task<DomicilioResposta> AtualizarAsync(Guid id, AtualizacaoDeDomicilioDto dto, CancellationToken cancellationToken)
    {
        Validacao.Garantir(dto);
        Domicilio casa = await CasaAsync(id, cancellationToken);
        casa.AtualizarCadastro(dto.Apelido, Endereco.Criar(dto.Logradouro, dto.Numero, dto.Bairro, dto.Cidade, dto.Uf, dto.Cep));
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return DomicilioResposta.De(casa);
    }

    /// <summary>Remove a casa.</summary>
    /// <param name="id">A casa.</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>A remoção.</returns>
    public async Task RemoverAsync(Guid id, CancellationToken cancellationToken)
    {
        _domicilios.Remover(await CasaAsync(id, cancellationToken));
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
    }

    /// <summary>Lista os moradores, com CPF, Cartão SUS e saúde mascarados.</summary>
    /// <param name="id">A casa.</param>
    /// <param name="revelarContatos">Se quem pede é o Cuidador da casa (telefones inteiros, para ligar).</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>Os moradores.</returns>
    public async Task<IReadOnlyList<MoradorResposta>> ListarMoradoresAsync(Guid id, bool revelarContatos, CancellationToken cancellationToken)
    {
        Domicilio casa = await CasaAsync(id, cancellationToken);
        DateOnly hoje = Hoje;
        return [.. casa.Moradores.Select(m => MoradorResposta.De(m, hoje, revelarContatos))];
    }

    /// <summary>Cadastra um morador na casa (R1: CPF e Cartão SUS válidos e sem CPF repetido).</summary>
    /// <param name="id">A casa.</param>
    /// <param name="dto">Os dados do morador.</param>
    /// <param name="revelarContatos">Se quem pede é o Cuidador da casa (telefones inteiros, para ligar).</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>O morador, com a PII mascarada.</returns>
    public async Task<MoradorResposta> AdicionarMoradorAsync(Guid id, NovoMoradorDto dto, bool revelarContatos, CancellationToken cancellationToken)
    {
        Validacao.Garantir(dto);
        Domicilio casa = await CasaAsync(id, cancellationToken);
        DateOnly hoje = Hoje;
        Morador morador = Morador.Cadastrar(
            dto.Nome,
            NascimentoPelaIdade(dto.Idade.GetValueOrDefault(), hoje),
            Cpf.Criar(dto.Cpf),
            CartaoSus.Criar(dto.CartaoSus),
            PerfilDeSaude.Com(dto.FatoresDeRisco.Select(FatorDeRisco.DoCodigo)),
            hoje);
        morador.DefinirTelefone(string.IsNullOrWhiteSpace(dto.Telefone) ? null : Telefone.Criar(dto.Telefone));
        morador.DefinirObservacoesDeSaude(dto.PerfilDeSaude);
        casa.AdicionarMorador(morador);
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return MoradorResposta.De(morador, hoje, revelarContatos);
    }

    /// <summary>Um morador, com a PII mascarada.</summary>
    /// <param name="id">A casa.</param>
    /// <param name="moradorId">O morador.</param>
    /// <param name="revelarContatos">Se quem pede é o Cuidador da casa (telefones inteiros, para ligar).</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>O morador.</returns>
    public async Task<MoradorResposta> ObterMoradorAsync(Guid id, Guid moradorId, bool revelarContatos, CancellationToken cancellationToken)
        => MoradorResposta.De(MoradorDa(await CasaAsync(id, cancellationToken), moradorId), Hoje, revelarContatos);

    /// <summary>
    /// Edita um morador como o app manda (D17): nome, idade e fatores sempre; CPF, Cartão SUS, telefone e saúde só quando
    /// vêm preenchidos. Os fatores mudam os limites térmicos da casa (R3); o CPF segue sem repetir na casa (R1).
    /// </summary>
    /// <param name="id">A casa.</param>
    /// <param name="moradorId">O morador.</param>
    /// <param name="dto">O que mudou.</param>
    /// <param name="revelarContatos">Se quem pede é o Cuidador da casa (telefones inteiros, para ligar).</param>
    /// <param name="cancellationToken">Cancela a gravação.</param>
    /// <returns>O morador, com a PII mascarada.</returns>
    public async Task<MoradorResposta> AtualizarMoradorAsync(Guid id, Guid moradorId, AtualizacaoDeMoradorDto dto, bool revelarContatos, CancellationToken cancellationToken)
    {
        Validacao.Garantir(dto);
        Domicilio casa = await CasaAsync(id, cancellationToken);
        Morador morador = MoradorDa(casa, moradorId);
        DateOnly hoje = Hoje;
        DateOnly nascimento = morador.Idade(hoje) == dto.Idade ? morador.DataDeNascimento : NascimentoPelaIdade(dto.Idade.GetValueOrDefault(), hoje);
        morador.CorrigirCadastro(dto.Nome, nascimento, hoje);
        morador.AtualizarPerfil(PerfilDeSaude.Com(dto.FatoresDeRisco.Select(FatorDeRisco.DoCodigo)));
        if (!string.IsNullOrWhiteSpace(dto.Cpf))
        {
            casa.TrocarCpfDoMorador(morador.Id, Cpf.Criar(dto.Cpf));
        }

        if (!string.IsNullOrWhiteSpace(dto.CartaoSus))
        {
            morador.TrocarCartaoSus(CartaoSus.Criar(dto.CartaoSus));
        }

        if (!string.IsNullOrWhiteSpace(dto.Telefone))
        {
            morador.DefinirTelefone(Telefone.Criar(dto.Telefone));
        }

        if (!string.IsNullOrWhiteSpace(dto.PerfilDeSaude))
        {
            morador.DefinirObservacoesDeSaude(dto.PerfilDeSaude);
        }

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return MoradorResposta.De(morador, hoje, revelarContatos);
    }

    /// <summary>Remove um morador (R8: não remove o último de uma casa monitorada).</summary>
    /// <param name="id">A casa.</param>
    /// <param name="moradorId">O morador.</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>A remoção.</returns>
    public async Task RemoverMoradorAsync(Guid id, Guid moradorId, CancellationToken cancellationToken)
    {
        Domicilio casa = await CasaAsync(id, cancellationToken);
        if (!casa.RemoverMorador(moradorId))
        {
            throw new RecursoNaoEncontradoException("Morador não encontrado.");
        }

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
    }

    /// <summary>A rede de cuidado, na ordem de escalonamento.</summary>
    /// <param name="id">A casa.</param>
    /// <param name="revelarContatos">Se quem pede é o Cuidador da casa (telefones inteiros, para ligar).</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>A rede.</returns>
    public async Task<IReadOnlyList<ContatoResposta>> ObterRedeDeCuidadoAsync(Guid id, bool revelarContatos, CancellationToken cancellationToken)
        => ContatoResposta.DaRede(id, (await CasaAsync(id, cancellationToken)).RedeDeCuidado, revelarContatos);

    /// <summary>Substitui a rede de cuidado inteira, na ordem informada (R8).</summary>
    /// <param name="id">A casa.</param>
    /// <param name="dto">Os contatos.</param>
    /// <param name="revelarContatos">Se quem pede é o Cuidador da casa (telefones inteiros, para ligar).</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>A rede nova.</returns>
    public async Task<IReadOnlyList<ContatoResposta>> DefinirRedeDeCuidadoAsync(Guid id, RedeDeCuidadoDto dto, bool revelarContatos, CancellationToken cancellationToken)
    {
        Validacao.Garantir(dto);
        for (int i = 0; i < dto.Contatos.Count; i++)
        {
            Validacao.Garantir(dto.Contatos[i], string.Create(CultureInfo.InvariantCulture, $"contatos[{i}]"));
        }

        Domicilio casa = await CasaAsync(id, cancellationToken);
        casa.DefinirRedeDeCuidado(dto.Contatos.Select(c => new ContatoDeCuidado(c.Nome.Trim(), Telefone.Criar(c.Telefone), c.Papel.GetValueOrDefault())));
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return ContatoResposta.DaRede(casa.Id, casa.RedeDeCuidado, revelarContatos);
    }

    /// <summary>A rotina combinada com a pessoa.</summary>
    /// <param name="id">A casa.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>A rotina.</returns>
    public async Task<RotinaResposta> ObterRotinaAsync(Guid id, CancellationToken cancellationToken)
        => RotinaResposta.De((await CasaAsync(id, cancellationToken)).Rotina);

    /// <summary>Substitui a rotina (R12: check-in diário nunca à noite).</summary>
    /// <param name="id">A casa.</param>
    /// <param name="dto">A rotina.</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>A rotina nova.</returns>
    public async Task<RotinaResposta> DefinirRotinaAsync(Guid id, RotinaDto dto, CancellationToken cancellationToken)
    {
        Validacao.Garantir(dto);
        Validacao.Garantir(dto.CheckInDiario!, "checkInDiario");
        Validacao.Garantir(dto.LembreteDeAgua!, "lembreteDeAgua");
        Domicilio casa = await CasaAsync(id, cancellationToken);
        casa.DefinirRotina(Rotina.Criar(
            dto.CheckInDiario!.Ativo.GetValueOrDefault(),
            TimeOnly.ParseExact(dto.CheckInDiario.Horario, "HH:mm", CultureInfo.InvariantCulture),
            dto.LembreteDeAgua!.Ativo.GetValueOrDefault()));
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return RotinaResposta.De(casa.Rotina);
    }

    /// <summary>Vincula a Sentinela da casa e devolve, uma única vez, o segredo que assina as mensagens dela.</summary>
    /// <param name="id">A casa.</param>
    /// <param name="dto">O identificador da Sentinela.</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>A Sentinela vinculada, com o segredo.</returns>
    /// <exception cref="ConflitoException">Quando a Sentinela já é de outra casa.</exception>
    public async Task<SentinelaVinculadaResposta> VincularSentinelaAsync(Guid id, NovaSentinelaDto dto, CancellationToken cancellationToken)
    {
        Validacao.Garantir(dto);
        if (!_codigosDeVinculo.Confere(dto.DeviceId, dto.CodigoDeVinculo))
        {
            throw new RegraVioladaException("VINCULO", "O código de vínculo não é o desta Sentinela. Confira o QR.");
        }

        Domicilio casa = await CasaAsync(id, cancellationToken);
        Domicilio? dona = await _domicilios.ObterPorSentinelaAsync(dto.DeviceId, cancellationToken);
        if (dona is not null && dona.Id != casa.Id)
        {
            throw new ConflitoException("Esta Sentinela já está vinculada a outra casa.");
        }

        Sentinela sentinela = casa.VincularSentinela(dto.DeviceId);
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return new SentinelaVinculadaResposta(sentinela.DeviceId, sentinela.EstaOnline(_relogio.Agora), sentinela.SegredoHex ?? string.Empty);
    }

    /// <summary>As Sentinelas da casa e se estão falando com o Core (R9).</summary>
    /// <param name="id">A casa.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>As Sentinelas.</returns>
    public async Task<IReadOnlyList<SentinelaResposta>> ListarSentinelasAsync(Guid id, CancellationToken cancellationToken)
    {
        Domicilio casa = await CasaAsync(id, cancellationToken);
        DateTimeOffset agora = _relogio.Agora;
        return [.. casa.Sentinelas.Select(s => SentinelaResposta.De(s, agora))];
    }

    /// <summary>Liga o monitoramento (R8: precisa de morador e de contato).</summary>
    /// <param name="id">A casa.</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>A casa.</returns>
    public async Task<DomicilioResposta> AtivarMonitoramentoAsync(Guid id, CancellationToken cancellationToken)
    {
        Domicilio casa = await CasaAsync(id, cancellationToken);
        casa.AtivarMonitoramento();
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return DomicilioResposta.De(casa);
    }

    /// <summary>Desliga o monitoramento.</summary>
    /// <param name="id">A casa.</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>A casa.</returns>
    public async Task<DomicilioResposta> DesativarMonitoramentoAsync(Guid id, CancellationToken cancellationToken)
    {
        Domicilio casa = await CasaAsync(id, cancellationToken);
        casa.DesativarMonitoramento();
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return DomicilioResposta.De(casa);
    }

    private static Morador MoradorDa(Domicilio casa, Guid moradorId)
        => casa.Moradores.SingleOrDefault(m => m.Id == moradorId) ?? throw new RecursoNaoEncontradoException("Morador não encontrado.");

    // O app pede a idade, não a data (D17): guarda o aniversário de hoje, que dá exatamente essa idade até o próximo.
    private static DateOnly NascimentoPelaIdade(int idade, DateOnly hoje) => hoje.AddYears(-idade);

    private async Task<Domicilio> CasaAsync(Guid id, CancellationToken cancellationToken)
        => await _domicilios.ObterAsync(id, cancellationToken) ?? throw new RecursoNaoEncontradoException("Domicílio não encontrado.");

}
