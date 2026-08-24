using System.Collections.Immutable;
using UniClaw.Semantic.Infrastructure.Corpus;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Semantic.Tests.CorpusTests;

public sealed class SemanticCorpusTests
{
    private static Observation Obs(long seq) =>
        new(ImmutableArray<ObservedElement>.Empty, "com.android.settings", seq);

    [Fact]
    public void Corpus_StoresContainerIdentityCases()
    {
        var corpus = new SemanticCorpus(
            "DeveloperOptions-v1",
            ImmutableArray.Create(
                new SemanticCase(
                    "dev-001",
                    Obs(1),
                    "DeveloperOptions",
                    "DeveloperOptions",
                    SemanticCaseSource.RealWorld,
                    SemanticCaseDifficulty.Medium),
                new SemanticCase(
                    "dev-negative-001",
                    Obs(2),
                    "None",
                    null,
                    SemanticCaseSource.Synthetic,
                    SemanticCaseDifficulty.Hard)
                {
                    PreviousVerifiedIdentity = "DeveloperOptions",
                }));

        Assert.Equal("DeveloperOptions-v1", corpus.CorpusId);
        Assert.Equal(2, corpus.Cases.Length);
        Assert.Equal(SemanticCaseSource.RealWorld, corpus.Cases[0].Source);
        Assert.Null(corpus.Cases[1].ExpectedIdentity);
    }
}