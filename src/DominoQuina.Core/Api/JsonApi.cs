using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DominoQuina.Core.Api;

/// <summary>
/// Configuração e utilitários de JSON compartilhados pelo cliente da API.
/// </summary>
public static class JsonApi
{
    /// <summary>
    /// camelCase, nomes sem diferenciar maiúsculas, números aceitos como texto e
    /// identificadores aceitos como texto ou número.
    /// </summary>
    public static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        Converters = { new TextoOuNumeroConverter() },
    };

    /// <summary>Procura uma propriedade sem diferenciar maiúsculas de minúsculas.</summary>
    public static bool TentarObter(JsonElement objeto, string nome, out JsonElement valor)
    {
        if (objeto.ValueKind == JsonValueKind.Object)
        {
            foreach (var propriedade in objeto.EnumerateObject())
            {
                if (string.Equals(propriedade.Name, nome, StringComparison.OrdinalIgnoreCase))
                {
                    valor = propriedade.Value;
                    return true;
                }
            }
        }

        valor = default;
        return false;
    }

    /// <summary>Lê a primeira propriedade presente entre <paramref name="nomes"/> como texto.</summary>
    public static string? LerTexto(JsonElement objeto, params string[] nomes)
    {
        foreach (var nome in nomes)
        {
            if (!TentarObter(objeto, nome, out var valor)) continue;
            switch (valor.ValueKind)
            {
                case JsonValueKind.String:
                    return valor.GetString();
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return valor.GetRawText();
            }
        }

        return null;
    }

    /// <summary>
    /// Desserializa uma lista que pode vir como array na raiz ou dentro de um objeto
    /// (ex.: <c>{ "itens": [...] }</c>).
    /// </summary>
    public static List<T> LerLista<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        using var documento = JsonDocument.Parse(json);
        var raiz = documento.RootElement;

        if (raiz.ValueKind == JsonValueKind.Object)
        {
            raiz = raiz.EnumerateObject()
                .Select(p => p.Value)
                .FirstOrDefault(v => v.ValueKind == JsonValueKind.Array);
        }

        return raiz.ValueKind == JsonValueKind.Array
            ? raiz.Deserialize<List<T>>(Opcoes) ?? []
            : [];
    }
}

/// <summary>
/// Lê propriedades <see cref="string"/> que a API pode enviar como texto ou número
/// (por exemplo, identificadores GUID ou inteiros).
/// </summary>
public sealed class TextoOuNumeroConverter : JsonConverter<string>
{
    /// <inheritdoc />
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => Encoding.UTF8.GetString(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            _ => throw new JsonException($"Valor inesperado para texto: {reader.TokenType}."),
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
