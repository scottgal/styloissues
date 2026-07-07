using StyloIssues.Abstractions;

namespace StyloIssues.Sample;

/// <summary>
/// Demo implementation of ICurrentUser for offline/no-credentials sample usage.
/// Replace with your real authentication integration in production.
/// </summary>
public sealed class DemoCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => true;
    public string? StableId => "demo-user-001";
    public string DisplayName => "Demo User";
    public string? GitHubLogin => null;
}
