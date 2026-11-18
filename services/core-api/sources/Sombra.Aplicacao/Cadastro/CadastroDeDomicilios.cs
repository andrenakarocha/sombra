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

    /// <summary>Initializes a new instance of the <see cref="CadastroDeDomicilios"/> class.</summary>
    /// <param name="domicilios">As casas.</param>
    /// <param name="alertas">Os alertas.</param>
    /// <param name="leituras">As leituras.</param>
    /// <param name="checkIns">Os check-ins.</param>
    /// <param name="relogio">O relógio.</param>
    /// <param name="unidadeDeTrabalho">A transação.</param>
    public CadastroDeDomicilios(IDomicilioRepository domicilios, IAlertaRepository alertas, ILeituraRepository leituras, ICheckInRepository checkIns, IRelogio relogio, IUnitOfWork unidadeDeTrabalho)
    {
        _domicilios = domicilios ?? throw new ArgumentNullException(nameof(domicilios));
        _alertas = alertas ?? throw new ArgumentNullException(nameof(alertas));
        _leituras = leituras ?? throw new ArgumentNullException(nameof(leituras));
        _checkIns = checkIns ?? throw new ArgumentNullException(nameof(checkIns));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _unidadeDeTrabalho = unidadeDeTrabalho ?? throw new ArgumentNullException(nameof(unidadeDeTrabalho));
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
        AlertaDeCalor? alerta = abertos.FirstOrDefault(a => a is not AlertaPreventivo) ?? (abertos.Count > 0 ? abertos[0] : null);
        return DomicilioDetalheResposta.De(casa, ultimaLeitura, ultimoCheckIn, alerta, _relogio.Agora);
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

    /// <summary>Lista os moradores, com a PII mascarada.</summary>
    /// <param name="id">A casa.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>Os moradores.</returns>
    public async Task<IReadOnlyList<MoradorResposta>> ListarMoradoresAsync(Guid id, CancellationToken cancellationToken)
    {
        Domicilio casa = await CasaAsync(id, cancellationToken);
        DateOnly hoje = Hoje;
        return [.. casa.Moradores.Select(m => MoradorResposta.De(m, hoje))];
    }

    /// <summary>Cadastra um morador na casa (R1: CPF e Cartão SUS válidos e sem CPF repetido).</summary>
    /// <param name="id">A casa.</param>
    /// <param name="dto">Os dados do morador.</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>O morador, com a PII mascarada.</returns>
    public async Task<MoradorResposta> AdicionarMoradorAsync(Guid id, NovoMoradorDto dto, CancellationToken cancellationToken)
    {
        Validacao.Garantir(dto);
        Domicilio casa = await CasaAsync(id, cancellationToken);
        DateOnly hoje = Hoje;
        Morador morador = Morador.Cadastrar(
            dto.Nome,
            dto.DataDeNascimento.GetValueOrDefault(),
            Cpf.Criar(dto.Cpf),
            string.IsNullOrWhiteSpace(dto.CartaoSus) ? null : CartaoSus.Criar(dto.CartaoSus),
            PerfilDeSaude.Com(dto.FatoresDeRisco.Select(FatorDeRisco.DoCodigo)),
            hoje);
        casa.AdicionarMorador(morador);
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return MoradorResposta.De(morador, hoje);
    }

    /// <summary>Um morador, com a PII mascarada.</summary>
    /// <param name="id">A casa.</param>
    /// <param name="moradorId">O morador.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>O morador.</returns>
    public async Task<MoradorResposta> ObterMoradorAsync(Guid id, Guid moradorId, CancellationToken cancellationToken)
        => MoradorResposta.De(MoradorDa(await CasaAsync(id, cancellationToken), moradorId), Hoje);

    /// <summary>Atualiza os fatores de risco declarados de um morador (R3: mudam os limites térmicos da casa).</summary>
    /// <param name="id">A casa.</param>
    /// <param name="moradorId">O morador.</param>
    /// <param name="dto">Os fatores.</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>O morador.</returns>
    public async Task<MoradorResposta> AtualizarPerfilDoMoradorAsync(Guid id, Guid moradorId, PerfilDoMoradorDto dto, CancellationToken cancellationToken)
    {
        Validacao.Garantir(dto);
        Morador morador = MoradorDa(await CasaAsync(id, cancellationToken), moradorId);
        morador.AtualizarPerfil(PerfilDeSaude.Com(dto.FatoresDeRisco.Select(FatorDeRisco.DoCodigo)));
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return MoradorResposta.De(morador, Hoje);
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

    /// <summary>A rede de cuidado, com os telefones mascarados.</summary>
    /// <param name="id">A casa.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>A rede.</returns>
    public async Task<RedeDeCuidadoResposta> ObterRedeDeCuidadoAsync(Guid id, CancellationToken cancellationToken)
        => RedeDeCuidadoResposta.De((await CasaAsync(id, cancellationToken)).RedeDeCuidado);

    /// <summary>Substitui a rede de cuidado inteira, na ordem informada (R8).</summary>
    /// <param name="id">A casa.</param>
    /// <param name="dto">Os contatos.</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>A rede nova.</returns>
    public async Task<RedeDeCuidadoResposta> DefinirRedeDeCuidadoAsync(Guid id, RedeDeCuidadoDto dto, CancellationToken cancellationToken)
    {
        Validacao.Garantir(dto);
        for (int i = 0; i < dto.Contatos.Count; i++)
        {
            Validacao.Garantir(dto.Contatos[i], string.Create(CultureInfo.InvariantCulture, $"contatos[{i}]"));
        }

        Domicilio casa = await CasaAsync(id, cancellationToken);
        casa.DefinirRedeDeCuidado(dto.Contatos.Select(c => new ContatoDeCuidado(c.Nome.Trim(), Telefone.Criar(c.Telefone), c.Papel.GetValueOrDefault())));
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return RedeDeCuidadoResposta.De(casa.RedeDeCuidado);
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
        Domicilio casa = await CasaAsync(id, cancellationToken);
        casa.DefinirRotina(Rotina.Criar(dto.HorarioDoCheckInDiario, dto.LembreteDeAguaAtivo.GetValueOrDefault()));
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return RotinaResposta.De(casa.Rotina);
    }

    /// <summary>Vincula a Sentinela da casa.</summary>
    /// <param name="id">A casa.</param>
    /// <param name="dto">O identificador da Sentinela.</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>A Sentinela vinculada.</returns>
    /// <exception cref="ConflitoException">Quando a Sentinela já é de outra casa.</exception>
    public async Task<SentinelaResposta> VincularSentinelaAsync(Guid id, NovaSentinelaDto dto, CancellationToken cancellationToken)
    {
        Validacao.Garantir(dto);
        Domicilio casa = await CasaAsync(id, cancellationToken);
        Domicilio? dona = await _domicilios.ObterPorSentinelaAsync(dto.DeviceId, cancellationToken);
        if (dona is not null && dona.Id != casa.Id)
        {
            throw new ConflitoException("Esta Sentinela já está vinculada a outra casa.");
        }

        Sentinela sentinela = casa.VincularSentinela(dto.DeviceId);
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return SentinelaResposta.De(sentinela, _relogio.Agora);
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

    private async Task<Domicilio> CasaAsync(Guid id, CancellationToken cancellationToken)
        => await _domicilios.ObterAsync(id, cancellationToken) ?? throw new RecursoNaoEncontradoException("Domicílio não encontrado.");
}
