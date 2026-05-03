using System.Text.Json;
using Harmonie.API.SignalRDoc.Extensions;
using Harmonie.API.SignalRDoc.Models;
using Microsoft.Extensions.Options;


namespace Harmonie.API.SignalRDoc.Generator;

public sealed class AsyncApiGenerator
{
    private readonly Lazy<AsyncApiDocument> _document;

    public AsyncApiGenerator(IOptions<SignalRDocOptions> options, HubDiscovery discovery, SchemaGenerator schemaGenerator)
    {
        _document = new Lazy<AsyncApiDocument>(
            () => BuildDocument(options.Value, discovery, schemaGenerator),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public AsyncApiDocument Generate() => _document.Value;

    private static AsyncApiDocument BuildDocument(
        SignalRDocOptions options,
        HubDiscovery discovery,
        SchemaGenerator schemaGenerator)
    {
        var hubs = discovery.Discover(options.Assemblies, options.HubRoutes);

        var channels = new Dictionary<string, AsyncApiChannel>();
        var operations = new Dictionary<string, AsyncApiOperation>();
        var componentMessages = new Dictionary<string, AsyncApiMessage>();
        var componentSchemas = new Dictionary<string, AsyncApiSchema>();

        foreach (var hub in hubs)
        {
            var channelKey = HubChannelKey(hub.HubType);
            var channelMessages = new Dictionary<string, AsyncApiRef>();

            foreach (var method in hub.ClientToServerMethods)
                AddMethodToDocument(hub, method, channelKey, "receive", channelMessages, operations, componentMessages, componentSchemas, schemaGenerator, options.MethodTags);

            foreach (var method in hub.ServerToClientMethods)
                AddMethodToDocument(hub, method, channelKey, "send", channelMessages, operations, componentMessages, componentSchemas, schemaGenerator, options.MethodTags);

            channels[channelKey] = new AsyncApiChannel
            {
                Address = hub.Route,
                Messages = channelMessages.Count > 0 ? channelMessages : null,
            };
        }

        var doc = new AsyncApiDocument
        {
            Info = new AsyncApiInfo
            {
                Title = options.Title,
                Version = options.Version,
                Description = options.Description,
            },
            Channels = channels.Count > 0 ? channels : null,
            Operations = operations.Count > 0 ? operations : null,
            Components = componentMessages.Count > 0 || componentSchemas.Count > 0
                ? new AsyncApiComponents
                {
                    Messages = componentMessages.Count > 0 ? componentMessages : null,
                    Schemas = componentSchemas.Count > 0 ? componentSchemas : null,
                }
                : null,
        };

        if (!string.IsNullOrWhiteSpace(options.ServerHost))
        {
            doc.Servers = new Dictionary<string, AsyncApiServer>
            {
                ["default"] = new AsyncApiServer
                {
                    Host = options.ServerHost,
                    Protocol = "wss",
                    Description = "Harmonie SignalR server",
                },
            };
        }

        return doc;
    }

    private static void AddMethodToDocument(
        HubDescriptor hub,
        HubMethodDescriptor method,
        string channelKey,
        string action,
        Dictionary<string, AsyncApiRef> channelMessages,
        Dictionary<string, AsyncApiOperation> operations,
        Dictionary<string, AsyncApiMessage> componentMessages,
        Dictionary<string, AsyncApiSchema> componentSchemas,
        SchemaGenerator schemaGenerator,
        IReadOnlyDictionary<string, string[]> methodTags)
    {
        var operationKey = $"{channelKey}.{method.Name}";
        var messageKey = $"{operationKey}.Message";

        var payload = BuildPayloadSchema(hub.HubType, method, channelKey, componentSchemas, schemaGenerator);

        componentMessages[messageKey] = new AsyncApiMessage
        {
            Name = method.Name,
            Payload = payload,
        };

        channelMessages[messageKey] = new AsyncApiRef { Ref = $"#/components/messages/{messageKey}" };

        var tags = ResolveMethodTags(method.Name, methodTags);

        operations[operationKey] = new AsyncApiOperation
        {
            Action = action,
            Channel = new AsyncApiRef { Ref = $"#/channels/{channelKey}" },
            Messages = new[] { new AsyncApiRef { Ref = $"#/components/messages/{messageKey}" } },
            Summary = method.Summary,
            Tags = tags,
        };
    }

    private static AsyncApiSchema? BuildPayloadSchema(
        Type hubType,
        HubMethodDescriptor method,
        string channelKey,
        Dictionary<string, AsyncApiSchema> schemas,
        SchemaGenerator schemaGenerator)
    {
        var parameters = method.Parameters;

        if (parameters.Count == 0)
            return null;

        if (parameters.Count == 1)
            return schemaGenerator.GetSchema(parameters[0].Type, schemas);

        // Multiple parameters: create an inline object schema stored in components/schemas
        var schemaKey = $"{channelKey}.{method.Name}.Parameters";
        var props = new Dictionary<string, AsyncApiSchema>();
        foreach (var param in parameters)
        {
            var paramSchema = schemaGenerator.GetSchema(param.Type, schemas);
            if (paramSchema is not null)
                props[param.Name] = paramSchema;
        }

        var objectSchema = new AsyncApiSchema { Type = "object", Properties = props.Count > 0 ? props : null };
        schemas[schemaKey] = objectSchema;
        return new AsyncApiSchema { Ref = $"#/components/schemas/{schemaKey}" };
    }

    private static IReadOnlyList<AsyncApiTag>? ResolveMethodTags(
        string methodName,
        IReadOnlyDictionary<string, string[]> methodTags)
    {
        if (methodTags.Count == 0)
            return null;

        var matched = new List<AsyncApiTag>();
        foreach (var (tag, prefixes) in methodTags)
        {
            foreach (var prefix in prefixes)
            {
                if (methodName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matched.Add(new AsyncApiTag { Name = tag });
                    break;
                }
            }
        }

        return matched.Count > 0 ? matched : null;
    }

    internal static string HubChannelKey(Type hubType)
    {
        var name = hubType.Name;
        if (name.EndsWith("Hub", StringComparison.OrdinalIgnoreCase))
            name = name[..^3];
        return name;
    }
}
