using System.Security.Cryptography;
using System.Text;

namespace StyloIssues.Abstractions;

/// <summary>
/// Opaque, stable reporter identity derived from a host user id (e.g. a Keycloak
/// sub). Embedded as a hidden marker in the GitHub issue body so "my issues"
/// resolves via GitHub search with no local database and no PII.
/// </summary>
public static class ReporterMarker
{
    private const string Prefix = "sb-reporter:";

    public static string Compute(byte[] key, string subject)
    {
        using var h = new HMACSHA256(key);
        var hash = h.ComputeHash(Encoding.UTF8.GetBytes(subject));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Appends a hidden HTML-comment marker to the issue body.</summary>
    public static string Embed(string body, string marker) =>
        $"{body}\n\n<!-- {Prefix}{marker} -->";

    /// <summary>The literal GitHub search fragment that matches an embedded marker.</summary>
    public static string SearchTerm(string marker) => $"{Prefix}{marker}";
}
