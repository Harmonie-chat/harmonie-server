using System.Text.Json.Serialization;

namespace Harmonie.API.SignalRDoc.Models;

public sealed class AsyncApiDocument
{
    [JsonPropertyName("asyncapi")]
    public string Asyncapi { get; set; } = "3.1.0";

    [JsonPropertyName("info")]
    public AsyncApiInfo Info { get; set; } = new();

    [JsonPropertyName("servers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, AsyncApiServer>? Servers { get; set; }

    [JsonPropertyName("channels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, AsyncApiChannel>? Channels { get; set; }

    [JsonPropertyName("operations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, AsyncApiOperation>? Operations { get; set; }

    [JsonPropertyName("components")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AsyncApiComponents? Components { get; set; }
}

public sealed class AsyncApiInfo
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
}

public sealed class AsyncApiServer
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = "";

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "wss";

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
}

public sealed class AsyncApiChannel
{
    [JsonPropertyName("address")]
    public string Address { get; set; } = "";

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("messages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, AsyncApiRef>? Messages { get; set; }
}

public sealed class AsyncApiOperation
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    [JsonPropertyName("channel")]
    public AsyncApiRef Channel { get; set; } = new();

    [JsonPropertyName("messages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AsyncApiRef>? Messages { get; set; }

    [JsonPropertyName("summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Summary { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
}

public sealed class AsyncApiMessage
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("payload")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AsyncApiSchema? Payload { get; set; }
}

public sealed class AsyncApiSchema
{
    [JsonPropertyName("$ref")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ref { get; set; }

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonPropertyName("format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; set; }

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, AsyncApiSchema>? Properties { get; set; }

    [JsonPropertyName("items")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AsyncApiSchema? Items { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Enum { get; set; }

    [JsonPropertyName("nullable")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Nullable { get; set; }
}

public sealed class AsyncApiComponents
{
    [JsonPropertyName("schemas")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, AsyncApiSchema>? Schemas { get; set; }

    [JsonPropertyName("messages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, AsyncApiMessage>? Messages { get; set; }
}

public sealed class AsyncApiRef
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = "";
}
