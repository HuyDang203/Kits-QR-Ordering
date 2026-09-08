using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Annap.CoffeeQrOrdering.Web.Services;

public sealed class LanIpDetector : ILanIpDetector
{
    private static readonly string[] VirtualInterfaceHints =
    [
        "hyper-v",
        "vethernet",
        "virtual ethernet",
        "wsl",
        "docker",
        "virtualbox",
        "vmware",
        "tap-windows",
        "virtual ",
        "pseudo",
        "teredo",
        "isatap",
        "zero tier",
        "zerotier",
        "npcap",
        "nordlynx",
        "windscribe",
        "wg " // wireguard
    ];

    public string? TryGetPreferredLanBaseUrl(int port = 8080)
    {
        var ip = TryGetPreferredLanIPv4();
        return ip is null ? null : $"http://{ip}:{port}";
    }

    public string? TryGetPreferredLanIPv4()
    {
        var scored = new List<(int Score, string Ip)>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            var label = $"{ni.Name} {ni.Description}".ToLowerInvariant();
            if (VirtualInterfaceHints.Any(h => label.Contains(h, StringComparison.Ordinal)))
                continue;

            var ipProps = ni.GetIPProperties();
            foreach (var ua in ipProps.UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;
                var ip = ua.Address;
                if (IPAddress.IsLoopback(ip))
                    continue;
                if (!IsPrivateOrCarrierGradeNat(ip))
                    continue;
                if (IsWslHyperVSubnet(ip))
                    continue;

                var score = Score(ni, ip);
                if (score > 0)
                    scored.Add((score, ip.ToString()));
            }
        }

        return scored.OrderByDescending(x => x.Score).Select(x => x.Ip).FirstOrDefault();
    }

    /// <summary>172.20.0.0/16 is commonly used by WSL / Hyper-V vEthernet; phones cannot reach it from WiFi.</summary>
    private static bool IsWslHyperVSubnet(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return b.Length == 4 && b[0] == 172 && b[1] == 20;
    }

    private static bool IsPrivateOrCarrierGradeNat(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        if (b.Length != 4)
            return false;
        if (b[0] == 10)
            return true;
        if (b[0] == 192 && b[1] == 168)
            return true;
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            return true;
        // 100.64.0.0/10 CGNAT — sometimes real ISP; skip for "LAN demo" preference
        return false;
    }

    private static int Score(NetworkInterface ni, IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        var score = 1;
        if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            score += 80;

        var desc = (ni.Description + " " + ni.Name).ToLowerInvariant();
        if (desc.Contains("wi-fi", StringComparison.Ordinal) || desc.Contains("wireless", StringComparison.Ordinal) ||
            desc.Contains("wlan", StringComparison.Ordinal))
            score += 40;

        if (b[0] == 192 && b[1] == 168)
            score += 30;
        else if (b[0] == 10)
            score += 20;
        else if (b[0] == 172)
            score += 10;

        return score;
    }
}
