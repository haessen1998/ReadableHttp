using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ReadableHttp.Core;

[JsonConverter(typeof(ReadableVariableJsonConverter))]
public sealed class ReadableVariable
{
    public JsonNode? Value { get; set; }

    public ReadableVariableType Type { get; set; } = ReadableVariableType.String;

    public bool Enabled { get; set; } = true;

    public string? Description { get; set; }

    public static implicit operator ReadableVariable(string? value)
    {
        return new ReadableVariable
        {
            Value = value is null ? null : JsonValue.Create(value),
            Type = ReadableVariableType.String
        };
    }

    public string? ToTemplateValue()
    {
        if (!Enabled || Value is null)
        {
            return null;
        }

        return Value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
            ? text
            : Value.ToJsonString();
    }
}

public enum ReadableVariableType
{
    String,
    Number,
    Boolean,
    Json
}

internal sealed class ReadableVariableJsonConverter : JsonConverter<ReadableVariable>
{
    public override ReadableVariable? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("ReadableHttp variables must be JSON objects with a value property.");
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var variable = new ReadableVariable();

        if (root.TryGetProperty("value", out var valueProperty))
        {
            variable.Value = JsonNode.Parse(valueProperty.GetRawText());
            variable.Type = InferType(variable.Value);
        }

        if (root.TryGetProperty("type", out var typeProperty)
            && Enum.TryParse<ReadableVariableType>(typeProperty.GetString(), ignoreCase: true, out var type))
        {
            variable.Type = type;
        }

        if (root.TryGetProperty("enabled", out var enabledProperty)
            && enabledProperty.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            variable.Enabled = enabledProperty.GetBoolean();
        }

        if (root.TryGetProperty("description", out var descriptionProperty))
        {
            variable.Description = descriptionProperty.GetString();
        }

        return variable;
    }

    public override void Write(
        Utf8JsonWriter writer,
        ReadableVariable value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("value");
        if (value.Value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            value.Value.WriteTo(writer, options);
        }

        writer.WriteString("type", JsonNamingPolicy.CamelCase.ConvertName(value.Type.ToString()));
        writer.WriteBoolean("enabled", value.Enabled);

        if (!string.IsNullOrWhiteSpace(value.Description))
        {
            writer.WriteString("description", value.Description);
        }

        writer.WriteEndObject();
    }

    private static ReadableVariableType InferType(JsonNode? value)
    {
        if (value is not JsonValue jsonValue)
        {
            return ReadableVariableType.Json;
        }

        if (jsonValue.TryGetValue<bool>(out _))
        {
            return ReadableVariableType.Boolean;
        }

        if (jsonValue.TryGetValue<decimal>(out _))
        {
            return ReadableVariableType.Number;
        }

        return ReadableVariableType.String;
    }
}
