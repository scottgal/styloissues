using StyloIssues.Abstractions;
using StyloIssues;

public class AttachmentBodyTests
{
    [Fact]
    public void Append_adds_link_and_collapsed_manifest_summary()
    {
        var a = new IssueAttachment("dump.zip", null, "https://host/dumps/abc.zip",
            "application/zip", "{\"scope\":{\"fingerprint\":\"fp1\"}}");
        var body = AttachmentBody.Append("Original report.", a);

        Assert.Contains("Original report.", body);
        Assert.Contains("https://host/dumps/abc.zip", body);
        Assert.Contains("<details>", body);
        Assert.Contains("\"fingerprint\":\"fp1\"", body);
    }

    [Fact]
    public void Append_is_a_noop_when_nothing_to_add()
    {
        var a = new IssueAttachment("dump.zip", null, null, "application/zip", null);
        Assert.Equal("Original report.", AttachmentBody.Append("Original report.", a));
    }
}
