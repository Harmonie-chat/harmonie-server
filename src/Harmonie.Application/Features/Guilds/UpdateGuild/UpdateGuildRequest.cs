using System.Text.Json;
using System.Text.Json.Serialization;

namespace Harmonie.Application.Features.Guilds.UpdateGuild;

[JsonConverter(typeof(UpdateGuildRequestJsonConverter))]
public sealed class UpdateGuildRequest
{
    public string? Name { get; init; }
    public string? IconFileId { get; init; }

    public string? IconColor { get; init; }
    public string? IconName { get; init; }
    public string? IconBg { get; init; }

    [JsonIgnore] public bool NameIsSet { get; init; }
    [JsonIgnore] public bool IconFileIdIsSet { get; init; }

    [JsonIgnore] public bool IconIsSet { get; init; }
    [JsonIgnore] public bool IconColorIsSet { get; init; }
    [JsonIgnore] public bool IconNameIsSet { get; init; }
    [JsonIgnore] public bool IconBgIsSet { get; init; }
}

internal sealed class UpdateGuildRequestJsonConverter : JsonConverter<UpdateGuildRequest>
{
    public override UpdateGuildRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Request body must be a JSON object.");

        string? name = null;
        string? iconFileId = null;
        string? iconColor = null;
        string? iconName = null;
        string? iconBg = null;

        var nameIsSet = false;
        var iconFileIdIsSet = false;
        var iconIsSet = false;
        var iconColorIsSet = false;
        var iconNameIsSet = false;
        var iconBgIsSet = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new UpdateGuildRequest
                {
                    Name = name,
                    IconFileId = iconFileId,
                    IconColor = iconColor,
                    IconName = iconName,
                    IconBg = iconBg,
                    NameIsSet = nameIsSet,
                    IconFileIdIsSet = iconFileIdIsSet,
                    IconIsSet = iconIsSet,
                    IconColorIsSet = iconColorIsSet,
                    IconNameIsSet = iconNameIsSet,
                    IconBgIsSet = iconBgIsSet
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
                name = ReadRequiredString(ref reader, "name");
            }
            else if (propertyName.Equals("iconFileId", StringComparison.OrdinalIgnoreCase))
            {
                iconFileIdIsSet = true;
                iconFileId = ReadNullableString(ref reader, "iconFileId");
            }
            else if (propertyName.Equals("icon", StringComparison.OrdinalIgnoreCase))
            {
                iconIsSet = true;
                if (reader.TokenType == JsonTokenType.Null)
                {
                    iconColorIsSet = true;
                    iconNameIsSet = true;
                    iconBgIsSet = true;
                }
                else if (reader.TokenType == JsonTokenType.StartObject)
                {
                    ReadIconObject(
                        ref reader,
                        ref iconColor,
                        ref iconName,
                        ref iconBg,
                        ref iconColorIsSet,
                        ref iconNameIsSet,
                        ref iconBgIsSet);
                }
                else
                {
                    throw new JsonException("Property 'icon' must be an object or null.");
                }
            }
            else
            {
                reader.Skip();
            }
        }

        throw new JsonException("Unexpected end of JSON payload.");
    }

    public override void Write(Utf8JsonWriter writer, UpdateGuildRequest value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (value.NameIsSet)
        {
            writer.WritePropertyName("name");
            writer.WriteStringValue(value.Name);
        }

        if (value.IconFileIdIsSet)
            WriteNullableString(writer, "iconFileId", value.IconFileId);

        if (value.IconIsSet)
        {
            writer.WritePropertyName("icon");
            if (!value.IconColorIsSet && !value.IconNameIsSet && !value.IconBgIsSet
                && value.IconColor is null && value.IconName is null && value.IconBg is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStartObject();
                if (value.IconColorIsSet)
                    WriteNullableString(writer, "color", value.IconColor);
                if (value.IconNameIsSet)
                    WriteNullableString(writer, "name", value.IconName);
                if (value.IconBgIsSet)
                    WriteNullableString(writer, "bg", value.IconBg);
                writer.WriteEndObject();
            }
        }

        writer.WriteEndObject();
    }

    private static void ReadIconObject(
        ref Utf8JsonReader reader,
        ref string? iconColor,
        ref string? iconName,
        ref string? iconBg,
        ref bool iconColorIsSet,
        ref bool iconNameIsSet,
        ref bool iconBgIsSet)
    {
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected a JSON property name inside 'icon'.");

            var propertyName = reader.GetString();
            if (!reader.Read())
                throw new JsonException("Unexpected end of JSON payload inside 'icon'.");

            if (propertyName is null)
            {
                reader.Skip();
                continue;
            }

            if (propertyName.Equals("color", StringComparison.OrdinalIgnoreCase))
            {
                iconColorIsSet = true;
                iconColor = ReadNullableString(ref reader, "icon.color");
            }
            else if (propertyName.Equals("name", StringComparison.OrdinalIgnoreCase))
            {
                iconNameIsSet = true;
                iconName = ReadNullableString(ref reader, "icon.name");
            }
            else if (propertyName.Equals("bg", StringComparison.OrdinalIgnoreCase))
            {
                iconBgIsSet = true;
                iconBg = ReadNullableString(ref reader, "icon.bg");
            }
            else
            {
                reader.Skip();
            }
        }

        throw new JsonException("Unexpected end of JSON payload inside 'icon'.");
    }

    private static string ReadRequiredString(ref Utf8JsonReader reader, string propertyName)
    {
        if (reader.TokenType == JsonTokenType.Null)
            throw new JsonException($"Property '{propertyName}' cannot be null.");

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Property '{propertyName}' must be a string.");

        return reader.GetString() ?? string.Empty;
    }

    private static string? ReadNullableString(ref Utf8JsonReader reader, string propertyName)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            _ => throw new JsonException($"Property '{propertyName}' must be a string or null.")
        };
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        writer.WritePropertyName(propertyName);
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }
}
