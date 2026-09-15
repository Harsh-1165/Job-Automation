using System.Net;
using System.Net.Sockets;
using JobAutomation.Application.Interfaces;
using JobAutomation.Domain.Exceptions;

namespace JobAutomation.Infrastructure.Security;

public class SsrTargetValidator : ISsrTargetValidator
{
    private static readonly HashSet<string> BlockedHostnames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "localhost",
            "metadata",
            "metadata.google.internal"
        };

    public async Task ValidateAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        if (uri.Scheme is not "http" and not "https")
        {
            throw new SsrValidationException();
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new SsrValidationException();
        }

        if (BlockedHostnames.Contains(uri.Host)
            || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            throw new SsrValidationException();
        }

        if (IPAddress.TryParse(uri.Host, out var literalAddress))
        {
            if (IsBlockedAddress(literalAddress))
            {
                throw new SsrValidationException();
            }

            return;
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);
        }
        catch (SocketException)
        {
            throw new SsrValidationException();
        }

        if (addresses.Length == 0)
        {
            throw new SsrValidationException();
        }

        foreach (var address in addresses)
        {
            if (IsBlockedAddress(address))
            {
                throw new SsrValidationException();
            }
        }
    }

    internal static bool IsBlockedAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            if (bytes[0] == 0)
            {
                return true;
            }

            if (bytes[0] == 10)
            {
                return true;
            }

            if (bytes[0] == 127)
            {
                return true;
            }

            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return true;
            }

            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return true;
            }

            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return true;
            }

            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();

            if (address.Equals(IPAddress.IPv6Loopback))
            {
                return true;
            }

            if ((bytes[0] & 0xFE) == 0xFC)
            {
                return true;
            }

            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
            {
                return true;
            }
        }

        return false;
    }
}
