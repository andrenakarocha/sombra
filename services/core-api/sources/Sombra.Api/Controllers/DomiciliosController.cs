using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sombra.Api.Autenticacao;
using Sombra.Api.Composicao;
using Sombra.Aplicacao.Alertas;
using Sombra.Aplicacao.Cadastro;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Infraestrutura.Autenticacao;

namespace Sombra.Api.Controllers;

/// <summary>As casas monitoradas: cadastro, visão, monitoramento e check-in.</summary>
[ApiController]
[Route("api/v1/domicilios")]
[Authorize]
[Tags("Domicílios")]
public sealed class DomiciliosController : ControllerBase
{
    private readonly CadastroDeDomicilios _cadastro;
    private readonly IConsultasDoPainel _consultas;
    private readonly ContasDeAcesso _contas;
    private readonly RegistrarCheckIn _checkIn;
    private readonly IRelogio _relogio;

    /// <summary>Initializes a new instance of the <see cref="DomiciliosController"/> class.</summary>
    /// <param name="cadastro">O cadastro das casas.</param>
    /// <param name="consultas">As consultas.</param>
    /// <param name="contas">As contas de acesso.</param>
    /// <param name="checkIn">O caso de uso do check-in.</param>
    /// <param name="relogio">O relógio.</param>
    public DomiciliosController(CadastroDeDomicilios cadastro, IConsultasDoPainel consultas, ContasDeAcesso contas, RegistrarCheckIn checkIn, IRelogio relogio)
    {
        _cadastro = cadastro ?? throw new ArgumentNullException(nameof(cadastro));
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _contas = contas ?? throw new ArgumentNullException(nameof(contas));
        _checkIn = checkIn ?? throw new ArgumentNullException(nameof(checkIn));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
    }

    /// <summary>Lista as casas.</summary>
    /// <remarks>
    /// Cada casa sai como na tela da casa (última leitura, Sentinela, último check-in e o alerta que aparece), que é o que o
    /// card do app mostra (D17). O Cuidador vê só as casas dele; Operador e Admin veem todas. Filtra pelo nível da última
    /// leitura e pelo bairro.
    /// </remarks>
    /// <param name="filtro">Nível, bairro, página e tamanho.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>Uma página de casas.</returns>
    [HttpGet]
    [Authorize(Roles = Acesso.Todos)]
    [ProducesResponseType<Paginado<DomicilioDetalheResposta>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<Paginado<DomicilioDetalheResposta>>> Listar([FromQuery] FiltroDeDomiciliosDto filtro, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid>? casas = User.IsInRole(Papeis.Cuidador) ? await _contas.CasasDoUsuarioAsync(User.Id(), cancellationToken) : null;
        return await _consultas.ListarDomiciliosAsync(filtro, casas, _relogio.Agora, cancellationToken);
    }

    /// <summary>Cadastra uma casa.</summary>
    /// <remarks>A casa nasce sem monitoramento. Quando quem cadastra é um Cuidador, ele passa a ter acesso a ela.</remarks>
    /// <param name="dto">Apelido, endereço e região climática.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>A casa criada, com o endereço dela no header <c>Location</c>.</returns>
    [HttpPost]
    [Authorize(Roles = Acesso.CuidadorOuAdmin)]
    [ProducesResponseType<DomicilioResposta>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<DomicilioResposta>> Cadastrar(NovoDomicilioDto dto, CancellationToken cancellationToken)
    {
        DomicilioResposta casa = await _cadastro.CadastrarAsync(dto, cancellationToken);
        if (User.IsInRole(Papeis.Cuidador))
        {
            await _contas.VincularCasaAsync(User.Id(), casa.Id, cancellationToken);
        }

        return CreatedAtAction(nameof(Obter), new { id = casa.Id }, casa);
    }

    /// <summary>A visão da casa.</summary>
    /// <remarks>Nível atual, última leitura, Sentinela, último check-in e o alerta aberto mais importante.</remarks>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>A visão da casa.</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Acesso.Todos, Policy = CasaDoCuidador.Politica)]
    [ProducesResponseType<DomicilioDetalheResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<DomicilioDetalheResposta>> Obter(Guid id, CancellationToken cancellationToken)
        => await _cadastro.ObterAsync(id, cancellationToken);

    /// <summary>Corrige o apelido e o endereço.</summary>
    /// <remarks>A região climática não muda: ela decide qual estação atende a casa.</remarks>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="dto">O apelido e o endereço.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>A casa.</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Acesso.CuidadorOuAdmin, Policy = CasaDoCuidador.Politica)]
    [ProducesResponseType<DomicilioResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<DomicilioResposta>> Atualizar(Guid id, AtualizacaoDeDomicilioDto dto, CancellationToken cancellationToken)
        => await _cadastro.AtualizarAsync(id, dto, cancellationToken);

    /// <summary>Remove a casa.</summary>
    /// <remarks>Moradores, Sentinela, leituras, alertas e check-ins saem junto.</remarks>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>Nada.</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Acesso.CuidadorOuAdmin, Policy = CasaDoCuidador.Politica)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Remover(Guid id, CancellationToken cancellationToken)
    {
        await _cadastro.RemoverAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Liga o monitoramento.</summary>
    /// <remarks>Regra R8: precisa de pelo menos um morador e um contato de cuidado.</remarks>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>A casa.</returns>
    [HttpPost("{id:guid}/monitoramento")]
    [Authorize(Roles = Acesso.CuidadorOuAdmin, Policy = CasaDoCuidador.Politica)]
    [ProducesResponseType<DomicilioResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<DomicilioResposta>> AtivarMonitoramento(Guid id, CancellationToken cancellationToken)
        => await _cadastro.AtivarMonitoramentoAsync(id, cancellationToken);

    /// <summary>Desliga o monitoramento.</summary>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>A casa.</returns>
    [HttpDelete("{id:guid}/monitoramento")]
    [Authorize(Roles = Acesso.CuidadorOuAdmin, Policy = CasaDoCuidador.Politica)]
    [ProducesResponseType<DomicilioResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<DomicilioResposta>> DesativarMonitoramento(Guid id, CancellationToken cancellationToken)
        => await _cadastro.DesativarMonitoramentoAsync(id, cancellationToken);

    /// <summary>Registra um "estou bem" ou um "preciso de ajuda".</summary>
    /// <remarks>
    /// O "estou bem" encerra o alerta que pede resposta (caminho A). O "preciso de ajuda" avisa a rede toda na hora (R7),
    /// no alerta que já estiver aberto ou num pedido de ajuda novo (R4). Vindo da fila offline do app, vale o horário do
    /// toque em <c>momento</c> (R14: nunca no futuro, até 24 horas atrás), e a <c>Idempotency-Key</c> evita contar duas vezes.
    /// </remarks>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="dto">A origem e a situação.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>O check-in e o alerta afetado.</returns>
    [HttpPost("{id:guid}/check-ins")]
    [Idempotente]
    [Authorize(Roles = Acesso.CuidadorOuAdmin, Policy = CasaDoCuidador.Politica)]
    [ProducesResponseType<CheckInResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CheckInResposta>> RegistrarCheckIn(Guid id, NovoCheckInDto dto, CancellationToken cancellationToken)
        => await _checkIn.ExecutarAsync(id, dto, cancellationToken);
}
