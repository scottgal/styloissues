using System.Security.Cryptography;
using System.Text;
using StyloIssues.Webhook;
using Xunit;

public class WebhookVerifierTests
{
    const string Secret = "whsec-123";

    static string Sign(byte[] body)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        return "sha256=" + Convert.ToHexStringLower(h.ComputeHash(body));
    }

    [Fact]
    public void Accepts_a_correct_signature()
    {
        var body = Encoding.UTF8.GetBytes("{\"action\":\"opened\"}");
        Assert.True(WebhookVerifier.IsValid(body, Sign(body), Secret));
    }

    [Fact]
    public void Rejects_a_tampered_body()
    {
        var body = Encoding.UTF8.GetBytes("{\"action\":\"opened\"}");
        var sig = Sign(body);
        var tampered = Encoding.UTF8.GetBytes("{\"action\":\"closed\"}");
        Assert.False(WebhookVerifier.IsValid(tampered, sig, Secret));
    }

    [Fact]
    public void Rejects_missing_or_malformed_header()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        Assert.False(WebhookVerifier.IsValid(body, "", Secret));
        Assert.False(WebhookVerifier.IsValid(body, "garbage", Secret));
    }
}
