using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StyloIssues.Abstractions;

namespace StyloIssues.Sync;

public sealed class ReconcilerService : BackgroundService
{
    private readonly IIssueReader _reader;
    private readonly TimeProvider _time;
    private readonly TimeSpan _interval;

    public ReconcilerService(IIssueReader reader, IOptions<StyloIssuesOptions> o, TimeProvider time)
    { _reader = reader; _time = time; _interval = o.Value.ReconcileInterval; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval, _time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            _reader.InvalidateAll();
    }
}
