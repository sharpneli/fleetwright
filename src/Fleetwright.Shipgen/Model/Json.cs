using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Fleetwright.Shipgen;

/// <summary>The shipgen JSON: snake_case keys, nulls left out, // comments allowed, NaN and infinities written as
/// names (a broken build must still serialize, so the fuzzer can report it). Source-generated (AOT).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals)]
[JsonSerializable(typeof(Design))]
[JsonSerializable(typeof(List<BatteryInput>))]
[JsonSerializable(typeof(BatteryInput))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Ship))]
[JsonSerializable(typeof(Dressing))]
public sealed partial class ShipgenJson : JsonSerializerContext
{
    /// <summary>The same, indented one space per level.</summary>
    public static ShipgenJson Indented => indented ??= new(new JsonSerializerOptions(Default.Options) { WriteIndented = true, IndentSize = 1 });

    static ShipgenJson? indented;
}

/// <summary>A point as [x, y].</summary>
public sealed class PtConverter : JsonConverter<Pt>
{
    public override Pt Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("a point is [x, y]");
        reader.Read();
        double x = reader.GetDouble();
        reader.Read();
        double y = reader.GetDouble();
        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException("a point is [x, y]");
        return new Pt(x, y);
    }

    public override void Write(Utf8JsonWriter writer, Pt value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }
}

/// <summary>A list that the JSON may also give as its one item on its own (main and secondary batteries). Written as a
/// list.</summary>
public sealed class OneOrManyConverter<T> : JsonConverter<List<T>>
{
    public override List<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
            return JsonSerializer.Deserialize(ref reader, (JsonTypeInfo<List<T>>)options.GetTypeInfo(typeof(List<T>)));
        var one = JsonSerializer.Deserialize(ref reader, (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T)));
        return one is null ? [] : [one];
    }

    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, (JsonTypeInfo<List<T>>)options.GetTypeInfo(typeof(List<T>)));
}

/// <summary>superfire: true, false or {"fore": n, "aft": n}.</summary>
public sealed class SuperfireConverter : JsonConverter<Superfire>
{
    public override Superfire? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.True or JsonTokenType.False)
            return new Superfire(reader.GetBoolean(), null, null);
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("superfire: use true, false or {\"fore\": n, \"aft\": n}");
        int? fore = null, aft = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            string key = reader.GetString()!;
            reader.Read();
            int n = reader.GetInt32();
            if (key == "fore")
                fore = n;
            else if (key == "aft")
                aft = n;
            else
                throw new JsonException($"superfire.{key}: use fore and aft");
        }
        return new Superfire(null, fore, aft);
    }

    public override void Write(Utf8JsonWriter writer, Superfire value, JsonSerializerOptions options)
    {
        if (value.All is bool b)
        {
            writer.WriteBooleanValue(b);
            return;
        }
        writer.WriteStartObject();
        if (value.Fore is int f)
            writer.WriteNumber("fore", f);
        if (value.Aft is int a)
            writer.WriteNumber("aft", a);
        writer.WriteEndObject();
    }
}

/// <summary>machinery.arrangement: a name or a list of rooms.</summary>
public sealed class ArrangementConverter : JsonConverter<Arrangement>
{
    public override Arrangement? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? new Arrangement(reader.GetString(), null)
            : new Arrangement(null, JsonSerializer.Deserialize(ref reader, (JsonTypeInfo<List<string>>)options.GetTypeInfo(typeof(List<string>))));

    public override void Write(Utf8JsonWriter writer, Arrangement value, JsonSerializerOptions options)
    {
        if (value.Name != null)
            writer.WriteStringValue(value.Name);
        else
            JsonSerializer.Serialize(writer, value.Rooms ?? [], (JsonTypeInfo<List<string>>)options.GetTypeInfo(typeof(List<string>)));
    }
}

/// <summary>A label the JSON may give as a string or a number (a pennant number).</summary>
public sealed class TextConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => System.Text.Encoding.UTF8.GetString(reader.ValueSpan),
            _ => throw new JsonException("give a string or a number"),
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}

/// <summary>A palette entry: "#rrggbb" or a list of them.</summary>
public sealed class PaintConverter : JsonConverter<Paint>
{
    public override Paint? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return Paint.Of(reader.GetString()!);
        return Paint.Of(JsonSerializer.Deserialize(ref reader, (JsonTypeInfo<List<string>>)options.GetTypeInfo(typeof(List<string>)))!);
    }

    public override void Write(Utf8JsonWriter writer, Paint value, JsonSerializerOptions options)
    {
        if (!value.IsList)
        {
            writer.WriteStringValue(value.Colour);
            return;
        }
        writer.WriteStartArray();
        foreach (var c in value.Colours)
            writer.WriteStringValue(c);
        writer.WriteEndArray();
    }
}
