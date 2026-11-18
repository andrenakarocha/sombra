using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sombra.Dominio.Tests.Apoio;

namespace Sombra.Privacidade.Tests;

/// <summary>
/// A tabela do motor de PII (spec 07, §1): positivos, negativos e casos difíceis. Cada caso diz exatamente quais regras
/// disparam, então a tabela mede falso negativo e falso positivo ao mesmo tempo. Só dados sintéticos.
/// </summary>
internal static class CasosDePii
{
    /// <summary>Positivo: o dado está lá e precisa ser achado.</summary>
    internal const string Positivo = "Positivo";

    /// <summary>Negativo: parece, mas não é dado pessoal.</summary>
    internal const string Negativo = "Negativo";

    /// <summary>Difícil: o caso que costuma enganar um detector ingênuo.</summary>
    internal const string Dificil = "Difícil";

    private static readonly string _cpf = DocumentosSinteticos.Cpf(7);
    private static readonly string _cpfPontuado = string.Create(CultureInfo.InvariantCulture, $"{_cpf[..3]}.{_cpf[3..6]}.{_cpf[6..9]}-{_cpf[9..]}");
    private static readonly string _cns = DocumentosSinteticos.CartaoSus(7);
    private static readonly string _cnsComEspacos = $"{_cns[..3]} {_cns[3..7]} {_cns[7..11]} {_cns[11..]}";

    /// <summary>Gets todos os casos da tabela.</summary>
    internal static IReadOnlyList<CasoDePii> Todos { get; } =
    [
        // CPF
        new("CPF pontuado no meio de uma frase", Positivo, null, $"A moradora informou o CPF {_cpfPontuado} no cadastro.", ["cpf"]),
        new("CPF só com dígitos", Positivo, null, $"cpf {_cpf}", ["cpf"]),
        new("CPF colado em pontuação", Positivo, null, $"(CPF:{_cpfPontuado}).", ["cpf"]),
        new("CPF no campo documento", Positivo, "documento", _cpfPontuado, ["cpf"]),
        new("CPF digitado errado no campo cpf ainda é protegido", Dificil, "cpfDoResponsavel", "123.456.789-00", ["cpf"]),
        new("Onze dígitos com verificador errado em texto livre", Negativo, null, "Protocolo 12345678900 registrado.", []),
        new("Onze dígitos iguais", Negativo, null, "Código 11111111111", []),
        new("CPF trocando um verificador", Dificil, null, $"CPF {TrocarUltimoDigito(_cpf)}", []),

        // Cartão SUS
        new("Cartão SUS com espaços", Positivo, null, $"Cartão SUS {_cnsComEspacos}", ["cns"]),
        new("Cartão SUS só com dígitos", Positivo, null, $"cns={_cns}", ["cns"]),
        new("Cartão SUS no campo cartaoSus", Positivo, "cartaoSus", _cns, ["cns"]),
        new("Quinze dígitos que não passam na validação", Negativo, null, "Lote 345 6789 0123 4567", []),
        new("Número longo que contém quinze dígitos", Dificil, null, $"Pedido 9{_cns}9 faturado", []),

        // RG
        new("RG pontuado", Positivo, null, "RG 12.345.678-9 apresentado", ["rg"]),
        new("RG com dígito X", Positivo, null, "identidade 1.234.567-X", ["rg"]),
        new("RG sem pontuação no campo rg", Positivo, "rg", "123456789", ["rg"]),
        new("Campo organizacao não é RG", Dificil, "organizacao", "Defesa Civil", []),
        new("Nove dígitos soltos sem campo", Negativo, null, "Total de leituras: 123456789", []),

        // Telefone
        new("Celular com DDD e hífen", Positivo, null, "Ligar para (11) 98765-4321", ["telefone"]),
        new("Celular com +55", Positivo, null, "WhatsApp +55 11 98765-4321", ["telefone"]),
        new("Fixo com DDD", Positivo, null, "Fixo: (21) 3456-7890", ["telefone"]),
        new("Celular só com dígitos", Positivo, null, "contato 21987654321", ["telefone"]),
        new("Telefone no campo celular", Positivo, "celular", "não informado", ["telefone"]),
        new("Celular depois de emoji", Dificil, null, "📞 (31) 99876-5432", ["telefone"]),
        new("DDD inexistente", Negativo, null, "(20) 98765-4321", []),
        new("Data e hora ISO 8601", Negativo, null, "2026-11-18T15:14:00Z", []),

        // E-mail
        new("E-mail em frase", Positivo, null, "Contato: maria.exemplo@exemplo.com.br.", ["email"]),
        new("E-mail em maiúsculas", Positivo, null, "MARIA@EXEMPLO.COM", ["email"]),
        new("E-mail com acento no usuário", Dificil, null, "joão.exemplo@exemplo.org", ["email"]),
        new("Arroba sem domínio", Negativo, null, "fale com @defesacivil", []),

        // CEP
        new("CEP com hífen", Positivo, null, "Mora no CEP 01310-100", ["cep"]),
        new("CEP sem hífen no campo cep", Positivo, "cep", "01310100", ["cep"]),
        new("Oito dígitos soltos", Negativo, null, "Versão 20261118", []),

        // Saúde
        new("Campo laudo com texto comum", Positivo, "laudo", "Paciente estável", ["saude"]),
        new("Campo do perfil de saúde", Positivo, "perfilDeSaude", "sem restrições", ["saude"]),
        new("CID-10 no meio de uma frase", Positivo, null, "Confirmado CID I10 desde 2019", ["saude"]),
        new("CID-10 com subcategoria", Positivo, null, "cid-10: E11.9", ["saude"]),
        new("Remédios do dicionário", Positivo, null, "Toma metformina e losartana pela manhã", ["saude"]),
        new("Condição em maiúsculas e sem acento", Dificil, null, "HIPERTENSAO controlada", ["saude"]),
        new("Termo de saúde decomposto em NFD", Dificil, null, "Diagnóstico em análise", ["saude"]),
        new("Campo cidade não é CID", Dificil, "cidade", "São Paulo", []),
        new("Palavra que contém CID", Negativo, null, "O cidadão recebeu o alerta", []),
        new("Índice de calor não é dado de saúde", Negativo, null, "Índice de calor 41,3 °C, nível Perigo", []),

        // Credencial
        new("Campo senha", Positivo, "senha", "qualquer coisa", ["credencial"]),
        new("JWT em texto", Positivo, null, $"token recebido: {JwtSintetico()}", ["credencial"]),
        new("Cabeçalho Bearer", Positivo, null, $"Authorization: Bearer {SegredoSintetico(3, 40)}", ["credencial"]),
        new("Chave de API de alta entropia", Positivo, null, $"chave {SegredoSintetico(5, 32)} vazou", ["credencial"]),
        new("UUID de correlação", Negativo, null, "correlationId 3f2504e0-4f89-41d3-9a0c-0305e82c3301", []),
        new("Identificador longo sem dígitos", Negativo, null, "ProcessadorDeNotificacoesDeAlertaDoSombra", []),
        new("Hash curto de commit", Negativo, null, "versão 7ac3d9b", []),
        new("Assinatura HMAC no campo sig não é segredo", Dificil, "sig", Hex(SegredoSintetico(13, 32)), []),
        new("CPF escondido no campo sig continua sendo CPF", Dificil, "sig", _cpf, ["cpf"]),

        // Combinações e texto comum
        new("Vários dados no mesmo texto livre", Positivo, null, $"Ligar (11) 98765-4321 ou maria@exemplo.com, CPF {_cpf}", ["telefone", "email", "cpf"]),
        new("JSON serializado dentro de um campo", Dificil, null, $"{{\"cpf\":\"{_cpfPontuado}\",\"obs\":\"ok\"}}", ["cpf"]),
        new("Coordenadas", Negativo, null, "-23.55052, -46.633308", []),
        new("Mensagem de alerta comum", Negativo, null, "Alerta de calor enviado ao morador às 14h", []),
        new("Identificador da Sentinela", Negativo, null, "sentinela-0042", []),
        new("Campo vazio com chave sensível", Negativo, "cpf", "   ", []),
    ];

