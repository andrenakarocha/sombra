using System;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Alertas;

/// <summary>
/// Um episódio de risco numa casa, do início ao fim. Cada tipo decide o próprio prazo, a mensagem para a pessoa e como
/// escalona (polimorfismo); a máquina de estados comum garante que um alerta encerrado nunca volta (regra R5).
/// </summary>
public abstract class AlertaDeCalor : RaizDeAgregado
{
    /// <summary>Inicializes a new instance of the <see cref="AlertaDeCalor"/> class.</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="nivel">O nível de risco na abertura.</param>
    /// <param name="abertoEm">Quando foi aberto.</param>
    /// <param name="estadoInicial">O estado inicial, que depende do tipo.</param>
    protected AlertaDeCalor(Guid domicilioId, NivelDeRisco nivel, DateTimeOffset abertoEm, EstadoDoAlerta estadoInicial)
    {
        Id = Guid.CreateVersion7();
        DomicilioId = domicilioId;
        Nivel = nivel;
        AbertoEm = abertoEm.ToUniversalTime();
        Estado = estadoInicial;
    }

    /// <summary>Gets o identificador do alerta.</summary>
    public Guid Id { get; }

    /// <summary>Gets a casa.</summary>
    public Guid DomicilioId { get; }

    /// <summary>Gets o código do tipo (contrato da API e do SignalR).</summary>
    public abstract string Tipo { get; }

    /// <summary>Gets o texto curto para o OLED da Sentinela, sem dado pessoal.</summary>
    public abstract string MensagemAoMorador { get; }

    /// <summary>Gets o nível de risco atual.</summary>
    public NivelDeRisco Nivel { get; protected set; }

    /// <summary>Gets quando o alerta foi aberto, em UTC.</summary>
    public DateTimeOffset AbertoEm { get; }

    /// <summary>Gets o estado atual.</summary>
    public EstadoDoAlerta Estado { get; protected set; }

    /// <summary>Gets o degrau atual do escalonamento.</summary>
    public DegrauDeEscalonamento Degrau { get; protected set; }

    /// <summary>Gets o prazo para o "estou bem", quando o tipo pede check-in.</summary>
    public DateTimeOffset? Prazo { get; protected set; }

    /// <summary>Gets a resposta do cuidador, se houve.</summary>
    public AcaoDoCuidador? Reconhecimento { get; private set; }

    /// <summary>Gets quando o alerta foi encerrado.</summary>
    public DateTimeOffset? EncerradoEm { get; private set; }

    /// <summary>Gets a value indicating whether o alerta ainda está aberto.</summary>
    public bool EstaAberto => Estado is not (EstadoDoAlerta.Resolvido or EstadoDoAlerta.Expirado);

    /// <summary>A pessoa confirmou que está bem: encerra o alerta.</summary>
    /// <param name="momento">Quando confirmou.</param>
    /// <exception cref="RegraVioladaException">Em alerta encerrado ou em tipo que não pede check-in (R5).</exception>
    public virtual void ConfirmarEstouBem(DateTimeOffset momento)
    {
        GarantirAberto();
        Encerrar(EstadoDoAlerta.Resolvido, momento);
    }

    /// <summary>Um cuidador respondeu pelo app. "Ela está bem" encerra; "vou ligar" e "estou indo" param o escalonamento.</summary>
    /// <param name="acao">A resposta.</param>
    /// <param name="momento">Quando respondeu.</param>
    /// <exception cref="RegraVioladaException">Em alerta encerrado (R5).</exception>
    public void Reconhecer(AcaoDoCuidador acao, DateTimeOffset momento)
    {
        GarantirAberto();
        Reconhecimento = acao;
        Registrar(new AlertaReconhecido(Id, DomicilioId, acao, momento));
        if (acao == AcaoDoCuidador.ElaEstaBem)
        {
            Encerrar(EstadoDoAlerta.Resolvido, momento);
        }
        else
        {
            Estado = EstadoDoAlerta.Reconhecido;
        }
    }

    /// <summary>Sobe os degraus que já venceram até agora. O padrão é não escalonar.</summary>
    /// <param name="agora">O instante de referência.</param>
    /// <param name="rede">A rede de cuidado da casa.</param>
    public virtual void Escalonar(DateTimeOffset agora, RedeDeCuidado rede)
    {
    }

    /// <summary>Confere que o alerta ainda está aberto (regra R5).</summary>
    /// <exception cref="RegraVioladaException">Quando já foi encerrado.</exception>
    protected void GarantirAberto()
    {
        if (!EstaAberto)
        {
            throw new RegraVioladaException("R5", "Este alerta já foi encerrado.");
        }
    }

    /// <summary>Encerra o alerta e registra quanto tempo levou.</summary>
    /// <param name="estado">Resolvido ou expirado.</param>
    /// <param name="momento">Quando encerrou.</param>
    protected void Encerrar(EstadoDoAlerta estado, DateTimeOffset momento)
    {
        Estado = estado;
        EncerradoEm = momento.ToUniversalTime();
        Registrar(new AlertaEncerrado(Id, DomicilioId, estado, (momento - AbertoEm).TotalMinutes, momento));
    }

    /// <summary>Sobe um degrau e registra o evento.</summary>
    /// <param name="degrau">O novo degrau.</param>
    /// <param name="momento">Quando subiu.</param>
    protected void SubirPara(DegrauDeEscalonamento degrau, DateTimeOffset momento)
    {
        Degrau = degrau;
        Estado = EstadoDoAlerta.Escalonado;
        Registrar(new AlertaEscalonado(Id, DomicilioId, degrau, momento));
    }

    /// <summary>Registra a abertura do alerta (chamado pelos tipos concretos depois de prontos).</summary>
    protected void RegistrarAbertura() => Registrar(new AlertaAberto(Id, DomicilioId, Tipo, Nivel, AbertoEm));
}
