using StyloIssues.Abstractions;
using Xunit;

public class ReporterMarkerTests
{
    static readonly byte[] Key = System.Text.Encoding.UTF8.GetBytes("test-marker-key-0123456789");

    [Fact]
    public void Compute_is_stable_and_opaque_for_a_subject()
    {
        var a = ReporterMarker.Compute(Key, "kc-sub-abc");
        var b = ReporterMarker.Compute(Key, "kc-sub-abc");
        Assert.Equal(a, b);
        Assert.DoesNotContain("kc-sub-abc", a);      // opaque: raw sub never present
        Assert.Matches("^[0-9a-f]{64}$", a);          // hex sha256
    }

    [Fact]
    public void Compute_differs_per_subject()
    {
        Assert.NotEqual(ReporterMarker.Compute(Key, "sub-1"), ReporterMarker.Compute(Key, "sub-2"));
    }

    [Fact]
    public void Embed_then_SearchTerm_round_trips_in_body()
    {
        var marker = ReporterMarker.Compute(Key, "sub-1");
        var body = ReporterMarker.Embed("My bug report.", marker);
        Assert.Contains(ReporterMarker.SearchTerm(marker), body);
        Assert.Contains("My bug report.", body);
    }
}
