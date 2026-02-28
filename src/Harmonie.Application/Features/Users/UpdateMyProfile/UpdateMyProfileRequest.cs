using System.Text.Json;
using System.Text.Json.Serialization;

namespace Harmonie.Application.Features.Users.UpdateMyProfile;

[JsonConverter(typeof(UpdateMyProfileRequestJsonConverter))]
public sealed class UpdateMyProfileRequest
{
    public string? DisplayName { get; init; }

    public string? Bio { get; init; }

    public string? AvatarUrl { get; init; }

    [JsonIgnore]
    public bool DisplayNameIsSet { get; init; }

    [JsonIgnore]
    public bool BioIsSet { get; init; }

    [JsonIgnore]
    public bool AvatarUrlIsSet { get; init; }
}

internal sealed class UpdateMyProfileRequestJsonConverter : JsonConverter<UpdateMyProfileRequest>
{
    public override UpdateMyProfileRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Request body must be a JSON object.");

        string? displayName = null;
        string? bio = null;
        string? avatarUrl = null;
        var displayNameIsSet = false;
        var bioIsSet = false;
        var avatarUrlIsSet = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new UpdateMyProfileRequest
                {
                    DisplayName = displayName,
                    Bio = bio,
                    AvatarUrl = avatarUrl,
                    DisplayNameIsSet = displayNameIsSet,
                    BioIsSet = bioIsSet,
                    AvatarUrlIsSet = avatarUrlIsSet
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

            if (propertyName.Equals("displayName", StringComparison.OrdinalIgnoreCase))
            {
                displayNameIsSet = true;
                displayName = ReadNullableString(ref reader, "displayName");
            }
            else if (propertyName.Equals("bio", StringComparison.OrdinalIgnoreCase))
            {
                bioIsSet = true;
                bio = ReadNullableString(ref reader, "bio");
            }
            else if (propertyName.Equals("avatarUrl", StringComparison.OrdinalIgnoreCase))
            {
                avatarUrlIsSet = true;
                avatarUrl = ReadNullableString(ref reader, "avatarUrl");
            }
            else
            {
                reader.Skip();
            }
        }

        throw new JsonException("Unexpected end of JSON payload.");
    }

    public override void Write(Utf8JsonWriter writer, UpdateMyProfileRequest value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (value.DisplayNameIsSet)
        {
            writer.WritePropertyName("displayName");
            if (value.DisplayName is null)
                writer.WriteNullValue();
            else
                writer.WriteStringValue(value.DisplayName);
        }

        if (value.BioIsSet)
        {
            writer.WritePropertyName("bio");
            if (value.Bio is null)
                writer.WriteNullValue();
            else
                writer.WriteStringValue(value.Bio);
        }

        if (value.AvatarUrlIsSet)
        {
            writer.WritePropertyName("avatarUrl");
            if (value.AvatarUrl is null)
                writer.WriteNullValue();
            else
                writer.WriteStringValue(value.AvatarUrl);
        }

        writer.WriteEndObject();
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
}
