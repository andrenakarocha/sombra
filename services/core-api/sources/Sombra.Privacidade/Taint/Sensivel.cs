using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sombra.Privacidade.Regras;

namespace Sombra.Privacidade.Taint;

/// <summary>
/// Taint como tipo (spec 07 §2): um dado pessoal embrulhado. Em log e em <c>ToString</c> vira <c>[PII:FAMILIA]</c>; o
/// serializador JSON recusa. Para sair, só com <see cref="Revelar"/>, dizendo a finalidade, num endpoint
/// <c>[PermitePii]</c>. Vazar por descuido vira impossível ou barulhento.
/// </summary>
/// <typeparam name="T">O tipo do dado.</typeparam>
[JsonConverter(typeof(SensivelNaoSerializa))]
public readonly record struct Sensivel<T>
{
    private readonly T _valor;

    /// <summary>Initializes a new instance of the <see cref="Sensivel{T}"/> struct.</summary>
    /// <param name="valor">O dado.</param>
    /// <param name="categoria">A família do dado.</param>
    public Sensivel(T valor, CategoriaDePii categoria)
    {
        _valor = valor;
        Categoria = categoria;
    }

    /// <summary>Gets a família do dado.</summary>
    public CategoriaDePii Categoria { get; }

    /// <summary>Devolve o dado, de propósito.</summary>
    /// <param name="finalidade">Para que o dado vai sair (ex.: "o cuidador liga para o contato").</param>
    /// <returns>O dado.</returns>
    /// <exception cref="ArgumentException">Sem finalidade.</exception>
    public T Revelar(string finalidade)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalidade);
        return _valor;
    }

    /// <summary>Nunca mostra o dado.</summary>
    /// <returns><c>[PII:FAMILIA]</c>.</returns>
    public override string ToString() => $"[PII:{Categoria.ToString().ToUpperInvariant()}]";
}

/// <summary>O conversor que faz o JSON recusar um <see cref="Sensivel{T}"/>.</summary>
public sealed class SensivelNaoSerializa : JsonConverterFactory
{
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert?.IsGenericType == true && typeToConvert.GetGenericTypeDefinition() == typeof(Sensivel<>);

    /// <inheritdoc/>
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter?)Activator.CreateInstance(typeof(Recusa<>).MakeGenericType(typeToConvert!.GetGenericArguments()[0]));

    private sealed class Recusa<T> : JsonConverter<Sensivel<T>>
    {
        public override Sensivel<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new JsonException("Sensivel<T> não é lido de JSON.");

        public override void Write(Utf8JsonWriter writer, Sensivel<T> value, JsonSerializerOptions options)
            => throw new InvalidOperationException($"Um dado {value.Categoria} (Sensivel<T>) não sai em JSON: chame Revelar(finalidade) num endpoint [PermitePii].");
    }
}
