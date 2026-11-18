using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sombra.Privacidade.Deteccao;

namespace Sombra.Privacidade.Regras;

/// <summary>
/// Lê o <c>pii-regras.json</c> e valida o schema na carga: campos obrigatórios e desconhecidos, enums, ids únicos,
/// regex que compila, validador e máscara conhecidos. Uma regra errada derruba a carga com todos os problemas listados,
/// em vez de virar um detector que não detecta nada.
/// </summary>
public static class CarregadorDeRegras
{
    /// <summary>A versão do formato que este carregador entende.</summary>
    public const int VersaoDoFormato = 1;

    /// <summary>O nome do recurso embarcado com as regras padrão.</summary>
    public const string RecursoPadrao = "pii-regras.json";

    private static readonly HashSet<string> _camposDaRaiz = new(StringComparer.Ordinal) { "versao", "regras" };

    private static readonly HashSet<string> _camposDaRegra = new(StringComparer.Ordinal)
    {
        "id", "categoria", "severidade", "chaves", "padrao", "validador", "termos", "mascara", "baseLegal", "excetoChaves", "campoLivre",
    };

    private static readonly Lazy<IReadOnlyList<RegraDePii>> _padrao = new(() =>
    {
        using Stream recurso = typeof(CarregadorDeRegras).Assembly.GetManifestResourceStream(RecursoPadrao)
            ?? throw new InvalidOperationException($"recurso {RecursoPadrao} ausente no assembly");
        using StreamReader leitor = new(recurso);
        return Carregar(leitor.ReadToEnd());
    });

    /// <summary>As regras padrão, embarcadas no assembly (carregadas e validadas uma vez).</summary>
    /// <returns>As regras na ordem do arquivo.</returns>
    public static IReadOnlyList<RegraDePii> CarregarPadrao() => _padrao.Value;

    /// <summary>Lê e valida um conjunto de regras.</summary>
    /// <param name="json">O conteúdo do arquivo.</param>
    /// <returns>As regras na ordem do arquivo.</returns>
    /// <exception cref="RegrasDePiiInvalidasException">Quando o arquivo não segue o schema.</exception>
    public static IReadOnlyList<RegraDePii> Carregar(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        List<string> erros = [];
        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new RegrasDePiiInvalidasException([$"JSON malformado: {ex.Message}"]);
        }

