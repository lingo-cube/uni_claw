using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>AGT-013：策略在首次咨询前加载、投影与 dispatch 前 guard。</summary>
[Collection("SettingsCoverageConfigSerial")]
public sealed class SettingsActionPolicyTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;
            directory = directory.Parent!;
        }
        throw new InvalidOperationException("未定位到仓库根");
    }

    private static AgentDecisionContext Context(params ElementSummary[] elements) => new(
        DecisionId: "decision-1",
        RunId: "run-1",
        ContractVersion: "v0",
        Objective: "settings",
        AllowedEffects: new HashSet<string>(StringComparer.Ordinal) { "tap", "swipe-up" },
        CurrentWorldClaims: new Dictionary<string, ClaimSummary>(),
        PendingObligations: Array.Empty<AgentObligationView>(),
        Phase: AgentDecisionPhase.InitialPlanning,
        Elements: elements);

    private static AgentActionStep Step(string target, string? desired = null, string effect = "tap") =>
        new("ui.element", target, effect, desired);

    private static SettingsActionPolicy Policy() =>
        SettingsCoverageConfig.LoadDefault().ActionPolicy
        ?? throw new InvalidOperationException("default settings profile did not load action policy");

    /// <summary>AGT-015 B1 真实回合暴露：目标不是 Wi-Fi 的任务在策略下仍放行
    /// toggle:Wi-Fi。traversal 目标必须与策略声明的 targeted toggle 绑定，
    /// 冲突在运行前拒绝（配置冲突不得静默通过）。</summary>
    [Fact]
    public void TraversalTarget_UndeclaredByPolicy_IsConfigConflict()
    {
        var policy = Policy();

        var mismatch = policy.ValidateTraversalTargetDescriptor("Network & internet");
        Assert.NotNull(mismatch);
        Assert.Contains("toggle:Network & internet", mismatch);
        Assert.Contains("toggle:Wi-Fi", mismatch); // 声明面必须可见，供排障
        Assert.Null(policy.ValidateTraversalTargetDescriptor("Wi-Fi"));
        Assert.NotNull(policy.ValidateTraversalTargetDescriptor("  ")); // 空/缺省也是冲突
    }

    /// <summary>可见性冻结规则不得绑定字面 "Wi-Fi"：策略声明的目标开关才冻结
    /// 导航。NFC 任务穿过 Wi-Fi 开关可见页面时，导航 tap 必须按 safe class
    /// 放行，而不是被无关目标冻结。</summary>
    [Fact]
    public void UndeclaredSwitchVisible_DoesNotBlockNavigation()
    {
        var nfcPolicy = new SettingsActionPolicy(
            SettingsActionPolicy.SupportedSchemaVersion,
            "fixture/android-settings/decoy-target-policy", 1, true,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "observe", "scroll", "navigate", "back" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "toggle:NFC" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "toggle-non-target", "destructive", "unknown-action" },
            new[] { "factory reset", "developer options" },
            SettingsActionPolicy.RejectUnknown,
            "digest-test-nfc", "test");

        var result = SettingsActionGuard.Evaluate(
            Step("T-Mobile"),
            Context(
                new ElementSummary("switch", "Wi-Fi", null, true, true, true,
                    ElementEpistemic.Observed, "unchecked"),
                new ElementSummary("ui.element", "T-Mobile", null, true, false, true,
                    ElementEpistemic.Observed)),
            nfcPolicy);

        Assert.Equal(SettingsActionGuardVerdict.Allow, result.Verdict);
        Assert.Equal("navigate", result.SemanticAction);
    }

    [Fact]
    public void DefaultProfile_LoadsRequiredPolicyBeforeConsultation()
    {
        var config = SettingsCoverageConfig.LoadDefault();

        Assert.True(config.ActionPolicyRequired);
        Assert.NotNull(config.ActionPolicy);
        Assert.Equal(SettingsActionPolicy.SupportedSchemaVersion, config.ActionPolicy!.SchemaVersion);
        Assert.False(string.IsNullOrWhiteSpace(config.ActionPolicy.Digest));
    }

    [Fact]
    public void AgentProjection_DistinguishesRuntimeTokensFromPolicySemantics()
    {
        var projection = Policy().AgentProjection();

        Assert.Contains("effectClass=runtime token from context.allowedEffects", projection);
        Assert.Contains("semanticMapping={navigate:tap,back:tap,scroll:swipe-up}", projection);
    }

    [Fact]
    public void MissingRequiredPolicy_FailsClosed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"settings-policy-missing-{Guid.NewGuid():N}.yaml");
        try
        {
            File.WriteAllText(path, """
                configVersion: "1"
                session:
                  taskTitle: t
                  workspace: w
                  workspaceReuse: true
                  autoCloseTurn: false
                bounds:
                  maxSteps: 1
                  maxConsultRounds: 1
                  maxScrolls: 0
                  maxConsecutiveFailures: 1
                  maxDirectiveRetries: 0
                coverage:
                  rootPage: true
                  firstLevelMode: all-visible
                  scrollDiscoveredEntries: 1
                  secondLevelPages: 1
                  backNavigation: true
                  repeatedEntries: 0
                targetPages:
                  - Network & internet
                termination:
                  onCoverageComplete: true
                  onMaxSteps: true
                  onMaxScrolls: true
                  onConsecutiveFailures: true
                rootRoute: Settings
                scrollContainerDescriptor: scroll
                backDescriptor: Navigate up
                actionPolicy:
                  required: true
                  path: missing-policy.json
                """);

            var error = Assert.Throws<InvalidOperationException>(() => SettingsCoverageConfig.Load(path));
            Assert.Contains("PROFILE_CONTRACT_NOT_READY", error.Message);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ForbiddenAndUnknownTargets_AreRejectedBeforeDispatch()
    {
        var policy = Policy();

        var forbidden = SettingsActionGuard.Evaluate(
            Step("USB debugging"), Context(), policy);
        var unknown = SettingsActionGuard.Evaluate(
            Step("Some unknown control", desired: "checked"), Context(), policy);

        Assert.Equal(SettingsActionGuardVerdict.Reject, forbidden.Verdict);
        Assert.Equal("forbidden-target", forbidden.SemanticAction);
        Assert.Equal(SettingsActionGuardVerdict.Reject, unknown.Verdict);
        Assert.Equal("toggle-non-target", unknown.SemanticAction);
        Assert.Equal(policy.Digest, forbidden.PolicyDigest);
    }

    [Fact]
    public void SatisfiedTargetedToggle_BecomesNoAction()
    {
        var policy = Policy();
        var result = SettingsActionGuard.Evaluate(
            Step("Wi-Fi", desired: "checked"),
            Context(new ElementSummary(
                "switch", "Wi-Fi", null, true, true, true,
                ElementEpistemic.Observed, "checked")),
            policy);

        Assert.Equal(SettingsActionGuardVerdict.NoAction, result.Verdict);
        Assert.Equal("toggle:Wi-Fi", result.SemanticAction);
    }

    [Fact]
    public void UnsatisfiedTargetedToggle_IsAllowedOnce()
    {
        var policy = Policy();
        var result = SettingsActionGuard.Evaluate(
            Step("Wi-Fi", desired: "checked"),
            Context(new ElementSummary(
                "switch", "Wi-Fi", null, true, true, true,
                ElementEpistemic.Observed, "unchecked")),
            policy);

        Assert.Equal(SettingsActionGuardVerdict.Allow, result.Verdict);
        Assert.Equal("toggle:Wi-Fi", result.SemanticAction);
    }

    [Fact]
    public void SwitchTargetWithoutDesiredState_IsRejectedBeforeDispatch()
    {
        var policy = Policy();
        var result = SettingsActionGuard.Evaluate(
            Step("Wi-Fi"),
            Context(new ElementSummary(
                "switch", "Wi-Fi", null, true, true, true,
                ElementEpistemic.Observed, "unchecked")),
            policy);

        Assert.Equal(SettingsActionGuardVerdict.Reject, result.Verdict);
        Assert.Equal("unknown-action", result.SemanticAction);
        Assert.Contains("desiredState", result.Reason);
    }

    [Fact]
    public void VisibleTargetSwitch_RejectsUnrelatedNavigation()
    {
        var policy = Policy();
        var result = SettingsActionGuard.Evaluate(
            Step("T-Mobile"),
            Context(
                new ElementSummary("switch", "Wi-Fi", null, true, true, true,
                    ElementEpistemic.Observed, "unchecked"),
                new ElementSummary("ui.element", "T-Mobile", null, true, false, true,
                    ElementEpistemic.Observed)),
            policy);

        Assert.Equal(SettingsActionGuardVerdict.Reject, result.Verdict);
        Assert.Equal("unknown-action", result.SemanticAction);
        Assert.Contains("target switch is visible", result.Reason);
    }

    [Fact]
    public void GuardDecision_RejectsBeforeKernelDispatch()
    {
        var policy = Policy();
        var context = Context();
        var decision = new AgentDecision.Act(new AgentActionProposal(
            context.DecisionId,
            new[] { Step("Developer options") },
            "model proposal"));

        var guarded = SettingsActionGuard.GuardDecision(decision, context, policy, out var result);

        Assert.Null(guarded);
        Assert.NotNull(result);
        Assert.Equal(SettingsActionGuardVerdict.Reject, result!.Verdict);
        Assert.Equal("forbidden-target", result.SemanticAction);
    }

    [Fact]
    public void MultiActProposal_IsRejectedBeforePolicyCanBeBypassed()
    {
        var policy = Policy();
        var context = Context();
        var decision = new AgentDecision.Plan(
            context.DecisionId,
            new AgentPlanProposal(new PlanItem[]
            {
                new PlanItem.ActItem("ui.element", "Wi-Fi", "tap", null),
                new PlanItem.ActItem("ui.element", "USB debugging", "tap", null),
            }, "multi-act"));

        var guarded = SettingsActionGuard.GuardDecision(decision, context, policy, out var result);

        Assert.Null(guarded);
        Assert.Equal(SettingsActionGuardVerdict.Reject, result!.Verdict);
        Assert.Contains("multi-act", result.Reason);
    }

    // ---- PRF-005（ADR-0041）：policy 升格产品声明面 + 消费方加载点执法 ----

    [Fact]
    public void Canonical_Policy_Loads_From_Product_Policy_Surface()
    {
        var policy = Policy();

        Assert.Equal(1, policy.SafetyPolicyRevision);
        Assert.Equal("product/android-settings/forbidden-actions", policy.PolicyRef);
        Assert.Contains("product/policy/android-settings-forbidden-actions.json",
            policy.SourcePath, StringComparison.Ordinal);
        Assert.Contains("safetyPolicyRevision=1", policy.AgentProjection(), StringComparison.Ordinal);
        Assert.All(SettingsActionPolicy.RequiredForbiddenFloor,
            @class => Assert.Contains(@class, policy.ForbiddenActionClasses));
    }

    private static string WritePolicyFixture(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"product-policy-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Policy_Without_Revision_Fails_Closed()
    {
        var path = WritePolicyFixture("""
            {
              "schemaVersion": "android-settings-action-policy.v1",
              "policyRef": "product/android-settings/forbidden-actions",
              "generatedBeforeFirstConsultation": true,
              "safeActionClasses": ["observe", "scroll", "navigate", "back"],
              "targetedActionClasses": ["toggle:Wi-Fi"],
              "forbiddenActionClasses": ["toggle-non-target", "destructive", "account-removal", "credential-change", "developer-debug", "permission-grant", "unknown-action"],
              "forbiddenTargetPatterns": ["factory reset"],
              "unknownTargetDisposition": "reject"
            }
            """);
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() => SettingsActionPolicy.Load(path));
            Assert.Contains("PROFILE_CONTRACT_NOT_READY", error.Message);
            Assert.Contains("safetyPolicyRevision", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Policy_Missing_Safety_Floor_Fails_Closed()
    {
        // 危险类别词汇被缩小（缺 credential-change 与 permission-grant）→ 拒载。
        var path = WritePolicyFixture("""
            {
              "schemaVersion": "android-settings-action-policy.v1",
              "policyRef": "product/android-settings/forbidden-actions",
              "safetyPolicyRevision": 2,
              "generatedBeforeFirstConsultation": true,
              "safeActionClasses": ["observe", "scroll", "navigate", "back"],
              "targetedActionClasses": ["toggle:Wi-Fi"],
              "forbiddenActionClasses": ["toggle-non-target", "destructive", "account-removal", "developer-debug", "unknown-action"],
              "forbiddenTargetPatterns": ["factory reset"],
              "unknownTargetDisposition": "reject"
            }
            """);
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() => SettingsActionPolicy.Load(path));
            Assert.Contains("PROFILE_CONTRACT_NOT_READY", error.Message);
            Assert.Contains("credential-change", error.Message);
            Assert.Contains("permission-grant", error.Message);
            Assert.Contains("safety floor", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
