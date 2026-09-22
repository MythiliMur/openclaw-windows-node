using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace OpenClawTray.Presentation;

/// <summary>
/// Activates the package-owned broker entry point rather than starting an
/// executable by path. The Hub neither owns nor can replace the broker binary.
/// </summary>
internal static class GatewayToolsBrokerActivation
{
    private const string GatewayPackageName = "OpenClaw.Gateway";
    private const string GatewayPublisher =
        "CN=OpenClaw Foundation, O=OpenClaw Foundation, L=Mill Valley, S=California, C=US";
    private const string BrokerApplicationId = "Broker";

    public static void EnsureStarted()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            string aumid = $"{GatewayPackageName}_{GetPublisherId()}!{BrokerApplicationId}";
            int result = ActivateApplication(aumid, null, 0, out _);
            // The broker could already be active. Connection below is the
            // authoritative readiness check; do not surface raw HRESULTs.
            _ = result;
        }
        catch (DllNotFoundException)
        {
            // The connection attempt returns the normal unavailable result.
        }
        catch (EntryPointNotFoundException)
        {
            // The connection attempt returns the normal unavailable result.
        }
    }

    // Package Family Name uses the first 13 base32 characters of the SHA-256
    // publisher-DN hash. This avoids accepting an executable path or a
    // caller-provided AUMID as an activation authority.
    private static string GetPublisherId()
    {
        byte[] hash = SHA256.HashData(Encoding.Unicode.GetBytes(GatewayPublisher));
        const string alphabet = "abcdefghijklmnopqrstuvwxyz234567";
        var value = new StringBuilder(13);
        int buffer = 0;
        int bits = 0;
        foreach (byte item in hash)
        {
            buffer = (buffer << 8) | item;
            bits += 8;
            while (bits >= 5 && value.Length < 13)
            {
                value.Append(alphabet[(buffer >> (bits - 5)) & 0x1f]);
                bits -= 5;
            }

            if (value.Length == 13)
            {
                break;
            }
        }

        return value.ToString();
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int ActivateApplication(
        string appUserModelId,
        string? arguments,
        uint options,
        out uint processId);
}
