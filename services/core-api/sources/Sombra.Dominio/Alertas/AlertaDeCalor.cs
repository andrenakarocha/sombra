using System;
using System.Collections.Generic;
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
    private readonly List<EventoDaLinhaDoTempo> _linhaDoTempo = [];

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

    /// <summary>Gets a história do alerta, na ordem em que aconteceu (o app mostra como linha do tempo).</summary>
    public IReadOnlyList<EventoDaLinhaDoTempo> LinhaDoTempo => _linhaDoTempo;

    /// <summary>Gets a value indicating whether o alerta ainda está aberto.</summary>
    public bool EstaAberto => Estado is not (EstadoDoAlerta.Resolvido or EstadoDoAlerta.Expirado);

    /// <summary>Gets o primeiro passo da linha do tempo, que cada tipo de alerta descreve do seu jeito.</summary>
    protected abstract string DescricaoDaAbertura { get; }

    /// <summary>A pessoa confirmou que está bem: encerra o alerta.</summary>
    /// <param name="momento">Quando confirmou.</param>
    /// <exception cref="RegraVioladaException">Em alerta encerrado ou em tipo que não pede check-in (R5).</exception>
    public void ConfirmarEstouBem(DateTimeOffset momento) => ConfirmarEstouBem(momento, OrigemDoCheckIn.BotaoSentinela);

    /// <summary>Encerra o alerta com o "estou bem" (caminho A), dizendo na linha do tempo de onde ele veio.</summary>
    /// <param name="momento">Quando a pessoa confirmou.</param>
    /// <param name="origem">De onde veio o "estou bem".</param>
    /// <exception cref="RegraVioladaException">Quando o alerta já foi encerrado (R5).</exception>
    public virtual void ConfirmarEstouBem(DateTimeOffset momento, OrigemDoCheckIn origem)
    {
        GarantirAberto();
        Anotar(momento, origem switch
        {
            OrigemDoCheckIn.App => "Apertou \"estou bem\" no celular.",
            OrigemDoCheckIn.Cuidador => "A família registrou que está tudo bem.",
            _ => "Apertou o botão da Sentinela: está tudo bem.",
        });
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
        Anotar(momento, acao switch
        {
            AcaoDoCuidador.VouLigar => "Alguém da família vai ligar.",
            AcaoDoCuidador.EstouIndo => "Alguém da família está indo até a casa.",
            _ => "A família confirmou que está tudo bem.",
        });
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
        => SubirPara(degrau, momento, degrau switch
        {
            DegrauDeEscalonamento.LembreteLeve => "Lembrete leve para o primeiro familiar.",
            DegrauDeEscalonamento.QuemEstaPerto => "\"Quem está perto\": pedido para quem mora perto dar uma olhada.",
            _ => "Alerta para toda a rede de cuidado.",
        });

    /// <summary>Sobe para um degrau com uma descrição própria na linha do tempo.</summary>
    /// <param name="degrau">O novo degrau.</param>
    /// <param name="momento">Quando subiu.</param>
    /// <param name="descricao">O que aparece na linha do tempo.</param>
    protected void SubirPara(DegrauDeEscalonamento degrau, DateTimeOffset momento, string descricao)
    {
        Degrau = degrau;
        Anotar(momento, descricao);
        Estado = EstadoDoAlerta.Escalonado;
        Registrar(new AlertaEscalonado(Id, DomicilioId, degrau, momento));
    }

    /// <summary>Registra a abertura do alerta (chamado pelos tipos concretos depois de prontos).</summary>
    protected void RegistrarAbertura()
    {
        Anotar(AbertoEm, DescricaoDaAbertura);
        Registrar(new AlertaAberto(Id, DomicilioId, Tipo, Nivel, AbertoEm));
    }

    /// <summary>Acrescenta um passo à linha do tempo, no degrau atual.</summary>
    /// <param name="momento">Quando aconteceu.</param>
    /// <param name="descricao">O que aconteceu, sem dado pessoal.</param>
    protected void Anotar(DateTimeOffset momento, string descricao)
        => _linhaDoTempo.Add(new EventoDaLinhaDoTempo(momento.ToUniversalTime(), Degrau, descricao));
}
