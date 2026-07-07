using System.Security.Cryptography;
using System.Text;

namespace StyloIssues.Webhook;

public static class WebhookVerifier
{
    public static bool IsValid(byte[] payload, string signatureHeader, string secret)
    {
        const string prefix = "sha256=";
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith(prefix)) return false;
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = "sha256=" + Convert.ToHexStringLower(h.ComputeHash(payload));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signatureHeader));
    }
}