    /// <summary>Gets os casos no formato do <c>DynamicData</c>.</summary>
    internal static IEnumerable<object[]> ComoLinhas => Todos.Select(c => new object[] { c });

    /// <summary>Gera um segredo sintético de alta entropia (letras e dígitos), igual a cada execução.</summary>
    /// <param name="semente">Qualquer número.</param>
    /// <param name="tamanho">O tamanho do segredo.</param>
    /// <returns>O segredo.</returns>
    internal static string SegredoSintetico(int semente, int tamanho)
    {
        const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789";
        uint estado = (uint)semente * 2_654_435_761u;
        return string.Create(tamanho, estado, static (destino, s) =>
        {
            for (int i = 0; i < destino.Length; i++)
            {
                s = (s * 1_103_515_245u) + 12_345u;
                destino[i] = alfabeto[(int)((s >> 16) % (uint)alfabeto.Length)];
            }
        });
    }

    /// <summary>Monta um JWT sintético em tempo de execução (assim nenhum token fica literal no repositório).</summary>
    /// <returns>Um JWT com cabeçalho, corpo e assinatura fictícios.</returns>
    internal static string JwtSintetico()
    {
        static string Base64Url(string texto) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(texto)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return string.Join('.', Base64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"), Base64Url("{\"sub\":\"pessoa-ficticia\"}"), Base64Url("assinatura-ficticia"));
    }

    private static string Hex(string texto) => Convert.ToHexStringLower(System.Text.Encoding.UTF8.GetBytes(texto));

    private static string TrocarUltimoDigito(string digitos)
        => digitos[..^1] + (char)('0' + ((digitos[^1] - '0' + 1) % 10));
}
