using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using StyloIssues.Abstractions;
using StyloIssues;
using Xunit;

public class CachingIssueReaderTests
{
    [Fact]
    public async Task Serves_from_cache_within_ttl_then_refetches_after_invalidate()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-07T00:00:00Z"));
        var gw = new Mock<IIssueGateway>();
        var calls = 0;
        gw.Setup(g => g.GetIssueAsync(7, It.IsAny<CancellationToken>()))
          .ReturnsAsync(() => { calls++; return new IssueDetail(7, "t", "open", time.GetUtcNow(), "u", "b", []); });

        var reader = new CachingIssueReader(gw.Object,
            Options.Create(new StyloIssuesOptions { CacheTtl = TimeSpan.FromMinutes(2) }), time);

        await reader.GetIssueAsync(7, default);
        await reader.GetIssueAsync(7, default);
        Assert.Equal(1, calls);                 // second served from cache

        reader.Invalidate(7);
        await reader.GetIssueAsync(7, default);
        Assert.Equal(2, calls);                 // refetched after invalidation
    }
}