        using (documento)
        {
            List<RegraDePii> regras = LerRaiz(documento.RootElement, erros);
            if (erros.Count > 0)
            {
                throw new RegrasDePiiInvalidasException(erros);
            }

            return regras;
        }
    }

    private static List<RegraDePii> LerRaiz(JsonElement raiz, List<string> erros)
    {
        if (raiz.ValueKind != JsonValueKind.Object)
        {
            erros.Add("$: precisa ser um objeto");
            return [];
        }

        erros.AddRange(raiz.EnumerateObject().Where(p => !_camposDaRaiz.Contains(p.Name)).Select(p => $"$.{p.Name}: campo desconhecido"));
        if (!raiz.TryGetProperty("versao", out JsonElement versao) || versao.ValueKind != JsonValueKind.Number
            || !versao.TryGetInt32(out int numero) || numero != VersaoDoFormato)
        {
            erros.Add($"$.versao: precisa ser {VersaoDoFormato}");
        }

        if (!raiz.TryGetProperty("regras", out JsonElement lista) || lista.ValueKind != JsonValueKind.Array || lista.GetArrayLength() == 0)
        {
            erros.Add("$.regras: precisa ser uma lista com pelo menos uma regra");
            return [];
        }

        List<RegraDePii> regras = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        int indice = 0;
        foreach (JsonElement item in lista.EnumerateArray())
        {
            string caminho = $"$.regras[{indice++}]";
            if (LerRegra(item, caminho, erros) is { } regra)
            {
                if (!ids.Add(regra.Id))
                {
                    erros.Add($"{caminho}.id: '{regra.Id}' repetido");
                }

                regras.Add(regra);
            }
        }

        return regras;
    }

    private static RegraDePii? LerRegra(JsonElement item, string caminho, List<string> erros)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            erros.Add($"{caminho}: precisa ser um objeto");
            return null;
        }

        int antes = erros.Count;
        erros.AddRange(item.EnumerateObject().Where(p => !_camposDaRegra.Contains(p.Name)).Select(p => $"{caminho}.{p.Name}: campo desconhecido"));

        string? id = Texto(item, "id", caminho, erros, obrigatorio: true);
        if (id is not null && !IdValido(id))
        {
            erros.Add($"{caminho}.id: use minúsculas, dígitos e hífen (ex.: cpf, cartao-sus)");
        }

        CategoriaDePii categoria = Enum<CategoriaDePii>(item, "categoria", caminho, erros);
        SeveridadeDePii severidade = Enum<SeveridadeDePii>(item, "severidade", caminho, erros);
        List<string> chaves = ListaDeTextos(item, "chaves", caminho, erros);
        List<string> termos = ListaDeTextos(item, "termos", caminho, erros);
        List<string> excetoChaves = ListaDeTextos(item, "excetoChaves", caminho, erros);
        string? padrao = Texto(item, "padrao", caminho, erros, obrigatorio: false);
        if (padrao is not null && !RegexCompila(padrao, out string? motivo))
        {
            erros.Add($"{caminho}.padrao: regex inválida ({motivo})");
        }

        string validador = Texto(item, "validador", caminho, erros, obrigatorio: false) ?? Validadores.Nenhum;
        if (!Validadores.TentarObter(validador, out _))
        {
            erros.Add($"{caminho}.validador: '{validador}' desconhecido (use {string.Join(", ", Validadores.Nomes.Order(StringComparer.Ordinal))})");
        }

        string? mascara = Texto(item, "mascara", caminho, erros, obrigatorio: true);
        if (mascara is not null && !MascaradorDePii.Formatos.Contains(mascara))
        {
            erros.Add($"{caminho}.mascara: '{mascara}' desconhecida (use {string.Join(", ", MascaradorDePii.Formatos)})");
        }

        string? baseLegal = Texto(item, "baseLegal", caminho, erros, obrigatorio: true);
        if (baseLegal is not null && !baseLegal.StartsWith("LGPD art.", StringComparison.Ordinal))
        {
            erros.Add($"{caminho}.baseLegal: cite o artigo (ex.: LGPD art. 11)");
        }

        bool campoLivre = false;
        if (item.TryGetProperty("campoLivre", out JsonElement livre))
        {
            if (livre.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                campoLivre = livre.GetBoolean();
            }
            else
            {
                erros.Add($"{caminho}.campoLivre: precisa ser true ou false");
            }
        }

        if (campoLivre && chaves.Count == 0)
        {
            erros.Add($"{caminho}.campoLivre: só vale para regras com chaves");
        }

        if (excetoChaves.Count > 0 && termos.Count == 0 && padrao is null)
        {
            erros.Add($"{caminho}.excetoChaves: só vale para regras com padrao ou termos");
        }

        if (chaves.Count == 0 && termos.Count == 0 && padrao is null)
        {
            erros.Add($"{caminho}: a regra precisa de chaves, padrao ou termos");
        }

        return erros.Count > antes
            ? null
            : new RegraDePii(id!, categoria, severidade, chaves, padrao, validador, termos, mascara!, baseLegal!, excetoChaves, campoLivre);
    }

    private static string? Texto(JsonElement item, string campo, string caminho, List<string> erros, bool obrigatorio)
    {
        if (!item.TryGetProperty(campo, out JsonElement valor) || valor.ValueKind == JsonValueKind.Null)
        {
            if (obrigatorio)
            {
                erros.Add($"{caminho}.{campo}: obrigatório");
            }

            return null;
        }

        if (valor.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(valor.GetString()))
        {
            erros.Add($"{caminho}.{campo}: precisa ser um texto não vazio");
            return null;
        }

        return valor.GetString();
    }

    private static List<string> ListaDeTextos(JsonElement item, string campo, string caminho, List<string> erros)
    {
        if (!item.TryGetProperty(campo, out JsonElement valor))
        {
            return [];
        }

        if (valor.ValueKind != JsonValueKind.Array
            || valor.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(e.GetString())))
        {
            erros.Add($"{caminho}.{campo}: precisa ser uma lista de textos não vazios");
            return [];
        }

        return [.. valor.EnumerateArray().Select(e => e.GetString()!)];
    }

    private static T Enum<T>(JsonElement item, string campo, string caminho, List<string> erros)
        where T : struct, Enum
    {
        if (item.TryGetProperty(campo, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            && System.Enum.TryParse(valor.GetString(), ignoreCase: false, out T resultado) && System.Enum.IsDefined(resultado)
            && !int.TryParse(valor.GetString(), out _))
        {
            return resultado;
        }

        erros.Add($"{caminho}.{campo}: use {string.Join(", ", System.Enum.GetNames<T>())}");
        return default;
    }

    private static bool IdValido(string id)
        => char.IsAsciiLetterLower(id[0]) && id.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-');

    private static bool RegexCompila(string padrao, out string? motivo)
    {
        try
        {
            _ = new Regex(padrao, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            motivo = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            motivo = ex.Message;
            return false;
        }
    }
}
