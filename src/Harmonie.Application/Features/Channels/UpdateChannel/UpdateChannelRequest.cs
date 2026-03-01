using System.Text.Json;
using System.Text.Json.Serialization;

namespace Harmonie.Application.Features.Channels.UpdateChannel;

[JsonConverter(typeof(UpdateChannelRequestJsonConverter))]
public sealed class UpdateChannelRequest
{
    public string? Name { get; init; }

    public int? Position { get; init; }

    [JsonIgnore]
    public bool NameIsSet { get; init; }

    [JsonIgnore]
    public bool PositionIsSet { get; init; }
}

internal sealed class UpdateChannelRequestJsonConverter : JsonConverter<UpdateChannelRequest>
{
    public override UpdateChannelRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Request body must be a JSON object.");

        string? name = null;
        int? position = null;
        var nameIsSet = false;
        var positionIsSet = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new UpdateChannelRequest
                {
                    Name = name,
                    Position = position,
                    NameIsSet = nameIsSet,
                    PositionIsSet = positionIsSet
                };
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected a JSON property name.");

            var propertyName = reader.GetString();
            if (!reader.Read())
                throw new JsonException("Unexpected end of JSON payload.");

            if (propertyName is null)
            {
                reader.Skip();
                continue;
            }

            if (propertyName.Equals("name", StringComparison.OrdinalIgnoreCase))
            {
                nameIsSet = true;
                name = reader.TokenType switch
                {
                    JsonTokenType.Null => null,
                    JsonTokenType.String => reader.GetString(),
                    _ => throw new JsonException("Property 'name' must be a string or null.")
                };
            }
            else if (propertyName.Equals("position", StringComparison.OrdinalIgnoreCase))
            {
                positionIsSet = true;
                position = reader.TokenType switch
                {
                    JsonTokenType.Null => null,
                    JsonTokenType.Number => reader.GetInt32(),
                    _ => throw new JsonException("Property 'position' must be a number or null.")
                };
            }
            else
            {
                reader.Skip();
            }
        }

        throw new JsonException("Unexpected end of JSON payload.");
    }

    public override void Write(Utf8JsonWriter writer, UpdateChannelRequest value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (value.NameIsSet)
        {
            writer.WritePropertyName("name");
            if (value.Name is null)
                writer.WriteNullValue();
            else
                writer.WriteStringValue(value.Name);
        }

        if (value.PositionIsSet)
        {
            writer.WritePropertyName("position");
            if (value.Position is null)
                writer.WriteNullValue();
            else
                writer.WriteNumberValue(value.Position.Value);
        }

        writer.WriteEndObject();
    }
}
