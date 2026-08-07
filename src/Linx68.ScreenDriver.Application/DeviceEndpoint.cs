using System.Net;
using System.Net.Sockets;

namespace Linx68.ScreenDriver.Application;

public static class DeviceEndpoint
{
    public static bool TryCreate(string? value, out Uri endpoint)
    {
        endpoint = null!;
        if (!IPAddress.TryParse(ExtractIp(value), out IPAddress? address)
            || address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        endpoint = new Uri($"http://{address}/image/upload", UriKind.Absolute);
        return true;
    }

    public static string ExtractIp(string? value)
    {
        string text = value?.Trim() ?? string.Empty;
        if (Uri.TryCreate(text, UriKind.Absolute, out Uri? result)
            && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps))
        {
            return result.Host;
        }

        return text;
    }
}
