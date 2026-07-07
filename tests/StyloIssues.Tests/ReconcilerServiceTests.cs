using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using StyloIssues;
using StyloIssues.Abstractions;
using StyloIssues.Sync;

namespace StyloIssues.Tests;

public class ReconcilerServiceTests
{
    [Fact]
    public async Task Invalidates_all_on_each_interval()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-07T00:00:00Z"));
        var reader = new Mock<IIssueReader>();
        var sut = new ReconcilerService(reader.Object,
            Options.Create(new StyloIssuesOptions { ReconcileInterval = TimeSpan.FromMinutes(10) }), time);

        await sut.StartAsync(default);
        await Task.Delay(100); // let background service initialize

        time.Advance(TimeSpan.FromMinutes(11));   // past first tick
        await Task.Delay(200);

        time.Advance(TimeSpan.FromMinutes(11));   // past second tick
        await Task.Delay(200);

        await sut.StopAsync(default);

        reader.Verify(r => r.InvalidateAll(), Times.AtLeast(2));
    }
}
