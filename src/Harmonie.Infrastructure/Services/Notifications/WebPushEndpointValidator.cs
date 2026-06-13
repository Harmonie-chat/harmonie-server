using System.Net;
using System.Net.Sockets;

namespace Harmonie.Infrastructure.Services.Notifications;

public sealed class WebPushEndpointValidator
{
    public async Task<bool> IsAllowedAsync(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(uri.Host))
            return false;

        if (IPAddress.TryParse(uri.Host, out var literalAddress))
            return IsPublicAddress(literalAddress);

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);
        }
        catch (SocketException)
        {
            return false;
        }

        return addresses.Length > 0 && addresses.All(IsPublicAddress);
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return false;

        if (address.AddressFamily == AddressFamily.InterNetwork)
            return IsPublicIpv4(address);

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return IsPublicIpv6(address);

        return false;
    }

    private static bool IsPublicIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        if (bytes[0] == 10)
            return false;
        if (bytes[0] == 127)
            return false;
        if (bytes[0] == 169 && bytes[1] == 254)
            return false;
        if (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            return false;
        if (bytes[0] == 192 && bytes[1] == 168)
            return false;
        if (bytes[0] == 0)
            return false;

        return true;
    }

    private static bool IsPublicIpv6(IPAddress address)
    {
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
            return false;

        var bytes = address.GetAddressBytes();
        if ((bytes[0] & 0xfe) == 0xfc)
            return false;

        return true;
    }
}
