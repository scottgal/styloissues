using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using StyloIssues.Abstractions;

namespace StyloIssues;

/// <summary>
/// GitHub is the source of truth. This is a transient per-process read cache
/// (not a store): it never holds authoritative state, only a short-lived copy
/// the reconciler and webhook keep honest. Fine as a ConcurrentDictionary since
/// it is a performance cache, not persistence.
/// </summary>
public sealed class CachingIssueReader : IIssueReader
{
    private readonly IIssueGateway _gw;
    private readonly TimeProvider _time;
    private readonly TimeSpan _ttl;
    private readonly ConcurrentDictionary<int, (DateTimeOffset at, IssueDetail v)> _issues = new();

    public CachingIssueReader(IIssueGateway gw, IOptions<StyloIssuesOptions> o, TimeProvider time)
    { _gw = gw; _time = time; _ttl = o.Value.CacheTtl; }

    public async Task<IssueDetail?> GetIssueAsync(int number, CancellationToken ct)
    {
        if (_issues.TryGetValue(number, out var e) && _time.GetUtcNow() - e.at < _ttl) return e.v;
        var fresh = await _gw.GetIssueAsync(number, ct);
        if (fresh is not null) _issues[number] = (_time.GetUtcNow(), fresh);
        return fresh;
    }

    public Task<IReadOnlyList<IssueSummary>> ListByReporterAsync(string marker, CancellationToken ct) =>
        _gw.ListByReporterAsync(marker, ct);   // lists are not cached: freshness over locality

    public Task<IReadOnlyList<IssueSummary>> ListPublicAsync(CancellationToken ct) =>
        _gw.ListPublicAsync(ct);

    public void Invalidate(int number) => _issues.TryRemove(number, out _);
    public void InvalidateAll() => _issues.Clear();
}
