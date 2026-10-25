using UniClaw.Host.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RequiredReportCoverageReviewTests
{
    [Fact]
    public void CoverageHasOnlyTheCanonicalGenerator()
    {
        Assert.Null(typeof(CoverageReportDeliveryOptions).GetProperty("GeneratorPath"));
    }

    [Fact]
    public async Task CaseAmbiguousDeclaredAttachmentsAreRejectedBeforeTaskWork()
    {
        var root = Directory.CreateTempSubdirectory("coverage-review-").FullName;
        try
        {
            await using var delivery = new CoverageReportDelivery(root, new()
            {
                Attachments = [new("evidence/A.xml", "first", true), new("evidence/a.xml", "second", true)]
            });
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => delivery.RequireAsync("run-case"));
            Assert.Contains("report-attachment-requirement-invalid", error.Message);
        }
        finally { Directory.Delete(root, true); }
    }
}
