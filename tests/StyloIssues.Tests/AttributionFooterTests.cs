using StyloIssues.Abstractions;
using StyloIssues.GitHub;
using Xunit;

public class AttributionFooterTests
{
    [Fact]
    public void Build_names_display_name_and_mentions_linked_handle()
    {
        var f = AttributionFooter.Build(new ReporterContext("Ada L.", "adalovelace", "m"));
        Assert.Contains("Ada L.", f);
        Assert.Contains("@adalovelace", f);
        Assert.Contains("stylo.bot", f);
    }

    [Fact]
    public void Build_omits_mention_when_not_linked()
    {
        var f = AttributionFooter.Build(new ReporterContext("Ada L.", null, "m"));
        Assert.Contains("Ada L.", f);
        Assert.DoesNotContain("@", f);
    }
}
