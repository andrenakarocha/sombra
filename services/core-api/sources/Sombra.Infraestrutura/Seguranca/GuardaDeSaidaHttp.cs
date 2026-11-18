using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Comum;
using Sombra.Privacidade.Sinks;
using Sombra.Privacidade.Taint;

namespace Sombra.Infraestrutura.Seguranca;

/// <summary>
/// O guarda de saída externa (spec 07 §2.3): todo <c>HttpClient</c> que sai do Core (Clima, LLM) passa por aqui. Se o
/// corpo ou a query levam dado pessoal, a chamada nem sai: o guarda registra o vazamento e lança
/// <see cref="SaidaBloqueadaException"/>. "Nem a IA vê o dado."
/// </summary>
public sealed class GuardaDeSaidaHttp : DelegatingHandler
{
    private readonly GuardaDeSaida _guarda;
    private readonly IObservadorDeVazamentos _observador;
    private readonly ModoDaPrivacidade _modo;

    /// <summary>Initializes a new instance of the <see cref="GuardaDeSaidaHttp"/> class.</summary>
    /// <param name="guarda">A guarda de saída.</param>
    /// <param name="observador">Quem registra o bloqueio.</param>
    /// <param name="modo">Se bloqueia ou só registra (modo inseguro da auditoria).</param>
    public GuardaDeSaidaHttp(GuardaDeSaida guarda, IObservadorDeVazamentos observador, ModoDaPrivacidade modo)
    {
        _guarda = guarda ?? throw new ArgumentNullException(nameof(guarda));
        _observador = observador ?? throw new ArgumentNullException(nameof(observador));
        _modo = modo ?? throw new ArgumentNullException(nameof(modo));
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string corpo = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        string host = request.RequestUri?.Host ?? "(sem host)";
        AchadoNaSaida[] achados = [
            .. _guarda.Verificar(corpo, ContextoDeTaint.Atual).Achados,
            .. _guarda.Verificar(Uri.UnescapeDataString(request.RequestUri?.Query ?? string.Empty), ContextoDeTaint.Atual).Achados];
        if (achados.Length > 0)
        {
            foreach (AchadoNaSaida a in achados.Distinct())
            {
                _observador.Registrar(new VazamentoBloqueado(SinkDePii.SaidaExterna, host, a.RegraId, a.Categoria, a.Severidade, a.PeloTaint, Correlacao.Atual, Bloqueado: _modo.Bloqueia));
            }

            if (!_modo.Bloqueia)
            {
                return await base.SendAsync(request, cancellationToken);
            }

            throw new SaidaBloqueadaException(SinkDePii.SaidaExterna, host, string.Join(", ", achados.Select(a => a.RegraId).Distinct()));
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
