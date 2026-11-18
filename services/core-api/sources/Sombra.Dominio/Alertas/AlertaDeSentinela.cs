using System;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;
using Sombra.Dominio.Sentinelas;

namespace Sombra.Dominio.Alertas;

/// <summary>
/// Nasce de uma leitura da casa a partir de <see cref="NivelDeRisco.CautelaExtrema"/>. Pede o "estou bem" com prazo por
/// nível (R6) e, sem resposta, sobe os degraus da D13: lembrete leve, quem está perto e a rede toda. Os degraus 2 e 3 só
/// existem a partir de <see cref="NivelDeRisco.Perigo"/> (R8): esquecer de apertar num calor menor nunca vira emergência.
/// </summary>
public sealed class AlertaDeSentinela : AlertaDeCalor
{
    /// <summary>Quanto tempo entre um degrau e o próximo.</summary>
    public static readonly TimeSpan IntervaloEntreDegraus = TimeSpan.FromMinutes(10);

    /// <summary>Quanto tempo depois do "estou bem" a casa fica sem novo alerta no mesmo nível (regra R13).</summary>
    public static readonly TimeSpan Carencia = TimeSpan.FromHours(1);

    private DateTimeOffset _ultimoDegrauEm;

    private AlertaDeSentinela(Guid domicilioId, NivelDeRisco nivel, DateTimeOffset abertoEm)
        : base(domicilioId, nivel, abertoEm, EstadoDoAlerta.AguardandoCheckIn)
    {
        Prazo = AbertoEm + PrazoPara(nivel);
        _ultimoDegrauEm = AbertoEm;
    }

    /// <inheritdoc/>
    public override string Tipo => "DE_SENTINELA";

    /// <inheritdoc/>
    public override string MensagemAoMorador => Degrau >= DegrauDeEscalonamento.LembreteLeve
        ? "Sua família foi avisada. Aperte o botão verde se você está bem."
        : Nivel >= NivelDeRisco.Perigo
            ? "Calor perigoso. Aperte o verde. Sem resposta em 15 min, avisamos sua família."
            : "Calor forte. Aperte o verde. Sem resposta em 30 min, avisamos sua família.";

    /// <summary>Regra R6: o prazo do "estou bem" para cada nível.</summary>
    /// <param name="nivel">O nível da leitura.</param>
    /// <returns>O prazo.</returns>
    public static TimeSpan PrazoPara(NivelDeRisco nivel) => nivel >= NivelDeRisco.Perigo
        ? TimeSpan.FromMinutes(15)
        : TimeSpan.FromMinutes(30);

    /// <summary>
    /// Regra R13 (carência): a pessoa acabou de dizer que está bem, então, por uma hora, uma leitura no mesmo nível ou
    /// abaixo não abre outro alerta. Só um nível mais alto reabre. Evita pedir o botão de novo a cada leitura.
    /// </summary>
    /// <param name="ultimoEncerrado">O último alerta encerrado da casa, se houver.</param>
    /// <param name="leitura">A leitura nova.</param>
    /// <returns><c>true</c> quando a leitura cai na carência.</returns>
    public static bool EmCarencia(AlertaDeCalor? ultimoEncerrado, Leitura leitura)
    {
        ArgumentNullException.ThrowIfNull(leitura);
        return ultimoEncerrado is AlertaDeSentinela { Estado: EstadoDoAlerta.Resolvido, EncerradoEm: { } fim } anterior
            && leitura.Momento - fim < Carencia
            && leitura.Nivel <= anterior.Nivel;
    }

    /// <summary>Abre o alerta a partir de uma leitura de cautela extrema ou acima.</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="leitura">A leitura que disparou o alerta.</param>
    /// <returns>O alerta, aguardando o "estou bem".</returns>
    /// <exception cref="RegraVioladaException">Quando a leitura está abaixo de cautela extrema (R6).</exception>
    public static AlertaDeSentinela Abrir(Guid domicilioId, Leitura leitura)
    {
        ArgumentNullException.ThrowIfNull(leitura);
        if (leitura.Nivel < NivelDeRisco.CautelaExtrema)
        {
            throw new RegraVioladaException("R6", "O \"estou bem\" só é pedido a partir de cautela extrema.");
        }

        AlertaDeSentinela alerta = new(domicilioId, leitura.Nivel, leitura.Momento);
        alerta.RegistrarAbertura();
        return alerta;
    }

    /// <summary>
    /// Regra R4: uma leitura nova atualiza o alerta aberto em vez de abrir outro. Se o nível subir, o prazo encurta (nunca
    /// aumenta).
    /// </summary>
    /// <param name="leitura">A leitura nova.</param>
    public void AtualizarComLeitura(Leitura leitura)
    {
        ArgumentNullException.ThrowIfNull(leitura);
        GarantirAberto();
        if (leitura.Nivel <= Nivel)
        {
            return;
        }

        Nivel = leitura.Nivel;
        DateTimeOffset novoPrazo = leitura.Momento + PrazoPara(leitura.Nivel);
        if (Prazo is null || novoPrazo < Prazo)
        {
            Prazo = novoPrazo;
        }

        Registrar(new AlertaAgravado(Id, DomicilioId, Nivel, leitura.Momento));
    }

    /// <summary>
    /// A pessoa pediu ajuda durante o alerta (botão vermelho ou app). Regras R4, R7 e R8: o mesmo alerta vai direto ao
    /// degrau 3, a rede toda, em qualquer nível, sem abrir um segundo alerta.
    /// </summary>
    /// <param name="momento">Quando a pessoa pediu ajuda.</param>
    /// <exception cref="RegraVioladaException">Em alerta encerrado (R5).</exception>
    public void PedirAjuda(DateTimeOffset momento)
    {
        GarantirAberto();
        if (Degrau < DegrauDeEscalonamento.AlertaParaARede)
        {
            _ultimoDegrauEm = momento.ToUniversalTime();
            SubirPara(DegrauDeEscalonamento.AlertaParaARede, momento);
        }
    }

    /// <inheritdoc/>
    public override void Escalonar(DateTimeOffset agora, RedeDeCuidado rede)
    {
        ArgumentNullException.ThrowIfNull(rede);
        if (Estado is not (EstadoDoAlerta.AguardandoCheckIn or EstadoDoAlerta.Escalonado))
        {
            return;
        }

        DegrauDeEscalonamento maximo = Nivel >= NivelDeRisco.Perigo
            ? DegrauDeEscalonamento.AlertaParaARede
            : DegrauDeEscalonamento.LembreteLeve;

        while (Degrau < maximo)
        {
            DateTimeOffset venceEm = Degrau == DegrauDeEscalonamento.Sentinela
                ? Prazo!.Value
                : _ultimoDegrauEm + IntervaloEntreDegraus;
            if (agora < venceEm)
            {
                return;
            }

            DegrauDeEscalonamento proximo = Degrau + 1;
            if (proximo == DegrauDeEscalonamento.QuemEstaPerto && rede.QuemEstaPerto is null)
            {
                proximo = DegrauDeEscalonamento.AlertaParaARede;
            }

            _ultimoDegrauEm = venceEm;
            SubirPara(proximo, venceEm);
        }
    }
}
