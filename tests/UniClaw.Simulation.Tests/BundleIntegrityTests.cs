using Xunit;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// Minimal Scenario Bundle integrity 门（fail closed）锁 + D19 digest 完整性：
/// canonical digest 覆盖 bundle 全部语义字段——任何代表性 with-篡改都必须
/// 改变 Compute 输出（系统性 tamper 套件锁定）。
/// </summary>
public sealed class BundleIntegrityTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("b")]
    [InlineData("c")]
    [InlineData("d")]
    [InlineData("e")]
    [InlineData("f")]
    public void AllFactories_VerifyWithoutException(string which)
    {
        var bundle = which switch
        {
            "a" => GoldenScenarioBundles.WifiToggleOffToOn(),
            "b" => GoldenScenarioBundles.AlreadyOnZeroEffect(),
            "c" => GoldenScenarioBundles.MissingPostActionStimulus(),
            "d" => GoldenScenarioBundles.CancelThenLateStimulus(),
            "e" => GoldenScenarioBundles.TwoStepToggleThenMenuItem(),
            _ => GoldenScenarioBundles.TwoStepMissingMiddleEvidence(),
        };
        bundle.Verify();
    }

    [Fact]
    public void TamperedAssetSha256_FailsVerification()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var tamperedAssets = bundle.Assets.Select((asset, index) => index == 0
            ? asset with { Sha256 = new string('0', 64) }
            : asset).ToList();
        var tampered = ScenarioBundleDigest.Sealed(bundle with { Assets = tamperedAssets });
        Assert.Throws<ScenarioBundleException>(() => tampered.Verify());
    }

    [Fact]
    public void DuplicateStimulusId_FailsVerification()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var duplicated = ScenarioBundleDigest.Sealed(bundle with
        {
            Stimuli = new[] { bundle.Stimuli[0], bundle.Stimuli[0] },
        });
        Assert.Throws<ScenarioBundleException>(() => duplicated.Verify());
    }

    [Fact]
    public void TamperedBundleDigest_FailsVerification()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var tampered = bundle with { BundleDigest = "deadbeef" };
        Assert.Throws<ScenarioBundleException>(() => tampered.Verify());
    }

    [Fact]
    public void AssetRegistries_AreInstanceIsolated()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var firstAsset = bundle.Assets[0];
        var first = new BundleAssetRegistry(bundle.Assets);
        _ = new BundleAssetRegistry(bundle.Assets.Skip(1).ToList());

        var bytes = first.ReadArtifact(firstAsset.AssetId);

        Assert.NotEmpty(bytes);
    }

    /// <summary>
    /// D19 系统化 tamper 套件：≥10 个代表性 with-突变逐一断言
    /// Compute(original) != Compute(mutated)（digest canonical 覆盖全部
    /// 语义字段；被改字段落入任何角落都会改变 digest）。
    /// </summary>
    [Fact]
    public void RepresentativeTamperMutations_AllChangeDigest()
    {
        var original = GoldenScenarioBundles.WifiToggleOffToOn();
        var baseline = ScenarioBundleDigest.Compute(original with { BundleDigest = "" });

        static ScenarioStimulus.ObservationFrame FrameAt(MinimalScenarioBundle b, int i) =>
            (ScenarioStimulus.ObservationFrame)b.Stimuli[i];

        var mutations = new (string Name, Func<MinimalScenarioBundle, MinimalScenarioBundle> Mutate)[]
        {
            ("stimulus-virtual-time", b => b with
            {
                Stimuli = new[] { FrameAt(b, 0) with { VirtualTime = FrameAt(b, 0).VirtualTime.AddTicks(1) }, b.Stimuli[1] },
            }),
            ("stimulus-id", b => b with
            {
                Stimuli = new[] { FrameAt(b, 0) with { StimulusId = "obs-1-tampered" }, b.Stimuli[1] },
            }),
            ("reviewed-state-value", b => b with
            {
                Stimuli = new[]
                {
                    FrameAt(b, 0) with
                    {
                        ReviewedStateClaims = new[] { ("switch.wifi", "tampered") },
                    },
                    b.Stimuli[1],
                },
            }),
            ("reviewed-element-bounds", b => b with
            {
                Stimuli = new[]
                {
                    FrameAt(b, 0) with
                    {
                        ReviewedElements = FrameAt(b, 0).ReviewedElements
                            .Select(e => e with { X2 = e.X2 + 0.001 })
                            .ToList(),
                    },
                    b.Stimuli[1],
                },
            }),
            ("reviewed-element-state", b => b with
            {
                Stimuli = new[]
                {
                    FrameAt(b, 0) with
                    {
                        ReviewedElements = FrameAt(b, 0).ReviewedElements
                            .Select(e => e with { State = e.State is null ? "true" : null })
                            .ToList(),
                    },
                    b.Stimuli[1],
                },
            }),
            ("contract-obligation-required-value", b => b with
            {
                Contract = b.Contract with
                {
                    Obligations = new[]
                    {
                        new UniClaw.Kernel.Run.RunObligation("wifi", UniClaw.Kernel.Run.RunObligationKind.MaterialEffect, "switch.wifi", "tampered", true),
                    },
                },
            }),
            ("contract-obligation-mandatory", b => b with
            {
                Contract = b.Contract with
                {
                    Obligations = new[]
                    {
                        new UniClaw.Kernel.Run.RunObligation("wifi", UniClaw.Kernel.Run.RunObligationKind.MaterialEffect, "switch.wifi", "true", false),
                    },
                },
            }),
            ("contract-obligation-entity-scope", b => b with
            {
                Contract = b.Contract with
                {
                    Obligations = new[]
                    {
                        b.Contract.Obligations![0] with
                        {
                            EntityScope = new UniClaw.Kernel.World.UiRealization.TargetDescriptor(
                                "toggle", "wifi", "settings-root"),
                        },
                    },
                },
            }),
            ("contract-scope-member", b => b with
            {
                Contract = b.Contract with { Scope = new HashSet<string> { "live.frame" } },
            }),
            ("contract-allowed-effect", b => b with
            {
                Contract = b.Contract with { AllowedEffects = new HashSet<string> { "tap", "swipe" } },
            }),
            ("goal-criterion", b => b with
            {
                Goal = b.Goal with { RequiredObligationIds = new[] { "wifi", "extra" } },
            }),
            ("goal-statement", b => b with { Goal = b.Goal with { Statement = "tampered" } }),
            ("expectation-effects", b => b with
            {
                Expected = b.Expected with { ExpectedEffects = 2 },
            }),
            ("expectation-status", b => b with
            {
                Expected = b.Expected with { ExpectedStatus = "WaitingForInput" },
            }),
            ("producer-identity", b => b with
            {
                ProducerIdentities = b.ProducerIdentities with { StrategyVersion = "tampered" },
            }),
            ("target-os-version", b => b with
            {
                TargetUiSystem = b.TargetUiSystem with { OsVersion = "tampered" },
            }),
            ("target-app-build", b => b with
            {
                TargetUiSystem = b.TargetUiSystem with { AppBuild = "1.2.3-tampered" },
            }),
            ("script-desired-state", b => b with
            {
                AgentScript = b.AgentScript with
                {
                    Steps = new[] { new ScriptActionStep("toggle", null, "tap", "tampered") },
                },
            }),
            ("script-second-step", b => b with
            {
                AgentScript = b.AgentScript with
                {
                    Steps = new[]
                    {
                        b.AgentScript.Steps[0],
                        new ScriptActionStep("menuItem", null, "tap", null),
                    },
                },
            }),
            ("runtime-artifact-hash", b => b with
            {
                RuntimeArtifact = b.RuntimeArtifact with { KernelAssemblySha256 = new string('0', 64) },
            }),
            ("runtime-target-framework", b => b with
            {
                RuntimeArtifact = b.RuntimeArtifact with { TargetFramework = "tampered" },
            }),
            ("asset-sha256", b => b with
            {
                Assets = b.Assets.Select((a, i) => i == 0 ? a with { Sha256 = new string('0', 64) } : a).ToList(),
            }),
        };

        foreach (var (name, mutate) in mutations)
        {
            var mutated = mutate(original) with { BundleDigest = "" };
            var digest = ScenarioBundleDigest.Compute(mutated);
            Assert.NotEqual(baseline, digest);
        }
    }
}
