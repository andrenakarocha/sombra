// Canário do taint estático (F2.7): cada linha marcada com "ruleid" é um vazamento que a regra precisa achar, e cada
// "ok" é o jeito certo, que não pode ser acusado. Este arquivo não compila no Core: existe só para o Semgrep.
// Dados sintéticos; nenhum valor real.
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sombra.Privacidade;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Taint;

namespace Sombra.Canario;

public sealed record MoradorDto(string Nome, string Cpf, string CartaoSus, string Laudo, string Senha, string Telefone);

public sealed class CanarioController : ControllerBase
{
    private readonly ILogger<CanarioController> _log;
    private readonly HttpClient _http;
    private readonly ConexaoMqtt _mqtt;

    public CanarioController(ILogger<CanarioController> log, HttpClient http, ConexaoMqtt mqtt)
    {
        _log = log;
        _http = http;
        _mqtt = mqtt;
    }

    public void Logs(MoradorDto dto)
    {
        // ruleid: sombra-pii-para-log
        _log.LogInformation("cadastro do CPF {Cpf}", dto.Cpf);

        string laudo = dto.Laudo;
        // ruleid: sombra-pii-para-log
        _log.LogWarning($"laudo recebido: {laudo}");

        // ruleid: sombra-pii-para-log
        Console.WriteLine("senha " + dto.Senha);

        // ruleid: sombra-pii-para-log
        CartaoDeDepuracao(_log, dto.CartaoSus);

        // ok: sombra-pii-para-log
        _log.LogInformation("cadastro do CPF {Cpf}", MascaradorDePii.MascararCpf(dto.Cpf));

        // ok: sombra-pii-para-log
        _log.LogInformation("telefone {Telefone}", new Sensivel<string>(dto.Telefone, CategoriaDePii.Contato));

        // ok: sombra-pii-para-log
        _log.LogInformation("morador {Nome} cadastrado", dto.Nome);
    }

    public IActionResult Erros(MoradorDto dto)
    {
        if (dto.Nome.Length == 0)
        {
            // ruleid: sombra-pii-na-mensagem-de-erro
            throw new InvalidOperationException($"Laudo recusado: {dto.Laudo}");
        }

        if (dto.Nome.Length == 1)
        {
            // ruleid: sombra-pii-na-mensagem-de-erro
            return Problem(detail: "CPF inválido: " + dto.Cpf);
        }

        // ok: sombra-pii-na-mensagem-de-erro
        throw new InvalidOperationException("Laudo recusado.");
    }

    public async Task SaidasAsync(MoradorDto dto)
    {
        // ruleid: sombra-pii-para-fora
        await _http.PostAsJsonAsync("https://llm.invalid/v1/gerar", new { prompt = $"Explique o caso do CPF {dto.Cpf}" });

        // ruleid: sombra-pii-para-fora
        await _mqtt.PublicarAsync("sombra/v1/snt-0001/comando", $"{{\"ligar\":\"{dto.Telefone}\"}}", default);

        // ok: sombra-pii-para-fora
        await _http.PostAsJsonAsync("https://llm.invalid/v1/gerar", new { prompt = "Explique um acesso negado a dado de saúde." });
    }

    [LoggerMessage(EventId = 9001, Level = LogLevel.Information, Message = "cartão {Cartao}")]
    private static partial void CartaoDeDepuracao(ILogger logger, string cartao);
}
