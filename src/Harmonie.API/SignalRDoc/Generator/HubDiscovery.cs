using System.Reflection;
using Microsoft.AspNetCore.SignalR;

namespace Harmonie.API.SignalRDoc.Generator;

public sealed class HubDiscovery
{
    private static readonly HashSet<string> ExcludedMethods = new(StringComparer.Ordinal)
    {
        "OnConnectedAsync",
        "OnDisconnectedAsync",
        "Dispose",
    };

    public IReadOnlyList<HubDescriptor> Discover(
        IEnumerable<Assembly> assemblies,
        IReadOnlyDictionary<Type, string>? routeOverrides = null)
    {
        var result = new List<HubDescriptor>();

        foreach (var assembly in assemblies)
        {
            IEnumerable<Type> types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.OfType<Type>();
            }

            foreach (var type in types)
            {
                if (type.IsAbstract || !type.IsClass)
                    continue;

                if (!TryGetClientInterface(type, out var clientInterface) || clientInterface is null)
                    continue;

                var route = ResolveRoute(type, routeOverrides);
                var clientToServer = ExtractClientToServerMethods(type);
                var serverToClient = ExtractServerToClientMethods(clientInterface);

                result.Add(new HubDescriptor(type, clientInterface, route, clientToServer, serverToClient));
            }
        }

        return result;
    }

    private static bool TryGetClientInterface(Type type, out Type? clientInterface)
    {
        clientInterface = null;
        var current = type.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Hub<>))
            {
                clientInterface = current.GetGenericArguments()[0];
                return true;
            }
            current = current.BaseType;
        }
        return false;
    }

    private static string ResolveRoute(Type hubType, IReadOnlyDictionary<Type, string>? routeOverrides)
    {
        if (routeOverrides is not null && routeOverrides.TryGetValue(hubType, out var override_))
            return override_;

        var name = hubType.Name;
        if (name.EndsWith("Hub", StringComparison.OrdinalIgnoreCase))
            name = name[..^3];

        return $"/hubs/{name.ToLowerInvariant()}";
    }

    private static IReadOnlyList<HubMethodDescriptor> ExtractClientToServerMethods(Type hubType)
    {
        var methods = hubType.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        var result = new List<HubMethodDescriptor>();
        foreach (var method in methods)
        {
            if (ExcludedMethods.Contains(method.Name))
                continue;

            var parameters = method.GetParameters()
                .Where(p => p.ParameterType != typeof(CancellationToken))
                .Select(p => new ParameterDescriptor(p.Name ?? p.Position.ToString(), p.ParameterType))
                .ToList();

            var returnType = UnwrapTask(method.ReturnType);
            result.Add(new HubMethodDescriptor(method.Name, parameters, returnType, null));
        }
        return result;
    }

    private static IReadOnlyList<HubMethodDescriptor> ExtractServerToClientMethods(Type clientInterface)
    {
        var result = new List<HubMethodDescriptor>();
        foreach (var method in clientInterface.GetMethods())
        {
            var parameters = method.GetParameters()
                .Where(p => p.ParameterType != typeof(CancellationToken))
                .Select(p => new ParameterDescriptor(p.Name ?? p.Position.ToString(), p.ParameterType))
                .ToList();

            var returnType = UnwrapTask(method.ReturnType);
            result.Add(new HubMethodDescriptor(method.Name, parameters, returnType, null));
        }
        return result;
    }

    private static Type UnwrapTask(Type type)
    {
        if (type == typeof(void) || type == typeof(Task) || type == typeof(ValueTask))
            return typeof(void);

        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def == typeof(Task<>) || def == typeof(ValueTask<>))
                return type.GetGenericArguments()[0];
        }

        return type;
    }
}

public sealed record HubDescriptor(
    Type HubType,
    Type ClientInterface,
    string Route,
    IReadOnlyList<HubMethodDescriptor> ClientToServerMethods,
    IReadOnlyList<HubMethodDescriptor> ServerToClientMethods);

public sealed record HubMethodDescriptor(
    string Name,
    IReadOnlyList<ParameterDescriptor> Parameters,
    Type ReturnType,
    string? Summary);

public sealed record ParameterDescriptor(string Name, Type Type);
