using System.Collections.Immutable;
using UniClaw.Semantic.Infrastructure.Corpus;
using UniClaw.Runtime.Model;
using UniClaw.Semantic.Tests.BenchmarkTests;
using Xunit;

namespace UniClaw.Semantic.Tests.CorpusTests;

public sealed class SemanticCorpusEngineeringTests
{
    private static Observation Obs(long seq, params ObservedElement[] elements) =>
        new(elements.ToImmutableArray(), "com.android.settings", seq);

    private static SemanticCase ValidCase(string id) =>
        new(
            id,
            Obs(1, new ObservedElement("Developer options", null, 0, null, "text")),
            "DeveloperOptions",
            "DeveloperOptions",
            SemanticCaseSource.RealTrace,
            SemanticCaseDifficulty.Easy)
        {
            ViewportState = SemanticViewportState.TitleVisible,
            VisibleAnchorState = SemanticVisibleAnchorState.AnchorVisible,
            NoiseLevel = 0,
            AmbiguityLevel = 0,
            ScrollPosition = 0,
        };

    [Fact]
    public void T1_CaseValidation()
    {
        var corpus = new SemanticCorpus("valid-v1", ImmutableArray.Create(ValidCase("valid-001")));
        var result = SemanticCorpusValidator.Validate(corpus);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void T2_CategoryLoading()
    {
        Assert.All(ExpandedContainerIdentityCorpora.AllGolden(), c => Assert.Equal(SemanticCorpusCategory.Golden, c.Category));
        Assert.Equal(SemanticCorpusCategory.Regression, ExpandedContainerIdentityCorpora.RegressionCorpus().Category);
        Assert.Equal(SemanticCorpusCategory.Adversarial, ExpandedContainerIdentityCorpora.AdversarialCorpus().Category);
    }

    [Fact]
    public void T3_GoldenCorpusLoading()
    {
        var golden = ExpandedContainerIdentityCorpora.DeveloperOptionsGolden();
        Assert.Equal(5, golden.Cases.Length);
        Assert.Equal(SemanticCorpusCategory.Golden, golden.Category);
        Assert.True(SemanticCorpusValidator.Validate(golden).IsValid);
    }

    [Fact]
    public void T4_RegressionCorpusLoading()
    {
        var regression = ExpandedContainerIdentityCorpora.RegressionCorpus();
        Assert.Equal(SemanticCorpusCategory.Regression, regression.Category);
        Assert.Equal(3, regression.Cases.Length);
        Assert.Contains(regression.Cases, c => c.CaseId == "reg-scrolled-drift");
        Assert.True(SemanticCorpusValidator.Validate(regression).IsValid);
    }

    [Fact]
    public void T5_InvalidCaseRejection()
    {
        var invalid = new SemanticCase(
            "invalid-001",
            Obs(1, new ObservedElement("Developer options", null, 0, null, "text")),
            "DeveloperOptions",
            "DeveloperOptions",
            SemanticCaseSource.Manual,
            SemanticCaseDifficulty.Easy);

        // Metadata intentionally incomplete: ViewportState Unknown.
        var corpus = new SemanticCorpus("invalid-v1", ImmutableArray.Create(invalid));
        var result = SemanticCorpusValidator.Validate(corpus);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ViewportState", StringComparison.Ordinal));
    }

    [Fact]
    public void T6_BenchmarkCategoryFiltering()
    {
        var all = ExpandedContainerIdentityCorpora.AllGolden()
            .Add(ExpandedContainerIdentityCorpora.RegressionCorpus())
            .Add(ExpandedContainerIdentityCorpora.AdversarialCorpus());

        var golden = SemanticCorpusCatalog.FilterByCategory(all, SemanticCorpusCategory.Golden);
        var regression = SemanticCorpusCatalog.FilterByCategory(all, SemanticCorpusCategory.Regression);

        Assert.Equal(4, golden.Length);
        Assert.Single(regression);
    }

    [Fact]
    public void T7_MetadataPreservation()
    {
        var testCase = ValidCase("meta-001");

        Assert.Equal(SemanticViewportState.TitleVisible, testCase.ViewportState);
        Assert.Equal(SemanticVisibleAnchorState.AnchorVisible, testCase.VisibleAnchorState);
        Assert.Equal(0, testCase.NoiseLevel);
        Assert.Equal(0, testCase.AmbiguityLevel);
        Assert.Equal(0, testCase.ScrollPosition);
    }
}