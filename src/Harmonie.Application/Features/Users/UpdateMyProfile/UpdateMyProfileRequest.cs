using System.Text.Json;
using System.Text.Json.Serialization;

namespace Harmonie.Application.Features.Users.UpdateMyProfile;

[JsonConverter(typeof(UpdateMyProfileRequestJsonConverter))]
public sealed class UpdateMyProfileRequest
{
    public string? DisplayName { get; init; }
    public string? Bio { get; init; }
    public string? AvatarUrl { get; init; }

    public string? AvatarColor { get; init; }
    public string? AvatarIcon { get; init; }
    public string? AvatarBg { get; init; }

    public string? Theme { get; init; }
    public string? Language { get; init; }

    [JsonIgnore] public bool DisplayNameIsSet { get; init; }
    [JsonIgnore] public bool BioIsSet { get; init; }
    [JsonIgnore] public bool AvatarUrlIsSet { get; init; }

    [JsonIgnore] public bool AvatarIsSet { get; init; }
    [JsonIgnore] public bool AvatarColorIsSet { get; init; }
    [JsonIgnore] public bool AvatarIconIsSet { get; init; }
    [JsonIgnore] public bool AvatarBgIsSet { get; init; }

    [JsonIgnore] public bool ThemeIsSet { get; init; }
    [JsonIgnore] public bool LanguageIsSet { get; init; }
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
        string? avatarColor = null;
        string? avatarIcon = null;
        string? avatarBg = null;
        string? theme = null;
        string? language = null;

        var displayNameIsSet = false;
        var bioIsSet = false;
        var avatarUrlIsSet = false;
        var avatarIsSet = false;
        var avatarColorIsSet = false;
        var avatarIconIsSet = false;
        var avatarBgIsSet = false;
        var themeIsSet = false;
        var languageIsSet = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new UpdateMyProfileRequest
                {
                    DisplayName = displayName,
                    Bio = bio,
                    AvatarUrl = avatarUrl,
                    AvatarColor = avatarColor,
                    AvatarIcon = avatarIcon,
                    AvatarBg = avatarBg,
                    Theme = theme,
                    Language = language,
                    DisplayNameIsSet = displayNameIsSet,
                    BioIsSet = bioIsSet,
                    AvatarUrlIsSet = avatarUrlIsSet,
                    AvatarIsSet = avatarIsSet,
                    AvatarColorIsSet = avatarColorIsSet,
                    AvatarIconIsSet = avatarIconIsSet,
                    AvatarBgIsSet = avatarBgIsSet,
                    ThemeIsSet = themeIsSet,
                    LanguageIsSet = languageIsSet
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
            else if (propertyName.Equals("avatar", StringComparison.OrdinalIgnoreCase))
            {
                avatarIsSet = true;
                if (reader.TokenType == JsonTokenType.Null)
                {
                    // avatar: null => clear all avatar fields
                    avatarColorIsSet = true;
                    avatarIconIsSet = true;
                    avatarBgIsSet = true;
                }
                else if (reader.TokenType == JsonTokenType.StartObject)
                {
                    ReadAvatarObject(ref reader, ref avatarColor, ref avatarIcon, ref avatarBg,
                        ref avatarColorIsSet, ref avatarIconIsSet, ref avatarBgIsSet);
                }
                else
                {
                    throw new JsonException("Property 'avatar' must be an object or null.");
                }
            }
            else if (propertyName.Equals("theme", StringComparison.OrdinalIgnoreCase))
            {
                themeIsSet = true;
                if (reader.TokenType == JsonTokenType.Null)
                    throw new JsonException("Property 'theme' cannot be null.");
                if (reader.TokenType != JsonTokenType.String)
                    throw new JsonException("Property 'theme' must be a string.");
                theme = reader.GetString();
            }
            else if (propertyName.Equals("language", StringComparison.OrdinalIgnoreCase))
            {
                languageIsSet = true;
                language = ReadNullableString(ref reader, "language");
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
            WriteNullableString(writer, "displayName", value.DisplayName);

        if (value.BioIsSet)
            WriteNullableString(writer, "bio", value.Bio);

        if (value.AvatarUrlIsSet)
            WriteNullableString(writer, "avatarUrl", value.AvatarUrl);

        if (value.AvatarIsSet)
        {
            writer.WritePropertyName("avatar");
            if (!value.AvatarColorIsSet && !value.AvatarIconIsSet && !value.AvatarBgIsSet
                && value.AvatarColor is null && value.AvatarIcon is null && value.AvatarBg is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStartObject();
                if (value.AvatarColorIsSet)
                    WriteNullableString(writer, "color", value.AvatarColor);
                if (value.AvatarIconIsSet)
                    WriteNullableString(writer, "icon", value.AvatarIcon);
                if (value.AvatarBgIsSet)
                    WriteNullableString(writer, "bg", value.AvatarBg);
                writer.WriteEndObject();
            }
        }

        if (value.ThemeIsSet)
        {
            writer.WritePropertyName("theme");
            writer.WriteStringValue(value.Theme);
        }

        if (value.LanguageIsSet)
            WriteNullableString(writer, "language", value.Language);

        writer.WriteEndObject();
    }

    private static void ReadAvatarObject(
        ref Utf8JsonReader reader,
        ref string? avatarColor,
        ref string? avatarIcon,
        ref string? avatarBg,
        ref bool avatarColorIsSet,
        ref bool avatarIconIsSet,
        ref bool avatarBgIsSet)
    {
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected a JSON property name inside 'avatar'.");

            var prop = reader.GetString();
            if (!reader.Read())
                throw new JsonException("Unexpected end of JSON payload inside 'avatar'.");

            if (prop is null)
            {
                reader.Skip();
                continue;
            }

            if (prop.Equals("color", StringComparison.OrdinalIgnoreCase))
            {
                avatarColorIsSet = true;
                avatarColor = ReadNullableString(ref reader, "avatar.color");
            }
            else if (prop.Equals("icon", StringComparison.OrdinalIgnoreCase))
            {
                avatarIconIsSet = true;
                avatarIcon = ReadNullableString(ref reader, "avatar.icon");
            }
            else if (prop.Equals("bg", StringComparison.OrdinalIgnoreCase))
            {
                avatarBgIsSet = true;
                avatarBg = ReadNullableString(ref reader, "avatar.bg");
            }
            else
            {
                reader.Skip();
            }
        }

        throw new JsonException("Unexpected end of JSON payload inside 'avatar'.");
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
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value);
    }
}
